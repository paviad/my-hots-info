using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MyHotsInfo.Pages;

/// <summary>One side of the draft or loading screen.</summary>
public record PrematchTeam(string Title, Color Accent, List<PrematchSlot> Slots);

/// <summary>
/// One player slot. A name can belong to several players in the database, and nothing on the
/// screenshot says which one this is, so every one with games is a candidate. Players met only
/// once, long ago, say little, so when there are many candidates those are folded into one line
/// that expands on request.
/// </summary>
public class PrematchSlot : INotifyPropertyChanged {
    private const int AlwaysShowAll = 2;
    private static readonly TimeSpan Recent = TimeSpan.FromDays(90);

    private readonly List<PrematchCandidate> _folded;

    private PrematchSlot(string name, string? status, List<PrematchCandidate> shown, List<PrematchCandidate> folded) {
        Name = name;
        Status = status;
        Candidates = new(shown);
        _folded = folded;
        if (folded.Count > 0) {
            var first = folded.Min(c => c.LastMetUtc)!.Value.ToLocalTime().ToString("MMM yyyy");
            var last = folded.Max(c => c.LastMetUtc)!.Value.ToLocalTime().ToString("MMM yyyy");
            var range = first == last ? first : $"{first} – {last}";
            FoldedText = shown.Count == 0 ? $"Each met once, {range}" : $"+{folded.Count} others, each met once, {range}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }
    public string? Status { get; }
    public bool HasStatus => Status is not null;
    public ObservableCollection<PrematchCandidate> Candidates { get; }
    public bool HasCandidates => Candidates.Count > 0;
    public string? FoldedText { get; }
    public bool HasFolded => _folded.Count > 0;

    public static PrematchSlot Plain(string name, string status) => new(name, status, [], []);

    /// <summary>Most recently met first; folds the one-time meetings when there are many.</summary>
    public static PrematchSlot WithCandidates(string name, List<PrematchCandidate> candidates, DateTime nowUtc) {
        var sorted = candidates.OrderByDescending(c => c.LastMetUtc).ToList();
        var status = sorted.Count > 1 ? $"{sorted.Count} players with this name" : null;
        if (sorted.Count <= AlwaysShowAll) {
            return new(name, status, sorted, []);
        }

        var recentOnce = sorted.FirstOrDefault(c => c.NumGames == 1 && nowUtc - c.LastMetUtc < Recent);
        var shown = sorted.Where(c => c.NumGames > 1 || c == recentOnce).ToList();
        return new(name, status, shown, [.. sorted.Except(shown)]);
    }

    public void Expand() {
        foreach (var c in _folded) {
            Candidates.Add(c);
        }

        _folded.Clear();
        PropertyChanged?.Invoke(this, new(nameof(HasCandidates)));
        PropertyChanged?.Invoke(this, new(nameof(HasFolded)));
    }
}

/// <summary>
/// A player the slot's name may belong to. <paramref name="WithYou"/>: games on your team and
/// the share you won; <paramref name="Against"/>: games on the other team and the share you
/// won; "–" when none.
/// </summary>
public record PrematchCandidate(
    string BattleTag,
    int NumGames,
    DateTime? LastMetUtc,
    string WithYou,
    string Against,
    List<PrematchHeroRecord> Heroes) {
    /// <summary>The "#1234" part, since the name is already the slot's heading.</summary>
    public string Tag => BattleTag.Contains('#') ? BattleTag[BattleTag.IndexOf('#')..] : BattleTag;

    public string LastMet {
        get {
            if (LastMetUtc is not { } t) {
                return "–";
            }

            var days = (int)(DateTime.UtcNow - t).TotalDays;
            return days switch {
                < 1 => "today",
                < 2 => "yesterday",
                < 30 => $"{days} days ago",
                _ => t.ToLocalTime().ToString("MMM yyyy"),
            };
        }
    }
}

/// <summary>A hero the player played in your games, and how many of those games they won.</summary>
public record PrematchHeroRecord(string Hero, int NumGames, int Wins) {
    public string ToolTip => $"{Hero}: won {Wins} of {NumGames} ({1.0 * Wins / NumGames:P0})";
}
