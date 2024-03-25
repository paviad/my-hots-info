namespace MyReplayLibrary.Data.Models;

public class ReplayCharacterScoreResult {
    public int ReplayId { get; init; }
    public int PlayerId { get; init; }

    public ReplayEntry Replay { get; init; } = null!;
    public PlayerEntry Player { get; init; } = null!;
    public int Level { get; set; }
    public int Takedowns { get; set; }
    public int SoloKills { get; set; }
    public int Assists { get; set; }
    public int Deaths { get; set; }
    public int HighestKillStreak { get; set; }
    public int HeroDamage { get; set; }
    public int SiegeDamage { get; set; }
    public int StructureDamage { get; set; }
    public int MinionDamage { get; set; }
    public int CreepDamage { get; set; }
    public int SummonDamage { get; set; }
    public int? Healing { get; set; }
    public int SelfHealing { get; set; }
    public int? DamageTaken { get; set; }
    public int ExperienceContribution { get; set; }
    public int TownKills { get; set; }
    public TimeSpan TimeSpentDead { get; set; }
    public int MercCampCaptures { get; set; }
    public int WatchTowerCaptures { get; set; }
    public int MetaExperience { get; set; }
    public ReplayCharacter ReplayCharacter { get; set; } = null!;
}
