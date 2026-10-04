using System.Text.RegularExpressions;
using System.Xml;
using CascLibCore;

namespace CascScraperCore;

/// <summary>
/// Extracts CASC files to disk and cross-checks a hero mod the way <see cref="Scraper"/> reads it,
/// so scrape failures on new heroes can be diagnosed without stepping through the scraper.
/// </summary>
public class CascDiagnostics {
    private const string HeromodsPath = @"mods\heromods";
    private const string GenericGameDataPath = @"mods\heroesdata.stormmod\base.stormdata\GameData";
    private const string CoreGameDataPath = @"mods\core.stormmod\base.stormdata\GameData";

    private readonly CASCHandler _casc;
    private readonly CASCFolder _root;
    private readonly string _outDir;

    public CascDiagnostics(string outDir, string? gameInstallationPath = null) {
        _outDir = Path.GetFullPath(outDir);
        (_casc, _root) = Scraper.OpenStorage(gameInstallationPath ?? Scraper.GameInstallationPath);
    }

    /// <summary>Extracts every file whose CASC path matches <paramref name="glob"/> (<c>*</c>, <c>**</c>, <c>?</c>).</summary>
    public List<string> Extract(string glob) {
        var regex = GlobToRegex(glob);
        var written = new List<string>();
        foreach (var file in CASCFolder.GetFiles(_root.Entries.Values)) {
            if (regex.IsMatch(Normalize(file.FullName))) {
                written.Add(ExtractToDisk(file));
            }
        }

        return written;
    }

    /// <summary>Lists CASC paths matching <paramref name="glob"/> without extracting them.</summary>
    public IEnumerable<string> List(string glob) {
        var regex = GlobToRegex(glob);
        return CASCFolder.GetFiles(_root.Entries.Values)
            .Select(x => Normalize(x.FullName))
            .Where(x => regex.IsMatch(x))
            .Order(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Searches XML under <paramref name="glob"/> for elements with <c>id="<paramref name="id"/>"</c>.
    /// Returns (CASC path, element name) pairs.
    /// </summary>
    public List<(string Path, string Element)> FindId(string id, string glob) {
        var regex = GlobToRegex(glob);
        var hits = new List<(string, string)>();
        foreach (var file in CASCFolder.GetFiles(_root.Entries.Values)) {
            var path = Normalize(file.FullName);
            if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || !regex.IsMatch(path)) {
                continue;
            }

            var doc = TryLoadXml(file);
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
        var heromods = (CASCFolder)_root.GetEntry("mods")!;
        heromods = (CASCFolder)heromods.GetEntry("heromods")!;
        var key = Squash(hero);
        var candidates = heromods.Entries
            .Where(x => Squash(x.Key.Replace(".stormmod", "")).Contains(key))
            .ToList();
        if (candidates.Count == 0) {
            output.WriteLine($"No heromod matches '{hero}'.");
            return;
        }

        foreach (var (modName, entry) in candidates) {
            DiagnoseHeroMod(modName, (CASCFolder)entry, output);
        }
    }

    private void DiagnoseHeroMod(string modName, CASCFolder modDir, TextWriter output) {
        output.WriteLine($"=== {HeromodsPath}\\{modName}");

        foreach (var file in CASCFolder.GetFiles(modDir.Entries.Values)) {
            var path = Normalize(file.FullName);
            if (path.Contains(@"\base.stormdata\", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(@"\enus.stormdata\LocalizedData\GameStrings.txt", StringComparison.OrdinalIgnoreCase)) {
                ExtractToDisk(file);
            }
        }

        output.WriteLine($"Extracted base.stormdata and enus GameStrings.txt to {Path.Combine(_outDir, HeromodsPath, modName)}");

        var baseStormdata = modDir.GetEntry("base.stormdata") as CASCFolder;
        if (baseStormdata?.GetEntry("GameData.xml") is not CASCFile cfConfig) {
            output.WriteLine("No base.stormdata\\GameData.xml: the scraper skips this mod as not a hero.");
            return;
        }

        var config = LoadXml(cfConfig);
        var catalogPaths = config.SelectNodes("//Catalog")!.Cast<XmlNode>()
            .Select(x => x.Attributes?["path"]?.Value)
            .OfType<string>()
            .ToList();
        output.WriteLine($"GameData.xml includes {catalogPaths.Count} catalog(s):");
        for (var i = 0; i < catalogPaths.Count; i++) {
            output.WriteLine($"  [{i}] {catalogPaths[i]}{(i == 0 ? "   <- the only one the scraper reads" : "")}");
        }

        var gameData = baseStormdata.GetEntry("GameData") as CASCFolder;
        var catalogs = new List<(string Name, XmlDocument Doc)>();
        foreach (var catalogPath in catalogPaths) {
            var relative = catalogPath.StartsWith("GameData/", StringComparison.OrdinalIgnoreCase)
                ? catalogPath[9..]
                : catalogPath;
            if (ResolveFile(gameData, relative) is not { } cf) {
                output.WriteLine($"  MISSING in CASC: {catalogPath}");
                continue;
            }

            catalogs.Add((catalogPath, LoadXml(cf)));
        }

        // Per-type files such as GameData/ButtonData.xml are loaded by convention, not via GameData.xml.
        var included = catalogs.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allDocs = new List<(string Name, XmlDocument Doc)>(catalogs);
        if (gameData != null) {
            var prefix = $@"{HeromodsPath}\{modName}\base.stormdata\";
            foreach (var file in CASCFolder.GetFiles(gameData.Entries.Values)) {
                var path = Normalize(file.FullName);
                var name = (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path[prefix.Length..] : path)
                    .Replace('\\', '/');
                if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !included.Contains(name) &&
                    TryLoadXml(file) is { } doc) {
                    allDocs.Add(($"{name} (not in GameData.xml includes)", doc));
                }
            }
        }

        var genericTalents = LoadGenericIds("TalentData.xml", "CTalent");
        var genericButtons = LoadGenericIds("ButtonData.xml", "CButton");
        var locStrings = LoadLocStrings(modDir);

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
        foreach (var dir in new[] { GenericGameDataPath, CoreGameDataPath }) {
            if (ResolveFile(_root, $@"{dir}\{fileName}") is not { } cf) {
                continue;
            }

            foreach (XmlNode node in LoadXml(cf).SelectNodes($"//{element}[@id]")!) {
                ids.Add(node.Attributes!["id"]!.Value);
            }
        }

        return ids;
    }

    private Dictionary<string, string> LoadLocStrings(CASCFolder modDir) {
        var result = new Dictionary<string, string>();
        if (ResolveFile(modDir, @"enus.stormdata\LocalizedData\GameStrings.txt") is not { } cf) {
            return result;
        }

        using var stream = _casc.OpenFile(cf.Hash);
        foreach (var line in Scraper.ReadAllLines(stream)) {
            var idx = line.IndexOf('=');
            if (idx > 0) {
                result.TryAdd(line[..idx], line[(idx + 1)..]);
            }
        }

        return result;
    }

    private static CASCFile? ResolveFile(CASCFolder? folder, string relativePath) {
        ICASCEntry? entry = folder;
        foreach (var part in relativePath.Split('\\', '/')) {
            entry = (entry as CASCFolder)?.GetEntry(part);
        }

        return entry as CASCFile;
    }

    private string ExtractToDisk(CASCFile file) {
        var target = Path.Combine(_outDir, Normalize(file.FullName));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var src = _casc.OpenFile(file.Hash);
        using var dst = File.Create(target);
        src.CopyTo(dst);
        return target;
    }

    private XmlDocument LoadXml(CASCFile file) {
        using var stream = _casc.OpenFile(file.Hash);
        var doc = new XmlDocument();
        doc.Load(stream);
        return doc;
    }

    private XmlDocument? TryLoadXml(CASCFile file) {
        try {
            return LoadXml(file);
        }
        catch (XmlException) {
            return null;
        }
    }

    private static string Normalize(string path) => path.Replace('/', '\\');

    private static string Squash(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static Regex GlobToRegex(string glob) {
        var escaped = Regex.Escape(Normalize(glob))
            .Replace(@"\*\*\\", "(.*\\\\)?")
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^\\]*")
            .Replace(@"\?", @"[^\\]");
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase);
    }
}
