using System.Text.Json;

namespace MyReplayLibrary;

/// <summary>The settings the app and the CLI share.</summary>
public sealed record AppSettings {
    /// <summary>
    /// The replay folders the owner said are theirs (true) or not (false), by
    /// <see cref="ReplayFolder.Key"/>. A folder not in here follows <see cref="ReplayFolder.MineByDefault"/>.
    /// </summary>
    public Dictionary<string, bool> ReplayFolders { get; init; } = [];
}

/// <summary>
/// Keeps <see cref="AppSettings"/> in <c>settings.json</c> in <see cref="AppPaths.DataDir"/>, and
/// tells whoever listens when they change.
/// </summary>
public sealed class SettingsStore {
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SettingsStore() : this(Path.Combine(AppPaths.DataDir, "settings.json")) { }

    public SettingsStore(string filePath) {
        FilePath = filePath;
        Current = Load(filePath);
    }

    public string FilePath { get; }

    public AppSettings Current { get; private set; }

    /// <summary>Raised after <see cref="Save"/>, on the thread that saved.</summary>
    public event Action? Changed;

    public void Save(AppSettings settings) {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        Current = settings;
        Changed?.Invoke();
    }

    private static AppSettings Load(string filePath) {
        if (!File.Exists(filePath)) {
            return new AppSettings();
        }

        try {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), JsonOptions) ?? new AppSettings();
        }
        catch (JsonException) {
            // A hand-edit gone wrong shouldn't stop the app; saving from the settings page rewrites it.
            return new AppSettings();
        }
    }
}
