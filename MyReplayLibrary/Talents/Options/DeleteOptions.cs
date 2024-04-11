namespace MyReplayLibrary.Talents.Options;

public class DeleteOptions : GlobalOptions
{
    public bool? DryRun { get; set; }

    public string? Hero { get; set; }

    public int TalentId { get; set; }

    public int Build { get; set; }
}
