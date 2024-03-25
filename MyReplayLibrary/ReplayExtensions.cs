using Heroes.ReplayParser;
using System.Security.Cryptography;
using System.Text;

namespace MyReplayLibrary;

public static class ReplayExtensions {
    public static Guid HashReplay(this Replay replay) {
        using var md5 = MD5.Create();
        var replayPlayersIds = replay.Players
            .OrderBy(i => i.BattleNetId)
            .Select(i => i.BattleNetId.ToString());
        var replayUniqueString = string.Join(string.Empty, replayPlayersIds) + replay.RandomValue;
        var replayUniqueBytes = Encoding.ASCII.GetBytes(replayUniqueString);
        var md5Hash = md5.ComputeHash(replayUniqueBytes);
        return new Guid(md5Hash);
    }
}
