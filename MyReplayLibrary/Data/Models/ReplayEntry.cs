using System.ComponentModel.DataAnnotations;
using Heroes.ReplayParser;

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
    public DateTime TimestampCreated { get; init; }
    public ICollection<ReplayCharacter> ReplayCharacters { get; init; } = [];
    public ICollection<ReplayTeamObjective> ReplayTeamObjectives { get; init; } = [];
    public ICollection<Takedown> Takedowns { get; init; } = [];
    public ICollection<Chat> Chats { get; init; } = [];
}
