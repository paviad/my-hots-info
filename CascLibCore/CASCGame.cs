namespace CascLibCore;

public enum CASCGameType {
    Unknown,
    HotS,
    WoW,
    D3,
    S2,
    Agent,
    Hearthstone,
    Overwatch,
    Bna,
    Client,
}

public class CASCGame {
    public static CASCGameType DetectLocalGame(string path) {
        if (Directory.Exists(Path.Combine(path, "HeroesData"))) {
            return CASCGameType.HotS;
        }

        if (Directory.Exists(Path.Combine(path, "SC2Data"))) {
            return CASCGameType.S2;
        }

        if (Directory.Exists(Path.Combine(path, "Hearthstone_Data"))) {
            return CASCGameType.Hearthstone;
        }

        if (Directory.Exists(Path.Combine(path, "Data"))) {
            if (File.Exists(Path.Combine(path, "Diablo III.exe"))) {
                return CASCGameType.D3;
            }

            if (File.Exists(Path.Combine(path, "Wow.exe"))) {
                return CASCGameType.WoW;
            }

            if (File.Exists(Path.Combine(path, "WowT.exe"))) {
                return CASCGameType.WoW;
            }

            if (File.Exists(Path.Combine(path, "WowB.exe"))) {
                return CASCGameType.WoW;
            }

            if (File.Exists(Path.Combine(path, "Agent.exe"))) {
                return CASCGameType.Agent;
            }

            if (File.Exists(Path.Combine(path, "Battle.net.exe"))) {
                return CASCGameType.Bna;
            }

            if (File.Exists(Path.Combine(path, "Overwatch Launcher.exe"))) {
                return CASCGameType.Overwatch;
            }
        }

        return CASCGameType.Unknown;
    }

    public static CASCGameType DetectOnlineGame(string uid) {
        if (uid.StartsWith("hero")) {
            return CASCGameType.HotS;
        }

        if (uid.StartsWith("hs")) {
            return CASCGameType.Hearthstone;
        }

        if (uid.StartsWith("s2")) {
            return CASCGameType.S2;
        }

        if (uid.StartsWith("wow")) {
            return CASCGameType.WoW;
        }

        if (uid.StartsWith("d3")) {
            return CASCGameType.D3;
        }

        if (uid.StartsWith("agent")) {
            return CASCGameType.Agent;
        }

        if (uid.StartsWith("pro")) {
            return CASCGameType.Overwatch;
        }

        if (uid.StartsWith("bna")) {
            return CASCGameType.Bna;
        }

        if (uid.StartsWith("clnt")) {
            return CASCGameType.Client;
        }

        return CASCGameType.Unknown;
    }

    public static string GetDataFolder(CASCGameType gameType) {
        return gameType switch {
            CASCGameType.HotS => "HeroesData",
            CASCGameType.S2 => "SC2Data",
            CASCGameType.Hearthstone => "Hearthstone_Data",
            CASCGameType.WoW or CASCGameType.D3 => "Data",
            CASCGameType.Overwatch => "data/casc",
            _ => throw new InvalidOperationException($"Unknown game type {gameType}"),
        };
    }

    public static bool SupportsLocaleSelection(CASCGameType gameType) {
        return gameType is CASCGameType.D3
            or CASCGameType.WoW
            or CASCGameType.HotS
            or CASCGameType.S2
            or CASCGameType.Overwatch;
    }
}
