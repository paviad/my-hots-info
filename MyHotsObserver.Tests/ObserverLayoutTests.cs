using MyHotsObserver;
using OpenCvSharp;

namespace MyHotsObserver.Tests;

public class ObserverLayoutTests {
    [Fact]
    public void KeepsTheMeasuredPositionsAt1080p() {
        var layout = new ObserverLayout(1920, 1080);

        Assert.Equal(new Rect(1552, 124, 200, 28), layout.PanelClock);
        Assert.Equal((1555, 1887, 175), layout.TimelineBar);
    }

    [Fact]
    public void ScalesWithA16By9Window() {
        var layout = new ObserverLayout(2560, 1440);

        Assert.Equal(new Rect(2069, 165, 267, 37), layout.PanelClock);
        Assert.Equal((2073, 2516, 233), layout.TimelineBar);
    }

    /// <summary>Clicks measured in a 41:53 replay: x → where it landed, to the second.</summary>
    [Theory]
    [InlineData(570, 1631)]   // 9:30 → landed 9:31 (read a second after landing)
    [InlineData(1214, 1716)]  // 20:14 → landed 20:12
    [InlineData(850, 1668)]   // 14:10 → landed 14:10
    [InlineData(180, 1579)]   // 3:00 → landed 2:58
    public void ClicksThePixelAtOrJustBeforeTheTarget(double seconds, int x) {
        Assert.Equal(x, new ObserverLayout(1920, 1080).TimelineX(seconds, 2513));
    }

    [Fact]
    public void ClampsTimelineClicksToTheBar() {
        var layout = new ObserverLayout(1920, 1080);

        Assert.Equal(1555, layout.TimelineX(-5, 2513));
        Assert.Equal(1887, layout.TimelineX(9999, 2513));
    }

    [Fact]
    public void RefusesOtherAspectRatios() {
        Assert.Throws<NotSupportedException>(() => new ObserverLayout(1920, 1200));
    }
}
