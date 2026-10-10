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

    /// <summary>The in-game timer under the score, top center.</summary>
    public Rect GameClock => Scale(new Rect(928, 126, 56, 20));

    /// <summary>Left end, right end and height of the replay panel's timeline bar.</summary>
    public (int Left, int Right, int Y) TimelineBar => (X(1555), X(1887), Y(175));

    private int X(int x) => (int)Math.Round(x * _sx);
    private int Y(int y) => (int)Math.Round(y * _sy);

    private Rect Scale(Rect r) => new(X(r.X), Y(r.Y), Math.Max(1, X(r.Width)), Math.Max(1, Y(r.Height)));
}
