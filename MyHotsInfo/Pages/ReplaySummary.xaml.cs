using MyReplayLibrary;

namespace MyHotsInfo.Pages;

public partial class ReplayPage : ContentPage, IQueryAttributable {
    private readonly IServiceProvider _svcp;

    public ReplayPage(IServiceProvider svcp) {
        _svcp = svcp;
        InitializeComponent();
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
            await DisplayAlert("Error", $"Can't show summary {e}", "Dismiss");
        }
    }
}
