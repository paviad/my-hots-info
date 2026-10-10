using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using MyReplayLibrary.Obs;

namespace MyHotsObserver;

/// <param name="Path">The recording's file.</param>
/// <param name="From">Replay time the recording starts at, in seconds.</param>
/// <param name="To">Replay time it stops at.</param>
public sealed record Clip(string Path, double From, double To);

/// <summary>
/// Records the observer scene with OBS into <see cref="ClipsFolder"/>, either between
/// <see cref="StartAsync"/> and <see cref="StopAsync"/> or a whole stretch of a replay with
/// <see cref="RecordClipAsync"/>.
/// </summary>
public sealed class ClipRecorder(
    ReplayController controller,
    ObserverCapture capture,
    ILogger<ClipRecorder> logger) {
    /// <summary>The longest stretch <see cref="RecordClipAsync"/> records.</summary>
    private const double MaxClipSeconds = 600;

    /// <summary>A replay whose clock stands still this long has stopped (paused, or ended).</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _recording;
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
        double start, double end, string? player, string? replay, double leadIn, double tail, string? name,
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
            if (all) {
                await controller.PressAsync(Key.E, [], cancellationToken);
            }
            else if (player is not null) {
                await controller.FocusPlayerAsync(player, cancellationToken);
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

                to = await PlayUntilAsync(end + Math.Max(0, tail), cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException) {
                var partial = await capture.StopRecordAsync(CancellationToken.None);
                _recording = false;
                var kept = await KeepAsync(partial, $"{Name(summary.Map, start, end, hero, name)} (incomplete)");
                throw new McpException($"{e.Message} The recording so far is in {kept}.");
            }

            var file = await capture.StopRecordAsync(cancellationToken);
            _recording = false;
            return new Clip(await KeepAsync(file, Name(summary.Map, start, end, hero, name)), from, to);
        }
        finally {
            _lock.Release();
        }
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

    /// <summary>Waits until the replay plays past <paramref name="until"/>; returns the time it stopped at.</summary>
    private async Task<double> PlayUntilAsync(double until, CancellationToken cancellationToken) {
        var (last, length) = await controller.PanelTimeAsync(cancellationToken);
        until = Math.Min(until, length - 1);
        var lastMoved = DateTime.UtcNow;
        while (true) {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            var (now, _) = await controller.PanelTimeAsync(cancellationToken);
            if (now >= until) {
                return now;
            }

            if (now > last) {
                last = now;
                lastMoved = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - lastMoved > StallTimeout) {
                throw new McpException($"The replay stopped advancing at {ReplayController.ReplayTime(now)}.");
            }
        }
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
