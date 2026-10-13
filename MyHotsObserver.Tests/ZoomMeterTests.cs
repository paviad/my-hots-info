using MyHotsObserver;
using OpenCvSharp;

namespace MyHotsObserver.Tests;

public class ZoomMeterTests {
    private static readonly ObserverLayout Layout = new(1920, 1080);

    /// <summary>
    /// The minimap corner (440×280 from 1480,800) of 1920×1080 frames at each zoom; out-2 is a
    /// later frame elsewhere on the map. Widths are read by eye to a few pixels.
    /// </summary>
    [Theory]
    [InlineData("minimap-bfe-default.png", 70)]
    [InlineData("minimap-bfe-out.png", 84)]
    [InlineData("minimap-bfe-far.png", 97)]
    [InlineData("minimap-bfe-out-2.png", 88)]
    public void MeasuresTheViewportOutline(string fixture, double width) {
        using var frame = Frame(fixture);

        var measured = ZoomMeter.ViewportWidth(frame, Layout);

        Assert.NotNull(measured);
        Assert.InRange(measured.Value, width - 5, width + 5);
    }

    [Theory]
    [InlineData("minimap-bfe-default.png", Zoom.Default)]
    [InlineData("minimap-bfe-out.png", Zoom.Out)]
    [InlineData("minimap-bfe-far.png", Zoom.Far)]
    [InlineData("minimap-bfe-out-2.png", Zoom.Out)]
    public void ClassifiesTheZoom(string fixture, Zoom zoom) {
        using var frame = Frame(fixture);

        Assert.Equal(zoom, ZoomMeter.Classify(ZoomMeter.ViewportWidth(frame, Layout)!.Value, 70));
    }

    [Theory]
    [InlineData(Zoom.Default, Zoom.Out, false)]
    [InlineData(Zoom.Out, Zoom.Default, false)]
    [InlineData(Zoom.Far, Zoom.Out, false)]
    [InlineData(Zoom.Default, Zoom.Far, true)]
    [InlineData(Zoom.Far, Zoom.Default, true)]
    [InlineData(Zoom.Out, Zoom.Far, true)]
    public void PicksOneKey(Zoom from, Zoom to, bool shift) {
        var key = ZoomMeter.KeyFor(from, to);

        Assert.Equal(Key.Z, key!.Value.Key);
        Assert.Equal(shift, key.Value.Modifiers.Contains(Key.Shift));
    }

    [Fact]
    public void NeedsNoKeyWhenThere() {
        Assert.Null(ZoomMeter.KeyFor(Zoom.Out, Zoom.Out));
    }

    private static Mat Frame(string fixture) {
        using var corner = Cv2.ImRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        var frame = new Mat(1080, 1920, MatType.CV_8UC3, Scalar.Black);
        corner.CopyTo(new Mat(frame, new Rect(1480, 800, corner.Width, corner.Height)));
        return frame;
    }
}
