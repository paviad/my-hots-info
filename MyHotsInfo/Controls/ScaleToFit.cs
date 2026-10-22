using Microsoft.Maui.Layouts;

namespace MyHotsInfo.Controls;

/// <summary>
/// Lays out its one child at the size the child asks for, then scales it to fit the space it's
/// given, so a layout built from fixed sizes (like the score screen) shrinks or grows as a whole
/// instead of being cut off or reflowed.
/// </summary>
public class ScaleToFit : Layout {
    protected override ILayoutManager CreateLayoutManager() => new Manager(this);

    private sealed class Manager(ScaleToFit layout) : ILayoutManager {
        public Size Measure(double widthConstraint, double heightConstraint) {
            if (layout.Count == 0) {
                return Size.Zero;
            }

            var natural = layout[0].Measure(double.PositiveInfinity, double.PositiveInfinity);
            if (natural.Width <= 0 || natural.Height <= 0) {
                return Size.Zero;
            }

            var scale = Math.Min(widthConstraint / natural.Width, heightConstraint / natural.Height);
            if (double.IsInfinity(scale)) {
                scale = 1;
            }

            return new Size(natural.Width * scale, natural.Height * scale);
        }

        public Size ArrangeChildren(Rect bounds) {
            if (layout.Count == 0) {
                return bounds.Size;
            }

            var child = layout[0];
            var natural = child.DesiredSize;
            if (natural.Width <= 0 || natural.Height <= 0) {
                return bounds.Size;
            }

            child.Arrange(new Rect(bounds.X, bounds.Y, natural.Width, natural.Height));
            if (child is VisualElement element) {
                element.AnchorX = 0;
                element.AnchorY = 0;
                element.Scale = Math.Min(bounds.Width / natural.Width, bounds.Height / natural.Height);
            }

            return bounds.Size;
        }
    }
}
