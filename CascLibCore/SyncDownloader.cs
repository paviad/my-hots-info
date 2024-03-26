using System.IO;
using System.Net;

namespace CASCExplorer
{
    public class SyncDownloader
    {
        private readonly BackgroundWorkerEx progressReporter;

        public SyncDownloader(BackgroundWorkerEx progressReporter)
        {
            this.progressReporter = progressReporter;
        }

        public void DownloadFile(string url, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var request = WebRequest.CreateHttp(url);

            using (var resp = (HttpWebResponse)request.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (Stream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                CacheMetaData.AddToCache(resp, path);
                CopyToStream(stream, fs, resp.ContentLength);
            }
        }

        public CacheMetaData GetMetaData(string url, string file)
        {
            try
            {
                var request = WebRequest.CreateHttp(url);
                request.Method = "HEAD";

                using (var resp = (HttpWebResponse)request.GetResponse())
                {
                    return CacheMetaData.AddToCache(resp, file);
                }
            }
            catch
            {
                return null;
            }
        }

        public MemoryStream OpenFile(string url)
        {
            var request = WebRequest.CreateHttp(url);

            using (var resp = (HttpWebResponse)request.GetResponse())
            using (var stream = resp.GetResponseStream())
            {
                var ms = new MemoryStream();

                CopyToStream(stream, ms, resp.ContentLength);

                ms.Position = 0;
                return ms;
            }
        }

        private void CopyToStream(Stream src, Stream dst, long len)
        {
            long done = 0;

            var buf = new byte[0x1000];

            int count;
            do
            {
                if (progressReporter != null && progressReporter.CancellationPending)
                {
                    return;
                }

                count = src.Read(buf, 0, buf.Length);
                dst.Write(buf, 0, count);

                done += count;

                progressReporter?.ReportProgress((int)(done / (float)len * 100));
            } while (count > 0);
        }
    }
}
