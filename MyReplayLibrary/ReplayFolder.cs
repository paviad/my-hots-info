using Heroes.ReplayParser;

namespace MyReplayLibrary;

/// <summary>
/// A toon folder (Accounts\&lt;account&gt;\&lt;region&gt;-Hero-1-&lt;toon id&gt;): one account's games
/// in one region.
/// </summary>
/// <param name="MineByDefault">Whether its games are the owner's when they haven't said.</param>
/// <param name="Chosen">What the owner said, from <see cref="AppSettings.ReplayFolders"/>; null if nothing.</param>
public sealed record ReplayFolder(
    string Account,
    int Region,
    int ToonId,
    string ToonDir,
    int NumReplays,
    bool MineByDefault,
    bool? Chosen) {
    public string Key => KeyOf(Account, Region);

    /// <summary>Whether its games are imported as the owner's.</summary>
    public bool IsMine => Chosen ?? MineByDefault;

    public string RegionName => Region switch {
        1 => "Americas",
        2 => "Europe",
        3 => "Asia",
        5 => "China",
        98 => "Public Test Realm",
        _ => $"Region {Region}",
    };

    public static string KeyOf(string account, int region) => $"{account}:{region}";

    /// <summary>
    /// The player name this toon had in its newest replay that names it; null when it has no
    /// readable replays. Parses replay headers, so call it off the UI thread.
    /// </summary>
    public string? ReadPlayerName() {
        var replayDir = Scanner.ReplayDir(ToonDir);
        if (!Directory.Exists(replayDir)) {
            return null;
        }

        var newestFirst = new DirectoryInfo(replayDir).GetFiles("*.StormReplay").OrderByDescending(f => f.LastWriteTimeUtc);
        foreach (var file in newestFirst.Take(5)) {
            try {
                var (result, replay) = DataParser.ParseReplay(file.FullName, false, ParseOptions.MinimalParsing);
                if (result == DataParser.ReplayParseResult.Success
                    && replay?.Players.FirstOrDefault(p => p.BattleNetId == ToonId) is { } me) {
                    return me.Name;
                }
            }
            catch (Exception e) when (e is IOException or InvalidDataException) {
                // The client may still be writing the newest one; try the next.
            }
        }

        return null;
    }
}
