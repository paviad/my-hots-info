using System.ComponentModel;
using System.Runtime.CompilerServices;
using MyHotsInfo.Extensions;
using MyHotsInfo.Utils;
using MyReplayLibrary;
using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Pages;

/// <summary>
/// Every hero's talents as of a scraped build, and what changed since the build scraped before it.
/// </summary>
public class HeroBrowserViewModel(Func<int, Task<List<HeroTalentInformation>>> loadTalents) : INotifyPropertyChanged {
    private readonly Dictionary<int, Task<List<HeroTalentInformation>>> _talentsByBuild = [];
    private List<TalentQuery.ScrapedBuild> _builds = [];
    private List<HeroBrowserHero> _allHeroes = [];
    private ILookup<string, HeroTalentInformation> _now = Enumerable.Empty<HeroTalentInformation>().ToLookup(t => t.Character);
    private ILookup<string, HeroTalentInformation> _before = Enumerable.Empty<HeroTalentInformation>().ToLookup(t => t.Character);
    private int _loadVersion;

    public List<string> BuildLabels { get; private set; } = [];

    public int SelectedBuildIndex {
        get;
        set {
            if (SetField(ref field, value) && value >= 0) {
                _ = LoadBuild();
            }
        }
    } = -1;

    public string? SearchText {
        get;
        set {
            if (SetField(ref field, value)) {
                ApplyFilter();
            }
        }
    }

    /// <summary>Only the heroes whose talents changed since the build before.</summary>
    public bool ChangedOnly {
        get;
        set {
            if (SetField(ref field, value)) {
                ApplyFilter();
            }
        }
    }

    public bool HasPreviousBuild => PreviousBuild is not null;

    public List<HeroBrowserHero> Heroes { get; private set => SetField(ref field, value); } = [];

    public HeroBrowserHero? SelectedHero {
        get;
        set {
            // Filtering the list away from the shown hero clears the selection; keep showing it.
            if (value is not null && SetField(ref field, value)) {
                ShowHero();
            }
        }
    }

    public string? HeroName { get; private set => SetField(ref field, value); }
    public string? HeroSummary { get; private set => SetField(ref field, value); }
    public List<HeroBrowserTier> Tiers { get; private set => SetField(ref field, value); } = [];

    private TalentQuery.ScrapedBuild? Build => _builds.ElementAtOrDefault(SelectedBuildIndex);

    // Newest first, so the one before is next in the list.
    private TalentQuery.ScrapedBuild? PreviousBuild =>
        SelectedBuildIndex >= 0 ? _builds.ElementAtOrDefault(SelectedBuildIndex + 1) : null;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetBuilds(List<TalentQuery.ScrapedBuild> builds) {
        _builds = builds;
        BuildLabels = [.. builds.Select(Label)];
        OnPropertyChanged(nameof(BuildLabels));
        SelectedBuildIndex = builds.Count > 0 ? 0 : -1;
    }

    public static string Label(TalentQuery.ScrapedBuild build) =>
        $"{build.Version ?? build.Build.ToString()}{(build.Date is { } d ? $"  ·  {d:yyyy-MM-dd}" : "")}";

    private async Task LoadBuild() {
        var version = ++_loadVersion;
        if (Build is not { } build) {
            return;
        }

        var previous = PreviousBuild;
        List<HeroTalentInformation> now, before;
        try {
            now = await Talents(build.Build);
            before = previous is null ? [] : await Talents(previous.Build);
        }
        catch (Exception x) {
            if (version == _loadVersion) {
                Heroes = [];
                HeroName = null;
                Tiers = [];
                HeroSummary = $"Couldn't load the talents: {x.Message}";
            }

            return;
        }

        // The user picked another build while this one loaded.
        if (version != _loadVersion) {
            return;
        }

        _now = now.ToLookup(t => t.Character, StringComparer.OrdinalIgnoreCase);
        _before = before.ToLookup(t => t.Character, StringComparer.OrdinalIgnoreCase);
        _allHeroes = [
            .. _now
                .Select(g => {
                    var isNew = previous is not null && !_before.Contains(g.Key);
                    var changes = isNew ? 0 : Compare(g, _before[g.Key]).Count(c => c.Kind != TalentChange.None);
                    return new HeroBrowserHero(g.Key, changes, isNew);
                })
                .OrderBy(h => h.Id, StringComparer.CurrentCultureIgnoreCase),
        ];
        OnPropertyChanged(nameof(HasPreviousBuild));

        var shown = SelectedHero?.Id;
        ApplyFilter();
        // The same hero as of the new build, or the first one when they weren't in it.
        var hero = _allHeroes.FirstOrDefault(h => h.Id == shown) ?? Heroes.FirstOrDefault();
        if (hero is not null) {
            SelectedHero = hero;
        }
        else {
            ShowHero();
        }

        return;

        Task<List<HeroTalentInformation>> Talents(int b) {
            if (!_talentsByBuild.TryGetValue(b, out var task) || task.IsFaulted) {
                _talentsByBuild[b] = task = loadTalents(b);
            }

            return task;
        }
    }

    private void ApplyFilter() {
        var search = SearchText.Strip();
        Heroes = [
            .. _allHeroes.Where(h =>
                (string.IsNullOrEmpty(search) || h.Id.Strip()!.Contains(search)) &&
                (!ChangedOnly || h.IsChanged)),
        ];
    }

    private void ShowHero() {
        if (SelectedHero is not { } hero || Build is not { } build) {
            HeroName = null;
            HeroSummary = null;
            Tiers = [];
            return;
        }

        var previous = PreviousBuild;
        HeroName = hero.Id;
        HeroSummary = previous is null ? $"As of {Label(build)}, the oldest build with talent data"
            : hero.IsNew ? $"New in {Label(build)}"
            : hero.Changes == 0 ? $"No changes since {Label(previous)}"
            : $"{hero.Changes} {(hero.Changes == 1 ? "change" : "changes")} since {Label(previous)}";

        var changes = hero.IsNew ? Compare(_now[hero.Id], []) : Compare(_now[hero.Id], _before[hero.Id]);
        Tiers = [
            .. TalentSlot.Tiers.Select(tier => new HeroBrowserTier(tier, [
                .. changes
                    .Where(c => (c.Now ?? c.Before)!.TalentTier == tier)
                    .OrderBy(c => c.Now is null).ThenBy(c => (c.Now ?? c.Before)!.TalentId)
                    .Select(c => new HeroBrowserTalent(hero.Id, c, c.Now is null ? previous!.Build : build.Build)),
            ])),
        ];
    }

    /// <summary>
    /// Pairs each talent with itself in the build before, by name, since the ids are renumbered whenever a tier
    /// changes. A name in two tiers (none so far) pairs up same tier first.
    /// </summary>
    private static List<TalentDiff> Compare(IEnumerable<HeroTalentInformation> now,
        IEnumerable<HeroTalentInformation> before) {
        var unmatched = before.ToList();
        var diffs = new List<TalentDiff>();
        var hasBefore = unmatched.Count > 0;
        foreach (var talent in now) {
            var old = unmatched.FirstOrDefault(b => b.TalentName == talent.TalentName && b.TalentTier == talent.TalentTier)
                ?? unmatched.FirstOrDefault(b => b.TalentName == talent.TalentName);
            if (old is not null) {
                unmatched.Remove(old);
            }

            var kind = !hasBefore ? TalentChange.None
                : old is null ? TalentChange.New
                : old.TalentTier != talent.TalentTier ? TalentChange.Moved
                : old.TalentDescription != talent.TalentDescription ? TalentChange.Changed
                : TalentChange.None;
            diffs.Add(new TalentDiff(talent, old, kind));
        }

        diffs.AddRange(unmatched.Select(b => new TalentDiff(null, b, TalentChange.Removed)));
        return diffs;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public enum TalentChange { None, New, Changed, Moved, Removed }

/// <summary>A talent as of the build and as of the build before; one of them is missing when it was added or removed.</summary>
public record TalentDiff(HeroTalentInformation? Now, HeroTalentInformation? Before, TalentChange Kind);

public class HeroBrowserHero(string id, int changes, bool isNew) {
    public string Id { get; } = id;
    public int Changes { get; } = changes;
    public bool IsNew { get; } = isNew;
    public bool IsChanged => IsNew || Changes > 0;

    public string? ChangeLabel { get; } = isNew ? "new" : changes > 0 ? changes.ToString() : null;
}

public class HeroBrowserTier(int level, List<HeroBrowserTalent> talents) {
    public string Title { get; } = $"Level {level}";
    public List<HeroBrowserTalent> Talents { get; } = talents;
}

public class HeroBrowserTalent {
    public HeroBrowserTalent(string characterId, TalentDiff diff, int build) {
        var talent = (diff.Now ?? diff.Before)!;
        Name = talent.TalentName;
        Description = talent.TalentDescription;
        Image = TalentIcons.Find(characterId, talent.TalentName, build);
        IsRemoved = diff.Kind == TalentChange.Removed;
        (Badge, BadgeColor) = diff.Kind switch {
            TalentChange.New => ("New", Color.FromArgb("#4CC24A")),
            TalentChange.Changed => ("Changed", Color.FromArgb("#E8B04A")),
            TalentChange.Moved => ($"Moved from level {diff.Before!.TalentTier}", Color.FromArgb("#6FA8FF")),
            TalentChange.Removed => ("Removed", Color.FromArgb("#E5484D")),
            _ => ((string?)null, Colors.Transparent),
        };
        if (diff.Kind is TalentChange.Changed or TalentChange.Moved &&
            diff.Before!.TalentDescription != talent.TalentDescription) {
            Was = $"Was: {diff.Before.TalentDescription}";
        }
    }

    public string Name { get; }
    public string Description { get; }
    public string? Image { get; }
    public string? Badge { get; }
    public Color BadgeColor { get; }
    public string? Was { get; }
    public bool IsRemoved { get; }
    public double Opacity => IsRemoved ? 0.45 : 1;
}
