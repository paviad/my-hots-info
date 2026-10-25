using Microsoft.EntityFrameworkCore;
using MyHotsInfo.Utils;
using MyReplayLibrary;
using MyReplayLibrary.Data;
using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Pages;

public partial class ReplayList : ContentPage {
    private readonly IServiceProvider _svcp;
    private readonly MyNavigator _myNavigator;
    private List<ReplayEntry>? _replays;

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
                await DisplayAlertAsync("Error", $"Couldn't fetch replays {x}", "Dismiss");
            }
        }
    }

    private async Task InitAsync() {
        // The list reloads whenever the page is shown, including on the way back from a game's
        // summary; keep the same game selected.
        var selectedId = _vm?.SelectedReplay?.Id;

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

        var talentInfos = await scope.ServiceProvider.GetRequiredService<PlayerQuery>().GetTalentInfos(_replays);
        if (_vm is not null) {
            _vm.TalentInfos = talentInfos;
        }

        _vm?.Replays.Clear();
        _replays.ForEach(r => _vm?.Replays.Add(r));

        if (_vm is not null && _replays.Find(r => r.Id == selectedId) is { } selected) {
            _vm.SelectedReplay = selected;
            ReplaysView.ScrollTo(selected, position: ScrollToPosition.MakeVisible, animate: false);
        }
    }

    private void FullscreenTapped(object? sender, TappedEventArgs e) {
        if (_vm?.SelectedReplay is { } replay) {
            _myNavigator.GoToReplay(replay.Id);
        }
    }

    private void PlayerTapped(object? sender, PlayerEntry player) {
        _myNavigator.GoToPlayer($"{player.Name}#{player.BattleTag}");
    }

    private async void Refresh_Click(object? sender, EventArgs e) {
        try {
            await InitAsync();
        }
        catch (Exception exception) {
            await DisplayAlertAsync("Error", $"Unable to init replay list {exception}", "Dismiss");
        }
    }
}
