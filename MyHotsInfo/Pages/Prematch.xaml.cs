using MyHotsInfo.Utils;
using MyReplayLibrary;

namespace MyHotsInfo.Pages;

public partial class Prematch : ContentPage, IQueryAttributable {
    // Placeholders the game shows instead of a real name; looking them up finds strangers.
    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase) {
        "Player",
        "Aventurier",
    };

    private static readonly Color MyTeamColor = Color.FromArgb("#3B82F6");
    private static readonly Color OtherTeamColor = Color.FromArgb("#E5484D");
    private static readonly Color UnknownTeamColor = Colors.Gray;

    private readonly IServiceProvider _svcp;
    private readonly MyNavigator _myNavigator;

    public Prematch(IServiceProvider svcp, MyNavigator myNavigator) {
        _svcp = svcp;
        _myNavigator = myNavigator;
        InitializeComponent();
    }

    private void FoldedTapped(object? sender, TappedEventArgs e) {
        if ((sender as BindableObject)?.BindingContext is PrematchSlot slot) {
            slot.Expand();
        }
    }

    private void CandidateTapped(object? sender, TappedEventArgs e) {
        if ((sender as BindableObject)?.BindingContext is PrematchCandidate candidate) {
            _myNavigator.GoToPlayer(candidate.BattleTag);
        }
    }

    /// <param name="query">"names": the ten screen slots, 0-4 left and 5-9 right, "" where unread.</param>
    public async void ApplyQueryAttributes(IDictionary<string, object> query) {
        try {
            BindingContext = PrematchViewModel.Loading();
            var slots = (List<string>)query["names"];

            using var scope = _svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            var myNames = await playerQuery.GetMyNames();
            var matcher = await playerQuery.GetNameMatcher();

            var left = slots.Take(5).ToList();
            var right = slots.Skip(5).ToList();
            // The draft screen always shows the owner's team on the left; the loading screen shows
            // teams on fixed sides, so look for the owner's name.
            var meLeft = left.Any(myNames.Contains);
            var meRight = right.Any(myNames.Contains);
            var known = meLeft != meRight;
            var (mine, other) = meRight && !meLeft ? (right, left) : (left, right);

            var myTeam = new PrematchTeam(known ? "Your team" : "Left team", known ? MyTeamColor : UnknownTeamColor,
                await BuildSlots(mine));
            var otherTeam = new PrematchTeam(known ? "Enemy team" : "Right team", known ? OtherTeamColor : UnknownTeamColor,
                await BuildSlots(other));
            BindingContext = PrematchViewModel.Loaded(myTeam, otherTeam);

            async Task<List<PrematchSlot>> BuildSlots(List<string> names) {
                List<PrematchSlot> rc = [];
                foreach (var name in names) {
                    rc.Add(await BuildSlot(name));
                }

                return rc;
            }

            async Task<PrematchSlot> BuildSlot(string name) {
                if (name == "") {
                    return PrematchSlot.Plain("—", "Couldn't read name");
                }

                if (myNames.Contains(name)) {
                    return PrematchSlot.Plain(name, "You");
                }

                if (GenericNames.Contains(name)) {
                    return PrematchSlot.Plain(name, "Name hidden");
                }

                var results = await playerQuery.QueryByName(matcher.Resolve(name) ?? name, true);
                var players = results.Where(r => r.Totals.NumGames > 0).OrderByDescending(r => r.Totals.NumGames).ToList();
                if (players.Count == 0) {
                    // Either a stranger or a misread name; can't tell which.
                    return PrematchSlot.Plain(name, "No games together");
                }

                var title = players[0].BattleTag!.Split('#')[0];
                return PrematchSlot.WithCandidates(title, [.. players.Select(ToCandidate)], DateTime.UtcNow);
            }

            static PrematchCandidate ToCandidate(PlayerQuery.PlayerRecord p) {
                var t = p.Totals;

                var heroes =
                    from kv in p.ByHero
                    orderby kv.Value.NumGames descending
                    select new PrematchHeroRecord(kv.Key, kv.Value.NumGames, kv.Value.Wins);

                return new PrematchCandidate(p.BattleTag!, t.NumGames, p.LastMet,
                    Record(t.WeWon, t.WeLost), Record(t.WeBeatThem, t.TheyBeatUs), [.. heroes.Take(6)]);
            }
        }
        catch (Exception e) {
            BindingContext = PrematchViewModel.Failed($"Couldn't load player stats: {e.Message}");
        }

        return;

        // "games · share you won", or a dash when there were none
        static string Record(int won, int lost) => won + lost == 0 ? "–" : $"{won + lost} · {1.0 * won / (won + lost):P0}";
    }
}
