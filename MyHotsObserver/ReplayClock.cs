using System.Text.RegularExpressions;
using MyReplayLibrary;
using OpenCvSharp;
using Tesseract;
using Rect = OpenCvSharp.Rect;

namespace MyHotsObserver;

/// <param name="Replay">Replay panel's elapsed time; null when the panel is hidden or a seek is running.</param>
/// <param name="Length">Replay panel's total length.</param>
/// <param name="Game">
/// The in-game timer: it counts down to the gates opening, then runs about 38s behind <paramref name="Replay"/>.
/// </param>
public sealed record ClockReading(TimeSpan? Replay, TimeSpan? Length, TimeSpan? Game) {
    /// <summary>
    /// Whether a replay is open: either clock shows. At the end the in-game timer is gone (the MVP
    /// screen) but the panel still reads "Replay Complete" with the length.
    /// </summary>
    public bool InReplay => Game is not null || Length is not null;
}

/// <summary>Reads the replay panel's clock and the in-game timer off a frame.</summary>
public sealed partial class ReplayClock : IDisposable {
    private readonly Lock _lock = new();
    private TesseractEngine? _digits;
    private TesseractEngine? _letters;

    public ClockReading Read(Mat frame, ObserverLayout layout) {
        var text = Ocr(frame, layout.PanelClock);
        var game = ParseGame(Ocr(frame, layout.GameClock));
        if (ParsePanel(text) is { } panel) {
            return new(panel.Elapsed, panel.Length, game);
        }

        // At the end the panel says "Replay Complete" and shows the length alone, in the same spot.
        return ParseGame(text) is { } end ? new(end, end, game) : new(null, null, game);
    }

    /// <summary>Parses the replay panel's "elapsed / total" text, e.g. <c>1:33 / 41:53</c>.</summary>
    public static (TimeSpan Elapsed, TimeSpan Length)? ParsePanel(string text) {
        var m = PanelRegex().Match(text);
        if (!m.Success) {
            return null;
        }

        var elapsed = Time(m.Groups["em"].Value, m.Groups["es"].Value);
        var length = Time(m.Groups["lm"].Value, m.Groups["ls"].Value);
        return elapsed <= length ? (elapsed, length) : null;
    }

    /// <summary>Parses the in-game timer, e.g. <c>00:55</c>.</summary>
    public static TimeSpan? ParseGame(string text) {
        var m = GameRegex().Match(text);
        return m.Success ? Time(m.Groups["m"].Value, m.Groups["s"].Value) : null;
    }

    /// <summary>
    /// Whether Ctrl+T has collapsed the replay panel to just the elapsed time, without the total
    /// and the timeline.
    /// </summary>
    public bool IsPanelCollapsed(Mat frame, ObserverLayout layout) =>
        ParsePanel(Ocr(frame, layout.PanelClock)) is null && ParseGame(Ocr(frame, layout.CollapsedPanelClock)) is not null;

    /// <summary>The replay panel's playback speed label, e.g. "Normal"; null when unreadable.</summary>
    public string? ReadSpeed(Mat frame, ObserverLayout layout) {
        var text = Ocr(frame, layout.SpeedLabel, letters: true);
        return text.Length >= 3 ? text : null;
    }

    public void Dispose() {
        _digits?.Dispose();
        _letters?.Dispose();
    }

    private static TimeSpan Time(string minutes, string seconds) =>
        TimeSpan.FromSeconds(int.Parse(minutes) * 60 + int.Parse(seconds));

    /// <summary>
    /// Light digits on a dark panel: enlarge, then Otsu to black on white, which is what Tesseract
    /// reads best.
    /// </summary>
    private string Ocr(Mat frame, Rect region, bool letters = false) {
        using var crop = new Mat(frame, region);
        using var gray = new Mat();
        Cv2.CvtColor(crop, gray, ColorConversionCodes.BGR2GRAY);
        using var big = new Mat();
        Cv2.Resize(gray, big, new Size(), 4, 4, InterpolationFlags.Cubic);
        using var bw = new Mat();
        Cv2.Threshold(big, bw, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
        Cv2.CopyMakeBorder(bw, bw, 20, 20, 20, 20, BorderTypes.Constant, Scalar.White);

        using var pix = Pix.LoadFromMemory(bw.ToBytes(".png"));
        lock (_lock) {
            var engine = letters
                ? _letters ??= CreateEngine("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz")
                : _digits ??= CreateEngine("0123456789:/");
            using var page = engine.Process(pix, PageSegMode.SingleLine);
            return page.GetText().Trim();
        }
    }

    private static TesseractEngine CreateEngine(string whitelist) {
        var engine = new TesseractEngine(AppPaths.TessDataPath, "eng");
        engine.SetVariable("tessedit_char_whitelist", whitelist);
        return engine;
    }

    [GeneratedRegex(@"(?<em>\d{1,2}):(?<es>[0-5]\d)\s*/\s*(?<lm>\d{1,2}):(?<ls>[0-5]\d)")]
    private static partial Regex PanelRegex();

    [GeneratedRegex(@"(?<m>\d{1,2}):(?<s>[0-5]\d)")]
    private static partial Regex GameRegex();
}
