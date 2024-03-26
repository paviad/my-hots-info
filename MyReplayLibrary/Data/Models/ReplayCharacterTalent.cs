namespace MyReplayLibrary.Data.Models;

public class ReplayCharacterTalent {
    public int ReplayId { get; init; }
    public int PlayerId { get; init; }
    public int TalentId { get; init; }

    public PlayerEntry Player { get; init; } = null!;
    public ReplayEntry Replay { get; init; } = null!;
    public ReplayCharacter ReplayCharacter { get; init; } = null!;
}
