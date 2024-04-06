using System.Net;
using System.Security.Cryptography;

namespace CascLibCore;

public class CacheMetaData(long size, byte[] md5) {
    public long Size { get; } = size;
    public byte[] MD5 { get; } = md5;

    public static CacheMetaData AddToCache(HttpWebResponse resp, string file) {
        var md5 = resp.Headers[HttpResponseHeader.ETag].Split(':')[0][1..];
        var meta = new CacheMetaData(resp.ContentLength, md5.ToByteArray());
        meta.Save(file);
        return meta;
    }

    public static CacheMetaData? Load(string file) {
        if (File.Exists(file + ".dat")) {
            var tokens = File.ReadAllText(file + ".dat").Split(' ');
            return new CacheMetaData(Convert.ToInt64(tokens[0]), tokens[1].ToByteArray());
        }

        return null;
    }

    public void Save(string file) {
        File.WriteAllText(file + ".dat", $"{Size} {MD5.ToHexString()}");
    }
}

public class CDNCache(string path) {
    private readonly SyncDownloader _downloader = new(null);

    private readonly MD5 _md5 = MD5.Create();

    public bool Enabled { get; set; } = true;
    public bool CacheData => false;
    public bool Validate => true;

    public bool HasFile(string name) {
        return File.Exists(Path.Combine(path, name));
    }

    public Stream? OpenFile(string name, string url, bool isData) {
        if (!Enabled) {
            return null;
        }

        if (isData && !CacheData) {
            return null;
        }

        var file = Path.Combine(path, name);

        Logger.WriteLine("CDNCache: Opening file {0}", file);

        var fi = new FileInfo(file);

        if (!fi.Exists) {
            _downloader.DownloadFile(url, file);
        }

        if (Validate) {
            var meta = CacheMetaData.Load(file) ?? SyncDownloader.GetMetaData(url, file);

            if (meta == null) {
                throw new Exception($"unable to validate file {file}");
            }

            bool sizeOk, md5Ok;

            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                sizeOk = fs.Length == meta.Size;
                md5Ok = _md5.ComputeHash(fs).EqualsTo(meta.MD5);
            }

            if (!sizeOk || !md5Ok) {
                _downloader.DownloadFile(url, file);
            }
        }

        return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }
}
