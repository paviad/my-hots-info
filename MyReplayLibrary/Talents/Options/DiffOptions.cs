namespace MyReplayLibrary.Talents.Options;

public class DiffOptions : GlobalOptions
{
    public string? Hero { get; set; }

    public int? TalentId { get; set; }

    public int? MinBuild { get; set; }

    public int? MaxBuild { get; set; }
}
