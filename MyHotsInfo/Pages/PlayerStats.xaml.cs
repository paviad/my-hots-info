using MyReplayLibrary;

namespace MyHotsInfo.Pages;

public partial class PlayerStats : ContentPage, IQueryAttributable {
    private readonly IServiceProvider _svcp;

    public PlayerStats(IServiceProvider svcp) {
        _svcp = svcp;
        InitializeComponent();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query) {
        try {
            var battleTag = (string)query["battleTag"];
            Title = battleTag;
            BindingContext = null;

            using var scope = _svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            var results = await playerQuery.QueryByName(battleTag);

            var records = results
                .Where(r => r.Totals.NumGames > 0)
                .Select(ToRecord)
                .ToList();

            BindingContext = new PlayerStatsViewModel(records);
        }
        catch (Exception e) {
            await DisplayAlertAsync("Error", $"Can't show player stats {e}", "Dismiss");
        }

        return;

        static PlayerStatsRecord ToRecord(PlayerQuery.PlayerRecord record) {
            var heroes =
                from kv in record.ByHero
                orderby kv.Value.NumGames descending
                select new PlayerStatsRow(kv.Key, kv.Key, kv.Value);

            return new(record.BattleTag!, [new(null, "Overall", record.Totals), .. heroes]);
        }
    }
}
