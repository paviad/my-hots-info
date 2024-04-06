namespace CascLibCore;

[Flags]
public enum LoadFlags {
    All = -1,
    None = 0,
    Download = 1,
    Install = 2,
}

internal class VerBarConfig {
    private readonly List<Dictionary<string, string>> Data = [];

    public int Count => Data.Count;

    public Dictionary<string, string> this[int index] => Data[index];

    public static VerBarConfig ReadVerBarConfig(Stream stream) {
        using var sr = new StreamReader(stream);
        return ReadVerBarConfig(sr);
    }

    public static VerBarConfig ReadVerBarConfig(TextReader reader) {
        var result = new VerBarConfig();

        var lineNum = 0;

        string[]? fields = null;

        while (reader.ReadLine() is { } line) {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) // skip empty lines and comments
            {
                continue;
            }

            var tokens = line.Split('|');

            if (lineNum == 0) // keys
            {
                fields = new string[tokens.Length];

                for (var i = 0; i < tokens.Length; ++i) {
                    fields[i] = tokens[i].Split('!')[0].Replace(" ", "");
                }
            }
            else // values
            {
                result.Data.Add([]);

                for (var i = 0; i < tokens.Length; ++i) {
                    result.Data[lineNum - 1].Add(fields![i], tokens[i]);
                }
            }

            lineNum++;
        }

        return result;
    }
}

public class KeyValueConfig {
    private readonly Dictionary<string, List<string>> Data = [];

    public List<string> this[string key] => Data[key];

    public static KeyValueConfig ReadKeyValueConfig(Stream stream) {
        var sr = new StreamReader(stream);
        return ReadKeyValueConfig(sr);
    }

    public static KeyValueConfig ReadKeyValueConfig(TextReader reader) {
        var result = new KeyValueConfig();

        while (reader.ReadLine() is { } line) {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) // skip empty lines and comments
            {
                continue;
            }

            var tokens = line.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length != 2) {
                throw new Exception("KeyValueConfig: tokens.Length != 2");
            }

            var values = tokens[1].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var valuesList = values.ToList();
            result.Data.Add(tokens[0].Trim(), valuesList);
        }

        return result;
    }
}

public class CASCConfig {
    private VerBarConfig _BuildInfo;

    private KeyValueConfig _CDNConfig;
    private VerBarConfig _CDNData;
    private VerBarConfig _VersionsData;
    public static bool ValidateData => true;
    public static bool ThrowOnFileNotFound => true;
    public static LoadFlags LoadFlags { get; set; } = LoadFlags.None;

    public string Region { get; private init; }
    public CASCGameType GameType { get; private set; }

    public string BasePath { get; private set; }

    public bool OnlineMode { get; private init; }

    public int ActiveBuild { get; set; }

    public string BuildName => Builds[ActiveBuild]["build-name"][0];

    public string Product { get; private set; }

    public MD5Hash RootMD5 => Builds[ActiveBuild]["root"][0].ToByteArray().ToMD5();

    public MD5Hash DownloadMD5 => Builds[ActiveBuild]["download"][0].ToByteArray().ToMD5();

    public MD5Hash InstallMD5 => Builds[ActiveBuild]["install"][0].ToByteArray().ToMD5();

    public MD5Hash EncodingMD5 => Builds[ActiveBuild]["encoding"][0].ToByteArray().ToMD5();

    public MD5Hash EncodingKey => Builds[ActiveBuild]["encoding"][1].ToByteArray().ToMD5();

    public string BuildUID => Builds[ActiveBuild]["build-uid"][0];

    public string CDNHost {
        get {
            if (OnlineMode) {
                return _CDNData[0]["Hosts"].Split(' ')[0]; // use first
            }

            return _BuildInfo[0]["CDNHosts"].Split(' ')[0];
        }
    }

    public string CDNPath {
        get {
            if (OnlineMode) {
                return _CDNData[0]["Path"]; // use first
            }

            return _BuildInfo[0]["CDNPath"];
        }
    }

    public string CDNUrl {
        get {
            if (OnlineMode) {
                var index = 0;

                for (var i = 0; i < _CDNData.Count; ++i) {
                    if (_CDNData[i]["Name"] == Region) {
                        index = i;
                        break;
                    }
                }

                return string.Format(
                    "http://{0}/{1}",
                    _CDNData[index]["Hosts"].Split(' ')[0],
                    _CDNData[index]["Path"]);
            }

            return string.Format(
                "http://{0}{1}",
                _BuildInfo[0]["CDNHosts"].Split(' ')[0],
                _BuildInfo[0]["CDNPath"]);
        }
    }

    public List<string> Archives => _CDNConfig["archives"];

    public string ArchiveGroup => _CDNConfig["archive-group"][0];

    public List<string> PatchArchives => _CDNConfig["patch-archives"];

    public string PatchArchiveGroup => _CDNConfig["patch-archive-group"][0];

    public List<KeyValueConfig> Builds { get; private set; }

    public static CASCConfig LoadLocalStorageConfig(string basePath) {
        var config = new CASCConfig {
            OnlineMode = false,
            BasePath = basePath,
            GameType = CASCGame.DetectLocalGame(basePath),
        };

        if (config.GameType is CASCGameType.Agent or CASCGameType.Hearthstone) {
            throw new Exception("Local mode not supported for this game!");
        }

        var buildInfoPath = Path.Combine(basePath, ".build.info");

        using (Stream buildInfoStream = new FileStream(buildInfoPath, FileMode.Open)) {
            config._BuildInfo = VerBarConfig.ReadVerBarConfig(buildInfoStream);
        }

        Dictionary<string, string>? bi = null;

        for (var i = 0; i < config._BuildInfo.Count; ++i) {
            if (config._BuildInfo[i]["Active"] == "1") {
                bi = config._BuildInfo[i];
                break;
            }
        }

        if (bi == null) {
            throw new Exception("Can't find active BuildInfoEntry");
        }

        var dataFolder = CASCGame.GetDataFolder(config.GameType);

        config.ActiveBuild = 0;

        config.Builds = [];

        var buildKey = bi["BuildKey"];
        var buildCfgPath = Path.Combine(
            basePath,
            dataFolder,
            "config",
            buildKey[..2],
            buildKey.Substring(2, 2),
            buildKey);
        using (Stream stream = new FileStream(buildCfgPath, FileMode.Open)) {
            config.Builds.Add(KeyValueConfig.ReadKeyValueConfig(stream));
        }

        var cdnKey = bi["CDNKey"];
        var cdnCfgPath = Path.Combine(
            basePath,
            dataFolder,
            "config",
            cdnKey[..2],
            cdnKey.Substring(2, 2),
            cdnKey);
        using (Stream stream = new FileStream(cdnCfgPath, FileMode.Open)) {
            config._CDNConfig = KeyValueConfig.ReadKeyValueConfig(stream);
        }

        return config;
    }

    public static CASCConfig LoadOnlineStorageConfig(string product, string region, bool useCurrentBuild = false) {
        var config = new CASCConfig { OnlineMode = true,
            Region = region,
            Product = product,
        };

        using (var cdnsStream =
               CDNIndexHandler.OpenFileDirect($"http://us.patch.battle.net/{product}/cdns")) {
            config._CDNData = VerBarConfig.ReadVerBarConfig(cdnsStream);
        }

        using (var versionsStream =
               CDNIndexHandler.OpenFileDirect($"http://us.patch.battle.net/{product}/versions")) {
            config._VersionsData = VerBarConfig.ReadVerBarConfig(versionsStream);
        }

        var versionIndex = 0;

        for (var i = 0; i < config._VersionsData.Count; ++i) {
            if (config._VersionsData[i]["Region"] == region) {
                versionIndex = i;
                break;
            }
        }

        config.GameType = CASCGame.DetectOnlineGame(product);

        var cdnKey = config._VersionsData[versionIndex]["CDNConfig"];
        using (var stream = CDNIndexHandler.OpenConfigFileDirect(config, cdnKey)) {
            config._CDNConfig = KeyValueConfig.ReadKeyValueConfig(stream);
        }

        config.ActiveBuild = 0;

        config.Builds = [];

        for (var i = 0; i < config._CDNConfig["builds"].Count; i++) {
            try {
                using var stream = CDNIndexHandler.OpenConfigFileDirect(config, config._CDNConfig["builds"][i]);
                var cfg = KeyValueConfig.ReadKeyValueConfig(stream);
                config.Builds.Add(cfg);
            }
            catch { }
        }

        if (useCurrentBuild) {
            var buildKey = config._VersionsData[versionIndex]["BuildConfig"];

            var buildIndex = config._CDNConfig["builds"].IndexOf(buildKey);

            if (buildIndex != -1) {
                config.ActiveBuild = buildIndex;
            }
        }

        return config;
    }
}
