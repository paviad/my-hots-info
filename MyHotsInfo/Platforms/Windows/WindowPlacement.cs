using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace MyHotsInfo.WinUI;

/// <summary>Where the main window opens: 20% taller than the WinUI default, centered on the
/// monitor just left of the primary one when there is one, otherwise on its own monitor.</summary>
public static class WindowPlacement {
    public static void Place(Microsoft.UI.Xaml.Window window) {
        var appWindow = window.AppWindow;
        var display = LeftOfPrimary() ?? DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var work = display.WorkArea;

        var width = Math.Min(appWindow.Size.Width, work.Width);
        var height = Math.Min((int)(appWindow.Size.Height * 1.2), work.Height);
        appWindow.MoveAndResize(new RectInt32(
            work.X + (work.Width - width) / 2, work.Y + (work.Height - height) / 2, width, height));
    }

    private static DisplayArea? LeftOfPrimary() {
        var primary = DisplayArea.Primary.OuterBounds;
        DisplayArea? best = null;

        // Indexed rather than enumerated: enumerating FindAll()'s result has thrown in some
        // Windows App SDK versions.
        var all = DisplayArea.FindAll();
        for (var i = 0; i < all.Count; i++) {
            var bounds = all[i].OuterBounds;
            if (bounds.X + bounds.Width <= primary.X && (best is null || bounds.X > best.OuterBounds.X)) {
                best = all[i];
            }
        }

        return best;
    }
}
