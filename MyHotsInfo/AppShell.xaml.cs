using Microsoft.EntityFrameworkCore;
using MyHotsInfo.Pages;
using MyHotsInfo.Utils;
using MyReplayLibrary;
using MyReplayLibrary.Data;
using MyReplayLibrary.Obs;

namespace MyHotsInfo;

public partial class AppShell : Shell, IDisposable {
    private readonly GameRecorder _gameRecorder;
    private readonly MyNavigator _myNavigator;
    private readonly IServiceProvider _svcp;
    private readonly CancellationTokenSource _tks = new();
    private Scanner? _scanner;
    private IServiceScope? _watchScope;

    public AppShell(IServiceProvider svcp, MyNavigator myNavigator) {
        _svcp = svcp;
        _myNavigator = myNavigator;
        _gameRecorder = svcp.GetRequiredService<GameRecorder>();
        InitializeComponent();

        Routing.RegisterRoute("Replay", typeof(ReplayPage));
        Routing.RegisterRoute("Prematch", typeof(Prematch));
        Routing.RegisterRoute("Player", typeof(PlayerStats));
#if WINDOWS
        HandlerChanged += (_, _) => EnableScreenshotDrop();
#endif

        _ = InternalInit();

        return;

        async Task InternalInit() {
            try {
                await InitAsync();
            }
            catch (OperationCanceledException) {
                /* ignored */
            }
            catch (Exception x) {
                await DisplayAlertAsync("Error", $"Failed to init app {x}", "Dismiss");
            }
        }
    }

    public void Dispose() {
        _tks.Cancel();
        _watchScope?.Dispose();
    }

    private static async Task<FileResult?> PickAndShow(PickOptions options) {
        try {
            var result = await FilePicker.Default.PickAsync(options);

            return result;
        }
        catch {
            /* ignored */
        }

        return null;
    }

    private async Task InitAsync() {
        await InitDbPath();

        _watchScope = _svcp.CreateScope();
        _scanner = _watchScope.ServiceProvider.GetRequiredService<Scanner>();
        var acct = _scanner.GetAllFolders().MaxBy(r => r.NumReplays);
        await _scanner.Scan(acct.Account, acct.Region, true, ReplayCallback, GameScreenshotCallback,
            _gameRecorder.OnReplayFileAsync, _tks.Token);
    }

    private async Task InitDbPath() {
        var prefs = Preferences.Default;
        if (prefs.ContainsKey("ConnectionStringSet")) {
            return;
        }

        var result = await PickAndShow(new() { PickerTitle = "Pick db file" });
        if (result is null) {
            return;
        }

        Preferences.Default.Set("DefaultConnection", $"Data Source={result.FullPath};foreign keys=true;");
        try {
            await using var dc = _svcp.GetRequiredService<ReplayDbContext>();
            await dc.Database.MigrateAsync();
            Preferences.Default.Set("ConnectionStringSet", true);
            await DisplayAlertAsync("Database Set", "Database Set Successfully", "Dismiss");
        }
        catch {
            await DisplayAlertAsync("Database Error", "Selected database doesn't belong to this application", "Dismiss");
        }
    }

    private Task ReplayCallback(int replayId) {
        _myNavigator.GoToReplay(replayId);
        return Task.CompletedTask;
    }

#if WINDOWS
    /// <summary>
    /// Lets a screenshot file be dropped on the window to be handled as if the game had just
    /// saved it, for trying out the prematch page without being in a game.
    /// </summary>
    private void EnableScreenshotDrop() {
        if (Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement view) {
            return;
        }

        view.AllowDrop = true;
        view.DragOver += (_, e) => {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)) {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Read screenshot";
            }
        };
        view.Drop += async (_, e) => {
            try {
                if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)) {
                    return;
                }

                var items = await e.DataView.GetStorageItemsAsync();
                if (items.OfType<Windows.Storage.StorageFile>().FirstOrDefault() is { } file) {
                    await HandleDroppedScreenshot(file.Path);
                }
            }
            catch (Exception x) {
                await DisplayAlertAsync("Screenshot", $"Couldn't read the screenshot: {x.Message}", "Dismiss");
            }
        };
    }
#endif

    private async Task HandleDroppedScreenshot(string path) {
        if (_scanner is null) {
            await DisplayAlertAsync("Screenshot", "Still starting up, try again in a moment", "Dismiss");
            return;
        }

        var slots = await _scanner.ReadScreenshot(path);
        if (slots.Count == 0) {
            await DisplayAlertAsync("Screenshot", "That isn't a draft or loading screen screenshot", "Dismiss");
            return;
        }

        _myNavigator.GoToPrematch(slots);
    }

    /// <summary>
    /// A screenshot the game saved. Unlike a dropped one, it means a game is starting, so it also
    /// starts recording.
    /// </summary>
    private async Task GameScreenshotCallback(List<string> slots) {
        // Empty when the screenshot wasn't of a draft or loading screen.
        if (slots.Count > 0) {
            _myNavigator.GoToPrematch(slots);
            await _gameRecorder.OnGameScreenshotAsync();
        }
    }
}
