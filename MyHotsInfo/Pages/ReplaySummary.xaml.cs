using MyHotsInfo.Utils;
using MyReplayLibrary;
using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Pages;

public partial class ReplayPage : ContentPage, IQueryAttributable {
    private readonly IServiceProvider _svcp;
    private readonly MyNavigator _myNavigator;

    public ReplayPage(IServiceProvider svcp, MyNavigator myNavigator) {
        _svcp = svcp;
        _myNavigator = myNavigator;
        InitializeComponent();
    }

    private void PlayerTapped(object? sender, TappedEventArgs e) {
        if ((sender as BindableObject)?.BindingContext is ReplayCharacter { Player: { } player }) {
            _myNavigator.GoToPlayer($"{player.Name}#{player.BattleTag}");
        }
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query) {
        try {
            using var scope = _svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            var replayId = int.Parse((string)query["id"]);
            var replay = await playerQuery.GetReplay(replayId);
            var vm = new ReplaySummaryViewModel(replay);
            BindingContext = vm;
        }
        catch (Exception e) {
            await DisplayAlertAsync("Error", $"Can't show summary {e}", "Dismiss");
        }
    }
}
