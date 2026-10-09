namespace CascScraperCore;

/// <summary>
/// Reads GameStrings.txt files: one <c>key=value</c> per line, where the value runs to the end of the line and
/// may itself contain <c>=</c>. When a key repeats, the first occurrence wins.
/// </summary>
public static class GameStrings {
    public static Dictionary<string, string> Parse(IEnumerable<string> lines) {
        var result = new Dictionary<string, string>();
        foreach (var line in lines) {
            var idx = line.IndexOf('=');
            if (idx > 0) {
                result.TryAdd(line[..idx], line[(idx + 1)..]);
            }
        }

        return result;
    }

    public static Dictionary<string, string> ReadGameStrings(this CascFileSystem fs, string path) =>
        Parse(fs.ReadAllLines(path));

    public static Dictionary<string, string> ReadGameStrings(this CascFileSystem fs, IEnumerable<string> paths) =>
        Parse(paths.SelectMany(fs.ReadAllLines));
}
