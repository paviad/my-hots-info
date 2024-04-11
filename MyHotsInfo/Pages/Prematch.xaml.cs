using MyReplayLibrary;

namespace MyHotsInfo.Pages;

public partial class Prematch : ContentPage, IQueryAttributable {
    private readonly IServiceProvider _svcp;

    public Prematch(IServiceProvider svcp) {
        _svcp = svcp;
        InitializeComponent();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query) {
        try {
            using var scope = _svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            var names = (List<string>)query["names"];
            List<PrematchRecord> records = [];
            records.Clear();
            foreach (var name in names) {
                var results = await playerQuery.QueryByName(name, true);
                var r = Enumerate(results).ToList();
                records.AddRange(r);
            }

            var vm = new PrematchViewModel(records);
            BindingContext = vm;
        }
        catch (Exception e) {
            await DisplayAlert("Error", $"Can't show summary {e}", "Dismiss");
        }

        return;

        IEnumerable<PrematchRecord> Enumerate(List<PlayerQuery.PlayerRecord> results) {
            foreach (var (name, (games, wins, _, _, _, _), resultRecords) in results) {
                if (games == 0) {
                    continue;
                }

                var winRate = 1.0 * wins / games;

                var q =
                    from resultRecord in resultRecords
                    let hero = resultRecord.Key
                    let z = resultRecord.Value
                    let games1 = z.NumGames
                    let wins1 = z.Wins
                    let winRate1 = 1.0 * wins1 / games1
                    orderby games1 descending
                    select new PrematchHeroRecord(hero, winRate1);

                var overall = new PrematchRecord(name!, winRate, [.. q.Take(3)]);

                yield return overall;
            }
        }
    }
}
