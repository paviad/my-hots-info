using Tesseract;

namespace MyReplayLibrary;

public enum OcrThreshold {
    /// <summary>Fixed per-team black/white cutoffs tuned by hand.</summary>
    Fixed,

    /// <summary>Black/white cutoff chosen per crop (Otsu).</summary>
    Otsu,

    /// <summary>No black/white conversion; inverted greyscale.</summary>
    None,
}

/// <summary>How screenshot crops are prepared and read. The defaults are what the app uses.</summary>
public record OcrOptions {
    /// <summary>
    /// Tesseract models to load. Measured on 210 screenshots: adding spa read more names right,
    /// while chi_sim/chi_tra read none of the 12 Chinese names correctly and made OCR 2.4x slower.
    /// </summary>
    public string Languages { get; init; } = "eng+ces+por+rus+hun+spa";

    /// <summary>Tesseract layout analysis; null leaves the engine default.</summary>
    public PageSegMode? PageSegMode { get; init; }

    public double Scale { get; init; } = 4;

    /// <summary>
    /// Draft names: enlarge before rotating them level, not after, so the rotation doesn't blur
    /// the small original. Measured: +17 names read exactly, -14 wrong, on 210 screenshots.
    /// </summary>
    public bool ScaleBeforeRotate { get; init; } = true;

    public OcrThreshold Threshold { get; init; } = OcrThreshold.Fixed;

    /// <summary>White margin added around each crop, in pixels of the final image.</summary>
    public int Border { get; init; }
}
