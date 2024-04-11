namespace MyReplayLibrary.Talents.Options;

public class GetOptions : GlobalOptions
{
    public string? Hero { get; set; }

    public string? Build { get; set; }

    public int? TalentId { get; set; }

    public int? MinBuild { get; set; }

    public int? MaxBuild { get; set; }
}
