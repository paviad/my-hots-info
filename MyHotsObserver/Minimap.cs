using MyReplayQuery.Events;

namespace MyHotsObserver;

/// <summary>
/// Where a map position is on the minimap, so a click there points the camera at it. Each map's
/// minimap has its own scale, so maps are calibrated one at a time; an uncalibrated map has no
/// <see cref="Minimap"/> and the camera plans fall back to the game's own following.
/// </summary>
/// <remarks>
/// Pixels at 1920×1080: x = <see cref="X0"/> + <see cref="ScaleX"/> × map x, and
/// y = <see cref="Y0"/> − <see cref="ScaleY"/> × map y (map y points up).
/// </remarks>
public sealed record Minimap(double X0, double Y0, double ScaleX, double ScaleY) {
    /// <summary>
    /// Measured from the cores (and on Dragon Shire the shrines) on the minimap against their
    /// positions in the replay, then checked by clicking where heroes were.
    /// </summary>
    private static readonly Dictionary<string, Minimap> Calibrated = new(StringComparer.OrdinalIgnoreCase) {
        ["Dragon Shire"] = new(1484.4, 1088.3, 1.686, 1.731),
        // Cores only (one row), so y uses the x scale, which on Dragon Shire was within 3%.
        ["Tomb of the Spider Queen"] = new(1506.6, 1026.0, 1.487, 1.487),
        // Haunted Mines has a second, underground layer with its own minimap view; one mapping
        // can't serve both, so it stays uncalibrated.
    };

    public static Minimap? For(string map) => Calibrated.GetValueOrDefault(map);

    /// <summary>The minimap pixel to click to look at <paramref name="point"/>.</summary>
    public (int X, int Y) Pixel(MapPoint point, ObserverLayout layout) {
        var x = X0 + ScaleX * point.X;
        var y = Y0 - ScaleY * point.Y;
        return ((int)Math.Round(x * layout.Width / 1920.0), (int)Math.Round(y * layout.Height / 1080.0));
    }
}
