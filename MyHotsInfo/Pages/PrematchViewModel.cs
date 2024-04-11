using MyReplayLibrary;

namespace MyHotsInfo.Pages;

public class PrematchViewModel(List<PrematchRecord> records) {
    public List<PrematchRecord> Records { get; } = records;
}
