using System.Xml;

namespace CascScraperCore;

/// <summary>
/// Extracts CASC files to disk and cross-checks a hero mod the way <see cref="Scraper"/> reads it,
/// so scrape failures on new heroes can be diagnosed without stepping through the scraper.
/// </summary>
public class CascDiagnostics {
    private readonly CascFileSystem _fs;
    private readonly string _outDir;

    public CascDiagnostics(string outDir, string? gameInstallationPath = null) {
        _outDir = Path.GetFullPath(outDir);
        _fs = CascFileSystem.Open(gameInstallationPath ?? Scraper.GameInstallationPath);
    }

    /// <summary>Extracts every file whose CASC path matches <paramref name="glob"/> (<c>*</c>, <c>**</c>, <c>?</c>).</summary>
    public List<string> Extract(string glob) => _fs.EnumerateFiles(glob).Select(ExtractToDisk).ToList();

    /// <summary>Lists CASC paths matching <paramref name="glob"/> without extracting them.</summary>
    public IEnumerable<string> List(string glob) => _fs.EnumerateFiles(glob).Order(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Searches XML under <paramref name="glob"/> for elements with <c>id="<paramref name="id"/>"</c>.
    /// Returns (CASC path, element name) pairs.
    /// </summary>
    public List<(string Path, string Element)> FindId(string id, string glob) {
        var hits = new List<(string, string)>();
        foreach (var path in _fs.EnumerateFiles(glob)) {
            if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var doc = TryLoadXml(path);
            if (doc?.SelectNodes($"//*[@id='{id}']") is not { } nodes) {
                continue;
            }

            foreach (XmlNode node in nodes) {
                hits.Add((path, node.Name));
            }
        }

        return hits;
    }

    /// <summary>
    /// Extracts the hero mod matching <paramref name="hero"/> and reports every talent, button and string
    /// lookup that <see cref="Scraper"/>'s DoHero would make but that the first included catalog cannot satisfy.
    /// </summary>
    public void DiagnoseHero(string hero, TextWriter output) {
        var key = Squash(hero);
        var candidates = _fs.EnumerateDirectories(Mods.HeroModsDir)
            .Select(x => new StormMod(x))
            .Where(x => Squash(x.Name.Replace(".stormmod", "")).Contains(key))
            .ToList();
        if (candidates.Count == 0) {
            output.WriteLine($"No heromod matches '{hero}'.");
            return;
        }

        foreach (var mod in candidates) {
            DiagnoseHeroMod(mod, output);
        }
    }

    private void DiagnoseHeroMod(StormMod mod, TextWriter output) {
        output.WriteLine($"=== {mod.Root}");

        foreach (var path in _fs.EnumerateFiles($"{mod.BaseData}/**").Concat(_fs.EnumerateFiles(mod.GameStrings))) {
            ExtractToDisk(path);
        }

        output.WriteLine($"Extracted base.stormdata and enus GameStrings.txt to {LocalPath(mod.Root)}");

        if (!_fs.FileExists(mod.GameDataConfig)) {
            output.WriteLine("No base.stormdata/GameData.xml: the scraper skips this mod as not a hero.");
            return;
        }

        var config = _fs.LoadXml(mod.GameDataConfig);
        var catalogPaths = config.SelectNodes("//Catalog")!.Cast<XmlNode>()
            .Select(x => x.Attributes?["path"]?.Value)
            .OfType<string>()
            .ToList();
        output.WriteLine($"GameData.xml includes {catalogPaths.Count} catalog(s):");
        for (var i = 0; i < catalogPaths.Count; i++) {
            output.WriteLine($"  [{i}] {catalogPaths[i]}{(i == 0 ? "   <- the only one the scraper reads" : "")}");
        }

        var catalogs = new List<(string Name, XmlDocument Doc)>();
        foreach (var catalogPath in catalogPaths) {
            var path = catalogPath.StartsWith("GameData/", StringComparison.OrdinalIgnoreCase)
                ? CascFileSystem.Combine(mod.BaseData, catalogPath)
                : CascFileSystem.Combine(mod.GameData, catalogPath);
            if (!_fs.FileExists(path)) {
                output.WriteLine($"  MISSING in CASC: {catalogPath}");
                continue;
            }

            catalogs.Add((catalogPath, _fs.LoadXml(path)));
        }

        // Per-type files such as GameData/ButtonData.xml are loaded by convention, not via GameData.xml.
        var included = catalogs.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allDocs = new List<(string Name, XmlDocument Doc)>(catalogs);
        foreach (var path in _fs.EnumerateFiles($"{mod.GameData}/**/*.xml")) {
            var name = path[(mod.BaseData.Length + 1)..];
            if (!included.Contains(name) && TryLoadXml(path) is { } doc) {
                allDocs.Add(($"{name} (not in GameData.xml includes)", doc));
            }
        }

        var genericTalents = LoadGenericIds("TalentData.xml", "CTalent");
        var genericButtons = LoadGenericIds("ButtonData.xml", "CButton");
        var locStrings = LoadLocStrings(mod);

        string? Where(string element, string id) =>
            allDocs.FirstOrDefault(c => c.Doc.SelectSingleNode($"//{element}[@id='{id}']") != null).Name;

        output.WriteLine("Entry counts per file:");
        foreach (var (name, doc) in allDocs) {
            var counts = new[] { "CHero", "CTalent", "CButton" }
                .Select(e => (e, doc.SelectNodes($"//{e}[@id]")!.Count))
                .Where(x => x.Count > 0)
                .Select(x => $"{x.e}={x.Count}");
            var summary = string.Join(", ", counts);
            if (summary.Length > 0) {
                output.WriteLine($"  {name}: {summary}");
            }
        }

        if (catalogs.Count == 0) {
            return;
        }

        var primary = catalogs[0].Doc;
        var heroes = primary.SelectNodes("//CHero[TalentTreeArray]")!.Cast<XmlNode>().ToList();
        if (heroes.Count == 0) {
            output.WriteLine("No CHero with TalentTreeArray in the first catalog.");
            foreach (var (name, doc) in catalogs.Skip(1)) {
                if (doc.SelectSingleNode("//CHero[TalentTreeArray]") != null) {
                    output.WriteLine($"  ...but {name} has one.");
                }
            }
        }

        foreach (var heroNode in heroes) {
            var heroId = heroNode.Attributes!["id"]!.Value;
            var problems = 0;
            output.WriteLine($"--- CHero {heroId}");
            if (!locStrings.ContainsKey($"Hero/Name/{heroId}")) {
                output.WriteLine($"  string Hero/Name/{heroId} missing");
                problems++;
            }

            foreach (XmlNode tt in heroNode.SelectNodes("TalentTreeArray")!) {
                var talentId = tt.Attributes?["Talent"]?.Value;
                if (talentId == null) {
                    continue;
                }

                var talentNode = primary.SelectSingleNode($"//CTalent[@id='{talentId}']");
                string? face;
                if (talentNode != null) {
                    face = talentNode.SelectSingleNode("Face")?.Attributes?["value"]?.Value;
                    if (face == null && !genericTalents.Contains(talentId)) {
                        output.WriteLine($"  talent {talentId}: no Face and not in generic TalentData.xml");
                        problems++;
                        continue;
                    }
                }
                else {
                    var elsewhere = Where("CTalent", talentId);
                    var generic = genericTalents.Contains(talentId);
                    output.WriteLine(
                        $"  talent {talentId}: not in first catalog; " +
                        (elsewhere != null ? $"defined in {elsewhere}; " : "") +
                        (generic ? "found in generic TalentData.xml" : "NOT in generic TalentData.xml (scraper skips it silently)"));
                    problems++;
                    continue;
                }

                if (face == null) {
                    continue;
                }

                if (primary.SelectSingleNode($"//CButton[@id='{face}']") is not { } buttonNode) {
                    var elsewhere = Where("CButton", face);
                    output.WriteLine(
                        $"  talent {talentId}: button {face} not in first catalog -> KeyNotFoundException; " +
                        (elsewhere != null ? $"defined in {elsewhere}" : "not in any included catalog") +
                        (genericButtons.Contains(face) ? "; present in generic ButtonData.xml" : ""));
                    problems++;
                    continue;
                }

                var nameKey = buttonNode.SelectSingleNode("Name")?.Attributes?["value"]?.Value ?? $"Button/Name/{face}";
                var tooltipKey = buttonNode.SelectSingleNode("Tooltip")?.Attributes?["value"]?.Value ?? $"Button/Tooltip/{face}";
                foreach (var stringKey in new[] { nameKey, tooltipKey }) {
                    if (!locStrings.ContainsKey(stringKey)) {
                        output.WriteLine($"  talent {talentId}: string {stringKey} missing from hero GameStrings.txt");
                        problems++;
                    }
                }

                if (buttonNode.SelectSingleNode("Icon") == null) {
                    output.WriteLine($"  talent {talentId}: button {face} has no Icon");
                    problems++;
                }
            }

            output.WriteLine(problems == 0 ? "  no problems found" : $"  {problems} problem(s)");
        }
    }

    private HashSet<string> LoadGenericIds(string fileName, string element) {
        var ids = new HashSet<string>();
        foreach (var mod in new[] { Mods.HeroesData, Mods.Core }) {
            var path = $"{mod.GameData}/{fileName}";
            if (!_fs.FileExists(path)) {
                continue;
            }

            foreach (XmlNode node in _fs.LoadXml(path).SelectNodes($"//{element}[@id]")!) {
                ids.Add(node.Attributes!["id"]!.Value);
            }
        }

        return ids;
    }

    private Dictionary<string, string> LoadLocStrings(StormMod mod) {
        var result = new Dictionary<string, string>();
        if (!_fs.FileExists(mod.GameStrings)) {
            return result;
        }

        foreach (var line in _fs.ReadAllLines(mod.GameStrings)) {
            var idx = line.IndexOf('=');
            if (idx > 0) {
                result.TryAdd(line[..idx], line[(idx + 1)..]);
            }
        }

        return result;
    }

    private string LocalPath(string cascPath) =>
        Path.Combine(_outDir, cascPath.Replace('/', Path.DirectorySeparatorChar));

    private string ExtractToDisk(string cascPath) {
        var target = LocalPath(cascPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var src = _fs.OpenRead(cascPath);
        using var dst = File.Create(target);
        src.CopyTo(dst);
        return target;
    }

    private XmlDocument? TryLoadXml(string path) {
        try {
            return _fs.LoadXml(path);
        }
        catch (XmlException) {
            return null;
        }
    }

    private static string Squash(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
