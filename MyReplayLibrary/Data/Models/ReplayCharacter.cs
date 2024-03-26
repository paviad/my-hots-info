using System.ComponentModel.DataAnnotations;

namespace MyReplayLibrary.Data.Models;

public class ReplayCharacter {
    public bool IsAutoSelect { get; init; }

    [MaxLength(450)]
    public string CharacterId { get; init; } = null!;
    public int CharacterLevel { get; init; }
    public bool IsWinner { get; init; }
    public bool IsMe { get; init; }
    public int ReplayId { get; init; }
    public int PlayerId { get; init; }

    public ReplayEntry Replay { get; init; } = null!;
    public PlayerEntry Player { get; init; } = null!;
    public ReplayCharacterDraftOrder? ReplayCharacterDraftOrder { get; set; }
    public ICollection<ReplayCharacterTalent> ReplayCharacterTalents { get; init; } = [];
    public ReplayCharacterScoreResult ReplayCharacterScoreResult { get; set; } = null!;
    public ICollection<ReplayCharacterMatchAward> ReplayCharacterMatchAwards { get; init; } = [];
}
