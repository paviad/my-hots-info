using Microsoft.EntityFrameworkCore;
using MyHotsInfo.Utils;
using MyReplayLibrary.Data;
using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Pages;

public partial class ReplayList : ContentPage {
    private readonly IServiceProvider _svcp;
    private readonly MyNavigator _myNavigator;
    private List<ReplayEntry>? _replays;
    private readonly HeroTalentInformation _unknownTalent = new() {
        TalentName = "<Unknown>",
    };

    private readonly ReplayListViewModel? _vm;

    public ReplayList(IServiceProvider svcp, MyNavigator myNavigator) {
        _svcp = svcp;
        _myNavigator = myNavigator;
        InitializeComponent();

        _vm = BindingContext as ReplayListViewModel;

    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args) {
        _ = InternalInitAsync();
        return;

        async Task InternalInitAsync() {
            try {
                await InitAsync();
            }
            catch (Exception x) {
                await DisplayAlert("Error", $"Couldn't fetch replays {x}", "Dismiss");
            }
        }
    }

    private async Task InitAsync() {
        using var scope = _svcp.CreateScope();
        var dc = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        _replays = await dc.Replays
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.Player)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.ReplayCharacterTalents)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.ReplayCharacterMatchAwards)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.ReplayCharacterDraftOrder)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.ReplayCharacterScoreResult)
            .OrderByDescending(r => r.TimestampReplay)
            .Take(40)
            .AsSplitQuery()
            .ToListAsync();

        var buildMin = _replays.Min(r => r.ReplayBuild);
        var buildMax = _replays.Max(r => r.ReplayBuild);

        var talents = await dc.HeroTalentInformations
            .Where(r =>
                r.ReplayBuildFirst <= buildMin && r.ReplayBuildLast >= buildMin ||
                r.ReplayBuildFirst <= buildMax && r.ReplayBuildLast >= buildMax)
            .ToListAsync();

        var dic = _replays
            .SelectMany(r => r.ReplayCharacters)
            .SelectMany(r => r.ReplayCharacterTalents)
            .Select(r => (r, talents
                .SingleOrDefault(z =>
                    z.Character == r.ReplayCharacter.CharacterId &&
                    z.TalentId == r.TalentId &&
                    r.Replay.ReplayBuild >= z.ReplayBuildFirst &&
                    r.Replay.ReplayBuild <= z.ReplayBuildLast)))
            .ToDictionary(r => r.r, r => r.Item2 ?? _unknownTalent);

        _replays.ForEach(r => _vm?.Replays.Add(r));
    }

    private void GoToReplayClicked(object? sender, EventArgs e) {
        _myNavigator.GoToReplay(_vm!.SelectedReplay!.Id);
    }
}
