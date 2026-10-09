using System.Text.Json;
using System.Text.Json.Nodes;
using Heroes.ReplayParser;
using Microsoft.Extensions.Logging;

namespace MyReplayLibrary.Obs;

public record GameRecorderOptions {
    /// <summary>The OBS scene switched to before recording starts.</summary>
    public string SceneName { get; init; } = "Record Hots";

    /// <summary>
    /// Holds only the recordings this class made, named after their replay. Anything else in it
    /// is left alone.
    /// </summary>
    public string RecordingsFolder { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "MyHotsInfo Recordings");

    /// <summary>Where <see cref="GameRecorder.KeepAsync"/> moves a recording so it's never pruned.</summary>
    public string KeptFolder { get; init; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

    public int RecordingsToKeep { get; init; } = 3;

    /// <summary>Stops a recording whose game never produced a replay.</summary>
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(90);
}

/// <summary>
/// Records games with OBS: starts when a draft or loading screen screenshot is taken and stops
/// when the game's replay is written. Every OBS failure is logged and otherwise ignored, so a
/// game is never interrupted because OBS isn't running.
/// </summary>
public sealed class GameRecorder(GameRecorderOptions options, TimeProvider timeProvider, ILogger<GameRecorder> logger)
    : IAsyncDisposable {
    private const string UnmatchedPrefix = "unmatched-";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private Session? _session;

    public async Task OnGameScreenshotAsync() {
        await _lock.WaitAsync();
        try {
            if (_session is not null) {
                return;
            }

            _session = await StartAsync();
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>Stops the recording if this replay belongs to the game being recorded.</summary>
    public async Task OnReplayFileAsync(string replayPath) {
        await _lock.WaitAsync();
        try {
            // A replay written before the recording started (or rewritten later) belongs to another game.
            if (_session is null || File.GetCreationTimeUtc(replayPath) < _session.StartedAt.UtcDateTime) {
                return;
            }

            await StopAsync(replayPath);
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>The recording of the game with this replay hash, or null when there is none (any more).</summary>
    public string? FindRecording(Guid replayHash) {
        if (!Directory.Exists(options.RecordingsFolder)) {
            return null;
        }

        return Directory.EnumerateFiles(options.RecordingsFolder, $"{replayHash}.*").FirstOrDefault();
    }

    /// <summary>
    /// Moves the game's recording to <see cref="GameRecorderOptions.KeptFolder"/> as
    /// <paramref name="name"/>, and returns its new path.
    /// </summary>
    public async Task<string> KeepAsync(Guid replayHash, string name) {
        await _lock.WaitAsync();
        try {
            var source = FindRecording(replayHash)
                         ?? throw new FileNotFoundException("The recording of this game is gone");
            Directory.CreateDirectory(options.KeptFolder);
            var target = UniquePath(options.KeptFolder, name, Path.GetExtension(source));

            // Off the UI thread: across drives this is a copy of several GB.
            await Task.Run(() => File.Move(source, target));
            logger.LogInformation("Kept recording {path}", target);
            return target;
        }
        finally {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync() {
        if (_session is not null) {
            await _session.DisposeAsync();
        }

        _lock.Dispose();
    }

    private async Task<Session?> StartAsync() {
        ObsClient? client = null;
        try {
            var connection = ObsConnection.FromObsConfig();
            if (connection is null) {
                logger.LogInformation("Not recording: OBS's WebSocket server isn't enabled");
                return null;
            }

            using var timeout = new CancellationTokenSource(RequestTimeout);
            client = await ObsClient.ConnectAsync(connection, ObsClient.OutputEvents, timeout.Token);

            var status = await client.RequestAsync("GetRecordStatus", null, timeout.Token);
            if (status.GetProperty("outputActive").GetBoolean()) {
                logger.LogInformation("Not recording: OBS is already recording");
                await client.DisposeAsync();
                return null;
            }

            var session = new Session(client, timeProvider.GetUtcNow());
            client.EventReceived += session.OnEvent;

            await client.RequestAsync("SetCurrentProgramScene",
                new JsonObject { ["sceneName"] = options.SceneName }, timeout.Token);
            await client.RequestAsync("StartRecord", null, timeout.Token);
            logger.LogInformation("Recording started");

            _ = StopAfterMaxDuration(session);
            return session;
        }
        catch (Exception x) {
            logger.LogInformation(x, "Not recording: couldn't start OBS recording");
            if (client is not null) {
                await client.DisposeAsync();
            }

            return null;
        }
    }

    private async Task StopAfterMaxDuration(Session session) {
        try {
            await Task.Delay(options.MaxDuration, timeProvider, session.Cancellation.Token);
        }
        catch (OperationCanceledException) {
            return;
        }

        await _lock.WaitAsync();
        try {
            if (_session == session) {
                logger.LogInformation("No replay after {duration}, stopping the recording", options.MaxDuration);
                await StopAsync(null);
            }
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>Must hold <see cref="_lock"/>; always ends the session.</summary>
    private async Task StopAsync(string? replayPath) {
        var session = _session!;
        _session = null;

        string? outputPath;
        try {
            await using (session) {
                // Stopped from OBS by hand: the file is the user's, leave it where it is.
                if (session.Stopped.Task.IsCompleted) {
                    logger.LogInformation("Recording was already stopped in OBS");
                    return;
                }

                using var timeout = new CancellationTokenSource(StopTimeout);
                var response = await session.Client.RequestAsync("StopRecord", null, timeout.Token);
                var responsePath = response.ValueKind == JsonValueKind.Object
                                   && response.TryGetProperty("outputPath", out var p)
                    ? p.GetString()
                    : null;

                // The file is only complete once OBS reports the output stopped.
                outputPath = await session.Stopped.Task.WaitAsync(timeout.Token) ?? responsePath;
            }
        }
        catch (Exception x) {
            logger.LogWarning(x, "Couldn't stop the OBS recording");
            return;
        }

        if (outputPath is null || !File.Exists(outputPath)) {
            logger.LogWarning("OBS didn't report where the recording is ({path})", outputPath);
            return;
        }

        try {
            var name = ReplayHash(replayPath)?.ToString()
                       ?? UnmatchedPrefix + session.StartedAt.ToLocalTime().ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(options.RecordingsFolder);
            var target = Path.Combine(options.RecordingsFolder, name + Path.GetExtension(outputPath));
            await Task.Run(() => File.Move(outputPath, target, true));
            logger.LogInformation("Recording saved as {path}", target);

            Prune();
        }
        catch (Exception x) {
            logger.LogWarning(x, "Couldn't move the recording {path}", outputPath);
        }
    }

    private Guid? ReplayHash(string? replayPath) {
        if (replayPath is null) {
            return null;
        }

        try {
            var (result, replay) = DataParser.ParseReplay(replayPath, false, ParseOptions.MinimalParsing);
            return result == DataParser.ReplayParseResult.Success ? replay.HashReplay() : null;
        }
        catch (Exception x) {
            logger.LogWarning(x, "Couldn't read replay {path} to name its recording", replayPath);
            return null;
        }
    }

    /// <summary>Deletes all but the newest recordings, touching only files this class named.</summary>
    private void Prune() {
        var old = new DirectoryInfo(options.RecordingsFolder).EnumerateFiles()
            .Where(f => {
                var name = Path.GetFileNameWithoutExtension(f.Name);
                return Guid.TryParse(name, out _) || name.StartsWith(UnmatchedPrefix);
            })
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Skip(options.RecordingsToKeep);

        foreach (var file in old) {
            try {
                file.Delete();
                logger.LogInformation("Deleted old recording {path}", file.FullName);
            }
            catch (Exception x) {
                logger.LogWarning(x, "Couldn't delete old recording {path}", file.FullName);
            }
        }
    }

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

    private sealed class Session(ObsClient client, DateTimeOffset startedAt) : IAsyncDisposable {
        public ObsClient Client { get; } = client;
        public DateTimeOffset StartedAt { get; } = startedAt;
        public CancellationTokenSource Cancellation { get; } = new();

        /// <summary>Completes with the output path once OBS reports the recording stopped.</summary>
        public TaskCompletionSource<string?> Stopped { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void OnEvent(string eventType, JsonElement data) {
            if (eventType == "RecordStateChanged"
                && data.GetProperty("outputState").GetString() == "OBS_WEBSOCKET_OUTPUT_STOPPED") {
                Stopped.TrySetResult(data.TryGetProperty("outputPath", out var p) ? p.GetString() : null);
            }
        }

        public async ValueTask DisposeAsync() {
            await Cancellation.CancelAsync();
            Cancellation.Dispose();
            await Client.DisposeAsync();
        }
    }
}
