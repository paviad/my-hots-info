namespace MyReplayLibrary.Talents;

public class TalentUpdatePackage
{
    public int Build { get; set; }
    public string Version { get; set; } = null!;
    public DateTime Date { get; set; }
    public List<TalentInfo> TalentInfoList { get; set; } = null!;
}
