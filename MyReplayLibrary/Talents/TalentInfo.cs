using JetBrains.Annotations;

namespace MyReplayLibrary.Talents;

[UsedImplicitly]
public class TalentInfo
{
    public string HeroName { get; set; } = null!;
    public string TalentIdString { get; set; } = null!;
    public int TalentId { get; set; }
    public string TalentName { get; set; } = null!;
    public string TalentDescription { get; set; } = null!;
    public int Tier { get; set; }
    public int Column { get; set; }
    public byte[] Image { get; set; } = null!;
}
