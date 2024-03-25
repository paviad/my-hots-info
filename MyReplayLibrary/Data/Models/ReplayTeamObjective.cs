using Heroes.ReplayParser;

namespace MyReplayLibrary.Data.Models;

public class ReplayTeamObjective {
    public int ReplayId { get; init; }
    public int? PlayerId { get; init; }

    public ReplayEntry Replay { get; init; } = null!;
    public PlayerEntry? Player { get; init; } = null!;
    public bool IsWinner { get; init; }
    public TeamObjectiveType TeamObjectiveType { get; init; }
    public TimeSpan TimeSpan { get; init; }
    public int Value { get; init; }
}
