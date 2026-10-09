using System.Text.RegularExpressions;
using Heroes.ReplayParser;
using Heroes.ReplayParser.MPQFiles;

namespace MyReplayQuery.Events;

/// <summary>Finds and loads replay files from the HotS documents folder.</summary>
public static partial class ReplayFiles {
    [GeneratedRegex(@"[/\\]\d+-Hero-\d+-(?<pid>\d+)[/\\]")]
    private static partial Regex PlayerIdRegex();

    public static string AccountsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Heroes of the Storm", "Accounts");

    /// <summary>All multiplayer replays of every account on this machine, newest first.</summary>
    public static IReadOnlyList<FileInfo> AllReplays() {
        if (!Directory.Exists(AccountsPath)) {
            return [];
        }

        return Directory.GetDirectories(AccountsPath)
            .SelectMany(account => Directory.GetDirectories(account, "*-Hero-*"))
            .Select(toon => Path.Combine(toon, "Replays", "Multiplayer"))
            .Where(Directory.Exists)
            .SelectMany(dir => new DirectoryInfo(dir).GetFiles("*.StormReplay"))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();
    }

    /// <summary>
    /// Resolves a replay spec: a file path, <c>latest</c>, or <c>N</c> for the N-th newest (1 = latest).
    /// </summary>
    public static string Resolve(string spec) {
        if (File.Exists(spec)) {
            return Path.GetFullPath(spec);
        }

        var n = spec.Equals("latest", StringComparison.OrdinalIgnoreCase) ? 1
            : int.TryParse(spec, out var i) && i >= 1 ? i
            : throw new ArgumentException($"Replay '{spec}' is not a file, 'latest', or a number.");
        var all = AllReplays();
        return n <= all.Count
            ? all[n - 1].FullName
            : throw new ArgumentException($"Only {all.Count} replays found under {AccountsPath}.");
    }

    /// <summary>The toon id in a replay path (…\2-Hero-1-612637\…), which identifies "me".</summary>
    public static int? MyToonId(string path) {
        var match = PlayerIdRegex().Match(path);
        return match.Success ? int.Parse(match.Groups["pid"].Value) : null;
    }

    /// <summary>Reads a replay even while the game client holds it open.</summary>
    public static byte[] ReadShared(string path) {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytes = new byte[fs.Length];
        fs.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>The replay's build, read from the MPQ header only.</summary>
    public static int ReadBuild(string path) {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var head = new byte[Math.Min(fs.Length, 4096)];
        fs.ReadExactly(head);
        var replay = new Replay();
        MpqHeader.ParseHeader(replay, head);
        return replay.ReplayBuild;
    }

    public static Replay Parse(string path, ParseOptions options) {
        var (result, replay) = DataParser.ParseReplay(ReadShared(path), options);
        return result == DataParser.ReplayParseResult.Success && replay is not null
            ? replay
            : throw new InvalidDataException($"Could not parse {Path.GetFileName(path)}: {result}");
    }
}
