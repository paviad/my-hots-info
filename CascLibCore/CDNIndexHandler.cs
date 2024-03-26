using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace CASCExplorer
{
    public class IndexEntry
    {
        public int Index;
        public int Offset;
        public int Size;
    }

    public class CDNIndexHandler
    {
        private static readonly MD5HashComparer comparer = new MD5HashComparer();
        public static readonly CDNCache Cache = new CDNCache("cache");
        private Dictionary<MD5Hash, IndexEntry> CDNIndexData = new Dictionary<MD5Hash, IndexEntry>(comparer);

        private CASCConfig config;
        private SyncDownloader downloader;
        private BackgroundWorkerEx worker;

        private CDNIndexHandler(CASCConfig cascConfig, BackgroundWorkerEx worker)
        {
            config = cascConfig;
            this.worker = worker;
            downloader = new SyncDownloader(worker);
        }

        public int Count => CDNIndexData.Count;

        public static CDNIndexHandler Initialize(CASCConfig config, BackgroundWorkerEx worker)
        {
            var handler = new CDNIndexHandler(config, worker);

            worker?.ReportProgress(0, "Loading \"CDN indexes\"...");

            for (var i = 0; i < config.Archives.Count; i++)
            {
                var archive = config.Archives[i];

                if (config.OnlineMode)
                {
                    handler.DownloadIndexFile(archive, i);
                }
                else
                {
                    handler.OpenIndexFile(archive, i);
                }

                worker?.ReportProgress((int)((i + 1) / (float)config.Archives.Count * 100));
            }

            return handler;
        }

        public static Stream OpenConfigFileDirect(CASCConfig cfg, string key)
        {
            var file = cfg.CDNPath + "/config/" + key.Substring(0, 2) + "/" + key.Substring(2, 2) + "/" + key;
            var url = "http://" + cfg.CDNHost + "/" + file;

            var stream = Cache.OpenFile(file, url, false);

            if (stream != null)
            {
                return stream;
            }

            return OpenFileDirect(url);
        }

        public static Stream OpenFileDirect(string url)
        {
            var req = WebRequest.CreateHttp(url);
            using (var resp = (HttpWebResponse)req.GetResponse())
            {
                var ms = new MemoryStream();
                resp.GetResponseStream().CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
        }

        public void Clear()
        {
            CDNIndexData.Clear();
            CDNIndexData = null;

            config = null;
            worker = null;
            downloader = null;
        }

        public IndexEntry GetIndexInfo(MD5Hash key)
        {
            IndexEntry result;

            if (!CDNIndexData.TryGetValue(key, out result))
            {
                Logger.WriteLine("CDNIndexHandler: missing index: {0}", key.ToHexString());
            }

            return result;
        }

        public Stream OpenDataFile(IndexEntry entry)
        {
            var archive = config.Archives[entry.Index];

            var file = config.CDNPath + "/data/" + archive.Substring(0, 2) + "/" + archive.Substring(2, 2) + "/" +
                       archive;
            var url = "http://" + config.CDNHost + "/" + file;

            var stream = Cache.OpenFile(file, url, true);

            if (stream != null)
            {
                stream.Position = entry.Offset;
                var ms = new MemoryStream(entry.Size);
                stream.CopyBytes(ms, entry.Size);
                ms.Position = 0;
                return ms;
            }

            var req = WebRequest.CreateHttp(url);
            req.AddRange(entry.Offset, entry.Offset + entry.Size - 1);
            using (var resp = (HttpWebResponse)req.GetResponse())
            {
                var ms = new MemoryStream(entry.Size);
                resp.GetResponseStream().CopyBytes(ms, entry.Size);
                ms.Position = 0;
                return ms;
            }
        }

        public Stream OpenDataFileDirect(MD5Hash key)
        {
            var keyStr = key.ToHexString().ToLower();

            worker?.ReportProgress(0, string.Format("Downloading \"{0}\" file...", keyStr));

            var file = config.CDNPath + "/data/" + keyStr.Substring(0, 2) + "/" + keyStr.Substring(2, 2) + "/" + keyStr;
            var url = "http://" + config.CDNHost + "/" + file;

            var stream = Cache.OpenFile(file, url, false);

            if (stream != null)
            {
                return stream;
            }

            return downloader.OpenFile(url);
        }

        private void DownloadIndexFile(string archive, int i)
        {
            try
            {
                var file = config.CDNPath + "/data/" + archive.Substring(0, 2) + "/" + archive.Substring(2, 2) + "/" +
                           archive + ".index";
                var url = "http://" + config.CDNHost + "/" + file;

                var stream = Cache.OpenFile(file, url, false);

                if (stream != null)
                {
                    ParseIndex(stream, i);
                }
                else
                {
                    using (var fs = downloader.OpenFile(url))
                    {
                        ParseIndex(fs, i);
                    }
                }
            }
            catch
            {
                throw new Exception("DownloadFile failed!");
            }
        }

        private void OpenIndexFile(string archive, int i)
        {
            try
            {
                var dataFolder = CASCGame.GetDataFolder(config.GameType);

                var path = Path.Combine(config.BasePath, dataFolder, "indices", archive + ".index");

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    ParseIndex(fs, i);
                }
            }
            catch
            {
                throw new Exception("OpenFile failed!");
            }
        }

        private void ParseIndex(Stream stream, int i)
        {
            using (var br = new BinaryReader(stream))
            {
                stream.Seek(-12, SeekOrigin.End);
                var count = br.ReadInt32();
                stream.Seek(0, SeekOrigin.Begin);

                if (count * (16 + 4 + 4) > stream.Length)
                {
                    throw new Exception("ParseIndex failed");
                }

                for (var j = 0; j < count; ++j)
                {
                    var key = br.Read<MD5Hash>();

                    if (key.IsZeroed()) // wtf?
                    {
                        key = br.Read<MD5Hash>();
                    }

                    if (key.IsZeroed()) // wtf?
                    {
                        throw new Exception("key.IsZeroed()");
                    }

                    var entry = new IndexEntry();
                    entry.Index = i;
                    entry.Size = br.ReadInt32BE();
                    entry.Offset = br.ReadInt32BE();

                    CDNIndexData.Add(key, entry);
                }
            }
        }
    }
}
