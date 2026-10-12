using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using MyReplayLibrary.Obs;

namespace MyHotsObserver;

/// <param name="Path">The recording's file.</param>
/// <param name="From">Replay time the recording starts at, in seconds.</param>
/// <param name="To">Replay time it stops at.</param>
/// <param name="Camera">The camera moves made during the clip, e.g. "23:39 hero view (takes off)".</param>
public sealed record Clip(string Path, double From, double To, IReadOnlyList<string> Camera);

/// <summary>
/// Records the observer scene with OBS into <see cref="ClipsFolder"/>, either between
/// <see cref="StartAsync"/> and <see cref="StopAsync"/> or a whole stretch of a replay with
/// <see cref="RecordClipAsync"/>.
/// </summary>
public sealed class ClipRecorder(
    ReplayController controller,
    ObserverCapture capture,
    ReplayCatalog catalog,
    ILogger<ClipRecorder> logger) {
    /// <summary>The longest stretch <see cref="RecordClipAsync"/> records.</summary>
    private const double MaxClipSeconds = 600;

    /// <summary>A replay whose clock stands still this long has stopped (paused, or ended).</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(10);

    /// <summary>How often a clip following in the everyone view re-clicks the hero's portrait.</summary>
    private static readonly TimeSpan RefollowInterval = TimeSpan.FromSeconds(4);

    /// <summary>
    /// No portrait re-click this long before a planned step: one landing as Deathwing takes off
    /// locks the camera on his flying unit at base, and the landing-spot hold then takes ~3s to
    /// win (measured; steps also fire up to a second early).
    /// </summary>
    private const double RefollowQuietBefore = 5;

    /// <summary>Pause between clock reads during a clip.</summary>
    private static readonly TimeSpan ReadPause = TimeSpan.FromSeconds(0.5);


    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _recording;

    /// <summary>The camera step a clip's camera plan made last.</summary>
    private CameraStep? _last;

    /// <summary>The map of the clip being recorded.</summary>
    private string? _map;

    /// <summary>
    /// The zoom the clip being recorded set (null: left as it was). A hero-view move resets the
    /// zoom, so the move zooms again.
    /// </summary>
    private Zoom? _zoom;

    /// <summary>
    /// A minimap hold the camera plan started. Let go only after the recording stops: the camera
    /// may move once it's released.
    /// </summary>
    private GameInput.Session? _hold;
    private string? _name;

    /// <summary>
    /// Next to the app's game recordings, which it prunes; it only prunes the files directly in
    /// that folder, not these.
    /// </summary>
    public static string ClipsFolder => Path.Combine(new GameRecorderOptions().RecordingsFolder, "Clips");

    public async Task StartAsync(string? name, CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            if (_recording) {
                throw new McpException("Already recording; stop that recording first.");
            }

            await capture.StartRecordAsync(cancellationToken);
            _recording = true;
            _name = name;
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>Stops the recording <see cref="StartAsync"/> began and returns where it was saved.</summary>
    public async Task<string> StopAsync(string? name, CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            if (!_recording) {
                throw new McpException("Not recording (nothing started with record_start).");
            }

            var file = await capture.StopRecordAsync(cancellationToken);
            _recording = false;
            return await KeepAsync(file, name ?? _name ?? $"Recording {DateTime.Now:yyyy-MM-dd HH-mm-ss}");
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>
    /// Records the replay from <paramref name="start"/> to <paramref name="end"/> (replay panel
    /// times), following <paramref name="player"/> when given: seeks to before the start, pauses
    /// there, starts recording, resumes, and stops once the replay passes the end.
    /// </summary>
    public async Task<Clip> RecordClipAsync(
        double start, double end, string? player, bool everyone, Zoom? zoom, string? replay, double leadIn, double tail, string? name,
        CancellationToken cancellationToken) {
        if (end <= start || end - start > MaxClipSeconds) {
            throw new McpException($"end must be after start and at most {MaxClipSeconds / 60:0} minutes later.");
        }


        await _lock.WaitAsync(cancellationToken);
        try {
            if (_recording) {
                throw new McpException("Already recording; stop that recording first.");
            }

            if (replay is not null) {
                var path = ReplayCatalog.Resolve(replay);
                if (await OnScreenAsync(cancellationToken) != path) {
                    await controller.OpenAsync(path, cancellationToken);
                }
            }

            var summary = await controller.CurrentReplayAsync(cancellationToken);
            var all = string.Equals(player, "all", StringComparison.OrdinalIgnoreCase);
            var hero = player is null || all ? null : ReplayController.ResolvePlayer(summary, player).Hero;

            // A timeline click lands up to a pixel (several seconds) early and leaves the replay
            // playing; let it play to the lead-in point and pause there, so the camera and the
            // recording are set up on the first frame of the clip.
            var from = Math.Max(0, start - Math.Max(0, leadIn));
            await controller.SeekAsync(from - 1.5, cancellationToken);
            from = await controller.PauseAtAsync(from, cancellationToken);

            // Following in the everyone view has a plan for the moments the game's own following
            // gets wrong (flying, dying); the first step sets the camera up.
            var target = player is null || all ? null : ReplayController.ResolvePlayer(summary, player);
            var plan = new Queue<CameraStep>(everyone && target is not null
                ? CameraPlan.EveryoneView(catalog.Timeline(summary.Path), target.Index, from, end + Math.Max(0, tail),
                    Minimap.For(summary.Map) is not null)
                : []);
            var camera = new List<string>();
            _last = null;
            _map = summary.Map;
            _zoom = zoom;

            // The everyone view follows the clicked portrait only in camera follow mode (L).
            if (everyone && target is not null) {
                await controller.EnsureCameraFollowAsync(target, cancellationToken);
            }

            if (all) {
                await controller.PressAsync(Key.E, [], cancellationToken);
            }
            else if (target is not null && plan.Count == 0) {
                await controller.FocusPlayerAsync(player!, everyone, cancellationToken);
            }
            else if (target is not null) {
                await MoveAsync(plan.Dequeue(), target, camera, cancellationToken);
            }

            if (zoom is { } z) {
                await controller.SetZoomAsync(z, cancellationToken);
            }

            // Recording starts paused: starting it stalls the game for a moment, which shouldn't
            // be in the clip, and a key pressed during the stall is lost, so wait it out.
            await capture.StartRecordAsync(cancellationToken);
            _recording = true;

            double to;
            try {
                await capture.SetRecordPausedAsync(true, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                await controller.PressAsync(Key.P, [], cancellationToken);
                await capture.SetRecordPausedAsync(false, cancellationToken);
                if (await controller.IsPausedNowAsync(cancellationToken)) {
                    // The game missed the press; the clip gets a second of still frame.
                    await controller.PressAsync(Key.P, [], cancellationToken);
                    if (await controller.IsPausedNowAsync(cancellationToken)) {
                        throw new McpException("The replay didn't resume.");
                    }
                }

                to = await PlayUntilAsync(end + Math.Max(0, tail), plan, target, camera, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException) {
                var partial = await capture.StopRecordAsync(CancellationToken.None);
                _recording = false;
                LetGo();
                var kept = await KeepAsync(partial, $"{Name(summary.Map, start, end, hero, name)} (incomplete)");
                throw new McpException($"{e.Message} The recording so far is in {kept}.");
            }

            var file = await capture.StopRecordAsync(cancellationToken);
            _recording = false;
            LetGo();
            return new Clip(await KeepAsync(file, Name(summary.Map, start, end, hero, name)), from, to, camera);
        }
        finally {
            LetGo();
            _lock.Release();
        }
    }

    private void LetGo() {
        _hold?.Dispose();
        _hold = null;
    }

    /// <summary>The replay on screen, or null when that can't be told.</summary>
    private async Task<string?> OnScreenAsync(CancellationToken cancellationToken) {
        try {
            return (await controller.CurrentReplayAsync(cancellationToken)).Path;
        }
        catch (McpException) {
            return null;
        }
    }

    /// <summary>
    /// Waits until the replay plays past <paramref name="until"/>, making the planned camera moves on
    /// the way; returns the time it stopped at.
    /// </summary>
    private async Task<double> PlayUntilAsync(
        double until, Queue<CameraStep> plan, PlayerSummary? target, List<string> camera, CancellationToken cancellationToken) {
        var (start, length) = await controller.PanelTimeAsync(cancellationToken);
        until = Math.Min(until, length - 1);

        // The clock shows whole seconds and a read takes most of a second, so it's read on its own
        // task, and in between the replay time is estimated: the second the clock last ticked to,
        // plus the time since the read that first saw it. The estimate lags by up to a read's
        // length, so steps fire a little late, never early.
        var clock = new ClockEstimate(start);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var reader = Task.Run(async () => {
            while (!stop.IsCancellationRequested) {
                var readStart = DateTime.UtcNow;
                if (await controller.PeekPanelTimeAsync(stop.Token) is { } read) {
                    clock.Seen(read.Now, readStart);
                }

                // Back-to-back screenshots load the machine the game runs on.
                await Task.Delay(ReadPause, stop.Token);
            }
        }, stop.Token);

        try {
            var lastFollow = DateTime.UtcNow;
            while (true) {
                await Task.Delay(50, cancellationToken);
                if (reader.IsFaulted) {
                    await reader;
                }

                var now = clock.Estimate();
                if (clock.Last >= until) {
                    return clock.Last;
                }

                if (clock.StalledFor > StallTimeout) {
                    throw new McpException($"The replay stopped advancing at {ReplayController.ReplayTime(clock.Last)}.");
                }

                while (target is not null && plan.TryPeek(out var due) && due.FireAt <= now) {
                    logger.LogInformation("Firing {move} due {due:0.0}s at about {now:0.0}s", due.Move, due.FireAt, now);
                    await MoveAsync(plan.Dequeue(), target, camera, cancellationToken);
                    lastFollow = DateTime.UtcNow;
                }

                // Repeat the move that holds the camera: the game drops an everyone-view follow now
                // and then (clicking the portrait again picks the hero back up). Not shortly before
                // a planned move.
                if (target is not null && _hold is null && _last is { Move: CameraMove.EveryoneFollow } held
                    && DateTime.UtcNow - lastFollow > RefollowInterval
                    && !(plan.TryPeek(out var next) && next.FireAt - now < RefollowQuietBefore)) {
                    await MoveAsync(held, target, null, cancellationToken);
                    lastFollow = DateTime.UtcNow;
                }
            }
        }
        finally {
            await stop.CancelAsync();
            try {
                await reader;
            }
            catch (OperationCanceledException) {
            }
        }
    }

    /// <summary>Replay time between reads of the clock, which shows whole seconds.</summary>
    private sealed class ClockEstimate(double start) {
        private readonly Lock _lock = new();
        private DateTime _tickedAt = DateTime.UtcNow;
        private DateTime _movedAt = DateTime.UtcNow;

        public double Last { get; private set; } = start;

        public TimeSpan StalledFor {
            get {
                lock (_lock) {
                    return DateTime.UtcNow - _movedAt;
                }
            }
        }

        /// <param name="readStart">When the read began: the frame it read is from about then.</param>
        public void Seen(double now, DateTime readStart) {
            lock (_lock) {
                if (now > Last) {
                    Last = now;
                    _tickedAt = readStart;
                    _movedAt = DateTime.UtcNow;
                }
            }
        }

        public double Estimate() {
            lock (_lock) {
                return Math.Min(Last + (DateTime.UtcNow - _tickedAt).TotalSeconds, Last + 1);
            }
        }
    }

    /// <param name="camera">Where to log the move; null for a repeat, which isn't logged.</param>
    private async Task MoveAsync(CameraStep step, PlayerSummary target, List<string>? camera, CancellationToken cancellationToken) {
        // A hold lasts until the next move.
        LetGo();
        switch (step.Move) {
            case CameraMove.HeroView:
                await controller.FocusPlayerAsync(target.Index.ToString(), false, cancellationToken);
                await controller.ZoomFromDefaultAsync(_zoom ?? Zoom.Default, cancellationToken);
                break;
            case CameraMove.EveryoneFollow:
                await controller.FocusPlayerAsync(target.Index.ToString(), true, cancellationToken);
                break;
            case CameraMove.StayPut:
                await controller.PressAsync(Key.E, [], cancellationToken);
                break;
            case CameraMove.LookAt:
                await controller.LookAtAsync(Minimap.For(_map!)!, step.At!.Value, cancellationToken);
                break;
            case CameraMove.HoldAt:
                // Measured: Backspace then hold moves the camera off a hero who just took off; at a
                // death the hold alone keeps it put (Backspace there is untested).
                _hold = await controller.HoldAtAsync(Minimap.For(_map!)!, step.At!.Value,
                    clearSelection: step.Reason is "takes off" or "flying", cancellationToken);
                break;
        }

        _last = step;
        if (camera is null) {
            return;
        }

        var text = $"{ReplayController.ReplayTime(step.FireAt)} {step.Move} ({step.Reason}){(step.At is { } at ? $" @{at}" : "")}";
        camera.Add(text);
        logger.LogInformation("Camera: {step}", text);
    }

    private async Task<string> KeepAsync(string file, string name) {
        Directory.CreateDirectory(ClipsFolder);
        var target = UniquePath(ClipsFolder, name, Path.GetExtension(file));

        // Across drives this is a copy of a large file.
        await Task.Run(() => File.Move(file, target));
        logger.LogInformation("Saved recording {path}", target);
        return target;
    }

    /// <summary>E.g. "Tomb of the Spider Queen 6-55 to 7-05 Chromie".</summary>
    public static string Name(string map, double start, double end, string? hero, string? name) =>
        name ?? $"{map} {Time(start)} to {Time(end)}{(hero is null ? "" : $" {hero}")}";

    private static string Time(double seconds) => ReplayController.ReplayTime(seconds).Replace(':', '-');

    private static string UniquePath(string folder, string name, string extension) {
        foreach (var invalid in Path.GetInvalidFileNameChars()) {
            name = name.Replace(invalid, '_');
        }

        var path = Path.Combine(folder, name + extension);
        for (var i = 2; File.Exists(path); i++) {
            path = Path.Combine(folder, $"{name} ({i}){extension}");
        }

        return path;
    }
}
