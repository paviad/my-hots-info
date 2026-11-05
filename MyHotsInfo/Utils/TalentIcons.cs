using MyHotsInfo.Extensions;

namespace MyHotsInfo.Utils;

/// <summary>
/// The talent icons from MyHotsCli\Scripts\talent_icons.py, named like the scraper names them: talent_x.jpg, then
/// talent_x_{build}.jpg for each later build that redrew it.
/// </summary>
public static class TalentIcons {
    // By name, the versions oldest first.
    private static readonly Dictionary<string, List<(int Build, string Path)>> Icons = Load();

    /// <summary>The talent's icon as of the build; builds older than the first scrape get the oldest we have.</summary>
    public static string? Find(string characterId, string talentName, int build) {
        // TalentsLib suffixes Alarak's level 20 upgrades of his heroics; the icon is the heroic's.
        var name = talentName.Replace(" (Lvl 20)", "");
        return Icons.TryGetValue($"talent_{$"{characterId}{name}".Strip()}", out var versions)
            ? versions.LastOrDefault(v => v.Build <= build, versions[0]).Path
            : null;
    }

    private static Dictionary<string, List<(int Build, string Path)>> Load() {
        var dir = Path.Combine(AppContext.BaseDirectory, "Talents");
        if (!Directory.Exists(dir)) {
            return [];
        }

        return Directory.EnumerateFiles(dir, "talent_*.jpg")
            .Select(path => {
                // Names are [a-z0-9] after "talent_", so a later "_" can only start a build.
                var file = Path.GetFileNameWithoutExtension(path);
                var sep = file.LastIndexOf('_');
                return sep > "talent".Length && int.TryParse(file.AsSpan(sep + 1), out var build)
                    ? (Name: file[..sep], Build: build, Path: path)
                    : (Name: file, Build: 0, Path: path);
            })
            .GroupBy(v => v.Name)
            .ToDictionary(g => g.Key, g => g.OrderBy(v => v.Build).Select(v => (v.Build, v.Path)).ToList());
    }
}
