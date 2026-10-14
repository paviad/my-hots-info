using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace MyHotsObserver;

/// <summary>
/// Runs the client on the observer's own options (ObserverVariables.txt), so the observer knows its
/// state, e.g. camera follow mode (L), which only toggles. The client reads Variables.txt when it
/// starts and ignores edits while it runs, so the observer starts it itself: it backs up the user's
/// global and account Variables.txt, puts its own in both places, and starts the game through
/// Battle.net, signed in. Once the client has been closed for a while, the user's files go back.
/// </summary>
public sealed class ClientSession(ILogger<ClientSession> logger) : BackgroundService {
    private const string BattleNetEnvVar = "MYHOTSINFO_BATTLENET";
    private const string DefaultBattleNet = @"C:\Program Files (x86)\Battle.net\Battle.net.exe";
    private const string SwitcherProcessName = "HeroesSwitcher_x64";

    /// <summary>How long the client stays closed before the user's files go back; a build switch closes it for seconds.</summary>
    private static readonly TimeSpan RestoreAfter = TimeSpan.FromSeconds(30);

    private static readonly string Backups =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyHotsObserver", "Variables");

    /// <summary>Which files are swapped and where their originals are; there while the observer's options are in place.</summary>
    private static readonly string StateFile = Path.Combine(Backups, "swapped.json");

    private static string Template => Path.Combine(AppContext.BaseDirectory, "ObserverVariables.txt");

    public const string UsersClientMessage =
        "Heroes of the Storm is running with the user's own settings. Ask the user to close the game: the observer starts " +
        "it itself, on its own Variables.txt, so it knows the client's state (e.g. camera follow mode).";

    /// <summary>A file the swap replaced, and its backup; null when there was none, so restoring deletes it.</summary>
    private sealed record SwappedFile(string Live, string? Backup);

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Whether the observer's options are in place, which makes a running client the observer's.</summary>
    public static bool Swapped => File.Exists(StateFile);

    /// <summary>Whether a client is running or starting (the Switcher runs while it switches builds).</summary>
    public static bool ClientRunning() =>
        Process.GetProcessesByName(GameWindow.ProcessName).Length > 0 || Process.GetProcessesByName(SwitcherProcessName).Length > 0;

    /// <summary>
    /// Makes sure the observer's client runs: starts it when none does, and refuses the user's own.
    /// A client started here is signed in and on the current build, at its home screen.
    /// </summary>
    public async Task EnsureClientAsync(CancellationToken cancellationToken) {
        await _lock.WaitAsync(cancellationToken);
        try {
            if (ClientRunning()) {
                if (!Swapped) {
                    throw new McpException(UsersClientMessage);
                }

                return;
            }

            SwapIn();
            var battleNet = Environment.GetEnvironmentVariable(BattleNetEnvVar) is { Length: > 0 } b ? b : DefaultBattleNet;
            if (!File.Exists(battleNet)) {
                throw new McpException($"{battleNet} not found; set {BattleNetEnvVar} to Battle.net.exe.");
            }

            Process.Start(new ProcessStartInfo(battleNet) { ArgumentList = { "--exec=launch Hero" }, UseShellExecute = false })?.Dispose();
            logger.LogInformation("Started the game through Battle.net on the observer's Variables.txt");

            var watch = Stopwatch.StartNew();
            while (GameWindow.RunningBuild() is null) {
                if (watch.Elapsed > TimeSpan.FromMinutes(3)) {
                    throw new McpException("Started the game through Battle.net, but its window didn't appear within 3 minutes.");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }

            // The window shows before the client signs in and reaches its home screen, where it
            // takes a replay.
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            logger.LogInformation("The client is up after {seconds:0}s", watch.Elapsed.TotalSeconds);
        }
        finally {
            _lock.Release();
        }
    }

    /// <summary>Puts the observer's options in place, backing up the user's unless an earlier swap still holds them.</summary>
    private void SwapIn() {
        if (!File.Exists(Template)) {
            throw new McpException($"{Template} not found; rebuild the observer.");
        }

        if (!Swapped) {
            Directory.CreateDirectory(Backups);
            var live = new[] { Path.Combine(GameVariables.Root, "Variables.txt") }.Concat(GameVariables.Account() is { } a ? [a] : []);
            var files = live.Select((path, i) => {
                if (!File.Exists(path)) {
                    return new SwappedFile(path, null);
                }

                var backup = Path.Combine(Backups, $"{i}-{Path.GetFileName(Path.GetDirectoryName(path))}-Variables.txt");
                File.Copy(path, backup, overwrite: true);
                return new SwappedFile(path, backup);
            }).ToList();
            File.WriteAllText(StateFile, JsonSerializer.Serialize(files));
        }

        foreach (var file in State()) {
            File.Copy(Template, file.Live, overwrite: true);
        }
    }

    /// <summary>Puts the user's files back.</summary>
    private void Restore() {
        foreach (var file in State()) {
            if (file.Backup is null) {
                File.Delete(file.Live);
            }
            else {
                File.Copy(file.Backup, file.Live, overwrite: true);
            }
        }

        File.Delete(StateFile);
        logger.LogInformation("The client is closed; restored the user's Variables.txt");
    }

    private static List<SwappedFile> State() =>
        JsonSerializer.Deserialize<List<SwappedFile>>(File.ReadAllText(StateFile)) ?? [];

    /// <summary>Restores the user's files once the observer's client has been closed for <see cref="RestoreAfter"/>.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        DateTime? closedSince = null;
        while (!stoppingToken.IsCancellationRequested) {
            try {
                if (Swapped && !ClientRunning() && _lock.CurrentCount > 0) {
                    closedSince ??= DateTime.UtcNow;
                    if (DateTime.UtcNow - closedSince > RestoreAfter) {
                        await _lock.WaitAsync(stoppingToken);
                        try {
                            if (!ClientRunning()) {
                                Restore();
                            }
                        }
                        finally {
                            _lock.Release();
                        }

                        closedSince = null;
                    }
                }
                else {
                    closedSince = null;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) {
                logger.LogWarning(e, "Couldn't restore the user's Variables.txt");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    /// <summary>Restores the user's files when the server stops with the client closed; a running one keeps them until it closes.</summary>
    public override async Task StopAsync(CancellationToken cancellationToken) {
        await base.StopAsync(cancellationToken);
        try {
            if (Swapped && !ClientRunning()) {
                Restore();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) {
            logger.LogWarning(e, "Couldn't restore the user's Variables.txt");
        }
    }
}
