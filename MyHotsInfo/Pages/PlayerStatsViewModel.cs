namespace MyHotsInfo.Pages;

public class PlayerStatsViewModel(List<PlayerStatsRecord> players) {
    public List<PlayerStatsRecord> Players { get; } = players;
    public bool IsEmpty => Players.Count == 0;
}
