using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using MyReplayQuery.Events;

namespace MyHotsObserver;

public sealed record SeekResult(double Target, double Landed, double Seconds);

/// <summary>The observer camera's zoom: the game's default, out (Z), or farther out (Shift+Z).</summary>
public enum Zoom { Default, Out, Far }

/// <summary>Drives the replay observer UI. One action at a time: they share the game's focus and clock.</summary>
public sealed class ReplayController(
    GameWindow gameWindow,
    GameInput input,
    ObserverCapture capture,
    ReplayClock clock,
    ReplayCatalog catalog,
    ClientSession session,
    ILogger<ReplayController> logger) {
    private const string HotsDirEnvVar = "MYHOTSINFO_HOTS_DIR";
    private const string DefaultHotsDir = @"C:\Program Files (x86)\Heroes of the Storm";

    /// <summary>The F/B keys jump this far.</summary>
    private const double SkipSeconds = 20;

    private static string HotsDir =>
        Environment.GetEnvironmentVariable(HotsDirEnvVar) is { Length: > 0 } dir ? dir : DefaultHotsDir;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(400);

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>The replay <see cref="OpenAsync"/> last opened; null when it opened none.</summary>
    private string? _opened;

    /// <summary>
    /// Sets the camera's zoom. Z and Shift+Z only toggle, so this reads the current zoom off the
    /// minimap (<see cref="ZoomMeter"/>, against the map's <see cref="ZoomCalibration"/>), presses
    /// the one key that gets there, and reads it again. Without a reading it falls back to
    /// Shift+Z, Z, Z, which always lands on the default, and zooms from there.
    /// </summary>
    public async Task SetZoomAsync(Zoom zoom, string map, CancellationToken cancellationToken) {
        if (ZoomCalibration.DefaultWidth(map) is { } defaultWidth) {
            for (var attempt = 0; attempt < 2; attempt++) {
                if (await ReadZoomAsync(defaultWidth, cancellationToken) is not { } current) {
                    break;
                }

                if (ZoomMeter.KeyFor(current, zoom) is not var (key, modifiers)) {
                    return;
                }

                await PressAsync(key, modifiers, cancellationToken);
                await Task.Delay(300, cancellationToken);
            }

            logger.LogInformation("Couldn't read the zoom off the minimap; resetting with Shift+Z, Z, Z");
        }

        foreach (var shift in new[] { true, false, false }) {
            await PressAsync(Key.Z, shift ? [Key.Shift] : [], cancellationToken);
            await Task.Delay(200, cancellationToken);
        }

        await ZoomFromDefaultAsync(zoom, cancellationToken);
    }

    /// <summary>
    /// Measures <paramref name="map"/>'s minimap outline at the default zoom, once per map: resets
    /// with Shift+Z, Z, Z, reads the outline, then reads it again zoomed out (Z, and Z back) and
    /// keeps the reading only if the two are <see cref="ZoomMeter.OutRatio"/> apart, which rules
    /// out a misread. It leaves the zoom at the default and the camera where it was. (A hero-view
    /// follow would reset the zoom too, but it spoils a following everyone-view follow.)
    /// </summary>
    public async Task CalibrateZoomAsync(string map, CancellationToken cancellationToken) {
        if (ZoomCalibration.DefaultWidth(map) is not null) {
            return;
        }

        foreach (var shift in new[] { true, false, false }) {
            await PressAsync(Key.Z, shift ? [Key.Shift] : [], cancellationToken);
            await Task.Delay(200, cancellationToken);
        }

        var atDefault = await MeasureViewportAsync(cancellationToken);
        await PressAsync(Key.Z, [], cancellationToken);
        var zoomedOut = await MeasureViewportAsync(cancellationToken);
        await PressAsync(Key.Z, [], cancellationToken);
        if (atDefault is { } d && zoomedOut is { } o && Math.Abs(o / d - ZoomMeter.OutRatio) < 0.1) {
            ZoomCalibration.Save(map, d);
            logger.LogInformation("{map}: minimap outline is {width:0} px at the default zoom", map, d);
        }
        else {
            logger.LogInformation("{map}: couldn't calibrate the zoom (read {d} and {o})", map, atDefault, zoomedOut);
        }
    }

    private async Task<double?> MeasureViewportAsync(CancellationToken cancellationToken) {
        // The zoom eases in over most of a second.
        await Task.Delay(1000, cancellationToken);
        using var frame = await capture.FrameAsync(cancellationToken);
        return ZoomMeter.ViewportWidth(frame, gameWindow.Layout());
    }

    /// <summary>The zoom the minimap shows; null when the outline can't be read or matches no level.</summary>
    private async Task<Zoom?> ReadZoomAsync(double defaultWidth, CancellationToken cancellationToken) {
        using var frame = await capture.FrameAsync(cancellationToken);
        if (ZoomMeter.ViewportWidth(frame, gameWindow.Layout()) is not { } width) {
            return null;
        }

        // A width far from every level means another minimap scale (Haunted Mines underground).
        var ratio = width / defaultWidth;
        return new[] { 1, ZoomMeter.OutRatio, ZoomMeter.FarRatio }.Any(r => Math.Abs(ratio - r) < 0.1)
            ? ZoomMeter.Classify(width, defaultWidth)
            : null;
    }

    /// <summary>Zooms a camera at the default zoom: just reset, or just followed in hero view, which resets it.</summary>
    public async Task ZoomFromDefaultAsync(Zoom zoom, CancellationToken cancellationToken) {
        if (zoom != Zoom.Default) {
            await PressAsync(Key.Z, zoom == Zoom.Far ? [Key.Shift] : [], cancellationToken);
        }
    }

    /// <summary>Opens a replay in the observer's client, starting it when none runs, and waits until it plays.</summary>
    public async Task<ReplaySummary> OpenAsync(string path, CancellationToken cancellationToken) {
        var summary = catalog.Summary(path) ?? throw new McpException($"Can't read {Path.GetFileName(path)}");
        await session.EnsureClientAsync(cancellationToken);
        gameWindow.Find();
        await _lock.WaitAsync(cancellationToken);
        try {
            var hotsDir = HotsDir;
            var switcher = Path.Combine(hotsDir, "Support64", "HeroesSwitcher_x64.exe");
            if (!File.Exists(switcher)) {
                throw new McpException($"{switcher} not found; set {HotsDirEnvVar} to the HotS install folder.");
            }

            // The Switcher restarts the client to switch builds. Onto an older build the client
            // plays the replay, but back onto the current one it lands on the login screen, so
            // close it and start the current build through Battle.net, signed in.
            var replayBuild = ReplayFiles.ReadBuild(path);
            if (GameWindow.RunningBuild() is { } clientBuild && clientBuild != replayBuild
                && replayBuild == CurrentBuild(hotsDir)) {
                foreach (var client in Process.GetProcessesByName(GameWindow.ProcessName)) {
                    client.Kill();
                    await client.WaitForExitAsync(cancellationToken);
                }

                logger.LogInformation("Closed the build {old} client to start the current build {build}", clientBuild, replayBuild);
                await session.EnsureClientAsync(cancellationToken);
            }

            var length = summary.Length.TotalSeconds;

            // A client on an older build was started by the Switcher and isn't signed in: quitting a
            // replay there lands on the login screen. So don't quit; close the client and start the
            // Switcher with the replay. It starts the current build, which restarts on the replay's
            // build and plays it, so this takes a minute or two.
            if (GameWindow.RunningBuild() is { } oldBuild && oldBuild != CurrentBuild(hotsDir)) {
                foreach (var client in Process.GetProcessesByName(GameWindow.ProcessName)) {
                    client.Kill();
                    await client.WaitForExitAsync(cancellationToken);
                }

                Process.Start(new ProcessStartInfo(switcher) { ArgumentList = { path }, UseShellExecute = false })?.Dispose();
                logger.LogInformation("Closed the build {old} client; the Switcher starts {path} from cold", oldBuild, path);
                _opened = path;
                await WaitForClientAsync(replayBuild, cancellationToken);
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < TimeSpan.FromMinutes(3)) {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    if ((await TryReadAsync(cancellationToken))?.Length is { } l && Math.Abs(l.TotalSeconds - length) <= 2) {
                        return summary;
                    }
                }

                throw new McpException($"Build {replayBuild}'s client didn't start {Path.GetFileName(path)} within 3 minutes.");
            }

            for (var handoff = 1; handoff <= 3; handoff++) {
                // The client ignores a replay handed to it while it plays another (or loads one we
                // handed over before), so quit that first.
                if (await InReplayAsync(cancellationToken)) {
                    await QuitReplayAsync(cancellationToken);
                }

                // A replay from another build makes the Switcher close the client and start that
                // build's client, which plays the replay once it's up. Otherwise the Switcher hands
                // the running client the replay and exits.
                var running = GameWindow.RunningBuild();
                var switching = running is not null && running != replayBuild;
                using (var process = Process.Start(new ProcessStartInfo(switcher) { ArgumentList = { path }, UseShellExecute = false })!) {
                    await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                }

                logger.LogInformation("Switcher handed over {path} (try {handoff}, build {build}, client {running})",
                    path, handoff, replayBuild, running);
                _opened = path;

                if (switching) {
                    await WaitForClientAsync(replayBuild, cancellationToken);
                }

                // A loading screen first; a handover the client missed leaves it where it was. A
                // freshly started client takes longer to load the replay.
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < TimeSpan.FromSeconds(switching ? 120 : 25)) {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    var reading = await TryReadAsync(cancellationToken);
                    if (reading?.Length is { } l && Math.Abs(l.TotalSeconds - length) <= 2) {
                        return summary;
                    }
                }

                if (switching) {
                    // A restart that left the client on the home screen gets the usual handover next.
                    logger.LogInformation("Build {build}'s client didn't start the replay", replayBuild);
                }
            }

            throw new McpException("The client didn't start the replay; is it on its home or Watch screen?");
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>
    /// Jumps to <paramref name="seconds"/>: a timeline click lands at or a few seconds before it,
    /// then 20-second skips (F) close a larger gap without passing it.
    /// </summary>
    public async Task<SeekResult> SeekAsync(double seconds, CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            var watch = Stopwatch.StartNew();
            var layout = gameWindow.Layout();
            var (now, length) = await PanelAsync(cancellationToken);
            var target = Math.Clamp(seconds, 0, length - 1);

            if (Math.Abs(now - target) > 2) {
                using (var session = input.Begin()) {
                    session.Click(layout.TimelineX(target, length), layout.TimelineBar.Y);
                }

                // Into time the replay hasn't played yet, the game simulates its way there.
                var timeout = TimeSpan.FromSeconds(20 + Math.Max(0, target - now) / 8);
                var settled = await SettleAsync(target, timeout, cancellationToken);
                logger.LogInformation("Clicked to {target:0}s, landed at {landed:0}s", target, settled);

                // A click can land past the target, which would cut the lead-in: click earlier by
                // the overshoot plus a margin.
                for (var retry = 0; settled - target > 2 && retry < 2; retry++) {
                    var earlier = Math.Max(0, target - (settled - target) - 5);
                    using (var session = input.Begin()) {
                        session.Click(layout.TimelineX(earlier, length), layout.TimelineBar.Y);
                    }

                    settled = await SettleAsync(earlier, TimeSpan.FromSeconds(20), cancellationToken);
                    logger.LogInformation("Landed past it; clicked to {earlier:0}s, landed at {landed:0}s", earlier, settled);
                }

                for (var skips = 0; target - settled >= SkipSeconds && skips < 6; skips++) {
                    using (var session = input.Begin()) {
                        session.Press(Key.F);
                    }

                    settled = await SettleAsync(settled + SkipSeconds, TimeSpan.FromSeconds(15), cancellationToken);
                }

                now = settled;
            }

            return new SeekResult(target, now, watch.Elapsed.TotalSeconds);
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>The client process <see cref="EnsureCameraFollowAsync"/> turned camera follow mode on in.</summary>
    private int? _followOnIn;

    /// <summary>
    /// Camera follow mode (L) as the client saved it in Variables.txt, which it writes the moment
    /// the mode changes; null when the file doesn't say. A client on the current build is signed
    /// in and uses its account's file; one the Switcher started on an older build isn't.
    /// </summary>
    public static bool? CameraFollow() =>
        bool.TryParse(GameVariables.Get("camerafollow", signedIn: GameWindow.RunningBuild() is { } b && b == CurrentBuild(HotsDir)),
            out var on) ? on : null;

    /// <summary>Whether camera follow mode (L) is known to be on in the running client.</summary>
    public bool CameraFollowKnownOn => CameraFollow() ?? (_followOnIn is { } pid && pid == GameWindow.RunningProcessId());

    /// <summary>
    /// Turns on camera follow mode (L), so a click on a top-bar portrait follows that hero, and
    /// leaves the camera following <paramref name="target"/>. L only toggles it, so this goes by
    /// <see cref="CameraFollow"/>: when it's off, press L and wait for the client to save it on.
    /// When the file doesn't say, nothing on screen shows the mode either: click the portrait,
    /// and if the view doesn't jump, press L, which centres the camera on the hero. That works
    /// only right after the replay starts, when the camera isn't on the hero yet.
    /// </summary>
    public async Task EnsureCameraFollowAsync(PlayerSummary target, CancellationToken cancellationToken) {
        var summary = await CurrentReplayAsync(cancellationToken);
        var slot = summary.Players.Where(p => p.Team == target.Team).ToList().IndexOf(target);
        var layout = gameWindow.Layout();
        var (px, py) = layout.Portrait(target.Team, slot);
        await _lock.WaitAsync(cancellationToken);
        try {
            if (CameraFollow() is { } saved) {
                if (!saved) {
                    using (var session = input.Begin()) {
                        session.Press(Key.L);
                    }

                    var watch = Stopwatch.StartNew();
                    while (CameraFollow() != true) {
                        if (watch.Elapsed > TimeSpan.FromSeconds(3)) {
                            throw new McpException("Pressed L for camera follow mode, but the client didn't save it as on in Variables.txt.");
                        }

                        await Task.Delay(PollInterval, cancellationToken);
                    }

                    logger.LogInformation("Turned camera follow mode on (L)");
                }

                using (var session = input.Begin()) {
                    session.Click(px, py);
                }

                return;
            }

            using var before = await MapViewAsync(layout, cancellationToken);
            using (var session = input.Begin()) {
                session.Click(px, py);
            }

            var clicked = await ViewChangeAsync(before, layout, cancellationToken);
            if (clicked > JumpThreshold) {
                _followOnIn = GameWindow.RunningProcessId();
                return;
            }

            using (var session = input.Begin()) {
                session.Press(Key.L);
            }

            var pressed = await ViewChangeAsync(before, layout, cancellationToken);
            logger.LogInformation("Portrait click changed the view by {clicked:0.0}; L by {pressed:0.0}", clicked, pressed);
            if (pressed > JumpThreshold) {
                _followOnIn = GameWindow.RunningProcessId();
                return;
            }

            throw new McpException(
                $"Couldn't turn on camera follow mode (L): neither the portrait click nor L moved the camera " +
                $"(view changes {clicked:0.0} and {pressed:0.0}). Is the camera already on the hero?");
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>A camera jump changes most of the view; units moving under a still camera change it by under 10.</summary>
    private const double JumpThreshold = 12;

    /// <summary>How much the view changed from <paramref name="before"/> a second later (mean gray level difference).</summary>
    private async Task<double> ViewChangeAsync(OpenCvSharp.Mat before, ObserverLayout layout, CancellationToken cancellationToken) {
        await Task.Delay(1000, cancellationToken);
        using var after = await MapViewAsync(layout, cancellationToken);
        using var diff = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.Absdiff(before, after, diff);
        return OpenCvSharp.Cv2.Mean(diff).Val0;
    }

    /// <summary>The middle of the screen in gray, scaled down.</summary>
    private async Task<OpenCvSharp.Mat> MapViewAsync(ObserverLayout layout, CancellationToken cancellationToken) {
        using var frame = await capture.FrameAsync(cancellationToken);
        using var crop = new OpenCvSharp.Mat(frame, layout.MapView);
        var gray = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.CvtColor(crop, gray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
        OpenCvSharp.Cv2.Resize(gray, gray, new OpenCvSharp.Size(), 0.25, 0.25, OpenCvSharp.InterpolationFlags.Area);
        return gray;
    }

    /// <summary>Follows a player: by index (0-9), or by player or hero name in the open replay.</summary>
    /// <param name="everyone">
    /// Follow in the everyone view (E, then a click on the hero's top-bar portrait): no hero
    /// panel. Otherwise the hero view (the player's number key), with the hero's panel.
    /// </param>
    public async Task<PlayerSummary> FocusPlayerAsync(string player, bool everyone, CancellationToken cancellationToken) {
        var summary = await CurrentReplayAsync(cancellationToken);
        var target = ResolvePlayer(summary, player);
        if (!everyone) {
            await PressAsync(Key.D1 + (ushort)target.Index, [], cancellationToken);
            return target;
        }

        var slot = summary.Players.Where(p => p.Team == target.Team).ToList().IndexOf(target);
        var (x, y) = gameWindow.Layout().Portrait(target.Team, slot);
        await _lock.WaitAsync(cancellationToken);
        try {
            // Clicking the portrait of the hero already selected does nothing, so clear the
            // selection first: the click then always starts a fresh follow.
            using var session = input.Begin();
            session.Press(Key.E);
            Thread.Sleep(150);
            session.Press(Key.Backspace);
            Thread.Sleep(150);
            session.Click(x, y);
        }
        finally {
            _lock.Release();
        }

        return target;
    }

    /// <summary>
    /// Points the camera at a map position: clears the selection (Backspace), then clicks the
    /// minimap; with the hero still selected the game can pull the camera back to him.
    /// </summary>
    public async Task LookAtAsync(Minimap minimap, MapPoint point, CancellationToken cancellationToken) {
        var (x, y) = minimap.Pixel(point, gameWindow.Layout());
        await _lock.WaitAsync(cancellationToken);
        try {
            using var session = input.Begin();
            session.Press(Key.Backspace);
            session.Click(x, y);
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>
    /// Holds the minimap down at a map position (see <see cref="GameInput.Session.Hold"/>), keeping
    /// the game in front until the returned session is disposed, which lets go.
    /// </summary>
    /// <param name="clearSelection">Backspace first, dropping the follow of the selected hero.</param>
    public async Task<GameInput.Session> HoldAtAsync(Minimap minimap, MapPoint point, bool clearSelection, CancellationToken cancellationToken) {
        var (x, y) = minimap.Pixel(point, gameWindow.Layout());
        await _lock.WaitAsync(cancellationToken);
        try {
            var session = input.Begin();
            if (clearSelection) {
                session.Press(Key.Backspace);
            }

            session.Hold(x, y);
            return session;
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>Sends one key (with modifiers) to the game.</summary>
    public async Task PressAsync(Key key, Key[] modifiers, CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            using var session = input.Begin();
            session.Press(key, modifiers);
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>
    /// Lets the playing replay run until <paramref name="seconds"/> and pauses it there, to within
    /// about a second (the clock shows whole seconds). Returns the time it paused at.
    /// </summary>
    /// <remarks>
    /// The pause takes effect about 1.5s after the clock is read past the threshold (the read's own
    /// delay, then bringing the game to the front), so it presses that much early.
    /// </remarks>
    public async Task<double> PauseAtAsync(double seconds, CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            var (now, _) = await PanelAsync(cancellationToken);
            var last = now;
            var lastMoved = Stopwatch.StartNew();
            var resumes = 0;
            while (now < seconds - 1.5) {
                if (lastMoved.Elapsed > TimeSpan.FromSeconds(2)) {
                    // Paused (a seek too short to click leaves it as it was): resume it. The game
                    // misses a press now and then, so allow a few.
                    if (++resumes > 3) {
                        throw new McpException($"The replay isn't playing (stuck at {ReplayTime(now)}).");
                    }

                    using (var session = input.Begin()) {
                        session.Press(Key.P);
                    }

                    lastMoved.Restart();
                }

                await Task.Delay(100, cancellationToken);
                (now, _) = await PanelAsync(cancellationToken);
                if (now > last) {
                    last = now;
                    lastMoved.Restart();
                }
            }

            // A press the game misses gets one more try.
            for (var attempt = 0; attempt < 2; attempt++) {
                using (var session = input.Begin()) {
                    session.Press(Key.P);
                }

                if (await IsPausedAsync(cancellationToken)) {
                    return (await PanelAsync(cancellationToken)).Now;
                }
            }

            throw new McpException("Couldn't pause the replay.");
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>Whether the replay is paused: its clock stands still for over a second.</summary>
    public async Task<bool> IsPausedNowAsync(CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            return await IsPausedAsync(cancellationToken);
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>
    /// The replay panel's clock without waiting for other actions: it only reads the screen, so it
    /// can run while an action sends input. Null while the panel can't be read.
    /// </summary>
    public async Task<(double Now, double Length)?> PeekPanelTimeAsync(CancellationToken cancellationToken) {
        var reading = await ReadAsync(cancellationToken);
        return reading is { Replay: { } t, Length: { } l } ? (t.TotalSeconds, l.TotalSeconds) : null;
    }

    /// <summary>The replay panel's elapsed time and the replay's length, in seconds.</summary>
    public async Task<(double Now, double Length)> PanelTimeAsync(CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            return await PanelAsync(cancellationToken);
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>Pauses or resumes; the panel's clock standing still is how it tells which it is.</summary>
    public async Task<bool> SetPausedAsync(bool paused, CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            // A press the game misses (it reads keys once a frame) gets one more try.
            for (var attempt = 0; attempt < 2 && await IsPausedAsync(cancellationToken) != paused; attempt++) {
                using var session = input.Begin();
                session.Press(Key.P);
            }

            return await IsPausedAsync(cancellationToken);
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>Presses the speed key once and reads the new speed label.</summary>
    public async Task<string?> ChangeSpeedAsync(bool faster, CancellationToken cancellationToken) {
        await PressAsync(faster ? Key.Equals : Key.Minus, [], cancellationToken);
        await Task.Delay(300, cancellationToken);
        var layout = gameWindow.Layout();
        using var frame = await capture.FrameAsync(cancellationToken);
        return clock.ReadSpeed(frame, layout);
    }

    /// <summary>
    /// Takes the replay on screen to be <paramref name="path"/> when it's as long, e.g. one the
    /// user opened, so it isn't reopened (on an older build's client, reopening means a restart).
    /// </summary>
    public async Task<bool> AdoptIfOnScreenAsync(string path, CancellationToken cancellationToken) {
        if (catalog.Summary(path) is not { } summary || await TryReadAsync(cancellationToken) is not { Length: { } length }
            || Math.Abs(summary.Length.TotalSeconds - length.TotalSeconds) > 2) {
            return false;
        }

        _opened = path;
        return true;
    }

    /// <summary>
    /// The replay on screen: the one <see cref="OpenAsync"/> opened, or else the only recent
    /// replay as long as the panel says.
    /// </summary>
    public async Task<ReplaySummary> CurrentReplayAsync(CancellationToken cancellationToken) {
        var (_, length) = await PanelAsync(cancellationToken);
        if (_opened is not null && catalog.Summary(_opened) is { } opened && Math.Abs(opened.Length.TotalSeconds - length) <= 2) {
            return opened;
        }

        var candidates = ReplayFiles.AllReplays().Take(100)
            .Select(f => catalog.Summary(f.FullName))
            .OfType<ReplaySummary>()
            .Where(s => Math.Abs(s.Length.TotalSeconds - length) <= 1)
            .ToList();
        return candidates.Count switch {
            1 => candidates[0],
            0 => throw new McpException(
                "Can't tell which replay is on screen; open it with open_replay, or follow a player by index (0-9)."),
            _ => throw new McpException(
                $"{candidates.Count} recent replays are as long as the one on screen; open it with open_replay, or follow a player by index (0-9)."),
        };
    }

    public static PlayerSummary ResolvePlayer(ReplaySummary summary, string player) {
        if (player.Equals("me", StringComparison.OrdinalIgnoreCase)) {
            return summary.Players.FirstOrDefault(p => p.IsMe)
                   ?? throw new McpException("This replay isn't from one of your accounts, so it has no 'me'.");
        }

        if (int.TryParse(player, out var index)) {
            return summary.Players.FirstOrDefault(p => p.Index == index)
                   ?? throw new McpException($"Player index {index} is out of range 0-{summary.Players.Count - 1}.");
        }

        var matches = summary.Players
            .Where(p => string.Equals(p.Name, player, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(p.Hero, player, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 0) {
            matches = summary.Players
                .Where(p => p.Name.Contains(player, StringComparison.OrdinalIgnoreCase)
                            || p.Hero.Contains(player, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return matches.Count switch {
            1 => matches[0],
            0 => throw new McpException(
                $"No player or hero '{player}' in this replay: {string.Join(", ", summary.Players.Select(p => $"{p.Index} {p.Name} ({p.Hero})"))}"),
            _ => throw new McpException(
                $"'{player}' matches {string.Join(", ", matches.Select(p => $"{p.Index} {p.Name} ({p.Hero})"))}; use the index."),
        };
    }

    /// <summary>Leaves the replay on screen through the game menu (F10, Quit Replay), back to the Watch screen.</summary>
    private async Task QuitReplayAsync(CancellationToken cancellationToken) {
        using (var session = input.Begin()) {
            session.Press(Key.F10);
            Thread.Sleep(700);
            session.Press(Key.Q);
        }

        var watch = Stopwatch.StartNew();
        while (await InReplayAsync(cancellationToken)) {
            if (watch.Elapsed > TimeSpan.FromSeconds(20)) {
                throw new McpException("Couldn't leave the replay on screen (F10, Quit Replay).");
            }

            await Task.Delay(500, cancellationToken);
        }

        // The clocks fade under the game menu before the Watch screen is up, and a replay handed
        // over before then is lost.
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        logger.LogInformation("Quit the replay on screen");
    }

    /// <summary>
    /// Waits for the client to restart on <paramref name="build"/>: the old one exits, and the new
    /// one shows its window. Starting an installed build takes a minute or two.
    /// </summary>
    private async Task WaitForClientAsync(int build, CancellationToken cancellationToken) {
        var timeout = TimeSpan.FromMinutes(4);
        var watch = Stopwatch.StartNew();
        while (GameWindow.RunningBuild() != build) {
            if (watch.Elapsed > timeout) {
                throw new McpException(
                    $"The client didn't restart on build {build} (the replay's) within {timeout.TotalMinutes:0} minutes; " +
                    $"it's {(GameWindow.RunningBuild() is { } b ? $"on build {b}" : "not running")}.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        logger.LogInformation("Client restarted on build {build} after {seconds:0}s", build, watch.Elapsed.TotalSeconds);
    }

    /// <summary>The newest build installed (Versions\Base98348), the one Battle.net starts; null if none is found.</summary>
    private static int? CurrentBuild(string hotsDir) {
        var versions = Path.Combine(hotsDir, "Versions");
        return Directory.Exists(versions)
            ? Directory.EnumerateDirectories(versions, "Base*")
                .Select(d => int.TryParse(Path.GetFileName(d)[4..], out var b) ? b : (int?)null)
                .Max()
            : null;
    }

    /// <summary>The clocks, or null while there's no game window to read, e.g. during a restart.</summary>
    private async Task<ClockReading?> TryReadAsync(CancellationToken cancellationToken) {
        try {
            return await ReadAsync(cancellationToken);
        }
        catch (McpException) {
            return null;
        }
    }

    /// <summary>Whether a replay is on screen, including one that has played to its end.</summary>
    private async Task<bool> InReplayAsync(CancellationToken cancellationToken) =>
        (await ReadAsync(cancellationToken)).InReplay;

    private async Task<bool> IsPausedAsync(CancellationToken cancellationToken) {
        var (first, _) = await PanelAsync(cancellationToken);
        // The clock shows whole seconds, so wait past a full second for it to move.
        await Task.Delay(TimeSpan.FromSeconds(1.2), cancellationToken);
        var (second, _) = await PanelAsync(cancellationToken);
        return Math.Abs(second - first) < 0.5;
    }

    /// <summary>
    /// Waits for a seek to finish: the panel clock disappears while the game catches up, then
    /// reads near the target and keeps counting.
    /// </summary>
    private async Task<double> SettleAsync(double target, TimeSpan timeout, CancellationToken cancellationToken) {
        var watch = Stopwatch.StartNew();
        double? previous = null;
        while (watch.Elapsed < timeout) {
            await Task.Delay(PollInterval, cancellationToken);
            var reading = await ReadAsync(cancellationToken);
            if (reading.Replay is not { } t) {
                previous = null;
                continue;
            }

            var now = t.TotalSeconds;
            if (Math.Abs(now - target) <= 15 && previous is { } p && now - p is >= 0 and <= 3) {
                return now;
            }

            previous = now;
        }

        throw new McpException($"The replay didn't reach {ReplayTime(target)} within {timeout.TotalSeconds:0}s.");
    }

    /// <summary>
    /// The panel clock. Waits out a running seek, and expands the panel when Ctrl+T collapsed it
    /// to a bare clock without the length and timeline.
    /// </summary>
    private async Task<(double Now, double Length)> PanelAsync(CancellationToken cancellationToken) {
        var expanded = false;
        for (var attempt = 0; attempt < 10; attempt++) {
            var layout = gameWindow.Layout();
            using (var frame = await capture.FrameAsync(cancellationToken)) {
                var reading = clock.Read(frame, layout);
                if (reading is { Replay: { } t, Length: { } l }) {
                    return (t.TotalSeconds, l.TotalSeconds);
                }

                if (!expanded && clock.IsPanelCollapsed(frame, layout)) {
                    using var session = input.Begin();
                    session.Press(Key.T, Key.Control);
                    expanded = true;
                    logger.LogInformation("Expanded the collapsed replay panel");
                }
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new McpException("Can't read the replay panel's clock. Is a replay open?");
    }

    private async Task<ClockReading> ReadAsync(CancellationToken cancellationToken) {
        var layout = gameWindow.Layout();
        using var frame = await capture.FrameAsync(cancellationToken);
        return clock.Read(frame, layout);
    }

    public static string ReplayTime(double seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";
}
