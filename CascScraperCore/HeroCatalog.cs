using System.Xml;
using CascScraperCore.Schema;

namespace CascScraperCore;

/// <summary>
/// A hero mod's data as the scraper reads it: the main catalog (the first one listed in GameData.xml) with the
/// rest of the hero's data merged in. That is the other listed catalogs that sit directly in GameData/ (skin and
/// sound data in subfolders are skipped), then type-named files such as GameData/ButtonData.xml, which the engine
/// loads by convention without listing them (e.g. Xal'atath keeps all its buttons there). When an id exists in
/// more than one file, the first one wins.
/// </summary>
public sealed class HeroCatalog {
    private HeroCatalog(StormMod mod, List<string> includes, XmlDocument doc, List<string> sources) {
        Mod = mod;
        Includes = includes;
        Doc = doc;
        Sources = sources;
    }

    public StormMod Mod { get; }

    /// <summary>Full CASC paths of the catalogs listed in GameData.xml, in order; they may not all exist.</summary>
    public IReadOnlyList<string> Includes { get; }

    /// <summary>The main catalog with the merged entries appended.</summary>
    public XmlDocument Doc { get; }

    /// <summary>Full CASC paths of the files merged into <see cref="Doc"/>, the main catalog first.</summary>
    public IReadOnlyList<string> Sources { get; }

    /// <summary>Full CASC paths of the catalogs listed in the mod's GameData.xml, or null if it has none.</summary>
    public static List<string>? ReadIncludes(CascFileSystem fs, StormMod mod) {
        if (!fs.FileExists(mod.GameDataConfig)) {
            return null;
        }

        // Catalog paths in GameData.xml are relative to base.stormdata
        return fs.Deserialize<Includes>(mod.GameDataConfig).Catalog
            .Select(x => CascFileSystem.Combine(mod.BaseData, x.Path))
            .ToList();
    }

    /// <summary>Loads the hero catalog of <paramref name="mod"/>, or returns null if the mod isn't a hero.</summary>
    public static HeroCatalog? TryLoad(CascFileSystem fs, StormMod mod) {
        var includes = ReadIncludes(fs, mod);
        if (includes is not [var main, ..] || !fs.FileExists(main)) {
            return null;
        }

        var included = includes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var extras = includes.Skip(1)
            .Where(x => CascFileSystem.GetDirectoryName(x).Equals(mod.GameData, StringComparison.OrdinalIgnoreCase))
            .Where(fs.FileExists)
            .Concat(fs.EnumerateFiles($"{mod.GameData}/*Data.xml")
                .Where(x => !CascFileSystem.GetFileName(x).Equals("GameData.xml", StringComparison.OrdinalIgnoreCase) &&
                            !included.Contains(x)))
            .ToList();

        var doc = fs.LoadXml(main);
        foreach (var path in extras) {
            doc.AppendNewEntries(fs.LoadXml(path));
        }

        return new HeroCatalog(mod, includes, doc, [main, .. extras]);
    }
}
