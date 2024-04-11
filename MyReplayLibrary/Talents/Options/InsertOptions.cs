namespace MyReplayLibrary.Talents.Options;

public class InsertOptions : GlobalOptions
{
    public bool? DryRun { get; set; }

    public string? Hero { get; set; }

    public int TalentId { get; set; }

    public int Build { get; set; }

    public bool? IncludeLater { get; set; }

    public string? TalentName { get; set; }

    public int TalentTier { get; set; }

    public string? TalentDescription { get; set; }
}
