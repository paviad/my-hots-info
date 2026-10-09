using MyHotsInfo.Utils;
using MyReplayLibrary;
using MyReplayLibrary.Data.Models;
using MyReplayLibrary.Obs;

namespace MyHotsInfo.Pages;

public partial class ReplayPage : ContentPage, IQueryAttributable {
    private readonly IServiceProvider _svcp;
    private readonly MyNavigator _myNavigator;
    private readonly GameRecorder _gameRecorder;
    private ReplayEntry? _replay;

    public ReplayPage(IServiceProvider svcp, MyNavigator myNavigator, GameRecorder gameRecorder) {
        _svcp = svcp;
        _myNavigator = myNavigator;
        _gameRecorder = gameRecorder;
        InitializeComponent();
    }

    private async void KeepRecordingClicked(object? sender, EventArgs e) {
        if (_replay is not { } replay) {
            return;
        }

        KeepRecordingButton.IsEnabled = false;
        KeepRecordingButton.Text = "Saving...";
        try {
            var name = $"{replay.TimestampReplay.ToLocalTime():yyyy-MM-dd HH.mm} {replay.MapId}";
            await _gameRecorder.KeepAsync(replay.ReplayHash, name);
            if (_replay == replay) {
                KeepRecordingButton.Text = "Saved to Videos";
            }
        }
        catch (Exception x) {
            if (_replay == replay) {
                KeepRecordingButton.Text = "Keep recording";
                KeepRecordingButton.IsEnabled = true;
            }

            await DisplayAlertAsync("Recording", $"Couldn't keep the recording: {x.Message}", "Dismiss");
        }
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

            // The page is reused, so reset the button for this game.
            _replay = replay;
            KeepRecordingButton.Text = "Keep recording";
            KeepRecordingButton.IsEnabled = true;
            KeepRecordingButton.IsVisible = _gameRecorder.FindRecording(replay.ReplayHash) is not null;
        }
        catch (Exception e) {
            await DisplayAlertAsync("Error", $"Can't show summary {e}", "Dismiss");
        }
    }
}
