using OpenCvSharp;

namespace MyHotsObserver;

/// <summary>
/// Reads the camera's zoom off the minimap, where a light gray outline shows what the camera sees.
/// The outline is wider the farther out the camera is: on Battlefield of Eternity 70 px at the
/// default zoom, 84 out (Z) and 97 far (Shift+Z). Minimaps are drawn at different scales, so the
/// default width is measured per map (see <see cref="ZoomCalibration"/>); the ratios hold on all.
/// </summary>
public static class ZoomMeter {
    public const double OutRatio = 1.2;
    public const double FarRatio = 1.39;

    /// <summary>An outline edge is a long run; icon borders make short ones.</summary>
    private const int MinEdge = 25;

    /// <summary>
    /// The outline's width at 1920 wide. Its top and bottom edges are thin horizontal lines that
    /// start and end at the same x, about 0.68 of the width apart; a stray line (the game draws a
    /// hairline across the screen at y 901) has no such partner. Gaps where icons cross an edge are
    /// bridged, and only the span both edges share counts, since an icon touching an edge stretches it. Null when no pair is found, e.g. with an
    /// edge fully covered by icons.
    /// </summary>
    public static double? ViewportWidth(Mat frame, ObserverLayout layout) {
        using var area = new Mat(frame, layout.MinimapArea);
        var runs = Edges(area, (int)Math.Round(MinEdge * layout.Width / 1920.0));
        double? best = null;
        foreach (var top in runs) {
            foreach (var bottom in runs) {
                var width = Math.Min(top.Right, bottom.Right) - Math.Max(top.Left, bottom.Left) + 1;
                var height = bottom.Y - top.Y;
                if (height >= 0.55 * width && height <= 0.85 * width
                    && Math.Abs(top.Left - bottom.Left) <= 4 && Math.Abs(top.Right - bottom.Right) <= 4) {
                    best = Math.Max(best ?? 0, width);
                }
            }
        }

        return best * 1920.0 / layout.Width;
    }

    /// <summary>Thin horizontal lines at least <paramref name="minLength"/> long, gaps of up to 4 px bridged.</summary>
    private static List<(int Y, int Left, int Right)> Edges(Mat area, int minLength) {
        var indexer = area.GetGenericIndexer<Vec3b>();
        int rows = area.Rows, cols = area.Cols;
        var runs = new List<(int Y, int Left, int Right)>();
        for (var y = Reach; y < rows - Reach; y++) {
            int start = -1, last = -1;
            for (var x = 0; x <= cols; x++) {
                var on = x < cols && IsRidge(indexer[y, x], indexer[y - Reach, x], indexer[y + Reach, x]);
                if (start >= 0 && (x == cols || (!on && x - last > 4))) {
                    if (last - start + 1 >= minLength) {
                        runs.Add((y, start, last));
                    }

                    start = -1;
                }

                if (on) {
                    if (start < 0) {
                        start = x;
                    }

                    last = x;
                }
            }
        }

        return runs;
    }

    /// <summary>The outline is about 2 px thick; compare with the pixels this far above and below.</summary>
    private const int Reach = 3;

    /// <summary>
    /// Whether <paramref name="p"/> sits on a thin line: in each channel it differs from both
    /// <paramref name="above"/> and <paramref name="below"/> the same way, by 40 levels in all.
    /// The outline is translucent gray, so over dark ground it lightens and over bright ground it
    /// pulls the colors toward gray; either way it's a ridge between the rows around it.
    /// </summary>
    private static bool IsRidge(Vec3b p, Vec3b above, Vec3b below) {
        var sum = 0;
        for (var c = 0; c < 3; c++) {
            int a = p[c] - above[c], b = p[c] - below[c];
            if (Math.Sign(a) != Math.Sign(b)) {
                if (Math.Abs(a) > 10 && Math.Abs(b) > 10) {
                    return false;
                }

                continue;
            }

            sum += Math.Min(Math.Abs(a), Math.Abs(b));
        }

        return sum >= 40;
    }

    /// <summary>The zoom whose outline width is nearest <paramref name="width"/>, given the map's default width.</summary>
    public static Zoom Classify(double width, double defaultWidth) {
        var ratio = width / defaultWidth;
        return ratio < (1 + OutRatio) / 2 ? Zoom.Default : ratio < (OutRatio + FarRatio) / 2 ? Zoom.Out : Zoom.Far;
    }

    /// <summary>
    /// The key that takes the camera from <paramref name="from"/> to <paramref name="to"/>: Z goes
    /// to out, or back to the default from out; Shift+Z likewise for far. (Shift+Z, Z, Z therefore
    /// always lands on the default.) Null when it's already there.
    /// </summary>
    public static (Key Key, Key[] Modifiers)? KeyFor(Zoom from, Zoom to) => (from, to) switch {
        _ when from == to => null,
        (Zoom.Out, Zoom.Default) or (_, Zoom.Out) => (Key.Z, []),
        _ => (Key.Z, [Key.Shift]),
    };
}
