﻿namespace CascLibCore;

public class LocalIndexHandler {
    private static readonly MD5HashComparer comparer = new();
    private Dictionary<MD5Hash, IndexEntry> LocalIndexData = new(comparer);

    private LocalIndexHandler() { }

    public int Count => LocalIndexData.Count;

    public static LocalIndexHandler Initialize(CASCConfig config, BackgroundWorkerEx? worker) {
        var handler = new LocalIndexHandler();

        var idxFiles = GetIdxFiles(config);

        if (idxFiles.Count == 0) {
            throw new FileNotFoundException("idx files missing!");
        }

        worker?.ReportProgress(0, "Loading \"local indexes\"...");

        var idxIndex = 0;

        foreach (var idx in idxFiles) {
            handler.ParseIndex(idx);

            worker?.ReportProgress((int)(++idxIndex / (float)idxFiles.Count * 100));
        }

        Logger.WriteLine("LocalIndexHandler: loaded {0} indexes", handler.Count);

        return handler;
    }

    public void Clear() {
        LocalIndexData.Clear();
        LocalIndexData = null;
    }

    public unsafe IndexEntry? GetIndexInfo(MD5Hash key) {
        var ptr = (ulong*)&key;
        ptr[1] &= 0xFF;

        if (!LocalIndexData.TryGetValue(key, out var result)) {
            Logger.WriteLine("LocalIndexHandler: missing index: {0}", key.ToHexString());
        }

        return result;
    }

    private static List<string> GetIdxFiles(CASCConfig config) {
        var latestIdx = new List<string>();

        var dataFolder = CASCGame.GetDataFolder(config.GameType);
        var dataPath = Path.Combine(dataFolder, "data");

        for (var i = 0; i < 0x10; ++i) {
            var files = Directory.EnumerateFiles(
                Path.Combine(config.BasePath, dataPath),
                $"{i:X2}*.idx").ToList();

            if (files.Count != 0) {
                latestIdx.Add(files.Last());
            }
        }

        return latestIdx;
    }

    private unsafe void ParseIndex(string idx) {
        using var fs = new FileStream(idx, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var br = new BinaryReader(fs);
        var h2Len = br.ReadInt32();
        var h2Check = br.ReadInt32();
        var h2 = br.ReadBytes(h2Len);

        var padPos = (8 + h2Len + 0x0F) & 0xFFFFFFF0;
        fs.Position = padPos;

        var dataLen = br.ReadInt32();
        var dataCheck = br.ReadInt32();

        var numBlocks = dataLen / 18;

        //byte[] buf = new byte[8];

        for (var i = 0; i < numBlocks; i++) {
            var info = new IndexEntry();
            var keyBytes = br.ReadBytes(9);
            Array.Resize(ref keyBytes, 16);

            MD5Hash key;

            fixed (byte* ptr = keyBytes) {
                key = *(MD5Hash*)ptr;
            }

            var indexHigh = br.ReadByte();
            var indexLow = br.ReadInt32BE();

            info.Index = (indexHigh << 2) | (byte)((indexLow & 0xC0000000) >> 30);
            info.Offset = indexLow & 0x3FFFFFFF;

            //for (int j = 3; j < 8; j++)
            //    buf[7 - j] = br.ReadByte();

            //long val = BitConverter.ToInt64(buf, 0);
            //info.Index = (int)(val / 0x40000000);
            //info.Offset = (int)(val % 0x40000000);

            info.Size = br.ReadInt32();

            // duplicate keys wtf...
            //IndexData[key] = info; // use last key
            LocalIndexData.TryAdd(key, info); // use first key
        }

        padPos = (dataLen + 0x0FFF) & 0xFFFFF000;
        fs.Position = padPos;

        fs.Position += numBlocks * 18;
        //for (int i = 0; i < numBlocks; i++)
        //{
        //    var bytes = br.ReadBytes(18); // unknown data
        //}

        //if (fs.Position != fs.Length)
        //    throw new Exception("idx file under read");
    }
}
