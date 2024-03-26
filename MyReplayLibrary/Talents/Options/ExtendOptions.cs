namespace MyReplayLibrary.Talents.Options;

public class ExtendOptions : GlobalOptions
{
    public bool? DryRun { get; set; }

    public string Hero { get; set; }

    public int? TalentId { get; set; }

    public int MinBuild { get; set; }

    public int MaxBuild { get; set; }
}
