using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Heroes.ReplayParser;
using MyReplayLibrary;

namespace MyHotsInfo.Pages;

/// <summary>
/// The Statistics tab of the hero browser: my win rate when I played the hero, when an ally did and when an enemy
/// did, over the maps and modes picked, overall and map by map.
/// </summary>
public class HeroStatsViewModel(
    Func<string, Task<List<HeroGame>>> loadGames,
    Func<Task<(List<string> Maps, List<GameMode> Modes)>> loadFilters) : INotifyPropertyChanged {
    private readonly Dictionary<string, Task<List<HeroGame>>> _gamesByHero = [];
    private Task? _filtersLoad;
    private List<HeroGame>? _games;
    private string? _hero;
    private bool _isActive;
    private int _loadVersion;

    public List<StatsFilter> Maps { get; private set => SetField(ref field, value); } = [];
    public List<StatsFilter> Modes { get; private set => SetField(ref field, value); } = [];

    public string MapsLabel { get; private set => SetField(ref field, value); } = "All maps";
    public string ModesLabel { get; private set => SetField(ref field, value); } = "All modes";

    public List<HeroStatsCard> Cards { get; private set => SetField(ref field, value); } = [];
    public List<HeroStatsMapRow> ByMap {
        get;
        private set {
            if (SetField(ref field, value)) {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasByMap)));
            }
        }
    } = [];

    public bool HasByMap => ByMap.Count > 0;

    /// <summary>Loading, an error, or that there are no games; null once there is something to show.</summary>
    public string? Status { get; private set => SetField(ref field, value); }

    public ICommand ClearMapsCommand => new Command(() => Clear(Maps));
    public ICommand ClearModesCommand => new Command(() => Clear(Modes));

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Whether the tab is showing; the games are only read once it is.</summary>
    public void SetActive(bool isActive) {
        _isActive = isActive;
        _ = Load();
    }

    public void SetHero(string? hero) {
        if (hero == _hero) {
            return;
        }

        _hero = hero;
        _games = null;
        _ = Load();
    }

    private async Task Load() {
        var version = ++_loadVersion;
        if (!_isActive || _hero is not { } hero || _games is not null) {
            if (_hero is null) {
                Show();
            }

            return;
        }

        Status = "Loading…";
        try {
            await (_filtersLoad is { IsFaulted: false } f ? f : _filtersLoad = LoadFilters());
            if (!_gamesByHero.TryGetValue(hero, out var task) || task.IsFaulted) {
                _gamesByHero[hero] = task = loadGames(hero);
            }

            var games = await task;
            if (version != _loadVersion) {
                return;
            }

            _games = games;
            Show();
        }
        catch (Exception x) {
            if (version == _loadVersion) {
                Cards = [];
                ByMap = [];
                Status = $"Couldn't read the games: {x.Message}";
            }
        }
    }

    private async Task LoadFilters() {
        var (maps, modes) = await loadFilters();
        Maps = [.. maps.Select(m => new StatsFilter(m, m, Show))];
        Modes = [.. modes.Select(m => new StatsFilter(m.ToString(), ModeLabel(m), Show))];
    }

    private void Clear(List<StatsFilter> filters) {
        foreach (var filter in filters) {
            filter.SetSelected(false);
        }

        Show();
    }

    /// <summary>Recounts the hero's games over the maps and modes picked; none picked means all of them.</summary>
    private void Show() {
        var maps = Maps.Where(f => f.IsSelected).Select(f => f.Key).ToHashSet();
        var modes = Modes.Where(f => f.IsSelected).Select(f => f.Key).ToHashSet();
        MapsLabel = maps.Count == 0 ? "All maps" : maps.Count == 1 ? maps.Single() : $"{maps.Count} maps";
        ModesLabel = modes.Count == 0 ? "All modes"
            : modes.Count == 1 ? Modes.First(f => f.IsSelected).Label
            : $"{modes.Count} modes";

        if (_hero is null || _games is null) {
            Cards = [];
            ByMap = [];
            Status = _hero is null ? null : Status;
            return;
        }

        var games = _games
            .Where(g => (maps.Count == 0 || maps.Contains(g.MapId)) &&
                        (modes.Count == 0 || modes.Contains(g.GameMode.ToString())))
            .ToList();

        Cards = [
            new HeroStatsCard("Playing it", "My games as this hero", Count(games, HeroSide.Me)),
            new HeroStatsCard("With it", "An ally played it", Count(games, HeroSide.Ally)),
            new HeroStatsCard("Against it", "An enemy played it", Count(games, HeroSide.Enemy)),
        ];
        ByMap = [
            .. games
                .GroupBy(g => g.MapId)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .Select(g => new HeroStatsMapRow(g.Key,
                    Count(g, HeroSide.Me), Count(g, HeroSide.Ally), Count(g, HeroSide.Enemy))),
        ];
        Status = games.Count == 0
            ? _games.Count == 0 ? $"No games with {_hero} yet" : $"No games with {_hero} on these maps and modes"
            : null;

        return;

        static WinLoss Count(IEnumerable<HeroGame> games, HeroSide side) {
            var mine = games.Where(g => g.Side == side).ToList();
            return new WinLoss(mine.Count(g => g.Won), mine.Count(g => !g.Won));
        }
    }

    public static string ModeLabel(GameMode mode) => mode switch {
        GameMode.QuickMatch => "Quick Match",
        GameMode.StormLeague => "Storm League",
        GameMode.UnrankedDraft => "Unranked Draft",
        GameMode.HeroLeague => "Hero League",
        GameMode.TeamLeague => "Team League",
        GameMode.TryMe => "Try Mode",
        _ => mode.ToString(),
    };

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

/// <summary>A map or a mode to count the games of, picked or not.</summary>
public class StatsFilter(string key, string label, Action changed) : INotifyPropertyChanged {
    public string Key { get; } = key;
    public string Label { get; } = label;
    public bool IsSelected { get; private set; }

    public ICommand ToggleCommand => new Command(() => {
        SetSelected(!IsSelected);
        changed();
    });

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetSelected(bool value) {
        if (value == IsSelected) {
            return;
        }

        IsSelected = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
    }
}

/// <summary>My team's wins and losses.</summary>
public record WinLoss(int Wins, int Losses) {
    private static readonly Color Good = Color.FromArgb("#4CC24A");
    private static readonly Color Bad = Color.FromArgb("#E5484D");
    private static readonly Color Even = Color.FromArgb("#9C92CC");

    public int Games => Wins + Losses;
    public double? WinRate => Games == 0 ? null : 1.0 * Wins / Games;
    public string WinRateText => WinRate is { } r ? $"{r:P0}" : "—";
    public string Record => Games == 0 ? "no games" : $"{Wins}–{Losses}";
    public string Cell => Games == 0 ? "—" : $"{WinRateText}  ({Games})";

    /// <summary>Green above even, red below; even and empty stay neutral.</summary>
    public Color Color => Wins > Losses ? Good : Wins < Losses ? Bad : Even;
}

public record HeroStatsCard(string Title, string Hint, WinLoss Result);

public record HeroStatsMapRow(string Map, WinLoss Me, WinLoss Ally, WinLoss Enemy);
