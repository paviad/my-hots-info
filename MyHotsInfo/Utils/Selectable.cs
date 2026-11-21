using Microsoft.Maui.Handlers;

namespace MyHotsInfo.Utils;

/// <summary>
///     Makes the text of every label selectable with the mouse, so it can be copied. Selectable text takes the
///     click for itself, so a label that is tapped, or sits in something that is, has to stay out: a label with
///     a gesture recognizer of its own is left out automatically, and <c>utils:Selectable.Disabled="True"</c> on
///     an element leaves out every label inside it.
/// </summary>
public static class Selectable {
    public static readonly BindableProperty DisabledProperty = BindableProperty.CreateAttached(
        "Disabled", typeof(bool), typeof(Selectable), false);

    public static bool GetDisabled(BindableObject view) => (bool)view.GetValue(DisabledProperty);

    public static void SetDisabled(BindableObject view, bool value) => view.SetValue(DisabledProperty, value);

    /// <summary>Call once at startup, before any label is shown.</summary>
    public static void Register() {
#if WINDOWS
        LabelHandler.Mapper.AppendToMapping(nameof(Selectable), (handler, view) => {
            if (view is not Label label) {
                return;
            }

            handler.PlatformView.IsTextSelectionEnabled = !IsDisabled(label);

            // The label may not be in the tree yet, so its ancestors are checked again once it is.
            label.Loaded -= OnLoaded;
            label.Loaded += OnLoaded;
        });
#endif
    }

#if WINDOWS
    private static void OnLoaded(object? sender, EventArgs e) {
        if (sender is Label { Handler.PlatformView: Microsoft.UI.Xaml.Controls.TextBlock textBlock } label) {
            textBlock.IsTextSelectionEnabled = !IsDisabled(label);
        }
    }

    private static bool IsDisabled(Label label) {
        if (label.GestureRecognizers.Count > 0) {
            return true;
        }

        for (Element? element = label; element is not null; element = element.Parent) {
            if (GetDisabled(element)) {
                return true;
            }
        }

        return false;
    }
#endif
}
