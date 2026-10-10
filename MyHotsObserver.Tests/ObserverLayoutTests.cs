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

    [Fact]
    public void RefusesOtherAspectRatios() {
        Assert.Throws<NotSupportedException>(() => new ObserverLayout(1920, 1200));
    }
}
