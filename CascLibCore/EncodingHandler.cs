namespace CascLibCore;

public struct EncodingEntry {
    public MD5Hash Key;
    public int Size;
}

public class EncodingHandler {
    private const int CHUNK_SIZE = 4096;
    private static readonly MD5HashComparer comparer = new();
    private Dictionary<MD5Hash, EncodingEntry> EncodingData = new(comparer);

    public EncodingHandler(BinaryReader stream, BackgroundWorkerEx? worker) {
        worker?.ReportProgress(0, "Loading \"encoding\"...");

        stream.Skip(2); // EN
        var b1 = stream.ReadByte();
        var checksumSizeA = stream.ReadByte();
        var checksumSizeB = stream.ReadByte();
        var flagsA = stream.ReadUInt16();
        var flagsB = stream.ReadUInt16();
        var numEntriesA = stream.ReadInt32BE();
        var numEntriesB = stream.ReadInt32BE();
        var b4 = stream.ReadByte();
        var stringBlockSize = stream.ReadInt32BE();

        stream.Skip(stringBlockSize);
        //string[] strings = Encoding.ASCII.GetString(stream.ReadBytes(stringBlockSize)).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);

        stream.Skip(numEntriesA * 32);
        //for (int i = 0; i < numEntriesA; ++i)
        //{
        //    byte[] firstHash = stream.ReadBytes(16);
        //    byte[] blockHash = stream.ReadBytes(16);
        //}

        var chunkStart = stream.BaseStream.Position;

        for (var i = 0; i < numEntriesA; ++i) {
            ushort keysCount;

            while ((keysCount = stream.ReadUInt16()) != 0) {
                var fileSize = stream.ReadInt32BE();
                var md5 = stream.Read<MD5Hash>();

                var entry = new EncodingEntry { Size = fileSize };

                // how do we handle multiple keys?
                for (var ki = 0; ki < keysCount; ++ki) {
                    var key = stream.Read<MD5Hash>();

                    // use first key for now
                    if (ki == 0) {
                        entry.Key = key;
                    }
                    else {
                        Logger.WriteLine(
                            "Multiple encoding keys for MD5 {0}: {1}",
                            md5.ToHexString(),
                            key.ToHexString());
                    }
                }

                //Encodings[md5] = entry;
                EncodingData.Add(md5, entry);
            }

            // each chunk is 4096 bytes, and zero padding at the end
            var remaining = CHUNK_SIZE - (stream.BaseStream.Position - chunkStart) % CHUNK_SIZE;

            if (remaining > 0) {
                stream.BaseStream.Position += remaining;
            }

            worker?.ReportProgress((int)((i + 1) / (float)numEntriesA * 100));
        }

        stream.Skip(numEntriesB * 32);
        //for (int i = 0; i < numEntriesB; ++i)
        //{
        //    byte[] firstKey = stream.ReadBytes(16);
        //    byte[] blockHash = stream.ReadBytes(16);
        //}

        var chunkStart2 = stream.BaseStream.Position;

        for (var i = 0; i < numEntriesB; ++i) {
            var key = stream.ReadBytes(16);
            var stringIndex = stream.ReadInt32BE();
            var unk1 = stream.ReadByte();
            var fileSize = stream.ReadInt32BE();

            // each chunk is 4096 bytes, and zero padding at the end
            var remaining = CHUNK_SIZE - (stream.BaseStream.Position - chunkStart2) % CHUNK_SIZE;

            if (remaining > 0) {
                stream.BaseStream.Position += remaining;
            }
        }

        // string block till the end of file
    }

    public int Count => EncodingData.Count;

    public IEnumerable<KeyValuePair<MD5Hash, EncodingEntry>> Entries {
        get {
            foreach (var entry in EncodingData) {
                yield return entry;
            }
        }
    }

    public void Clear() {
        EncodingData.Clear();
        EncodingData = null;
    }

    public bool GetEntry(MD5Hash md5, out EncodingEntry enc) {
        return EncodingData.TryGetValue(md5, out enc);
    }
}
