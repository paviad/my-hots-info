namespace MyReplayLibrary.Data.Models;

public class ReplayCharacterDraftOrder {
    public int PlayerId { get; init; }
    public int ReplayId { get; init; }
    public int DraftOrder { get; init; }

    public ReplayEntry Replay { get; init; } = null!;
    public PlayerEntry Player { get; init; } = null!;
    public ReplayCharacter ReplayCharacter { get; init; } = null!;
}
