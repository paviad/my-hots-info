using System.Collections;

namespace CascLibCore;

public class DownloadEntry {
    public int Index;
    //public byte[] Unk;

    public IEnumerable<KeyValuePair<string, DownloadTag>> Tags;
}

public class DownloadTag {
    public BitArray Bits;
    public short Type;
}

public class DownloadHandler {
    private static readonly MD5HashComparer comparer = new();
    private Dictionary<MD5Hash, DownloadEntry> DownloadData = new(comparer);
    private Dictionary<string, DownloadTag> Tags = [];

    public DownloadHandler(BinaryReader stream, BackgroundWorkerEx? worker) {
        worker?.ReportProgress(0, "Loading \"download\"...");

        stream.Skip(2); // DL

        var b1 = stream.ReadByte();
        var b2 = stream.ReadByte();
        var b3 = stream.ReadByte();

        var numFiles = stream.ReadInt32BE();

        var numTags = stream.ReadInt16BE();

        var numMaskBytes = (numFiles + 7) / 8;

        for (var i = 0; i < numFiles; i++) {
            var key = stream.Read<MD5Hash>();

            //byte[] unk = stream.ReadBytes(0xA);
            stream.Skip(0xA);

            //var entry = new DownloadEntry() { Index = i, Unk = unk };
            var entry = new DownloadEntry { Index = i };

            DownloadData.Add(key, entry);

            worker?.ReportProgress((int)((i + 1) / (float)numFiles * 100));
        }

        for (var i = 0; i < numTags; i++) {
            var tag = new DownloadTag();
            var name = stream.ReadCString();
            tag.Type = stream.ReadInt16BE();

            var bits = stream.ReadBytes(numMaskBytes);

            for (var j = 0; j < numMaskBytes; j++) {
                bits[j] = (byte)(((bits[j] * 0x0202020202) & 0x010884422010) % 1023);
            }

            tag.Bits = new BitArray(bits);

            Tags.Add(name, tag);
        }
    }

    public int Count => DownloadData.Count;

    public void Clear() {
        Tags.Clear();
        Tags = null;
        DownloadData.Clear();
        DownloadData = null;
    }

    public void Dump() {
        foreach (var entry in DownloadData) {
            if (entry.Value.Tags == null) {
                entry.Value.Tags = Tags.Where(kv => kv.Value.Bits[entry.Value.Index]);
            }

            Logger.WriteLine(
                "{0} {1}",
                entry.Key.ToHexString(),
                string.Join(",", entry.Value.Tags.Select(tag => tag.Key)));
        }
    }

    public DownloadEntry? GetEntry(MD5Hash key) {
        DownloadData.TryGetValue(key, out var entry);

        if (entry is { Tags: null }) {
            entry.Tags = Tags.Where(kv => kv.Value.Bits[entry.Index]);
        }

        return entry;
    }
}
