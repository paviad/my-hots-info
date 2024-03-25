using System.ComponentModel.DataAnnotations;

namespace MyReplayLibrary.Data.Models;

public class PlayerEntry {
    public int Id { get; init; }
    public int BattleNetRegionId { get; init; }
    public int BattleNetSubId { get; init; }
    public int BattleNetId { get; init; }
    [MaxLength(450)]
    public string Name { get; set; } = null!;
    public int BattleTag { get; set; }
    public DateTimeOffset TimestampCreated { get; init; }
}
