using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Pages;

public sealed class ReplayListViewModel : INotifyPropertyChanged {
    private ReplayEntry? _selectedReplay;
    private ReplaySummaryViewModel? _summary;

    public ObservableCollection<ReplayEntry> Replays { get; set; } = [];

    /// <summary>The talents picked in <see cref="Replays" />, for the summary's talents tab.</summary>
    public IReadOnlyDictionary<ReplayCharacterTalent, HeroTalentInformation> TalentInfos { get; set; } =
        new Dictionary<ReplayCharacterTalent, HeroTalentInformation>();

    public ReplayEntry? SelectedReplay {
        get => _selectedReplay;
        set {
            if (Equals(value, _selectedReplay)) {
                return;
            }

            _selectedReplay = value;
            Summary = value is null ? null : new ReplaySummaryViewModel(value, TalentInfos);
            OnPropertyChanged();
        }
    }

    public ReplaySummaryViewModel? Summary {
        get => _summary;
        private set {
            if (value == _summary) {
                return;
            }

            _summary = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
