using MyHotsInfo.Pages;
using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Controls;

public partial class ReplaySummaryView : ContentView {
    public ReplaySummaryView() {
        InitializeComponent();
    }

    /// <summary>A player's name was clicked.</summary>
    public event EventHandler<PlayerEntry>? PlayerTapped;

    private void OnStatsTapped(object? sender, TappedEventArgs e) => SummaryTab.Current.ShowTalents = false;

    private void OnTalentsTapped(object? sender, TappedEventArgs e) => SummaryTab.Current.ShowTalents = true;

    private void OnPlayerTapped(object? sender, TappedEventArgs e) {
        if ((sender as BindableObject)?.BindingContext is ReplaySummaryRow { Character.Player: { } player }) {
            PlayerTapped?.Invoke(this, player);
        }
    }
}
