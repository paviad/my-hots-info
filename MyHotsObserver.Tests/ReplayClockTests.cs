using MyHotsObserver;
using OpenCvSharp;

namespace MyHotsObserver.Tests;

public class ReplayClockTests {
    [Theory]
    [InlineData("1:33/41:53", 93, 2513)]
    [InlineData("1:33 / 41:53", 93, 2513)]
    [InlineData("16:01/41:53\n", 961, 2513)]
    public void ParsesThePanelClock(string text, int elapsed, int length) {
        var parsed = ReplayClock.ParsePanel(text);

        Assert.Equal((TimeSpan.FromSeconds(elapsed), TimeSpan.FromSeconds(length)), parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("41:53")]
    [InlineData("1:73/41:53")]
    [InlineData("42:00/41:53")]
    public void RejectsWhatIsNotAPanelClock(string text) {
        Assert.Null(ReplayClock.ParsePanel(text));
    }

    [Theory]
    [InlineData("00:55", 55)]
    [InlineData("15:23", 923)]
    public void ParsesTheGameClock(string text, int seconds) {
        Assert.Equal(TimeSpan.FromSeconds(seconds), ReplayClock.ParseGame(text));
    }

    /// <summary>The top 160 rows of real 1920×1080 replay frames, which hold both clocks.</summary>
    [Theory]
    [InlineData("clocks-1m33.png", 93, 2513, 55)]
    [InlineData("clocks-4m06.png", 246, 2513, 207)]
    [InlineData("clocks-16m01.png", 961, 2513, 923)]
    public void ReadsBothClocksOffAFrame(string fixture, int replay, int length, int game) {
        using var frame = Frame(fixture);
        using var clock = new ReplayClock();

        var reading = clock.Read(frame, Layout);

        Assert.Equal(new ClockReading(TimeSpan.FromSeconds(replay), TimeSpan.FromSeconds(length), TimeSpan.FromSeconds(game)), reading);
    }

    [Theory]
    [InlineData("collapsed-5m10.png", true)]
    [InlineData("clocks-1m33.png", false)]
    [InlineData("clocks-16m01.png", false)]
    public void TellsACollapsedPanel(string fixture, bool collapsed) {
        using var frame = Frame(fixture);
        using var clock = new ReplayClock();

        Assert.Equal(collapsed, clock.IsPanelCollapsed(frame, Layout));
    }

    [Fact]
    public void ReadsOnlyTheGameClockWhileThePanelIsCollapsed() {
        using var frame = Frame("collapsed-5m10.png");
        using var clock = new ReplayClock();

        Assert.Equal(new ClockReading(null, null, TimeSpan.FromSeconds(272)), clock.Read(frame, Layout));
    }

    private static readonly ObserverLayout Layout = new(1920, 1080);

    /// <summary>A 1920×1080 frame with a fixture strip (the top rows of a real frame) at its top.</summary>
    private static Mat Frame(string fixture) {
        using var strip = Cv2.ImRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        var frame = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
        strip.CopyTo(new Mat(frame, new Rect(0, 0, strip.Width, strip.Height)));
        return frame;
    }
}
