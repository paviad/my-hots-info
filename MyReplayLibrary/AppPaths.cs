namespace MyReplayLibrary;

/// <summary>
/// Single place that resolves machine-specific locations. Every value can be overridden
/// with an environment variable so nothing has to be hard-coded in source.
/// </summary>
public static class AppPaths {
    /// <summary>Full path of the SQLite database file.</summary>
    public const string DbEnvVar = "MYHOTSINFO_DB";

    /// <summary>Directory holding the Tesseract <c>*.traineddata</c> files.</summary>
    public const string TessDataEnvVar = "MYHOTSINFO_TESSDATA";

    public static string HotsDocumentsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Heroes of the Storm");

    public static string ScreenshotsPath => Path.Combine(HotsDocumentsPath, "Screenshots");

    /// <summary>
    /// <see cref="DbEnvVar"/> if set, otherwise <c>%LocalAppData%\MyHotsInfo\my.db</c>.
    /// </summary>
    public static string DefaultDbPath {
        get {
            var fromEnv = Environment.GetEnvironmentVariable(DbEnvVar);
            if (!string.IsNullOrWhiteSpace(fromEnv)) {
                return Path.GetFullPath(fromEnv);
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "MyHotsInfo", "my.db");
        }
    }

    /// <summary>
    /// Connection string for <see cref="DefaultDbPath"/>. Creates the containing directory so
    /// SQLite can create the file on first use.
    /// </summary>
    public static string GetDefaultConnectionString() {
        var dbPath = DefaultDbPath;
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) {
            Directory.CreateDirectory(dir);
        }

        return $"Data Source={dbPath};foreign keys=true;";
    }

    /// <summary>
    /// Resolution order: <see cref="TessDataEnvVar"/>, a <c>tessdata</c> folder next to the
    /// executable, then an <c>Ocr\tessdata</c> folder in any ancestor directory (the repo layout
    /// when running from <c>bin\Debug</c> or <c>bin\pub</c>).
    /// </summary>
    public static string TessDataPath {
        get {
            var fromEnv = Environment.GetEnvironmentVariable(TessDataEnvVar);
            if (!string.IsNullOrWhiteSpace(fromEnv)) {
                return Path.GetFullPath(fromEnv);
            }

            var baseDir = AppContext.BaseDirectory;
            var local = Path.Combine(baseDir, "tessdata");
            if (Directory.Exists(local)) {
                return local;
            }

            for (var dir = new DirectoryInfo(baseDir); dir is not null; dir = dir.Parent) {
                var candidate = Path.Combine(dir.FullName, "Ocr", "tessdata");
                if (Directory.Exists(candidate)) {
                    return candidate;
                }
            }

            return local;
        }
    }
}
