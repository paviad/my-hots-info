using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using MyReplayQuery.Events;

namespace MyHotsObserver;

public sealed record SeekResult(double Target, double Landed, double Seconds);

/// <summary>Drives the replay observer UI. One action at a time: they share the game's focus and clock.</summary>
public sealed class ReplayController(
    GameWindow gameWindow,
    GameInput input,
    ObserverCapture capture,
    ReplayClock clock,
    ReplayCatalog catalog,
    ILogger<ReplayController> logger) {
    private const string HotsDirEnvVar = "MYHOTSINFO_HOTS_DIR";
    private const string DefaultHotsDir = @"C:\Program Files (x86)\Heroes of the Storm";

    /// <summary>The F/B keys jump this far.</summary>
    private const double SkipSeconds = 10;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(400);

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>The replay <see cref="OpenAsync"/> last opened; null when it opened none.</summary>
    private string? _opened;

    /// <summary>Opens a replay in the running client and waits until it plays.</summary>
    public async Task<ReplaySummary> OpenAsync(string path, CancellationToken cancellationToken) {
        var summary = catalog.Summary(path) ?? throw new McpException($"Can't read {Path.GetFileName(path)}");
        gameWindow.Find();
        await _lock.WaitAsync(cancellationToken);
        try {
            var hotsDir = Environment.GetEnvironmentVariable(HotsDirEnvVar) is { Length: > 0 } dir ? dir : DefaultHotsDir;
            var switcher = Path.Combine(hotsDir, "Support64", "HeroesSwitcher_x64.exe");
            if (!File.Exists(switcher)) {
                throw new McpException($"{switcher} not found; set {HotsDirEnvVar} to the HotS install folder.");
            }

            var length = summary.Length.TotalSeconds;
            for (var handoff = 1; handoff <= 3; handoff++) {
                // The client ignores a replay handed to it while it plays another (or loads one we
                // handed over before), so quit that first.
                if (await InReplayAsync(cancellationToken)) {
                    await QuitReplayAsync(cancellationToken);
                }

                // With the client already running, the Switcher hands it the replay and exits.
                using (var process = Process.Start(new ProcessStartInfo(switcher) { ArgumentList = { path }, UseShellExecute = false })!) {
                    await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                }

                logger.LogInformation("Switcher handed over {path} (try {handoff})", path, handoff);
                _opened = path;

                // A loading screen first; a handover the client missed leaves it where it was.
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < TimeSpan.FromSeconds(25)) {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    var reading = await ReadAsync(cancellationToken);
                    if (reading.Length is { } l && Math.Abs(l.TotalSeconds - length) <= 2) {
                        return summary;
                    }
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
    /// then 10-second skips close a larger gap without passing it.
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

    /// <summary>Follows a player: by index (0-9), or by player or hero name in the open replay.</summary>
    public async Task<PlayerSummary> FocusPlayerAsync(string player, CancellationToken cancellationToken) {
        var summary = await CurrentReplayAsync(cancellationToken);
        var target = ResolvePlayer(summary, player);
        await PressAsync(Key.D1 + (ushort)target.Index, [], cancellationToken);
        return target;
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

    /// <summary>
    /// Resumes a replay known to be paused: presses P and checks the clock moves, pressing again if
    /// the game missed it. Cheaper than <see cref="SetPausedAsync"/>, which first checks the state.
    /// </summary>
    public async Task<bool> ResumeAsync(CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            for (var attempt = 0; attempt < 2; attempt++) {
                using (var session = input.Begin()) {
                    session.Press(Key.P);
                }

                if (!await IsPausedAsync(cancellationToken)) {
                    return true;
                }
            }

            return false;
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

    /// <summary>Whether a replay is on screen: its in-game timer shows.</summary>
    private async Task<bool> InReplayAsync(CancellationToken cancellationToken) =>
        (await ReadAsync(cancellationToken)).Game is not null;

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
