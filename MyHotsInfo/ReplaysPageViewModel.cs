using MyReplayLibrary.Data.Models;
using System.Collections.ObjectModel;

namespace MyHotsInfo;

public class ReplaysPageViewModel {
    public ObservableCollection<ReplayEntry> Replays { get; set; } = new();
}
