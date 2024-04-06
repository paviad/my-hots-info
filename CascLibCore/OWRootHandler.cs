using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace CascLibCore;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct APMEntry {
    public uint Index;
    public uint hashA;
    public uint hashB;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct APMPackage {
    public ulong localKey;
    public ulong primaryKey;
    public ulong externalKey;
    public ulong encryptionKeyHash;
    public ulong packageKey;
    public uint unk_0;
    public uint unk_1; // size?
    public uint unk_2;
    public uint unk_3;
    public MD5Hash indexContentKey; // Content key of the Package Index
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PackageIndex {
    public long
        recordsOffset; // Offset to GZIP compressed records chunk, read (recordsSize + numRecords) bytes here

    public ulong unkOffset_0;
    public long depsOffset; // Offset to dependencies chunk, read numDeps * uint here
    public ulong unkOffset_1;
    public uint unk_0;
    public uint numRecords;
    public int recordsSize;
    public uint unk_1;
    public uint numDeps;
    public uint totalSize;
    public ulong bundleKey; // Requires some sorcery, see Key
    public uint bundleSize;
    public ulong unk_2;

    public MD5Hash bundleContentKey; // Look this up in encoding
    //PackageIndexRecord[numRecords] records;   // See recordsOffset and PackageIndexRecord
    //u32[numDeps] dependencies;                // See depsOffset
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PackageIndexRecord {
    public ulong Key; // Requires some sorcery, see Key
    public int Size; // Size of asset
    public uint Flags; // Flags. Has 0x40000000 when in bundle, otherwise in encoding
    public uint Offset; // Offset into bundle
    public MD5Hash ContentKey; // If it doesn't have the above flag (0x40000000) look it up in encoding
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct OWRootEntry {
    public RootEntry baseEntry;
    public PackageIndex pkgIndex;
    public PackageIndexRecord pkgIndexRec;
}

public class OwRootHandler : RootHandlerBase {
    private readonly Dictionary<ulong, OWRootEntry> _rootData = [];

    private readonly List<APMFile> apmFiles = [];

    public OwRootHandler(BinaryReader stream, BackgroundWorkerEx? worker, CASCHandler casc) {
        worker?.ReportProgress(0, "Loading \"root\"...");

        var str = Encoding.ASCII.GetString(stream.ReadBytes((int)stream.BaseStream.Length));

        var array = str.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 1; i < array.Length; i++) {
            var filedata = array[i].Split('|');

            var name = filedata[4];

            if (Path.GetExtension(name) == ".apm" && name.Contains("LenUS")) {
                // add apm file for dev purposes
                var apmNameHash = Hasher.ComputeHash(name);
                var apmMD5 = filedata[0].ToByteArray().ToMD5();
                _rootData[apmNameHash] = new OWRootEntry {
                    baseEntry = new RootEntry {
                        MD5 = apmMD5,
                        LocaleFlags = LocaleFlags.All,
                        ContentFlags = ContentFlags.None,
                    },
                };

                CASCFile.FileNames[apmNameHash] = name;

                if (!casc.Encoding.GetEntry(apmMD5, out var apmEnc)) {
                    continue;
                }

                using var apmStream = casc.OpenFile(apmEnc.Key);
                apmFiles.Add(new APMFile(name, apmStream, casc));
            }

            worker?.ReportProgress((int)(i / (array.Length / 100f)));
        }
    }

    public override int Count => _rootData.Count;
    public IReadOnlyList<APMFile> APMFiles => apmFiles;

    public override void Clear() {
        _rootData.Clear();
        Root.Entries.Clear();
        CASCFile.FileNames.Clear();
    }

    public override void Dump() { }

    public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries() {
        foreach (var entry in _rootData) {
            yield return new KeyValuePair<ulong, RootEntry>(entry.Key, entry.Value.baseEntry);
        }
    }

    public override IEnumerable<RootEntry> GetAllEntries(ulong hash) {
        if (_rootData.TryGetValue(hash, out var entry)) {
            yield return entry.baseEntry;
        }
    }

    // Returns only entries that match current locale and content flags
    public override IEnumerable<RootEntry> GetEntries(ulong hash) {
        return GetAllEntries(hash);
    }

    public bool GetEntry(ulong hash, out OWRootEntry entry) {
        return _rootData.TryGetValue(hash, out entry);
    }

    public override void LoadListFile(string path, BackgroundWorkerEx? worker = null) {
        worker?.ReportProgress(0, "Loading \"listfile\"...");

        Logger.WriteLine("OWRootHandler: loading file names...");

        var pkgOnePct = apmFiles.Sum(a => a.Packages.Length) / 100f;

        var pkgCount = 0;

        foreach (var apm in apmFiles) {
            for (var i = 0; i < apm.Packages.Length; i++) {
                var package = apm.Packages[i];

                var pkgIndexMD5 = package.indexContentKey;

                var apmName = Path.GetFileNameWithoutExtension(apm.Name);
                var pkgName = $"{apmName}/package_{i:X4}_{package.packageKey:X16}";
                var fakeName = $"{pkgName}_index";

                var fileHash = Hasher.ComputeHash(fakeName);
                Logger.WriteLine("Adding package: {0:X16} {1}", fileHash, package.indexContentKey.ToHexString());
                if (_rootData.TryGetValue(fileHash, out var value)) {
                    if (!value.baseEntry.MD5.EqualsTo(package.indexContentKey)) {
                        Logger.WriteLine(
                            "Weird duplicate package: {0:X16} {1}",
                            fileHash,
                            package.indexContentKey.ToHexString());
                    }
                    else {
                        Logger.WriteLine(
                            "Duplicate package: {0:X16} {1}",
                            fileHash,
                            package.indexContentKey.ToHexString());
                    }

                    continue;
                }

                _rootData[fileHash] = new OWRootEntry {
                    baseEntry = new RootEntry {
                        MD5 = pkgIndexMD5,
                        LocaleFlags = LocaleFlags.All,
                        ContentFlags = ContentFlags.None,
                    },
                };

                CASCFile.FileNames[fileHash] = fakeName;

                var pkgIndex = apm.Indexes[i];

                fakeName = $"{pkgName}_bundle_{pkgIndex.bundleKey:X16}";

                fileHash = Hasher.ComputeHash(fakeName);
                Logger.WriteLine("Adding bundle: {0:X16} {1}", fileHash, pkgIndex.bundleContentKey.ToHexString());
                if (_rootData.TryGetValue(fileHash, out var value1)) {
                    Logger.WriteLine(
                        !value1.baseEntry.MD5.EqualsTo(pkgIndex.bundleContentKey)
                            ? "Weird duplicate bundle: {0:X16} {1}"
                            : "Duplicate bundle: {0:X16} {1}",
                        fileHash,
                        pkgIndex.bundleContentKey.ToHexString());

                    continue;
                }

                _rootData[fileHash] = new OWRootEntry {
                    baseEntry = new RootEntry {
                        MD5 = pkgIndex.bundleContentKey,
                        LocaleFlags = LocaleFlags.All,
                        ContentFlags = ContentFlags.None,
                    },
                    pkgIndex = pkgIndex,
                };

                CASCFile.FileNames[fileHash] = fakeName;

                var records = apm.Records[i];

                for (var k = 0; k < records.Length; k++) {
                    fakeName = string.Format(
                        "files/{0:X3}/{1:X12}.{0:X3}",
                        keyToTypeID(records[k].Key),
                        records[k].Key & 0xFFFFFFFFFFFF);

                    fileHash = Hasher.ComputeHash(fakeName);
                    //Logger.WriteLine("Adding package record: key {0:X16} hash {1} flags {2:X8}", fileHash, records[k].contentKey.ToHexString(), records[k].flags);
                    if (_rootData.TryGetValue(fileHash, out var value2)) {
                        if (!value2.baseEntry.MD5.EqualsTo(records[k].ContentKey)) {
                            Logger.WriteLine(
                                "Weird duplicate package record: {0:X16} {1}",
                                fileHash,
                                records[k].ContentKey.ToHexString());
                        }

                        //else
                        //    Logger.WriteLine("Duplicate package record: {0:X16} {1}", fileHash, records[k].contentKey.ToHexString());
                        continue;
                    }

                    _rootData[fileHash] = new OWRootEntry {
                        baseEntry = new RootEntry {
                            MD5 = records[k].ContentKey,
                            LocaleFlags = LocaleFlags.All,
                            ContentFlags = (ContentFlags)records[k].Flags,
                        },
                        pkgIndex = pkgIndex,
                        pkgIndexRec = records[k],
                    };

                    CASCFile.FileNames[fileHash] = fakeName;
                }

                worker?.ReportProgress((int)(++pkgCount / pkgOnePct));
            }
        }

        Logger.WriteLine("OWRootHandler: loaded {0} file names", _rootData.Count);
    }

    protected override CASCFolder CreateStorageTree() {
        var root = new CASCFolder("root");

        CountSelect = 0;
        CountUnknown = 0;

        foreach (var rootEntry in _rootData) {
            if ((rootEntry.Value.baseEntry.LocaleFlags & Locale) == 0) {
                continue;
            }

            CreateSubTree(root, rootEntry.Key, CASCFile.FileNames[rootEntry.Key]);
            CountSelect++;
        }

        Logger.WriteLine("OwRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

        return root;
    }

    private static ulong keyToTypeID(ulong key) {
        var num = key >> 48;
        num = ((num >> 1) & 0x55555555) | ((num & 0x55555555) << 1);
        num = ((num >> 2) & 0x33333333) | ((num & 0x33333333) << 2);
        num = ((num >> 4) & 0x0F0F0F0F) | ((num & 0x0F0F0F0F) << 4);
        num = ((num >> 8) & 0x00FF00FF) | ((num & 0x00FF00FF) << 8);
        num = (num >> 16) | (num << 16);
        num >>= 20;
        return num + 1;
    }
}

public class APMFile {
    private readonly uint[][] dependencies;

    public APMFile(string name, Stream stream, CASCHandler casc) {
        Name = name;

        using var reader = new BinaryReader(stream);
        var buildVersion = reader.ReadUInt64();
        var buildNumber = reader.ReadUInt32();
        var packageCount = reader.ReadUInt32();
        var entryCount = reader.ReadUInt32();
        var unk = reader.ReadUInt32();

        Entries = new APMEntry[entryCount];

        for (var j = 0; j < entryCount; j++) {
            Entries[j] = reader.Read<APMEntry>();
        }

        Packages = new APMPackage[packageCount];
        Indexes = new PackageIndex[packageCount];
        Records = new PackageIndexRecord[packageCount][];
        dependencies = new uint[packageCount][];

        for (var j = 0; j < Packages.Length; j++) {
            Packages[j] = reader.Read<APMPackage>();

            if (!casc.Encoding.GetEntry(Packages[j].indexContentKey, out var pkgIndexEnc)) {
                throw new Exception("pkgIndexEnc missing");
            }

            using var pkgIndexStream = casc.OpenFile(pkgIndexEnc.Key);
            using var pkgIndexReader = new BinaryReader(pkgIndexStream);
            Indexes[j] = pkgIndexReader.Read<PackageIndex>();

            pkgIndexStream.Position = Indexes[j].recordsOffset;

            using (var recordsStream = new GZipStream(pkgIndexStream, CompressionMode.Decompress, true))
            using (var recordsReader = new BinaryReader(recordsStream)) {
                var recs = new PackageIndexRecord[Indexes[j].numRecords];

                for (var k = 0; k < recs.Length; k++) {
                    recs[k] = recordsReader.Read<PackageIndexRecord>();
                }

                Records[j] = recs;
            }

            pkgIndexStream.Position = Indexes[j].depsOffset;

            var deps = new uint[Indexes[j].numDeps];

            for (var k = 0; k < deps.Length; k++) {
                deps[k] = pkgIndexReader.ReadUInt32();
            }

            dependencies[j] = deps;
        }
    }

    public APMPackage[] Packages { get; }

    public APMEntry[] Entries { get; }

    public PackageIndex[] Indexes { get; }

    public PackageIndexRecord[][] Records { get; }

    public string Name { get; }

    public APMEntry GetEntry(int index) {
        return Entries[index];
    }

    public APMPackage GetPackage(int index) {
        return Packages[index];
    }
}
