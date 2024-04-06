namespace CascLibCore;

public interface ICASCEntry {
    string Name { get; }
    ulong Hash { get; }
    int CompareTo(ICASCEntry entry, int col, CASCHandler casc);
}

public class CASCFolder(string name) : ICASCEntry {
    public Dictionary<string, ICASCEntry> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; } = name;

    public ulong Hash => 0;

    public int CompareTo(ICASCEntry other, int col, CASCHandler casc) {
        var result = 0;

        if (other is CASCFile) {
            return -1;
        }

        switch (col) {
            case 0:
            case 1:
            case 2:
            case 3:
                result = Name.CompareTo(other.Name);
                break;
            case 4:
                break;
        }

        return result;
    }

    public static IEnumerable<CASCFile> GetFiles(
        IEnumerable<ICASCEntry> entries,
        IEnumerable<int>? selection = null,
        bool recursive = true) {
        if (selection != null) {
            foreach (var index in selection) {
                var entry = entries.ElementAt(index);

                if (entry is CASCFile cascFile) {
                    yield return cascFile;
                }
                else {
                    if (recursive) {
                        var folder = (CASCFolder)entry;

                        foreach (var file in GetFiles(folder.Entries.Select(kv => kv.Value))) {
                            yield return file;
                        }
                    }
                }
            }
        }
        else {
            foreach (var entry in entries) {
                if (entry is CASCFile cascFile) {
                    yield return cascFile;
                }
                else {
                    if (recursive) {
                        var folder = (CASCFolder)entry;

                        foreach (var file in GetFiles(folder.Entries.Select(kv => kv.Value))) {
                            yield return file;
                        }
                    }
                }
            }
        }
    }

    public ICASCEntry? GetEntry(string name) {
        Entries.TryGetValue(name, out var entry);
        return entry;
    }
}

public class CASCFile(ulong hash) : ICASCEntry {
    public static readonly Dictionary<ulong, string> FileNames = [];

    public string FullName {
        get => FileNames[Hash];
        set => FileNames[Hash] = value;
    }

    public string Name => Path.GetFileName(FullName);

    public ulong Hash { get; } = hash;

    public int CompareTo(ICASCEntry other1, int col, CASCHandler casc) {
        var result = 0;

        if (other1 is CASCFolder) {
            return 1;
        }

        var other = (CASCFile)other1;

        switch (col) {
            case 0:
                result = Name.CompareTo(other.Name);
                break;
            case 1:
                result = Path.GetExtension(Name).CompareTo(Path.GetExtension(other.Name));
                break;
            case 2: {
                var e1 = casc.Root.GetEntries(Hash);
                var e2 = casc.Root.GetEntries(other.Hash);
                var flags1 = e1.Any() ? e1.First().LocaleFlags : LocaleFlags.None;
                var flags2 = e2.Any() ? e2.First().LocaleFlags : LocaleFlags.None;
                result = flags1.CompareTo(flags2);
            }
                break;
            case 3: {
                var e1 = casc.Root.GetEntries(Hash);
                var e2 = casc.Root.GetEntries(other.Hash);
                var flags1 = e1.Any() ? e1.First().ContentFlags : ContentFlags.None;
                var flags2 = e2.Any() ? e2.First().ContentFlags : ContentFlags.None;
                result = flags1.CompareTo(flags2);
            }
                break;
            case 4:
                var size1 = GetSize(casc);
                var size2 = other.GetSize(casc);

                if (size1 == size2) {
                    result = 0;
                }
                else {
                    result = size1 < size2 ? -1 : 1;
                }

                break;
        }

        return result;
    }

    public int GetSize(CASCHandler casc) {
        return casc.GetEncodingEntry(Hash, out var enc) ? enc.Size : 0;
    }
}
