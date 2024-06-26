﻿using System.Runtime.InteropServices;

namespace CascLibCore;

internal struct D3RootEntry {
    public MD5Hash MD5;
    public int Type;
    public int SNO;
    public int FileIndex;
    public string Name;

    public static D3RootEntry Read(int type, BinaryReader s) {
        var e = new D3RootEntry {
            Type = type,
            MD5 = s.Read<MD5Hash>(),
        };

        if (type is 0 or 1) // has SNO id
        {
            e.SNO = s.ReadInt32();

            if (type == 1) // has file index
            {
                e.FileIndex = s.ReadInt32();
            }
        }
        else // Named file
        {
            e.Name = s.ReadCString();
        }

        return e;
    }
}

public class D3RootHandler : RootHandlerBase {
    private readonly Dictionary<string, List<D3RootEntry>> D3RootData = [];
    private readonly MultiDictionary<ulong, RootEntry> RootData = [];
    private PackagesParser pkgParser;
    private CoreTOCParser tocParser;

    public D3RootHandler(BinaryReader stream, BackgroundWorkerEx? worker, CASCHandler casc) {
        worker?.ReportProgress(0, "Loading \"root\"...");

        var b1 = stream.ReadByte();
        var b2 = stream.ReadByte();
        var b3 = stream.ReadByte();
        var b4 = stream.ReadByte();

        var count = stream.ReadInt32();

        for (var j = 0; j < count; j++) {
            var md5 = stream.Read<MD5Hash>();
            var name = stream.ReadCString();

            var entries = new List<D3RootEntry>();
            D3RootData[name] = entries;

            if (!casc.Encoding.GetEntry(md5, out var enc)) {
                continue;
            }

            using (var s = new BinaryReader(casc.OpenFile(enc.Key))) {
                if (s != null) {
                    var magic = s.ReadUInt32();

                    var nEntries0 = s.ReadInt32();

                    for (var i = 0; i < nEntries0; i++) {
                        entries.Add(D3RootEntry.Read(0, s));
                    }

                    var nEntries1 = s.ReadInt32();

                    for (var i = 0; i < nEntries1; i++) {
                        entries.Add(D3RootEntry.Read(1, s));
                    }

                    var nNamedEntries = s.ReadInt32();

                    for (var i = 0; i < nNamedEntries; i++) {
                        entries.Add(D3RootEntry.Read(2, s));
                    }
                }
            }

            worker?.ReportProgress((int)((j + 1) / (float)(count + 2) * 100));
        }

        // Parse CoreTOC.dat
        var coreTocEntry = D3RootData["Base"].Find(e => e.Name == "CoreTOC.dat");

        casc.Encoding.GetEntry(coreTocEntry.MD5, out var enc1);

        using (var file = casc.OpenFile(enc1.Key)) {
            tocParser = new CoreTOCParser(file);
        }

        worker?.ReportProgress((int)((count + 1) / (float)(count + 2) * 100));

        // Parse Packages.dat
        var pkgEntry = D3RootData["Base"].Find(e => e.Name == @"Data_D3\PC\Misc\Packages.dat");

        casc.Encoding.GetEntry(pkgEntry.MD5, out var enc2);

        using (var file = casc.OpenFile(enc2.Key)) {
            pkgParser = new PackagesParser(file);
        }

        worker?.ReportProgress(100);
    }

    public override int Count => RootData.Count;

    public override int CountTotal {
        get { return RootData.Sum(re => re.Value.Count); }
    }

    public override void Clear() {
        RootData.Clear();
        D3RootData.Clear();
        tocParser = null;
        pkgParser = null;
        CASCFile.FileNames.Clear();
    }

    public override void Dump() { }

    public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries() {
        foreach (var set in RootData)
            foreach (var entry in set.Value) {
                yield return new KeyValuePair<ulong, RootEntry>(set.Key, entry);
            }
    }

    public override IEnumerable<RootEntry> GetAllEntries(ulong hash) {
        RootData.TryGetValue(hash, out var result);

        if (result == null) {
            yield break;
        }

        foreach (var entry in result) {
            yield return entry;
        }
    }

    public override IEnumerable<RootEntry> GetEntries(ulong hash) {
        var rootInfos = GetAllEntries(hash);

        if (!rootInfos.Any()) {
            yield break;
        }

        var rootInfosLocale = rootInfos.Where(re => (re.LocaleFlags & Locale) != 0);

        foreach (var entry in rootInfosLocale) {
            yield return entry;
        }
    }

    public override void LoadListFile(string path, BackgroundWorkerEx? worker = null) {
        worker?.ReportProgress(0, "Loading \"listfile\"...");

        Logger.WriteLine("D3RootHandler: loading file names...");

        var numFiles = D3RootData.Sum(p => p.Value.Count);

        var i = 0;

        foreach (var kv in D3RootData) {
            foreach (var e in kv.Value) {
                AddFile(kv.Key, e);

                worker?.ReportProgress((int)(++i / (float)numFiles * 100));
            }
        }

        Logger.WriteLine("D3RootHandler: loaded {0} file names", i);
    }

    protected override CASCFolder CreateStorageTree() {
        var root = new CASCFolder("root");

        CountSelect = 0;

        // Create new tree based on specified locale
        foreach (var rootEntry in RootData) {
            var rootInfosLocale = rootEntry.Value.Where(re => (re.LocaleFlags & Locale) != 0);

            if (!rootInfosLocale.Any()) {
                continue;
            }

            CreateSubTree(root, rootEntry.Key, CASCFile.FileNames[rootEntry.Key]);
            CountSelect++;
        }

        Logger.WriteLine("D3RootHandler: {0} file names missing extensions for locale {1}", CountUnknown, Locale);

        return root;
    }

    private void AddFile(string pkg, D3RootEntry e) {
        string name;

        switch (e.Type) {
            case 0:
                var sno1 = tocParser.GetSNO(e.SNO);
                name = $@"{sno1.GroupId}\{sno1.Name}{sno1.Ext}";
                break;
            case 1:
                var sno2 = tocParser.GetSNO(e.SNO);
                name = $@"{sno2.GroupId}\{sno2.Name}\{e.FileIndex:D4}";

                var ext = pkgParser.GetExtension(name);

                if (ext != null) {
                    name += ext;
                }
                else {
                    CountUnknown++;
                    name += ".xxx";
                }

                break;
            case 2:
                name = e.Name;
                break;
            default:
                name = "Unknown";
                break;
        }

        var entry = new RootEntry {
            MD5 = e.MD5,
            LocaleFlags = Enum.TryParse(pkg, out LocaleFlags locale)
                ? locale
                : LocaleFlags.All,
        };

        var fileHash = Hasher.ComputeHash(name);
        CASCFile.FileNames[fileHash] = name;

        RootData.Add(fileHash, entry);
    }
}

public class SNOInfo {
    public string Ext;
    public SNOGroup GroupId;
    public string Name;
}

public enum SNOGroup {
    Code = -2,
    None = -1,
    Actor = 1,
    Adventure = 2,
    AiBehavior = 3,
    AiState = 4,
    AmbientSound = 5,
    Anim = 6,
    Animation2D = 7,
    AnimSet = 8,
    Appearance = 9,
    Hero = 10,
    Cloth = 11,
    Conversation = 12,
    ConversationList = 13,
    EffectGroup = 14,
    Encounter = 15,
    Explosion = 17,
    FlagSet = 18,
    Font = 19,
    GameBalance = 20,
    Globals = 21,
    LevelArea = 22,
    Light = 23,
    MarkerSet = 24,
    Monster = 25,
    Observer = 26,
    Particle = 27,
    Physics = 28,
    Power = 29,
    Quest = 31,
    Rope = 32,
    Scene = 33,
    SceneGroup = 34,
    Script = 35,
    ShaderMap = 36,
    Shaders = 37,
    Shakes = 38,
    SkillKit = 39,
    Sound = 40,
    SoundBank = 41,
    StringList = 42,
    Surface = 43,
    Textures = 44,
    Trail = 45,
    UI = 46,
    Weather = 47,
    Worlds = 48,
    Recipe = 49,
    Condition = 51,
    TreasureClass = 52,
    Account = 53,
    Conductor = 54,
    TimedEvent = 55,
    Act = 56,
    Material = 57,
    QuestRange = 58,
    Lore = 59,
    Reverb = 60,
    PhysMesh = 61,
    Music = 62,
    Tutorial = 63,
    BossEncounter = 64,
    ControlScheme = 65,
    Accolade = 66,
    AnimTree = 67,
    Vibration = 68,
    DungeonFinder = 69,
}

public class CoreTOCParser {
    private const int NUM_SNO_GROUPS = 70;

    private readonly Dictionary<SNOGroup, string> extensions = new() {
        { SNOGroup.Code, "" },
        { SNOGroup.None, "" },
        { SNOGroup.Actor, ".acr" },
        { SNOGroup.Adventure, ".adv" },
        { SNOGroup.AiBehavior, "" },
        { SNOGroup.AiState, "" },
        { SNOGroup.AmbientSound, ".ams" },
        { SNOGroup.Anim, ".ani" },
        { SNOGroup.Animation2D, ".an2" },
        { SNOGroup.AnimSet, ".ans" },
        { SNOGroup.Appearance, ".app" },
        { SNOGroup.Hero, "" },
        { SNOGroup.Cloth, ".clt" },
        { SNOGroup.Conversation, ".cnv" },
        { SNOGroup.ConversationList, "" },
        { SNOGroup.EffectGroup, ".efg" },
        { SNOGroup.Encounter, ".enc" },
        { SNOGroup.Explosion, ".xpl" },
        { SNOGroup.FlagSet, "" },
        { SNOGroup.Font, ".fnt" },
        { SNOGroup.GameBalance, ".gam" },
        { SNOGroup.Globals, ".glo" },
        { SNOGroup.LevelArea, ".lvl" },
        { SNOGroup.Light, ".lit" },
        { SNOGroup.MarkerSet, ".mrk" },
        { SNOGroup.Monster, ".mon" },
        { SNOGroup.Observer, ".obs" },
        { SNOGroup.Particle, ".prt" },
        { SNOGroup.Physics, ".phy" },
        { SNOGroup.Power, ".pow" },
        { SNOGroup.Quest, ".qst" },
        { SNOGroup.Rope, ".rop" },
        { SNOGroup.Scene, ".scn" },
        { SNOGroup.SceneGroup, ".scg" },
        { SNOGroup.Script, "" },
        { SNOGroup.ShaderMap, ".shm" },
        { SNOGroup.Shaders, ".shd" },
        { SNOGroup.Shakes, ".shk" },
        { SNOGroup.SkillKit, ".skl" },
        { SNOGroup.Sound, ".snd" },
        { SNOGroup.SoundBank, ".sbk" },
        { SNOGroup.StringList, ".stl" },
        { SNOGroup.Surface, ".srf" },
        { SNOGroup.Textures, ".tex" },
        { SNOGroup.Trail, ".trl" },
        { SNOGroup.UI, ".ui" },
        { SNOGroup.Weather, ".wth" },
        { SNOGroup.Worlds, ".wrl" },
        { SNOGroup.Recipe, ".rcp" },
        { SNOGroup.Condition, ".cnd" },
        { SNOGroup.TreasureClass, "" },
        { SNOGroup.Account, "" },
        { SNOGroup.Conductor, "" },
        { SNOGroup.TimedEvent, "" },
        { SNOGroup.Act, ".act" },
        { SNOGroup.Material, ".mat" },
        { SNOGroup.QuestRange, ".qsr" },
        { SNOGroup.Lore, ".lor" },
        { SNOGroup.Reverb, ".rev" },
        { SNOGroup.PhysMesh, ".phm" },
        { SNOGroup.Music, ".mus" },
        { SNOGroup.Tutorial, ".tut" },
        { SNOGroup.BossEncounter, ".bos" },
        { SNOGroup.ControlScheme, "" },
        { SNOGroup.Accolade, ".aco" },
        { SNOGroup.AnimTree, ".ant" },
        { SNOGroup.Vibration, "" },
        { SNOGroup.DungeonFinder, "" },
    };

    private readonly Dictionary<int, SNOInfo> snoDic = [];

    public unsafe CoreTOCParser(Stream stream) {
        using var br = new BinaryReader(stream);
        var hdr = br.Read<TOCHeader>();

        for (var i = 0; i < NUM_SNO_GROUPS; i++) {
            if (hdr.entryCounts[i] > 0) {
                br.BaseStream.Position = hdr.entryOffsets[i] + Marshal.SizeOf(hdr);

                for (var j = 0; j < hdr.entryCounts[i]; j++) {
                    var snoGroup = (SNOGroup)br.ReadInt32();
                    var snoId = br.ReadInt32();
                    var pName = br.ReadInt32();

                    var oldPos = br.BaseStream.Position;
                    br.BaseStream.Position = hdr.entryOffsets[i] + Marshal.SizeOf(hdr) +
                                             12 * hdr.entryCounts[i] + pName;
                    var name = br.ReadCString();
                    br.BaseStream.Position = oldPos;

                    snoDic.Add(
                        snoId,
                        new SNOInfo {
                            GroupId = snoGroup,
                            Name = name,
                            Ext = extensions[snoGroup],
                        });
                }
            }
        }
    }

    public SNOInfo? GetSNO(int id) {
        snoDic.TryGetValue(id, out var sno);
        return sno;
    }

    public unsafe struct TOCHeader {
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_SNO_GROUPS)]
        public fixed int entryCounts[NUM_SNO_GROUPS];

        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_SNO_GROUPS)]
        public fixed int entryOffsets[NUM_SNO_GROUPS];

        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_SNO_GROUPS)]
        public fixed int entryUnkCounts[NUM_SNO_GROUPS];
        public int unk;
    }
}

public class PackagesParser {
    private readonly Dictionary<string, string> nameToExtDic = new(StringComparer.OrdinalIgnoreCase);

    public PackagesParser(Stream stream) {
        using var br = new BinaryReader(stream);
        var sign = br.ReadInt32();
        var namesCount = br.ReadInt32();

        for (var i = 0; i < namesCount; i++) {
            var name = br.ReadCString();
            nameToExtDic[name[..^4]] = Path.GetExtension(name);
        }
    }

    public string? GetExtension(string partialName) {
        nameToExtDic.TryGetValue(partialName, out var ext);
        return ext;
    }
}
