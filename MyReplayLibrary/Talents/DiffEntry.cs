using MyReplayLibrary.Data.Models;

namespace MyReplayLibrary.Talents;

public class DiffEntry
{
    public HeroTalentInformation Talent { get; set; } = null!;
    public HeroTalentInformation? NextTalent { get; set; }
    public int? NextRange { get; set; }
    public bool IsNew { get; set; }
    public bool IsRemovedInNext { get; set; }
    public bool IsTierChanged { get; set; }
    public bool IsDescriptionChanged { get; set; }
}
