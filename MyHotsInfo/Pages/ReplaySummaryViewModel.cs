using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Pages;

public class ReplaySummaryViewModel(ReplayEntry replay) {
    public List<ReplayCharacter> OrderedReplayCharacters { get; set; } =
        [.. replay.ReplayCharacters.OrderByDescending(r => r.IsWinner)];
}
