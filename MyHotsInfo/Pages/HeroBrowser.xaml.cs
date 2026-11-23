using Heroes.ReplayParser;
using MyReplayLibrary;
using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Pages;

public partial class HeroBrowser : ContentPage {
    private readonly IServiceProvider _svcp;
    private readonly HeroBrowserViewModel _vm;
    private bool _loaded;

    public HeroBrowser(IServiceProvider svcp) {
        _svcp = svcp;
        InitializeComponent();
        BindingContext = _vm = new HeroBrowserViewModel(LoadTalents, new HeroStatsViewModel(LoadGames, LoadFilters));
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args) {
        base.OnNavigatedTo(args);
        // The builds only change with a scrape, so once is enough; until it works, every visit tries again.
        if (_loaded) {
            return;
        }

        try {
            using var scope = _svcp.CreateScope();
            var builds = await scope.ServiceProvider.GetRequiredService<TalentQuery>().GetBuilds();
            _loaded = true;
            _vm.SetBuilds(builds);
        }
        catch (Exception x) {
            await DisplayAlertAsync("Heroes", $"Couldn't read the talent builds: {x.Message}", "Dismiss");
        }
    }

    private void OnTalentsTapped(object? sender, TappedEventArgs e) => _vm.ShowStatistics = false;

    private void OnStatisticsTapped(object? sender, TappedEventArgs e) => _vm.ShowStatistics = true;

    private async Task<List<HeroGame>> LoadGames(string hero) {
        using var scope = _svcp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlayerQuery>().GetHeroGames(hero);
    }

    private async Task<(List<string> Maps, List<GameMode> Modes)> LoadFilters() {
        using var scope = _svcp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlayerQuery>().GetMapsAndModes();
    }

    private async Task<List<HeroTalentInformation>> LoadTalents(int build) {
        using var scope = _svcp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TalentQuery>().GetTalents(build);
    }
}
