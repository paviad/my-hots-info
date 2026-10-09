using MyReplayLibrary;

namespace MyHotsInfo.Pages;

public record PlayerStatsRecord(string BattleTag, List<PlayerStatsRow> Rows);

public record PlayerStatsRow(string? Hero, string Label, PlayerQuery.ResultRecord Result) {
    public double WinRate => Result.NumGames == 0 ? 0 : 1.0 * Result.Wins / Result.NumGames;
}
