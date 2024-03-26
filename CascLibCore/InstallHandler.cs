using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public class InstallEntry
    {
        public MD5Hash MD5;
        public string Name;
        public int Size;

        public List<InstallTag> Tags;
    }

    public class InstallTag
    {
        public BitArray Bits;
        public string Name;
        public short Type;
    }

    public class InstallHandler
    {
        private static readonly Jenkins96 Hasher = new Jenkins96();
        private List<InstallEntry> InstallData = new List<InstallEntry>();

        public InstallHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"install\"...");

            stream.ReadBytes(2); // IN

            var b1 = stream.ReadByte();
            var b2 = stream.ReadByte();
            var numTags = stream.ReadInt16BE();
            var numFiles = stream.ReadInt32BE();

            var numMaskBytes = (numFiles + 7) / 8;

            var Tags = new List<InstallTag>();

            for (var i = 0; i < numTags; i++)
            {
                var tag = new InstallTag();
                tag.Name = stream.ReadCString();
                tag.Type = stream.ReadInt16BE();

                var bits = stream.ReadBytes(numMaskBytes);

                for (var j = 0; j < numMaskBytes; j++)
                {
                    bits[j] = (byte)(((bits[j] * 0x0202020202) & 0x010884422010) % 1023);
                }

                tag.Bits = new BitArray(bits);

                Tags.Add(tag);
            }

            for (var i = 0; i < numFiles; i++)
            {
                var entry = new InstallEntry();
                entry.Name = stream.ReadCString();
                entry.MD5 = stream.Read<MD5Hash>();
                entry.Size = stream.ReadInt32BE();

                InstallData.Add(entry);

                entry.Tags = Tags.FindAll(tag => tag.Bits[i]);

                worker?.ReportProgress((int)((i + 1) / (float)numFiles * 100));
            }
        }

        public int Count
        {
            get { return InstallData.Count; }
        }

        public void Clear()
        {
            InstallData.Clear();
            InstallData = null;
        }

        public IEnumerable<InstallEntry> GetEntries(string tag)
        {
            foreach (var entry in InstallData)
            {
                if (entry.Tags.Any(t => t.Name == tag))
                {
                    yield return entry;
                }
            }
        }

        public IEnumerable<InstallEntry> GetEntries(ulong hash)
        {
            foreach (var entry in InstallData)
            {
                if (Hasher.ComputeHash(entry.Name) == hash)
                {
                    yield return entry;
                }
            }
        }

        public IEnumerable<InstallEntry> GetEntries()
        {
            foreach (var entry in InstallData)
            {
                yield return entry;
            }
        }

        public InstallEntry GetEntry(string name)
        {
            return InstallData.Where(i => i.Name == name).FirstOrDefault();
        }

        public void Print()
        {
            for (var i = 0; i < InstallData.Count; ++i)
            {
                var data = InstallData[i];

                Logger.WriteLine("{0:D4}: {1} {2}", i, data.MD5.ToHexString(), data.Name);

                Logger.WriteLine("    {0}", string.Join(",", data.Tags.Select(t => t.Name)));
            }
        }
    }
}
