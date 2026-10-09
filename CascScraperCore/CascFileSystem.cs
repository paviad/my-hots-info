using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using CascLibCore;

namespace CascScraperCore;

/// <summary>
/// Path-based view of a CASC storage, so callers address files the way the game data does
/// (<c>mods/core.stormmod/base.stormdata/GameData/AbilData.xml</c>) instead of walking folder objects.
/// Paths are case-insensitive and accept <c>/</c> or <c>\</c>; paths returned are <c>/</c>-separated.
/// Missing files throw <see cref="FileNotFoundException"/> naming the first path segment that doesn't exist;
/// use <see cref="FileExists"/> where a file is legitimately optional.
/// </summary>
public sealed class CascFileSystem {
    private readonly CASCHandler _casc;
    private readonly CASCFolder _root;

    private CascFileSystem(CASCHandler casc, CASCFolder root) {
        _casc = casc;
        _root = root;
    }

    public static CascFileSystem Open(string installPath) {
        CASCConfig.LoadFlags |= LoadFlags.Install;
        var config = CASCConfig.LoadLocalStorageConfig(installPath);

        var casc = CASCHandler.OpenStorage(config);

        (casc.Root as WowRootHandler)?.LoadFileDataComplete(casc);

        using (var _ = new PerfCounter("LoadListFile()")) {
            var bgWorker = new BackgroundWorkerEx {
                WorkerReportsProgress = true,
            };
            var ev = new AutoResetEvent(false);
            bgWorker.ProgressChanged += (_, e) => Console.WriteLine($"{e.ProgressPercentage} {e.UserState}");
            bgWorker.RunWorkerCompleted += (_, _) => ev.Set();
            bgWorker.DoWork += (_, _) => {
                casc.Root.LoadListFile("listfile.txt", bgWorker);
            };
            bgWorker.RunWorkerAsync();
            ev.WaitOne();
        }

        var root = casc.Root.SetFlags(LocaleFlags.enUS, ContentFlags.None);
        casc.Root.MergeInstall(casc.Install);

        GC.Collect();

        return new CascFileSystem(casc, root);
    }

    public static string Normalize(string path) => path.Replace('\\', '/').Trim('/');

    public static string Combine(params string[] parts) =>
        string.Join('/', parts.Select(Normalize).Where(x => x.Length > 0));

    public static string GetFileName(string path) {
        path = Normalize(path);
        return path[(path.LastIndexOf('/') + 1)..];
    }

    public bool FileExists(string path) => Resolve(path, out _) is CASCFile;

    public bool DirectoryExists(string path) => Resolve(path, out _) is CASCFolder;

    public Stream OpenRead(string path) {
        var file = ResolveFile(path);
        try {
            return _casc.OpenFile(file.Hash) ?? throw new IOException($"CASC returned no data for '{Normalize(path)}'");
        }
        catch (Exception e) when (e is not IOException) {
            throw new IOException($"Can't read CASC file '{Normalize(path)}': {e.Message}", e);
        }
    }

    public byte[] ReadAllBytes(string path) {
        using var stream = OpenRead(path);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public List<string> ReadAllLines(string path) {
        using var reader = new StreamReader(OpenRead(path));
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) {
            lines.Add(line);
        }

        return lines;
    }

    public XmlDocument LoadXml(string path) {
        using var stream = OpenRead(path);
        var doc = new XmlDocument();
        doc.Load(stream);
        return doc;
    }

    public T Deserialize<T>(string path) {
        using var stream = OpenRead(path);
        return (T)new XmlSerializer(typeof(T)).Deserialize(stream)!;
    }

    /// <summary>Full paths of the immediate subdirectories of <paramref name="path"/>.</summary>
    public IEnumerable<string> EnumerateDirectories(string path) {
        var dir = Normalize(path);
        return ResolveFolder(dir).Entries
            .Where(x => x.Value is CASCFolder)
            .Select(x => Combine(dir, x.Key));
    }

    /// <summary>
    /// Full paths of files matching <paramref name="glob"/>: <c>*</c> and <c>?</c> stay within a path segment,
    /// <c>**</c> spans segments. The walk starts at the glob's literal prefix, so
    /// <c>mods/heromods/x.stormmod/**</c> only visits that mod. A missing prefix yields nothing.
    /// </summary>
    public IEnumerable<string> EnumerateFiles(string glob) {
        glob = Normalize(glob);
        var segments = glob.Split('/');
        var literal = segments.TakeWhile(s => s.IndexOfAny(['*', '?']) < 0).ToList();
        if (literal.Count == segments.Length) {
            // No wildcards: the glob names a single file
            return FileExists(glob) ? [glob] : [];
        }

        var startDir = string.Join('/', literal);
        if (Resolve(startDir, out _) is not CASCFolder start) {
            return [];
        }

        var regex = GlobToRegex(glob);
        return Walk(start, startDir).Where(x => regex.IsMatch(x));

        static IEnumerable<string> Walk(CASCFolder folder, string path) {
            foreach (var (name, entry) in folder.Entries) {
                var child = path.Length == 0 ? name : $"{path}/{name}";
                if (entry is CASCFolder sub) {
                    foreach (var x in Walk(sub, child)) {
                        yield return x;
                    }
                }
                else {
                    yield return child;
                }
            }
        }
    }

    // Transitional: hands out the folder object for code not yet ported to paths.
    internal CASCFolder GetFolder(string path) => ResolveFolder(path);

    private CASCFile ResolveFile(string path) =>
        Resolve(path, out var error) switch {
            CASCFile file => file,
            CASCFolder => throw new FileNotFoundException($"CASC path '{Normalize(path)}' is a directory, not a file"),
            _ => throw new FileNotFoundException(error),
        };

    private CASCFolder ResolveFolder(string path) =>
        Resolve(path, out var error) switch {
            CASCFolder folder => folder,
            CASCFile => throw new DirectoryNotFoundException($"CASC path '{Normalize(path)}' is a file, not a directory"),
            _ => throw new DirectoryNotFoundException(error),
        };

    private ICASCEntry? Resolve(string path, out string error) {
        error = "";
        ICASCEntry entry = _root;
        var walked = "";
        foreach (var part in Normalize(path).Split('/', StringSplitOptions.RemoveEmptyEntries)) {
            if ((entry as CASCFolder)?.GetEntry(part) is not { } next) {
                error = entry is CASCFolder
                    ? $"CASC path '{Normalize(path)}' not found: '{walked}' has no entry '{part}'"
                    : $"CASC path '{Normalize(path)}' not found: '{walked}' is a file";
                return null;
            }

            entry = next;
            walked = walked.Length == 0 ? part : $"{walked}/{part}";
        }

        return entry;
    }

    private static Regex GlobToRegex(string glob) {
        var escaped = Regex.Escape(glob)
            .Replace(@"\*\*/", "(.*/)?")
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", "[^/]");
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase);
    }
}
