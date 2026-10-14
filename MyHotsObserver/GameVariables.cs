namespace MyHotsObserver;

/// <summary>
/// The client's options, which it saves to Variables.txt (key=value lines) the moment one changes:
/// a global file, and one per account whose values a signed-in client uses over the global ones.
/// </summary>
public static class GameVariables {
    public static readonly string Root =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Heroes of the Storm");

    /// <summary>
    /// A setting as the running client has it; null when no file sets it. A client on an older
    /// build, started by the Switcher, isn't signed in and goes by the global file alone.
    /// </summary>
    public static string? Get(string name, bool signedIn) {
        var global = Path.Combine(Root, "Variables.txt");
        string[] files = signedIn && Account() is { } account ? [account, global] : [global];
        return files.Select(f => Read(f, name)).FirstOrDefault(v => v is not null);
    }

    /// <summary>The signed-in account's file: the most recently written one (Accounts\1831861\Variables.txt).</summary>
    public static string? Account() {
        var accounts = Path.Combine(Root, "Accounts");
        return Directory.Exists(accounts)
            ? Directory.EnumerateDirectories(accounts)
                .Select(d => Path.Combine(d, "Variables.txt"))
                .Where(File.Exists)
                .MaxBy(File.GetLastWriteTimeUtc)
            : null;
    }

    private static string? Read(string file, string name) {
        try {
            // The client may be writing it right now.
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line) {
                var eq = line.IndexOf('=');
                if (eq > 0 && line.AsSpan(0, eq).Equals(name, StringComparison.OrdinalIgnoreCase)) {
                    return line[(eq + 1)..].Trim();
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        }

        return null;
    }
}
