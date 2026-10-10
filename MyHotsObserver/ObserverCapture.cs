using System.Text.Json;
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

    /// <summary>How long OBS may take to finish writing a recording after StopRecord.</summary>
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private ObsClient? _client;

    /// <summary>Completes with the file's path when OBS reports a recording stopped.</summary>
    private TaskCompletionSource<string?>? _recordStopped;

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

    /// <summary>
    /// Starts recording <see cref="SceneName"/>. Refuses while OBS records anything else, such as a
    /// game the app is recording.
    /// </summary>
    public async Task StartRecordAsync(CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            // Right after a recording stops, OBS reports it active for a moment longer.
            for (var attempt = 0;; attempt++) {
                var status = await RequestAsync("GetRecordStatus", null, cancellationToken);
                if (!status.GetProperty("outputActive").GetBoolean()) {
                    break;
                }

                if (attempt == 10) {
                    throw new McpException("OBS is already recording; stop that recording first.");
                }

                await Task.Delay(300, cancellationToken);
            }

            await EnsureSceneAsync(cancellationToken);
            await RequestAsync("StartRecord", null, cancellationToken);
            logger.LogInformation("Recording started");
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>Stops recording and returns the finished file's path.</summary>
    public async Task<string> StopRecordAsync(CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            var stopped = _recordStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var response = await RequestAsync("StopRecord", null, cancellationToken);
            var responsePath = response.ValueKind == JsonValueKind.Object && response.TryGetProperty("outputPath", out var p)
                ? p.GetString()
                : null;

            // The file is only complete once OBS reports the output stopped.
            string? path;
            try {
                path = await stopped.Task.WaitAsync(StopTimeout, cancellationToken) ?? responsePath;
            }
            catch (TimeoutException) {
                path = responsePath;
            }

            logger.LogInformation("Recording stopped: {path}", path);
            return path is not null && File.Exists(path)
                ? path
                : throw new McpException($"OBS stopped recording but didn't say where the file is ({path}).");
        }
        finally {
            _recordStopped = null;
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
    private async Task<JsonElement> RequestAsync(string requestType, JsonObject? data,
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
            _client = await ObsClient.ConnectAsync(connection, ObsClient.OutputEvents, cancellationToken);
            _client.EventReceived += OnEvent;
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested) {
            throw new McpException($"Can't connect to OBS at {connection.Uri}; is it running? ({e.Message})");
        }

        return _client;
    }

    private void OnEvent(string eventType, JsonElement data) {
        if (eventType == "RecordStateChanged"
            && data.GetProperty("outputState").GetString() == "OBS_WEBSOCKET_OUTPUT_STOPPED") {
            _recordStopped?.TrySetResult(data.TryGetProperty("outputPath", out var p) ? p.GetString() : null);
        }
    }

    private async Task DropClientAsync() {
        if (_client is not null) {
            await _client.DisposeAsync();
            _client = null;
        }
    }
}
