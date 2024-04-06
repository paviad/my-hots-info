namespace CascLibCore;

public sealed class CASCHandlerLite : CASCHandlerBase {
    private static readonly MD5HashComparer comparer = new();
    private readonly Dictionary<MD5Hash, IndexEntry> CDNIndexData;
    private readonly Dictionary<int, ulong> FileDataIdToHash = [];
    private readonly Dictionary<ulong, MD5Hash> HashToKey = [];
    private readonly Dictionary<MD5Hash, IndexEntry> LocalIndexData;

    private CASCHandlerLite(CASCConfig config, LocaleFlags locale, BackgroundWorkerEx? worker) : base(config, worker) {
        if (config.GameType != CASCGameType.WoW) {
            throw new Exception("Unsupported game " + config.BuildUID);
        }

        Logger.WriteLine("CASCHandlerLite: loading encoding data...");

        EncodingHandler EncodingHandler;

        using (var _ = new PerfCounter("new EncodingHandler()")) {
            using (var fs = OpenEncodingFile(this)) {
                EncodingHandler = new EncodingHandler(fs, worker);
            }
        }

        Logger.WriteLine("CASCHandlerLite: loaded {0} encoding data", EncodingHandler.Count);

        Logger.WriteLine("CASCHandlerLite: loading root data...");

        WowRootHandler RootHandler;

        using (var _ = new PerfCounter("new RootHandler()")) {
            using (var fs = OpenRootFile(EncodingHandler, this)) {
                RootHandler = new WowRootHandler(fs, worker);
            }
        }

        Logger.WriteLine("CASCHandlerLite: loaded {0} root data", RootHandler.Count);

        RootHandler.SetFlags(locale, ContentFlags.None, false);

        CDNIndexData = new Dictionary<MD5Hash, IndexEntry>(comparer);

        if (LocalIndex != null) {
            LocalIndexData = new Dictionary<MD5Hash, IndexEntry>(comparer);
        }

        foreach (var entry in RootHandler.GetAllEntries()) {
            var rootEntry = entry.Value;

            if ((rootEntry.LocaleFlags == locale || (rootEntry.LocaleFlags & locale) != LocaleFlags.None) &&
                (rootEntry.ContentFlags & ContentFlags.LowViolence) == ContentFlags.None) {
                if (EncodingHandler.GetEntry(rootEntry.MD5, out var enc)) {
                    if (HashToKey.TryAdd(entry.Key, enc.Key)) {
                        FileDataIdToHash.Add(RootHandler.GetFileDataIdByHash(entry.Key), entry.Key);

                        var iLocal = LocalIndex?.GetIndexInfo(enc.Key);

                        if (iLocal != null) {
                            LocalIndexData.TryAdd(enc.Key, iLocal);
                        }

                        var iCDN = CDNIndex.GetIndexInfo(enc.Key);

                        if (iCDN != null) {
                            CDNIndexData.TryAdd(enc.Key, iCDN);
                        }
                    }
                }
            }
        }

        CDNIndex.Clear();
        //CDNIndex = null;
        LocalIndex?.Clear();
        LocalIndex = null;
        RootHandler.Clear();
        RootHandler = null;
        EncodingHandler.Clear();
        EncodingHandler = null;
        GC.Collect();

        Logger.WriteLine("CASCHandlerLite: loaded {0} files", HashToKey.Count);
    }

    public static CASCHandlerLite OpenLocalStorage(
        string basePath,
        LocaleFlags locale,
        BackgroundWorkerEx? worker = null) {
        var config = CASCConfig.LoadLocalStorageConfig(basePath);

        return Open(locale, worker, config);
    }

    public static CASCHandlerLite OpenOnlineStorage(
        string product,
        LocaleFlags locale,
        string region = "us",
        BackgroundWorkerEx? worker = null) {
        var config = CASCConfig.LoadOnlineStorageConfig(product, region);

        return Open(locale, worker, config);
    }

    public static CASCHandlerLite OpenStorage(
        LocaleFlags locale,
        CASCConfig config,
        BackgroundWorkerEx? worker = null) {
        return Open(locale, worker, config);
    }

    public override bool FileExists(int fileDataId) => FileDataIdToHash.ContainsKey(fileDataId);

    public override bool FileExists(string file) => FileExists(Hasher.ComputeHash(file));

    public override bool FileExists(ulong hash) => HashToKey.ContainsKey(hash);

    public override Stream? OpenFile(int filedata) {
        if (FileDataIdToHash.TryGetValue(filedata, out var hash)) {
            return OpenFile(hash);
        }

        return null;
    }

    public override Stream? OpenFile(string name) => OpenFile(Hasher.ComputeHash(name));

    public override Stream? OpenFile(ulong hash) {
        return HashToKey.TryGetValue(hash, out var key) ? OpenFile(key) : null;
    }

    public override void SaveFileTo(ulong hash, string extractPath, string fullName) {
        if (HashToKey.TryGetValue(hash, out var key)) {
            SaveFileTo(key, extractPath, fullName);
            return;
        }

        if (CASCConfig.ThrowOnFileNotFound) {
            throw new FileNotFoundException(fullName);
        }
    }

    protected override void ExtractFileOnline(MD5Hash key, string path, string name) {
        var idxInfo = CDNIndex.GetIndexInfo(key);

        if (idxInfo == null) {
            CDNIndexData.TryGetValue(key, out idxInfo);
        }

        ExtractFileOnlineInternal(idxInfo, key, path, name);
    }

    protected override Stream GetLocalDataStream(MD5Hash key) {
        IndexEntry? idxInfo;

        if (LocalIndex != null) {
            idxInfo = LocalIndex.GetIndexInfo(key);
        }
        else {
            LocalIndexData.TryGetValue(key, out idxInfo);
        }

        return GetLocalDataStreamInternal(idxInfo, key);
    }

    protected override Stream OpenFileOnline(MD5Hash key) {
        var idxInfo = CDNIndex.GetIndexInfo(key);

        if (idxInfo == null) {
            CDNIndexData.TryGetValue(key, out idxInfo);
        }

        return OpenFileLocalInternal(idxInfo, key);
    }

    private static CASCHandlerLite Open(LocaleFlags locale, BackgroundWorkerEx? worker, CASCConfig config) {
        using var _ = new PerfCounter("new CASCHandlerLite()");
        return new CASCHandlerLite(config, locale, worker);
    }
}
