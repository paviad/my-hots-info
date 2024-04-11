using Microsoft.EntityFrameworkCore;
using MyHotsInfo.Pages;
using MyHotsInfo.Utils;
using MyReplayLibrary;
using MyReplayLibrary.Data;

namespace MyHotsInfo;

public partial class AppShell : Shell, IDisposable {
    private readonly MyNavigator _myNavigator;
    private readonly IServiceProvider _svcp;
    private readonly CancellationTokenSource _tks = new();
    private Scanner? _scanner;
    private IServiceScope? _watchScope;

    public AppShell(IServiceProvider svcp, MyNavigator myNavigator) {
        _svcp = svcp;
        _myNavigator = myNavigator;
        InitializeComponent();

        Routing.RegisterRoute("Replay", typeof(ReplayPage));
        Routing.RegisterRoute("Prematch", typeof(Prematch));

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
                await DisplayAlert("Error", $"Failed to init app {x}", "Dismiss");
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
        await _scanner.Scan(acct.Account, acct.Region, true, ReplayCallback, ScreenshotCallback, _tks.Token);
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
            await DisplayAlert("Database Set", "Database Set Successfully", "Dismiss");
        }
        catch {
            await DisplayAlert("Database Error", "Selected database doesn't belong to this application", "Dismiss");
        }
    }

    private Task ReplayCallback(int replayId) {
        _myNavigator.GoToReplay(replayId);
        return Task.CompletedTask;
    }

    private Task ScreenshotCallback(List<string> names) {
        _myNavigator.GoToPrematch(names);
        return Task.CompletedTask;
    }
}
