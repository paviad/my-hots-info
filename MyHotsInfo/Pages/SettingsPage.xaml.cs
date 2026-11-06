using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyReplayLibrary;
using MyReplayLibrary.Data;

namespace MyHotsInfo.Pages;

public partial class SettingsPage : ContentPage {
    private readonly IServiceProvider _svcp;
    private readonly SettingsStore _store;
    private readonly SettingsViewModel _vm = new();

    // Reading a name parses a replay, so each folder's is read once per run.
    private readonly Dictionary<string, Task<string?>> _names = [];

    public SettingsPage(IServiceProvider svcp, SettingsStore store) {
        _svcp = svcp;
        _store = store;
        InitializeComponent();
        BindingContext = _vm;
        _vm.SettingsPath = store.FilePath;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args) {
        base.OnNavigatedTo(args);
        _vm.DatabasePath = CurrentDatabasePath();
        try {
            await LoadFolders();
        }
        catch (Exception x) {
            await DisplayAlertAsync("Settings", $"Couldn't list the replay folders: {x.Message}", "Dismiss");
        }
    }

    private async Task LoadFolders() {
        var folders = await Task.Run(ReadFolders);
        var items = folders.Select(f => new SettingsFolder(f, OnFolderChanged)).ToList();
        _vm.SetFolders(items);

        foreach (var item in items) {
            if (!_names.TryGetValue(item.Folder.Key, out var name)) {
                var folder = item.Folder;
                _names[folder.Key] = name = Task.Run(folder.ReadPlayerName);
            }

            item.PlayerName = await name;
        }
    }

    private List<ReplayFolder> ReadFolders() {
        using var scope = _svcp.CreateScope();
        return scope.ServiceProvider.GetRequiredService<Scanner>().GetFolders();
    }

    /// <summary>A checkbox changed: save the choice, which also restarts the scan on the new set.</summary>
    private void OnFolderChanged(SettingsFolder item, bool isMine) {
        var choices = new Dictionary<string, bool>(_store.Current.ReplayFolders) { [item.Folder.Key] = isMine };
        Save(_store.Current with { ReplayFolders = choices });
    }

    private async void OnResetFoldersClicked(object? sender, EventArgs e) {
        var reset = await DisplayAlertAsync("Replay folders",
            "Go back to the default for every folder: every account in the region with the most replays?", "Reset",
            "Cancel");
        if (reset) {
            Save(_store.Current with { ReplayFolders = [] });
        }
    }

    private void Save(AppSettings settings) {
        try {
            _store.Save(settings);
        }
        catch (Exception x) when (x is IOException or UnauthorizedAccessException) {
            Dispatcher.Dispatch(async () =>
                await DisplayAlertAsync("Settings", $"Couldn't save {_store.FilePath}: {x.Message}", "Dismiss"));
        }

        // Not from inside the checkbox's binding, which is what called this.
        Dispatcher.Dispatch(RefreshFolders);
    }

    /// <summary>Updates the rows in place, so the names already read stay.</summary>
    private void RefreshFolders() {
        var byKey = ReadFolders().ToDictionary(f => f.Key);
        foreach (var item in _vm.Folders) {
            if (byKey.TryGetValue(item.Folder.Key, out var folder)) {
                item.Update(folder);
            }
        }

        _vm.SetFolders(_vm.Folders);
    }

    private static string CurrentDatabasePath() {
        var connectionString = Preferences.Default.Get("DefaultConnection", AppPaths.GetDefaultConnectionString());
        return new SqliteConnectionStringBuilder(connectionString).DataSource;
    }

    private async void OnOpenDatabaseFolderClicked(object? sender, EventArgs e) =>
        await OpenFolder(Path.GetDirectoryName(CurrentDatabasePath()));

    private async void OnOpenSettingsFolderClicked(object? sender, EventArgs e) =>
        await OpenFolder(Path.GetDirectoryName(_store.FilePath));

    private async Task OpenFolder(string? path) {
        try {
            if (path is null || !Directory.Exists(path)) {
                throw new DirectoryNotFoundException($"{path} doesn't exist");
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception x) {
            await DisplayAlertAsync("Settings", x.Message, "Dismiss");
        }
    }

    /// <summary>Switches to another database file, which takes effect when the app next starts.</summary>
    private async void OnChangeDatabaseClicked(object? sender, EventArgs e) {
        FileResult? picked;
        try {
            picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Pick the database file" });
        }
        catch (Exception x) {
            await DisplayAlertAsync("Database", x.Message, "Dismiss");
            return;
        }

        if (picked is null) {
            return;
        }

        var connectionString = $"Data Source={picked.FullPath};foreign keys=true;";
        try {
            var options = new DbContextOptionsBuilder<ReplayDbContext>().UseSqlite(connectionString).Options;
            await using var dc = new ReplayDbContext(options);
            await dc.Database.MigrateAsync();
        }
        catch (Exception x) {
            await DisplayAlertAsync("Database", $"That file isn't a database this app can use: {x.Message}", "Dismiss");
            return;
        }

        Preferences.Default.Set("DefaultConnection", connectionString);
        Preferences.Default.Set("ConnectionStringSet", true);
        _vm.DatabasePath = picked.FullPath;
        await DisplayAlertAsync("Database", "Saved. Restart the app to use the new database.", "OK");
    }
}
