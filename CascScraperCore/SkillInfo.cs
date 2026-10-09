namespace CascScraperCore;

public class SkillInfo {
    public string HeroName { get; set; } = null!;
    public string SkillName { get; set; } = null!;
    public int Cooldown { get; set; }
    public string Key { get; set; } = null!;
    public byte[] Image { get; set; } = null!;
}
