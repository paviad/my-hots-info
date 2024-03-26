using System;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public sealed class CASCHandler : CASCHandlerBase
    {
        private CASCHandler(CASCConfig config, BackgroundWorkerEx worker) : base(config, worker)
        {
            Logger.WriteLine("CASCHandler: loading encoding data...");

            using (var _ = new PerfCounter("new EncodingHandler()"))
            {
                using (var fs = OpenEncodingFile(this))
                {
                    Encoding = new EncodingHandler(fs, worker);
                }
            }

            Logger.WriteLine("CASCHandler: loaded {0} encoding data", Encoding.Count);

            if ((CASCConfig.LoadFlags & LoadFlags.Download) != 0)
            {
                Logger.WriteLine("CASCHandler: loading download data...");

                using (var _ = new PerfCounter("new DownloadHandler()"))
                {
                    using (var fs = OpenDownloadFile(Encoding, this))
                    {
                        Download = new DownloadHandler(fs, worker);
                    }
                }

                Logger.WriteLine("CASCHandler: loaded {0} download data", Encoding.Count);
            }

            Logger.WriteLine("CASCHandler: loading root data...");

            using (var _ = new PerfCounter("new RootHandler()"))
            {
                using (var fs = OpenRootFile(Encoding, this))
                {
                    if (config.GameType == CASCGameType.S2 || config.GameType == CASCGameType.HotS)
                    {
                        Root = new MNDXRootHandler(fs, worker);
                    }
                    else if (config.GameType == CASCGameType.D3)
                    {
                        Root = new D3RootHandler(fs, worker, this);
                    }
                    else if (config.GameType == CASCGameType.WoW)
                    {
                        Root = new WowRootHandler(fs, worker);
                    }
                    else if (config.GameType == CASCGameType.Agent || config.GameType == CASCGameType.Bna ||
                             config.GameType == CASCGameType.Client)
                    {
                        Root = new AgentRootHandler(fs, worker);
                    }
                    else if (config.GameType == CASCGameType.Hearthstone)
                    {
                        Root = new HSRootHandler(fs, worker);
                    }
                    else if (config.GameType == CASCGameType.Overwatch)
                    {
                        Root = new OwRootHandler(fs, worker, this);
                    }
                    else
                    {
                        throw new Exception("Unsupported game " + config.BuildUID);
                    }
                }
            }

            Logger.WriteLine("CASCHandler: loaded {0} root data", Root.Count);

            if ((CASCConfig.LoadFlags & LoadFlags.Install) != 0)
            {
                Logger.WriteLine("CASCHandler: loading install data...");

                using (var _ = new PerfCounter("new InstallHandler()"))
                {
                    using (var fs = OpenInstallFile(Encoding, this))
                    {
                        Install = new InstallHandler(fs, worker);
                    }

                    Install.Print();
                }

                Logger.WriteLine("CASCHandler: loaded {0} install data", Install.Count);
            }
        }

        public EncodingHandler Encoding { get; private set; }

        public DownloadHandler Download { get; private set; }

        public RootHandlerBase Root { get; private set; }

        public InstallHandler Install { get; private set; }

        public static CASCHandler OpenLocalStorage(string basePath, BackgroundWorkerEx worker = null)
        {
            var config = CASCConfig.LoadLocalStorageConfig(basePath);

            return Open(worker, config);
        }

        public static CASCHandler OpenOnlineStorage(
            string product,
            string region = "us",
            BackgroundWorkerEx worker = null)
        {
            var config = CASCConfig.LoadOnlineStorageConfig(product, region);

            return Open(worker, config);
        }

        public static CASCHandler OpenStorage(CASCConfig config, BackgroundWorkerEx worker = null) =>
            Open(worker, config);

        public void Clear()
        {
            CDNIndex?.Clear();
            CDNIndex = null;

            foreach (var stream in DataStreams)
            {
                stream.Value.Dispose();
            }

            DataStreams.Clear();

            Encoding?.Clear();
            Encoding = null;

            Install?.Clear();
            Install = null;

            LocalIndex?.Clear();
            LocalIndex = null;

            Root?.Clear();
            Root = null;

            Download?.Clear();
            Download = null;
        }

        public override bool FileExists(int fileDataId)
        {
            var rh = Root as WowRootHandler;

            if (rh == null)
            {
                return false;
            }

            return FileExists(rh.GetHashByFileDataId(fileDataId));
        }

        public override bool FileExists(string file) => FileExists(Hasher.ComputeHash(file));

        public override bool FileExists(ulong hash) => Root.GetAllEntries(hash).Any();

        public bool GetEncodingEntry(ulong hash, out EncodingEntry enc)
        {
            var rootInfos = Root.GetEntries(hash);
            if (rootInfos.Any())
            {
                return Encoding.GetEntry(rootInfos.First().MD5, out enc);
            }

            if ((CASCConfig.LoadFlags & LoadFlags.Install) != 0)
            {
                var installInfos = Install.GetEntries().Where(e => Hasher.ComputeHash(e.Name) == hash);
                if (installInfos.Any())
                {
                    return Encoding.GetEntry(installInfos.First().MD5, out enc);
                }
            }

            enc = default(EncodingEntry);
            return false;
        }

        public override Stream OpenFile(int fileDataId)
        {
            var rh = Root as WowRootHandler;

            if (rh != null)
            {
                return OpenFile(rh.GetHashByFileDataId(fileDataId));
            }

            if (CASCConfig.ThrowOnFileNotFound)
            {
                throw new FileNotFoundException("FileData: " + fileDataId);
            }

            return null;
        }

        public override Stream OpenFile(string name) => OpenFile(Hasher.ComputeHash(name));

        public override Stream OpenFile(ulong hash)
        {
            EncodingEntry encInfo;

            if (GetEncodingEntry(hash, out encInfo))
            {
                return OpenFile(encInfo.Key);
            }

            if (Root is OwRootHandler)
            {
                OWRootEntry entry;

                if ((Root as OwRootHandler).GetEntry(hash, out entry))
                {
                    if ((entry.baseEntry.ContentFlags & ContentFlags.Bundle) != ContentFlags.None)
                    {
                        if (Encoding.GetEntry(entry.pkgIndex.bundleContentKey, out encInfo))
                        {
                            using (var bundle = OpenFile(encInfo.Key))
                            {
                                var ms = new MemoryStream();

                                bundle.Position = entry.pkgIndexRec.Offset;
                                bundle.CopyBytes(ms, entry.pkgIndexRec.Size);

                                return ms;
                            }
                        }
                    }
                }
            }

            if (CASCConfig.ThrowOnFileNotFound)
            {
                throw new FileNotFoundException(string.Format("{0:X16}", hash));
            }

            return null;
        }

        public override void SaveFileTo(ulong hash, string extractPath, string fullName)
        {
            EncodingEntry encInfo;

            if (GetEncodingEntry(hash, out encInfo))
            {
                SaveFileTo(encInfo.Key, extractPath, fullName);
                return;
            }

            if (Root is OwRootHandler)
            {
                OWRootEntry entry;

                if ((Root as OwRootHandler).GetEntry(hash, out entry))
                {
                    if ((entry.baseEntry.ContentFlags & ContentFlags.Bundle) != ContentFlags.None)
                    {
                        if (Encoding.GetEntry(entry.pkgIndex.bundleContentKey, out encInfo))
                        {
                            using (var bundle = OpenFile(encInfo.Key))
                            {
                                var fullPath = Path.Combine(extractPath, fullName);
                                var dir = Path.GetDirectoryName(fullPath);

                                if (!Directory.Exists(dir))
                                {
                                    Directory.CreateDirectory(dir);
                                }

                                using (var fileStream = File.Open(fullPath, FileMode.Create))
                                {
                                    bundle.Position = entry.pkgIndexRec.Offset;
                                    bundle.CopyBytes(fileStream, entry.pkgIndexRec.Size);
                                }
                            }

                            return;
                        }
                    }
                }
            }

            if (CASCConfig.ThrowOnFileNotFound)
            {
                throw new FileNotFoundException(fullName);
            }
        }

        protected override void ExtractFileOnline(MD5Hash key, string path, string name)
        {
            var idxInfo = CDNIndex.GetIndexInfo(key);
            ExtractFileOnlineInternal(idxInfo, key, path, name);
        }

        protected override Stream GetLocalDataStream(MD5Hash key)
        {
            var idxInfo = LocalIndex.GetIndexInfo(key);
            if (idxInfo == null)
            {
                Logger.WriteLine("Local index missing: {0}", key.ToHexString());
            }

            return GetLocalDataStreamInternal(idxInfo, key);
        }

        protected override Stream OpenFileOnline(MD5Hash key)
        {
            var idxInfo = CDNIndex.GetIndexInfo(key);
            return OpenFileLocalInternal(idxInfo, key);
        }

        private static CASCHandler Open(BackgroundWorkerEx worker, CASCConfig config)
        {
            using (var _ = new PerfCounter("new CASCHandler()"))
            {
                return new CASCHandler(config, worker);
            }
        }
    }
}
