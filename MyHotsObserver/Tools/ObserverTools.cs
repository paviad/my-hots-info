using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenCvSharp;

namespace MyHotsObserver.Tools;

[McpServerToolType]
public sealed class ObserverTools(GameWindow gameWindow, ObserverCapture capture, ReplayClock clock) {
    [McpServerTool(Name = "get_state", ReadOnly = true)]
    [Description("""
        Reads the replay's clocks off the screen. replayTime is the replay panel's clock (null while
        the panel is hidden or a seek is running); gameTime is the in-game timer. Makes the OBS scene
        'MyHotsInfo Observer' the program scene.
        """)]
    public async Task<object> GetState(CancellationToken cancellationToken) {
        var layout = gameWindow.Layout();
        using var frame = await capture.FrameAsync(cancellationToken);
        return State(clock.Read(frame, layout), layout);
    }

    [McpServerTool(Name = "screenshot", ReadOnly = true)]
    [Description("The game's current frame as a JPEG, plus the clocks. Works while other windows cover the game.")]
    public async Task<IEnumerable<ContentBlock>> Screenshot(
        [Description("Width to scale the image down to (the game window's width at most)")] int maxWidth = 1280,
        CancellationToken cancellationToken = default) {
        var layout = gameWindow.Layout();
        using var frame = await capture.FrameAsync(cancellationToken);
        var state = State(clock.Read(frame, layout), layout);

        using var scaled = new Mat();
        var scale = Math.Min(1.0, Math.Max(160, maxWidth) / (double)frame.Width);
        Cv2.Resize(frame, scaled, new Size(), scale, scale, InterpolationFlags.Area);
        var jpeg = scaled.ImEncode(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 85));

        return [
            ImageContentBlock.FromBytes(jpeg, "image/jpeg"),
            new TextContentBlock { Text = System.Text.Json.JsonSerializer.Serialize(state) },
        ];
    }

    private static object State(ClockReading reading, ObserverLayout layout) => new {
        replayTime = Format(reading.Replay),
        replayLength = Format(reading.Length),
        gameTime = Format(reading.Game),
        replaySeconds = reading.Replay?.TotalSeconds,
        gameSeconds = reading.Game?.TotalSeconds,
        window = $"{layout.Width}x{layout.Height}",
    };

    private static string? Format(TimeSpan? t) => t is { } v ? $"{(int)v.TotalMinutes}:{v.Seconds:00}" : null;
}
