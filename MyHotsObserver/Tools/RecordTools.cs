using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using MyReplayQuery.Query;

namespace MyHotsObserver.Tools;

/// <summary>Records the observer scene with OBS. Recordings go to MyHotsInfo Recordings\Clips under Videos.</summary>
[McpServerToolType]
public sealed class RecordTools(ClipRecorder recorder) {
    [McpServerTool(Name = "record_start")]
    [Description("""
        Starts recording the OBS scene 'MyHotsInfo Observer' (the game). Stop with record_stop.
        Refuses while OBS is already recording.
        """)]
    public async Task<string> RecordStart(
        [Description("File name for the recording, without extension")] string? name = null,
        CancellationToken cancellationToken = default) {
        await recorder.StartAsync(name, cancellationToken);
        return "Recording";
    }

    [McpServerTool(Name = "record_stop")]
    [Description("Stops the recording record_start began and returns the saved file.")]
    public async Task<object> RecordStop(
        [Description("File name, overriding the one given to record_start")] string? name = null,
        CancellationToken cancellationToken = default) {
        return new { file = await recorder.StopAsync(name, cancellationToken) };
    }

    [McpServerTool(Name = "record_clip")]
    [Description("""
        Records a stretch of a replay in one call: opens the replay when given, plays it to leadIn
        seconds before start and pauses there, sets the camera, records until tail seconds after
        end, and saves the file. Times are on the replay panel's clock, as find_moments
        reports them. Plays at the replay's current speed; at most 10 minutes.
        """)]
    public async Task<object> RecordClip(
        [Description("Start of the moment, m:ss or seconds")] string start,
        [Description("End of the moment, m:ss or seconds")] string end,
        [Description("Camera: follow a player by index 0-9, player name, hero or 'me'; 'all' for the free camera; omit to keep it as it is. The observer sees both teams' vision either way.")]
        string? player = null,
        [Description("How to follow player: 'hero' (with the hero's panel) or 'everyone' (the everyone view). In 'everyone', on calibrated maps (Dragon Shire, Tomb of the Spider Queen), the camera holds on Deathwing's landing spot while he flies, and holds where the hero falls from before his death so it doesn't snap to his core; this holds the left mouse button on the minimap, so don't use the mouse meanwhile.")]
        string view = "hero",
        [Description("Camera zoom: 'default', 'out' (Z) or 'far' (Shift+Z, farther out), e.g. for a long-range hero. Omit to leave it as it is.")]
        string? zoom = null,
        [Description("Replay file path, 'latest', or N for the N-th newest; omit for the one on screen")]
        string? replay = null,
        [Description("Seconds to start before start")] double leadIn = 5,
        [Description("Seconds to keep recording after end")] double tail = 3,
        [Description("File name, without extension")] string? name = null,
        CancellationToken cancellationToken = default) {
        var clip = await recorder.RecordClipAsync(
            Time(start), Time(end), player, ControlTools.Everyone(view), zoom is null ? null : ControlTools.ParseZoom(zoom), replay, leadIn, tail, name, cancellationToken);
        return new {
            file = clip.Path,
            from = ReplayController.ReplayTime(clip.From),
            to = ReplayController.ReplayTime(clip.To),
            camera = clip.Camera,
        };
    }

    private static double Time(string text) {
        try {
            return ResultFormatter.ParseTime(text);
        }
        catch (FormatException) {
            throw new McpException($"'{text}' is not a time; use m:ss or seconds");
        }
    }
}
