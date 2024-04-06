namespace CascLibCore;

public abstract class RootHandlerBase {
    private static readonly char[] PathDelimiters = ['/', '\\'];
    protected readonly Jenkins96 Hasher = new();
    protected CASCFolder Root;

    public virtual int Count { get; }
    public virtual int CountTotal { get; protected set; }
    public virtual int CountSelect { get; protected set; }
    public virtual int CountUnknown { get; protected set; }
    public virtual LocaleFlags Locale { get; protected set; }
    public virtual ContentFlags Content { get; protected set; }

    public abstract void Clear();

    public abstract void Dump();

    public abstract IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries();

    public abstract IEnumerable<RootEntry> GetAllEntries(ulong hash);

    public abstract IEnumerable<RootEntry> GetEntries(ulong hash);

    public abstract void LoadListFile(string path, BackgroundWorkerEx? worker = null);

    public void MergeInstall(InstallHandler install) {
        if (install == null) {
            return;
        }

        foreach (var entry in install.GetEntries()) {
            CreateSubTree(Root, Hasher.ComputeHash(entry.Name), entry.Name);
        }
    }

    public CASCFolder SetFlags(LocaleFlags locale, ContentFlags content, bool createTree = true) {
        using var _ = new PerfCounter(GetType().Name + "::SetFlags()");
        Locale = locale;
        Content = content;

        if (createTree) {
            Root = CreateStorageTree();
        }

        return Root;
    }

    protected abstract CASCFolder CreateStorageTree();

    protected void CreateSubTree(CASCFolder root, ulong filehash, string file) {
        var parts = file.Split(PathDelimiters);

        var folder = root;

        for (var i = 0; i < parts.Length - 1; ++i) {
            var folderName = parts[i];

            var folderEntry = folder.GetEntry(folderName);

            if (folderEntry is not null and not CASCFolder) {
                throw new InvalidOperationException($"Expected folder but found {folderEntry}");
            }

            if (folderEntry is null) {
                folderEntry = new CASCFolder(folderName);

                folder.Entries[folderName] = folderEntry;
            }

            folder = (CASCFolder)folderEntry;
        }

        var fileName = parts[^1];

        var fileEntry = folder.GetEntry(fileName);

        if (fileEntry is not null and not CASCFile) {
            throw new InvalidOperationException($"Expected file but found {fileEntry}");
        }

        if (fileEntry is null) {
            fileEntry = new CASCFile(filehash);
            CASCFile.FileNames[filehash] = file;

            folder.Entries[fileName] = fileEntry;
        }

    }
}
