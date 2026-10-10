using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using MyReplayLibrary.Obs;
using OpenCvSharp;

namespace MyHotsObserver;

/// <summary>
/// Sees the game through OBS: a window-mode Game Capture keeps getting frames while other windows
/// cover the game. OBS only renders the program scene, so taking a frame makes
/// <see cref="SceneName"/> the program scene and leaves it there.
/// </summary>
public sealed class ObserverCapture(GameWindow gameWindow, ILogger<ObserverCapture> logger) : IAsyncDisposable {
    public const string SceneName = "MyHotsInfo Observer";
    public const string InputName = "MyHotsInfo Observer Game";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>A freshly hooked capture shows nothing for a moment.</summary>
    private static readonly TimeSpan HookTimeout = TimeSpan.FromSeconds(8);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private ObsClient? _client;

    /// <summary>The game's current frame, at the size of its window, as a BGR image.</summary>
    public async Task<Mat> FrameAsync(CancellationToken cancellationToken) {
        var (_, width, height) = gameWindow.Find();
        await _lock.WaitAsync(cancellationToken);
        try {
            // A blank frame right after switching scenes means the capture hasn't hooked the game
            // yet; otherwise it's what the game shows, e.g. a black loading screen.
            var switched = await EnsureSceneAsync(cancellationToken);
            var deadline = DateTime.UtcNow + HookTimeout;
            while (true) {
                var frame = await ScreenshotAsync(width, height, cancellationToken);
                if (!switched || !IsBlank(frame)) {
                    return frame;
                }

                frame.Dispose();
                if (DateTime.UtcNow >= deadline) {
                    throw new McpException(
                        $"OBS's '{InputName}' source shows nothing. Check in OBS that it captures the Heroes of the Storm window.");
                }

                await Task.Delay(500, cancellationToken);
            }
        }
        finally {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync() {
        if (_client is not null) {
            await _client.DisposeAsync();
        }

        _lock.Dispose();
    }

    /// <summary>Creates the scene and its source when missing and makes it the program scene; true when it switched.</summary>
    private async Task<bool> EnsureSceneAsync(CancellationToken cancellationToken) {
        var scenes = await RequestAsync("GetSceneList", null, cancellationToken);
        if (!scenes.GetProperty("scenes").EnumerateArray().Any(s => s.GetProperty("sceneName").GetString() == SceneName)) {
            await RequestAsync("CreateScene", new JsonObject { ["sceneName"] = SceneName }, cancellationToken);
            await RequestAsync("CreateInput", new JsonObject {
                ["sceneName"] = SceneName,
                ["inputName"] = InputName,
                ["inputKind"] = "game_capture",
                ["inputSettings"] = new JsonObject {
                    ["capture_mode"] = "window",
                    ["window"] = $"Heroes of the Storm:Heroes of the Storm:{GameWindow.ProcessName}.exe",
                    ["priority"] = 2, // match the executable, whatever the window title
                    ["capture_cursor"] = false,
                    ["capture_overlays"] = false,
                },
            }, cancellationToken);
            logger.LogInformation("Created OBS scene {scene}", SceneName);
        }

        if (scenes.GetProperty("currentProgramSceneName").GetString() == SceneName) {
            return false;
        }

        // Switching scenes mid-recording would cut a recorded game short.
        var record = await RequestAsync("GetRecordStatus", null, cancellationToken);
        if (record.GetProperty("outputActive").GetBoolean()) {
            throw new McpException(
                $"OBS is recording another scene. Stop that recording or switch OBS to '{SceneName}' yourself.");
        }

        await RequestAsync("SetCurrentProgramScene", new JsonObject { ["sceneName"] = SceneName }, cancellationToken);
        logger.LogInformation("Switched OBS to scene {scene}", SceneName);
        return true;
    }

    private async Task<Mat> ScreenshotAsync(int width, int height, CancellationToken cancellationToken) {
        var shot = await RequestAsync("GetSourceScreenshot", new JsonObject {
            ["sourceName"] = InputName,
            ["imageFormat"] = "png",
            ["imageWidth"] = width,
            ["imageHeight"] = height,
        }, cancellationToken);
        var dataUri = shot.GetProperty("imageData").GetString()!;
        return Cv2.ImDecode(Convert.FromBase64String(dataUri[(dataUri.IndexOf(',') + 1)..]), ImreadModes.Color);
    }

    /// <summary>A capture that hasn't hooked the game yet is one flat color.</summary>
    private static bool IsBlank(Mat frame) {
        Cv2.MeanStdDev(frame, out _, out var stddev);
        return stddev.Val0 + stddev.Val1 + stddev.Val2 < 3;
    }

    /// <summary>Sends a request, reconnecting once when the connection to OBS was lost.</summary>
    private async Task<System.Text.Json.JsonElement> RequestAsync(string requestType, JsonObject? data,
        CancellationToken cancellationToken) {
        for (var attempt = 0;; attempt++) {
            var client = await ClientAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            try {
                // JsonObjects can have only one parent, so a retry needs its own copy.
                return await client.RequestAsync(requestType, (JsonObject?)data?.DeepClone(), timeout.Token);
            }
            catch (ObsRequestException e) {
                throw new McpException(e.Message);
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested && attempt == 0) {
                logger.LogInformation(e, "OBS request {request} failed; reconnecting", requestType);
                await DropClientAsync();
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested) {
                await DropClientAsync();
                throw new McpException($"Lost the connection to OBS: {e.Message}");
            }
        }
    }

    private async Task<ObsClient> ClientAsync(CancellationToken cancellationToken) {
        if (_client is not null) {
            return _client;
        }

        var connection = ObsConnection.FromObsConfig()
                         ?? throw new McpException("OBS's WebSocket server isn't enabled (OBS: Tools → WebSocket Server Settings).");
        try {
            _client = await ObsClient.ConnectAsync(connection, 0, cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested) {
            throw new McpException($"Can't connect to OBS at {connection.Uri}; is it running? ({e.Message})");
        }

        return _client;
    }

    private async Task DropClientAsync() {
        if (_client is not null) {
            await _client.DisposeAsync();
            _client = null;
        }
    }
}
