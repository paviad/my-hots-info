using System.Diagnostics;
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
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private Scanner? _scanner;
    private IServiceScope? _watchScope;
    private CancellationTokenSource? _scanCts;
    private Task _scanTask = Task.CompletedTask;

    public AppShell(IServiceProvider svcp, MyNavigator myNavigator) {
        _svcp = svcp;
        _myNavigator = myNavigator;
        _gameRecorder = svcp.GetRequiredService<GameRecorder>();
        InitializeComponent();

        // Which folders are the owner's may have changed: scan and watch the new set.
        svcp.GetRequiredService<SettingsStore>().Changed += () => MainThread.BeginInvokeOnMainThread(async () => {
            try {
                await RestartScanAsync();
            }
            catch (Exception x) {
                await DisplayAlertAsync("Error", $"Couldn't restart the replay scan: {x.Message}", "Dismiss");
            }
        });

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
        _scanCts?.Dispose();
    }

    /// <summary>The window's title bar, with buttons for the screenshots, the clips and the last game.</summary>
    public TitleBar CreateTitleBar() {
        var options = _svcp.GetRequiredService<GameRecorderOptions>();
        return new TitleBar {
            Title = "MyHotsInfo",
            TrailingContent = new HorizontalStackLayout {
                Spacing = 6,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalOptions = LayoutOptions.Center,
                Children = {
                    TitleBarButton("Screenshots", () => OpenFolder(AppPaths.ScreenshotsPath)),
                    TitleBarButton("Clips", () => OpenFolder(options.ClipsFolder)),
                    TitleBarButton("Last game", OpenLastGame),
                },
            },
        };

        Button TitleBarButton(string text, Func<Task> action) {
            var button = new Button {
                Text = text,
                FontSize = 12,
                Padding = new Thickness(10, 0),
                HeightRequest = 26,
                MinimumHeightRequest = 0,
            };
            button.Clicked += async (_, _) => {
                try {
                    await action();
                }
                catch (Exception x) {
                    await DisplayAlertAsync(text, x.Message, "Dismiss");
                }
            };
            return button;
        }
    }

    private static Task OpenFolder(string path) {
        // The clips folder is only made by the first clip.
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private async Task OpenLastGame() {
        using var scope = _svcp.CreateScope();
        var dc = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        var lastId = await dc.Replays
            .OrderByDescending(r => r.TimestampReplay)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync();
        if (lastId is null) {
            await DisplayAlertAsync("Last game", "There are no games yet", "Dismiss");
            return;
        }

        _myNavigator.GoToReplay(lastId.Value);
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
        await RestartScanAsync();
    }

    /// <summary>
    /// Stops the running scan, if any, and starts one on the owner's folders as the settings now
    /// say. Files already scanned are skipped, so only a newly added folder's replays get imported.
    /// </summary>
    private async Task RestartScanAsync() {
        await _scanLock.WaitAsync();
        try {
            if (_scanCts is { } old) {
                await old.CancelAsync();
                await _scanTask;
                old.Dispose();
            }

            // Disposing the scope disposes the old scanner's file system watchers.
            _watchScope?.Dispose();
            _scanCts = CancellationTokenSource.CreateLinkedTokenSource(_tks.Token);
            _watchScope = _svcp.CreateScope();
            _scanner = _watchScope.ServiceProvider.GetRequiredService<Scanner>();
            _scanTask = RunScanAsync(_scanner, _scanCts.Token);
        }
        finally {
            _scanLock.Release();
        }
    }

    /// <summary>Scans and then watches until cancelled; reports anything else that stops it.</summary>
    private async Task RunScanAsync(Scanner scanner, CancellationToken token) {
        try {
            await scanner.Scan(scanner.GetMyFolders(), true, ReplayCallback, GameScreenshotCallback,
                _gameRecorder.OnReplayFileAsync, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            /* restarted or closing */
        }
        catch (Exception x) {
            await DisplayAlertAsync("Error", $"Replay scanning stopped: {x}", "Dismiss");
        }
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
