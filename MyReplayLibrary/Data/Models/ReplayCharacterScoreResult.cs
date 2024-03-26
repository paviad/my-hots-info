namespace MyReplayLibrary.Data.Models;

public class ReplayCharacterScoreResult {
    public int ReplayId { get; init; }
    public int PlayerId { get; init; }

    public ReplayEntry Replay { get; init; } = null!;
    public PlayerEntry Player { get; init; } = null!;
    public int Level { get; init; }
    public int Takedowns { get; init; }
    public int SoloKills { get; init; }
    public int Assists { get; init; }
    public int Deaths { get; init; }
    public int HighestKillStreak { get; init; }
    public int HeroDamage { get; init; }
    public int SiegeDamage { get; init; }
    public int StructureDamage { get; init; }
    public int MinionDamage { get; init; }
    public int CreepDamage { get; init; }
    public int SummonDamage { get; init; }
    public int? Healing { get; init; }
    public int SelfHealing { get; init; }
    public int? DamageTaken { get; init; }
    public int ExperienceContribution { get; init; }
    public int TownKills { get; init; }
    public TimeSpan TimeSpentDead { get; init; }
    public int MercCampCaptures { get; init; }
    public int WatchTowerCaptures { get; init; }
    public int MetaExperience { get; init; }
    public ReplayCharacter ReplayCharacter { get; init; } = null!;
}
