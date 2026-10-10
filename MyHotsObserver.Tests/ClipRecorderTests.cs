using MyHotsObserver;

namespace MyHotsObserver.Tests;

public class ClipRecorderTests {
    [Fact]
    public void NamesClipsAfterTheMapTimesAndHero() {
        Assert.Equal("Tomb of the Spider Queen 6-52 to 7-05 Chromie",
            ClipRecorder.Name("Tomb of the Spider Queen", 412.9, 425, "Chromie", null));
    }

    [Fact]
    public void LeavesTheHeroOutWhenNoneIsFollowed() {
        Assert.Equal("Haunted Mines 0-05 to 1-00", ClipRecorder.Name("Haunted Mines", 5, 60, null, null));
    }

    [Fact]
    public void UsesAGivenName() {
        Assert.Equal("my kill", ClipRecorder.Name("Haunted Mines", 5, 60, "Chromie", "my kill"));
    }
}
