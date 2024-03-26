using Heroes.ReplayParser;

namespace MyReplayLibrary.Data.Models;

public class ReplayCharacterMatchAward {
    public int ReplayId { get; init; }
    public int PlayerId { get; init; }
    public MatchAwardType MatchAwardType { get; init; }

    public ReplayEntry Replay { get; init; } = null!;
    public PlayerEntry Player { get; init; } = null!;
    public ReplayCharacter ReplayCharacter { get; init; } = null!;
}
