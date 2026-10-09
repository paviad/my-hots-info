using System.Text.RegularExpressions;
using Tesseract;

namespace MyReplayLibrary;

public partial class Ocr(OcrOptions? options = null) : IDisposable {
    private TesseractEngine? _engine;
    private readonly Lock _ocrLock = new();
    private readonly OcrOptions _options = options ?? new OcrOptions();

    /// <summary>
    /// The ten slots to use from a screenshot's draft and loading readings: the reading with more
    /// names, with slots that came out empty or as a single noise character set to "". Slots 0-4
    /// are the left side of the screen, 5-9 the right. Across 289 real screenshots, draft and
    /// loading screens gave 7-10 such names and every other screen 0-2, so fewer than 5 means it
    /// was neither, and the result is empty.
    /// </summary>
    public static List<string> PickSlots(List<string> draft, List<string> loading) {
        var rc = ((List<string>[])[draft, loading])
            .Select(z => z.Select(w => w.Length >= 2 ? w : "").ToList())
            .MaxBy(z => z.Count(w => w != ""))!;
        return rc.Count(w => w != "") < 5 ? [] : rc;
    }

    /// <summary>The names in <see cref="PickSlots"/>, without the unread slots.</summary>
    public static List<string> PickNames(List<string> draft, List<string> loading) =>
        PickSlots(draft, loading).Where(w => w != "").ToList();

    public async Task<List<string>> OcrScreenshot(string ssName1, ScreenShotKind ssKind) {
        TaskCompletionSource<List<string>> tks = new();

        var t = new Thread(() => {
            // An exception escaping a raw thread terminates the process, so hand it to the caller.
            try {
                tks.SetResult(OcrOnThread(ssName1, ssKind));
            }
            catch (Exception e) {
                tks.SetException(e);
            }
        });

        t.Start();
        var rc = await tks.Task;
        return rc;
    }

    private List<string> OcrOnThread(string ssName1, ScreenShotKind ssKind) {
        _engine ??= new(AppPaths.TessDataPath, _options.Languages);

        var ssName = Path.GetFileName(ssName1);
        var path = Path.GetDirectoryName(ssName1);
        var latestSs = ssName1;

        List<byte[]> encoded = ssKind switch {
            ScreenShotKind.Draft => [
                GetBytes(latestSs, 12, 181),
                GetBytes(latestSs, 111, 349),
                GetBytes(latestSs, 12, 519),
                GetBytes(latestSs, 111, 689),
                GetBytes(latestSs, 12, 857),
                GetBytes(latestSs, 1786, 181, redTeam: true),
                GetBytes(latestSs, 1690, 349, redTeam: true),
                GetBytes(latestSs, 1786, 519, redTeam: true),
                GetBytes(latestSs, 1690, 689, redTeam: true),
                GetBytes(latestSs, 1786, 857, redTeam: true),
            ],
            ScreenShotKind.Loading => [
                GetBytes2(latestSs, 110, 269),
                GetBytes2(latestSs, 110, 401),
                GetBytes2(latestSs, 110, 533),
                GetBytes2(latestSs, 110, 665),
                GetBytes2(latestSs, 110, 797),
                GetBytes2(latestSs, 1605, 269, redTeam: true),
                GetBytes2(latestSs, 1605, 401, redTeam: true),
                GetBytes2(latestSs, 1605, 533, redTeam: true),
                GetBytes2(latestSs, 1605, 665, redTeam: true),
                GetBytes2(latestSs, 1605, 797, redTeam: true),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(ssKind), ssKind, null),
        };

        var names = new List<string>();

        for (var index = 0; index < encoded.Count; index++) {
            var enc = encoded[index];
            using var image = Pix.LoadFromMemory(enc);
            string[] text1;
            lock (_ocrLock) {
                using var page = _engine.Process(image, _options.PageSegMode);

                // Drop the empty pieces a stray leading/trailing symbol leaves behind, so they
                // don't get picked as the name below.
                text1 = MyRegex().Split(page.GetText().Trim()).Where(s => s != "").DefaultIfEmpty("").ToArray();
            }

            var chin2 = text1.Where(s => ChinCh().IsMatch(s)).ToArray();
            var chin1 = string.Join("", chin2);
            var text = ssKind switch {
                _ when chin1 is { Length: > 0 } => chin1,
                ScreenShotKind.Draft => text1[0],
                ScreenShotKind.Loading when index < 5 => text1[0],
                ScreenShotKind.Loading => text1[^1],
                _ => throw new ArgumentOutOfRangeException(nameof(ssKind), ssKind, null),
            };
            names.Add(text);
            continue;
        }

        return names;
    }

    private byte[] GetBytes(string latestSs, int cornerX, int cornerY, bool redTeam = false) {
        var img = new ImagePipeline(latestSs, cornerX, cornerY, redTeam);
        img.FromFile(121, 95);
        var angle = redTeam ? 31.1 : -31.1;
        var left = redTeam ? 0 : 13;
        var s = _options.Scale;
        if (_options.ScaleBeforeRotate) {
            img.Scale(s);
            img.Rotate(1, angle);
            img.Trim((int)(left * s), (int)(4 * s), 0, 0, (int)(110 * s), (int)(18 * s));
        }
        else {
            img.Rotate(1, angle);
            img.Trim(left, 4, 0, 0, 110, 18);
            img.Scale(s);
        }

        return Finish(img, redTeam ? 100 : 110);
    }

    private byte[] GetBytes2(string latestSs, int cornerX, int cornerY, bool redTeam = false) {
        var img = new ImagePipeline(latestSs, cornerX, cornerY, redTeam);
        img.FromFile(203, 28);
        img.Scale(_options.Scale);
        return Finish(img, redTeam ? 90 : 110);
    }

    private byte[] Finish(ImagePipeline img, int fixedThreshold) {
        img.Greyscale();
        switch (_options.Threshold) {
            case OcrThreshold.Fixed:
                img.Threshold(fixedThreshold);
                break;
            case OcrThreshold.Otsu:
                img.ThresholdOtsu();
                break;
            case OcrThreshold.None:
                img.Invert();
                break;
        }

        if (_options.Border > 0) {
            img.Pad(_options.Border);
        }

        return img.GetSaveImage("f");
    }

    [GeneratedRegex(@"[^\w\u4E00-\u9FA5]")]
    private static partial Regex MyRegex();

    public void Dispose() {
        _engine?.Dispose();
    }

    [GeneratedRegex(@"^[\u4E00-\u9FA5]+$")]
    private static partial Regex ChinCh();
}

public enum ScreenShotKind {
    Draft,
    Loading,
}
