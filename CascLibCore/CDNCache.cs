using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace CASCExplorer
{
    public class CacheMetaData
    {
        public CacheMetaData(long size, byte[] md5)
        {
            Size = size;
            MD5 = md5;
        }

        public long Size { get; }
        public byte[] MD5 { get; }

        public static CacheMetaData AddToCache(HttpWebResponse resp, string file)
        {
            var md5 = resp.Headers[HttpResponseHeader.ETag].Split(':')[0].Substring(1);
            var meta = new CacheMetaData(resp.ContentLength, md5.ToByteArray());
            meta.Save(file);
            return meta;
        }

        public static CacheMetaData Load(string file)
        {
            if (File.Exists(file + ".dat"))
            {
                var tokens = File.ReadAllText(file + ".dat").Split(' ');
                return new CacheMetaData(Convert.ToInt64(tokens[0]), tokens[1].ToByteArray());
            }

            return null;
        }

        public void Save(string file)
        {
            File.WriteAllText(file + ".dat", $"{Size} {MD5.ToHexString()}");
        }
    }

    public class CDNCache
    {
        private readonly string _cachePath;
        private readonly SyncDownloader _downloader = new SyncDownloader(null);

        private readonly MD5 _md5 = MD5.Create();

        public CDNCache(string path)
        {
            _cachePath = path;
        }

        public bool Enabled { get; set; } = true;
        public bool CacheData { get; set; } = false;
        public bool Validate { get; set; } = true;

        public bool HasFile(string name)
        {
            return File.Exists(Path.Combine(_cachePath, name));
        }

        public Stream OpenFile(string name, string url, bool isData)
        {
            if (!Enabled)
            {
                return null;
            }

            if (isData && !CacheData)
            {
                return null;
            }

            var file = Path.Combine(_cachePath, name);

            Logger.WriteLine("CDNCache: Opening file {0}", file);

            var fi = new FileInfo(file);

            if (!fi.Exists)
            {
                _downloader.DownloadFile(url, file);
            }

            if (Validate)
            {
                var meta = CacheMetaData.Load(file) ?? _downloader.GetMetaData(url, file);

                if (meta == null)
                {
                    throw new Exception(string.Format("unable to validate file {0}", file));
                }

                bool sizeOk, md5Ok;

                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    sizeOk = fs.Length == meta.Size;
                    md5Ok = _md5.ComputeHash(fs).EqualsTo(meta.MD5);
                }

                if (!sizeOk || !md5Ok)
                {
                    _downloader.DownloadFile(url, file);
                }
            }

            return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
    }
}
