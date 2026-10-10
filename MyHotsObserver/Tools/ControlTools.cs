using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using MyReplayQuery.Query;

namespace MyHotsObserver.Tools;

/// <summary>
/// Tools that send input to the game. Each brings the game to the front for a moment and then
/// gives the front back to the window that had it.
/// </summary>
[McpServerToolType]
public sealed class ControlTools(ReplayController controller) {
    [McpServerTool(Name = "open_replay")]
    [Description("""
        Opens a replay in the running HotS client (start the client from Battle.net first) and waits
        until it plays. Returns the replay's players.
        """)]
    public async Task<object> OpenReplay(
        [Description("Replay file path, 'latest', or N for the N-th newest")] string replay,
        CancellationToken cancellationToken) {
        var summary = await controller.OpenAsync(ReplayCatalog.Resolve(replay), cancellationToken);
        return new {
            opened = summary.Path,
            summary.Map,
            length = ReplayController.ReplayTime(summary.Length.TotalSeconds),
            players = summary.Players.Select(p => new { p.Index, p.FocusKey, p.Hero, p.Name, p.Team, p.IsMe }),
        };
    }

    [McpServerTool(Name = "seek")]
    [Description("""
        Jumps the open replay to a time, on the replay panel's clock (the clock find_moments uses).
        Lands at or a few seconds before the target, after leadIn. Jumping back, or forward into
        time already played, takes seconds; forward into unplayed time the game simulates its way
        there at about 11x, e.g. ~80s for 15 minutes.
        """)]
    public async Task<object> Seek(
        [Description("Target time, m:ss or seconds, e.g. \"9:33\" or \"573.1\"")] string time,
        [Description("Seconds to land before the target, so the moment plays from its lead-up")] double leadIn = 3,
        CancellationToken cancellationToken = default) {
        double target;
        try {
            target = ResultFormatter.ParseTime(time);
        }
        catch (FormatException) {
            throw new McpException($"'{time}' is not a time; use m:ss or seconds");
        }

        var result = await controller.SeekAsync(target - Math.Max(0, leadIn), cancellationToken);
        return new {
            aimedAt = ReplayController.ReplayTime(result.Target),
            landedAt = ReplayController.ReplayTime(result.Landed),
            secondsBeforeTarget = Math.Round(target - result.Landed, 1),
            took = $"{result.Seconds:0.0}s",
        };
    }

    [McpServerTool(Name = "focus_player")]
    [Description("""
        Makes the camera follow a player of the open replay, by index (0-9), player name, hero, or
        'me'; or 'all' to stop following. The observer sees both teams' vision in every view.
        view 'hero' follows with the hero's own panel (abilities, health) at the bottom; 'everyone'
        follows in the everyone view (E, then a click on the hero's top-bar portrait), without it.
        The game's following is flaky: in the everyone view it doesn't follow D.Va in her mech, and
        no view can follow Deathwing while he flies (the camera waits and picks him up on landing).
        """)]
    public async Task<object> FocusPlayer(
        [Description("Index 0-9, player name, hero, 'me' or 'all', e.g. \"5\", \"Player\" or \"Chromie\"")] string player,
        [Description("'hero' or 'everyone'")] string view = "hero",
        CancellationToken cancellationToken = default) {
        if (player.Equals("all", StringComparison.OrdinalIgnoreCase)) {
            await controller.PressAsync(Key.E, [], cancellationToken);
            return new { following = "all" };
        }

        var p = await controller.FocusPlayerAsync(player, Everyone(view), cancellationToken);
        return new { following = new { p.Index, p.FocusKey, p.Hero, p.Name, p.Team }, view };
    }

    internal static bool Everyone(string view) => view.ToLowerInvariant() switch {
        "hero" => false,
        "everyone" => true,
        _ => throw new McpException("view must be 'hero' or 'everyone'"),
    };

    [McpServerTool(Name = "playback")]
    [Description("Pauses, resumes, or changes the replay's speed by one step. Returns the resulting state.")]
    public async Task<object> Playback(
        [Description("'pause', 'play', 'faster' or 'slower'")] string action,
        CancellationToken cancellationToken) {
        switch (action.ToLowerInvariant()) {
            case "pause":
            case "play":
                var paused = await controller.SetPausedAsync(action.Equals("pause", StringComparison.OrdinalIgnoreCase), cancellationToken);
                return new { paused };
            case "faster":
            case "slower":
                var speed = await controller.ChangeSpeedAsync(action.Equals("faster", StringComparison.OrdinalIgnoreCase), cancellationToken);
                return new { speed };
            default:
                throw new McpException("action must be 'pause', 'play', 'faster' or 'slower'");
        }
    }

    [McpServerTool(Name = "panel")]
    [Description("""
        Toggles an observer panel: 'leaderboard' (Tab) is the ten players' talents and stats;
        'replay_panel' (Ctrl+T) collapses or expands the replay panel (seek and the clocks expand
        it again when they need it).
        """)]
    public async Task<string> Panel(
        [Description("'leaderboard' or 'replay_panel'")] string name,
        CancellationToken cancellationToken) {
        var (key, modifiers) = name.ToLowerInvariant() switch {
            "leaderboard" => (Key.Tab, Array.Empty<Key>()),
            "replay_panel" => (Key.T, [Key.Control]),
            _ => throw new McpException("name must be 'leaderboard' or 'replay_panel'"),
        };
        await controller.PressAsync(key, modifiers, cancellationToken);
        return $"Toggled {name}";
    }
}
