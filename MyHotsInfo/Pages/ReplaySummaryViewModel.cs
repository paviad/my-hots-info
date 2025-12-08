using MyReplayLibrary.Data.Models;

namespace MyHotsInfo.Pages;

public class ReplaySummaryViewModel(ReplayEntry replay) {
    public int Id { get; set; } = replay.Id;
    public List<ReplayCharacter> OrderedReplayCharacters { get; set; } =
        [.. replay.ReplayCharacters.OrderByDescending(r => r.IsWinner)];
}
