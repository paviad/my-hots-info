using System.Globalization;
using MyHotsInfo.Utils;

namespace MyHotsInfo.Controls;

/// <summary>
/// A hero portrait ringed by the games played on it: one segment per game, lit for a win and dim
/// for a loss, so "2 of 3" reads at a glance and a single game visibly means little. Past
/// <see cref="HeroRingDrawable.MaxSegments"/> games the segments get too short to count, so the
/// ring becomes one arc lit for the share won, with the game count in a badge.
/// </summary>
public class HeroRing : Grid {
    public static readonly BindableProperty HeroProperty = BindableProperty.Create(
        nameof(Hero), typeof(string), typeof(HeroRing), propertyChanged: (b, _, _) => ((HeroRing)b).Update());

    public static readonly BindableProperty GamesProperty = BindableProperty.Create(
        nameof(Games), typeof(int), typeof(HeroRing), propertyChanged: (b, _, _) => ((HeroRing)b).Update());

    public static readonly BindableProperty WinsProperty = BindableProperty.Create(
        nameof(Wins), typeof(int), typeof(HeroRing), propertyChanged: (b, _, _) => ((HeroRing)b).Update());

    private const double Size = 28;
    private const double PortraitSize = 20;

    private static readonly HeroToImageConverter PortraitConverter = new();

    private readonly Image _portrait;
    private readonly GraphicsView _ring;
    private readonly HeroRingDrawable _drawable = new();

    public HeroRing() {
        WidthRequest = Size;
        HeightRequest = Size;
        _portrait = new Image {
            WidthRequest = PortraitSize,
            HeightRequest = PortraitSize,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
        _ring = new GraphicsView { Drawable = _drawable, BackgroundColor = Colors.Transparent, InputTransparent = true };
        Children.Add(_portrait);
        Children.Add(_ring);
    }

    public string? Hero {
        get => (string?)GetValue(HeroProperty);
        set => SetValue(HeroProperty, value);
    }

    public int Games {
        get => (int)GetValue(GamesProperty);
        set => SetValue(GamesProperty, value);
    }

    public int Wins {
        get => (int)GetValue(WinsProperty);
        set => SetValue(WinsProperty, value);
    }

    private void Update() {
        _portrait.Source = PortraitConverter.Convert(Hero, typeof(ImageSource), "circle", CultureInfo.InvariantCulture) as string;
        _drawable.Games = Games;
        _drawable.Wins = Wins;
        _ring.Invalidate();
    }
}

public class HeroRingDrawable : IDrawable {
    public const int MaxSegments = 6;

    private const float Stroke = 2.5f;
    private const float GapDegrees = 14;

    // Lit vs dim differ in brightness, not just hue, so they read without red/green vision.
    private static readonly Color Won = Color.FromArgb("#3FB950");
    private static readonly Color Lost = Color.FromArgb("#4A4A4A");

    public int Games { get; set; }
    public int Wins { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect) {
        if (Games <= 0) {
            return;
        }

        var inset = Stroke / 2 + 0.5f;
        var r = new RectF(dirtyRect.X + inset, dirtyRect.Y + inset, dirtyRect.Width - 2 * inset, dirtyRect.Height - 2 * inset);
        canvas.StrokeSize = Stroke;
        canvas.StrokeLineCap = LineCap.Butt;

        if (Games == 1) {
            canvas.StrokeColor = Wins > 0 ? Won : Lost;
            canvas.DrawEllipse(r);
            return;
        }

        if (Games <= MaxSegments) {
            // Clockwise from 12 o'clock, wins first. Angles are counter-clockwise from 3 o'clock.
            var segment = 360f / Games;
            for (var i = 0; i < Games; i++) {
                canvas.StrokeColor = i < Wins ? Won : Lost;
                var start = 90 - i * segment - GapDegrees / 2;
                var end = 90 - (i + 1) * segment + GapDegrees / 2;
                canvas.DrawArc(r, start, end, true, false);
            }

            return;
        }

        canvas.StrokeColor = Lost;
        canvas.DrawEllipse(r);
        if (Wins > 0) {
            canvas.StrokeColor = Won;
            if (Wins >= Games) {
                canvas.DrawEllipse(r);
            }
            else {
                canvas.DrawArc(r, 90, 90 - 360f * Wins / Games, true, false);
            }
        }

        // Game count badge, bottom right
        const float badge = 11;
        var b = new RectF(dirtyRect.Right - badge, dirtyRect.Bottom - badge, badge, badge);
        canvas.FillColor = Color.FromArgb("#202020");
        canvas.FillEllipse(b);
        canvas.FontColor = Colors.White;
        canvas.FontSize = 8;
        canvas.DrawString(Games > 99 ? "99" : Games.ToString(CultureInfo.InvariantCulture), b,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
