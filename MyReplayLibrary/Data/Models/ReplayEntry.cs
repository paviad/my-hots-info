using System.ComponentModel.DataAnnotations;
using Heroes.ReplayParser;
using Microsoft.VisualBasic;

namespace MyReplayLibrary.Data.Models;

public class ReplayEntry {
    public int Id { get; init; }
    public Guid ReplayHash { get; init; }
    public int ReplayBuild { get; init; }
    public GameMode GameMode { get; init; }
    [MaxLength(450)]
    public string MapId { get; init; } = null!;
    public TimeSpan ReplayLength { get; init; }
    public DateTime TimestampReplay { get; init; }
    public DateTimeOffset TimestampCreated { get; init; }
    public ICollection<ReplayCharacter> ReplayCharacters { get; init; } = [];
    public ICollection<ReplayTeamObjective> ReplayTeamObjectives { get; init; } = [];
}
