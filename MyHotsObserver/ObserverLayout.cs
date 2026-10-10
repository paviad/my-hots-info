using OpenCvSharp;

namespace MyHotsObserver;

/// <summary>
/// Where the replay UI sits in the game window. Positions were measured at 1920×1080 and scale
/// with the window, which holds for 16:9 windows only.
/// </summary>
public sealed class ObserverLayout {
    private const int RefWidth = 1920;
    private const int RefHeight = 1080;

    private readonly double _sx;
    private readonly double _sy;

    public ObserverLayout(int width, int height) {
        if (Math.Abs((double)width / height - 16.0 / 9) > 0.01) {
            throw new NotSupportedException(
                $"The game window is {width}×{height}; only 16:9 windows are supported (e.g. 1920×1080).");
        }

        Width = width;
        Height = height;
        _sx = (double)width / RefWidth;
        _sy = (double)height / RefHeight;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>The replay panel's "elapsed / total" text, top right.</summary>
    public Rect PanelClock => Scale(new Rect(1552, 124, 200, 28));

    /// <summary>
    /// The elapsed time when Ctrl+T has collapsed the replay panel: lower than
    /// <see cref="PanelClock"/>, and without the total.
    /// </summary>
    public Rect CollapsedPanelClock => Scale(new Rect(1552, 160, 120, 28));

    /// <summary>The in-game timer under the score, top center.</summary>
    public Rect GameClock => Scale(new Rect(928, 126, 56, 20));

    /// <summary>The replay panel's playback speed ("Normal", ...), right of the clock.</summary>
    public Rect SpeedLabel => Scale(new Rect(1790, 128, 105, 26));

    /// <summary>Left end, right end and height of the replay panel's timeline bar.</summary>
    public (int Left, int Right, int Y) TimelineBar => (X(1555), X(1887), Y(175));

    /// <summary>
    /// The timeline pixel to click to land at or just before <paramref name="seconds"/>. A pixel
    /// spans several seconds of a long replay, and landing early is better than late.
    /// </summary>
    public int TimelineX(double seconds, double length) {
        var fraction = Math.Clamp(seconds / length, 0, 1);
        return (int)Math.Floor((TimelineOrigin + TimelineSpan * fraction) * _sx);
    }

    /// <summary>
    /// Where 0:00 is on the bar at 1920 wide, fitted to eight clicks: a click at x lands at
    /// (x - 1555.8) / 332 of the replay's length, each to within a second.
    /// </summary>
    private const double TimelineOrigin = 1555.8;

    private const double TimelineSpan = 332;

    private int X(int x) => (int)Math.Round(x * _sx);
    private int Y(int y) => (int)Math.Round(y * _sy);

    private Rect Scale(Rect r) => new(X(r.X), Y(r.Y), Math.Max(1, X(r.Width)), Math.Max(1, Y(r.Height)));
}
