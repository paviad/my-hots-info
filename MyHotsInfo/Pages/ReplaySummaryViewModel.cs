using System.ComponentModel;
using Heroes.ReplayParser;
using MyHotsInfo.Utils;
using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Pages;

public class ReplaySummaryViewModel {
    public ReplaySummaryViewModel(ReplayEntry replay,
        IReadOnlyDictionary<ReplayCharacterTalent, HeroTalentInformation> talentInfos) {
        Id = replay.Id;

        // Like the in-game score screen: your team on top with you first, the enemy below. Without
        // you in the game, the winners take the top.
        var me = replay.ReplayCharacters.FirstOrDefault(r => r.IsMe);
        var allyWon = me?.IsWinner ?? true;
        var allies = replay.ReplayCharacters.Where(r => r.IsWinner == allyWon).OrderByDescending(r => r.IsMe).ToList();
        var enemies = replay.ReplayCharacters.Where(r => r.IsWinner != allyWon).ToList();

        Rows = [
            .. allies.Select(r => new ReplaySummaryRow(r, true, replay.ReplayBuild, talentInfos)),
            .. enemies.Select(r => new ReplaySummaryRow(r, false, replay.ReplayBuild, talentInfos)),
        ];
        Title = (me is null ? replay.MapId : $"{replay.MapId} {(allyWon ? "Victory" : "Defeat")}").ToUpperInvariant();
        AllyTeam = new TeamSummary(me is null ? "Winners" : "Your Team", allies);
        EnemyTeam = new TeamSummary(me is null ? "Losers" : "Enemy Team", enemies);
        GameTime = replay.ReplayLength.ToString(replay.ReplayLength.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss");
    }

    public SummaryTab Tab => SummaryTab.Current;
    public int Id { get; }
    public string Title { get; }
    public List<ReplaySummaryRow> Rows { get; }
    public TeamSummary AllyTeam { get; }
    public TeamSummary EnemyTeam { get; }
    public string GameTime { get; }
}

public class ReplaySummaryRow(
    ReplayCharacter character,
    bool isAlly,
    int replayBuild,
    IReadOnlyDictionary<ReplayCharacterTalent, HeroTalentInformation> talentInfos) {
    public ReplayCharacter Character { get; } = character;
    public bool IsAlly { get; } = isAlly;
    public bool IsMe => Character.IsMe;
    public SummaryTab Tab => SummaryTab.Current;

    // One slot per tier in the order picked, like the score screen's talents tab; tiers not reached stay empty.
    public List<TalentSlot> Talents { get; } = [
        .. character.ReplayCharacterTalents
            .Select(t => (t.TalentId, Info: talentInfos.GetValueOrDefault(t)))
            .OrderBy(t => t.Info?.TalentTier ?? int.MaxValue).ThenBy(t => t.TalentId)
            .Select(t => new TalentSlot(character.CharacterId, replayBuild, t.Info))
            .Concat(Enumerable.Repeat(TalentSlot.Empty, TalentSlot.Tiers.Length))
            .Take(TalentSlot.Tiers.Length),
    ];

    public string? AwardImage { get; } =
        character.ReplayCharacterMatchAwards.FirstOrDefault() is { } award && AwardImages.TryGetValue(award.MatchAwardType, out var name)
            ? $"award_{name}_{(isAlly ? "blue" : "red")}.png"
            : null;

    // The score screen badge for each award (Resources/Images/Awards, from the game's
    // storm_ui_scorescreen_mvp_*.dds). Awards without a badge show none.
    private static readonly Dictionary<MatchAwardType, string> AwardImages = new() {
        [MatchAwardType.MVP] = "mvp",
        [MatchAwardType.HighestKillStreak] = "skull",
        [MatchAwardType.MostXPContribution] = "experienced",
        [MatchAwardType.MostHeroDamageDone] = "painbringer",
        [MatchAwardType.MostSiegeDamageDone] = "siegemaster",
        [MatchAwardType.MostDamageTaken] = "bulwark",
        [MatchAwardType.MostHealing] = "mainhealer",
        [MatchAwardType.MostStuns] = "stunner",
        [MatchAwardType.MostMercCampsCaptured] = "headhunter",
        [MatchAwardType.MostKills] = "finisher",
        [MatchAwardType.HatTrick] = "hattrick",
        [MatchAwardType.ClutchHealer] = "clutchhealer",
        [MatchAwardType.MostProtection] = "protector",
        [MatchAwardType.ZeroDeaths] = "solesurvivor",
        [MatchAwardType.MostRoots] = "trapper",
        [MatchAwardType.ZeroOutnumberedDeaths] = "teamplayer",
        [MatchAwardType.MostDaredevilEscapes] = "daredevil",
        [MatchAwardType.MostEscapes] = "escapeartist",
        [MatchAwardType.MostSilences] = "silencer",
        [MatchAwardType.MostTeamfightDamageTaken] = "guardian",
        [MatchAwardType.MostTeamfightHealingDone] = "combatmedic",
        [MatchAwardType.MostTeamfightHeroDamageDone] = "scrapper",
        [MatchAwardType.MostVengeancesPerformed] = "avenger",
        [MatchAwardType.MostImmortalDamage] = "immortalslayer",
        [MatchAwardType.MostCoinsPaid] = "moneybags",
        [MatchAwardType.MostCurseDamageDone] = "masterofthecurse",
        [MatchAwardType.MostDragonShrinesCaptured] = "shriner",
        [MatchAwardType.MostDamageToPlants] = "gardenterror",
        [MatchAwardType.MostSkullsCollected] = "skullcollector",
        [MatchAwardType.MostDamageToMinions] = "guardianslayer",
        [MatchAwardType.MostTimeInTemple] = "templemaster",
        [MatchAwardType.MostGemsTurnedIn] = "jeweler",
        [MatchAwardType.MostAltarDamage] = "cannoneer",
        [MatchAwardType.MostDamageDoneToZerg] = "zergcrusher",
        [MatchAwardType.MostNukeDamageDone] = "dabomb",
        [MatchAwardType.MostTimePushing] = "pusher",
        [MatchAwardType.MostTimeOnPoint] = "pointguard",
        [MatchAwardType.MostInterruptedCageUnlocks] = "loyaldefender",
    };
}

/// <summary>Whether the summaries show stats or talents; one choice for all of them, so it sticks between games.</summary>
public sealed class SummaryTab : INotifyPropertyChanged {
    public static SummaryTab Current { get; } = new();

    private bool _showTalents;

    public bool ShowTalents {
        get => _showTalents;
        set {
            if (value == _showTalents) {
                return;
            }

            _showTalents = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowTalents)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowStats)));
        }
    }

    public bool ShowStats => !ShowTalents;

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class TalentSlot {
    public static readonly int[] Tiers = [1, 4, 7, 10, 13, 16, 20];
    public static readonly TalentSlot Empty = new();

    private TalentSlot() { }

    public TalentSlot(string characterId, int replayBuild, HeroTalentInformation? info) {
        IsPicked = true;
        if (info is null) {
            ToolTip = "Unknown talent";
            return;
        }

        Image = TalentIcons.Find(characterId, info.TalentName, replayBuild);
        ToolTip = $"{info.TalentName}\n{info.TalentDescription}";
    }

    public bool IsPicked { get; }
    public string? Image { get; }
    public string? ToolTip { get; }
}

public class TeamSummary(string name, IReadOnlyCollection<ReplayCharacter> team) {
    public string Name { get; } = name;
    public int Kills { get; } = team.Sum(r => r.ReplayCharacterScoreResult.SoloKills);
    public int Level { get; } = team.Select(r => r.ReplayCharacterScoreResult.Level).DefaultIfEmpty().Max();
}
