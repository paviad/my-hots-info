namespace MyReplayLibrary.Data.Models;

public class Chat {
    public int ReplayId { get; set; }
    public int SeqId { get; set; }
    public TimeSpan TimeSpan { get; set; }
    public int PlayerId { get; set; }
    public string Text { get; set; } = null!;

    public PlayerEntry Player { get; set; } = null!;
    public ReplayCharacter ReplayCharacter { get; set; } = null!;
    public ReplayEntry Replay { get; set; } = null!;
}
