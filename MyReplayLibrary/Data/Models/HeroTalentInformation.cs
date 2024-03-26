using System.ComponentModel.DataAnnotations;

namespace MyReplayLibrary.Data.Models;

public class HeroTalentInformation {
    [MaxLength(450)]
    public string Character { get; init; } = null!;
    public int ReplayBuildFirst { get; init; }
    public int ReplayBuildLast { get; set; }
    [MaxLength(450)]
    public int TalentId { get; init; }
    public int TalentTier { get; init; }
    [MaxLength(450)]
    public string TalentName { get; init; } = null!;
    [MaxLength(1000)]
    public string TalentDescription { get; init; } = null!;
}
