namespace MyReplayLibrary.Data.Models;

public class Takedown {
    public int ReplayId { get; set; }
    public int SeqId { get; set; }
    public TimeSpan TimeSpan { get; set; }
    public int KillerId { get; set; }
    public int VictimId { get; set; }
    public bool? KillingBlow { get; set; }

    public ReplayCharacter Killer { get; set; } = null!;
    public ReplayCharacter Victim { get; set; } = null!;
    public ReplayEntry Replay { get; set; } = null!;
}
