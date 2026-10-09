using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyReplayLibrary;
using MyReplayLibrary.Data;
using Tesseract;

namespace MyHotsCli;

/// <summary>
/// Measures screenshot OCR against ground truth: the rosters of the replays the screenshots
/// were taken in. <see cref="BuildTruth"/> matches screenshots to replays once; <see cref="Run"/>
/// then scores any number of <see cref="OcrOptions"/> variants against that file.
/// </summary>
public class OcrEval(string screenshotsDir, string truthPath, int parallel) {
    public record TruthEntry(string File, int? ReplayId, List<string>? Roster);

    /// <summary>Option tweaks by name; a variant is one or more of these joined with '+'.</summary>
    private static readonly Dictionary<string, Func<OcrOptions, OcrOptions>> Tweaks = new() {
        ["current"] = o => o,
        ["psm7"] = o => o with { PageSegMode = PageSegMode.SingleLine },
        ["psm8"] = o => o with { PageSegMode = PageSegMode.SingleWord },
        ["psm13"] = o => o with { PageSegMode = PageSegMode.RawLine },
        ["border10"] = o => o with { Border = 10 },
        ["border20"] = o => o with { Border = 20 },
        ["rotatefirst"] = o => o with { ScaleBeforeRotate = false },
        ["otsu"] = o => o with { Threshold = OcrThreshold.Otsu },
        ["grey"] = o => o with { Threshold = OcrThreshold.None },
        ["scale2"] = o => o with { Scale = 2 },
        ["scale3"] = o => o with { Scale = 3 },
        ["scale5"] = o => o with { Scale = 5 },
        ["scale6"] = o => o with { Scale = 6 },
    };

    public static IEnumerable<string> TweakNames => Tweaks.Keys;

    public static OcrOptions ParseVariant(string variant) =>
        variant.Split('+').Aggregate(new OcrOptions(), (o, t) =>
            Tweaks.TryGetValue(t, out var f) ? f(o) : throw new ArgumentException($"Unknown OCR tweak '{t}'"));

    /// <summary>
    /// OCRs every screenshot with the app's settings and matches it to the replay, within 6 hours,
    /// sharing the most names with it (at least 3). Unmatched screenshots are treated as not a
    /// draft or loading screen.
    /// </summary>
    public async Task BuildTruth(ReplayDbContext dc) {
        var files = Directory.GetFiles(screenshotsDir, "*.jpg").Order().ToList();
        var read = await OcrAll(files, new OcrOptions());
        var times = files.ToDictionary(f => f, ScreenshotTimeUtc);
        var from = times.Values.Min().AddHours(-6);
        var to = times.Values.Max().AddHours(6);
        var replays = await dc.Replays
            .Where(r => r.TimestampReplay >= from && r.TimestampReplay <= to)
            .Select(r => new {
                r.Id, r.TimestampReplay, Roster = r.ReplayCharacters.Select(c => c.Player.Name).ToList(),
            })
            .ToListAsync();

        var truth = new List<TruthEntry>();
        foreach (var f in files) {
            var (draft, loading) = read[Path.GetFileName(f)];
            var names = Ocr.PickNames(draft, loading)
                .Select(n => n.ToLowerInvariant()).ToHashSet();
            var best = replays
                .Where(r => Math.Abs((r.TimestampReplay - times[f]).TotalHours) < 6)
                .Select(r => (r, Overlap: r.Roster.Count(n => names.Contains(n.ToLowerInvariant()))))
                .OrderByDescending(z => z.Overlap)
                .FirstOrDefault();
            truth.Add(best.Overlap >= 3
                ? new TruthEntry(Path.GetFileName(f), best.r.Id, best.r.Roster)
                : new TruthEntry(Path.GetFileName(f), null, null));
        }

        await File.WriteAllTextAsync(truthPath, JsonSerializer.Serialize(truth, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{truth.Count(t => t.ReplayId is not null)} game screenshots, " +
                          $"{truth.Count(t => t.ReplayId is null)} others -> {truthPath}");
    }

    /// <summary>
    /// Scores each variant. <paramref name="half"/> "tune" uses even-numbered screenshots, "check"
    /// odd-numbered ones, so a variant picked on one half can be confirmed on the other.
    /// </summary>
    public async Task Run(IEnumerable<string> variants, string half) {
        var truth = JsonSerializer.Deserialize<List<TruthEntry>>(await File.ReadAllTextAsync(truthPath))!;
        truth = [.. truth.Where((_, i) => half switch {
            "tune" => i % 2 == 0, "check" => i % 2 == 1, "all" => true,
            _ => throw new ArgumentException($"Unknown half '{half}'"),
        })];
        var games = truth.Count(t => t.ReplayId is not null);
        Console.WriteLine($"{half}: {truth.Count} screenshots, {games} of them draft/loading screens");
        Console.WriteLine();
        Console.WriteLine("variant                          | right | accent-only | wrong | lost slots | draft right | loading right | false shots | time");
        Console.WriteLine("---------------------------------|-------|-------------|-------|------------|-------------|---------------|-------------|------");
        List<string> falseShots = [];
        foreach (var variant in variants) {
            var options = ParseVariant(variant);
            var sw = Stopwatch.StartNew();
            var read = await OcrAll(truth.Select(t => Path.Combine(screenshotsDir, t.File)).ToList(), options);
            var s = Score(truth, read);
            Console.WriteLine($"{variant,-33}| {s.Right,5} | {s.FoldOnly,11} | {s.Wrong,5} | {s.Lost,10} | " +
                              $"{Pct(s.DraftRight, s.DraftNames),11} | {Pct(s.LoadingRight, s.LoadingNames),13} | " +
                              $"{s.FalseShots.Count,11} | {sw.Elapsed.TotalSeconds,4:F0}s");
            falseShots.AddRange(s.FalseShots.Select(f => $"{variant}: {f}"));
        }

        foreach (var f in falseShots) {
            Console.WriteLine($"false shot  {f}");
        }

        Console.WriteLine();
        Console.WriteLine("right: read exactly. accent-only: differs only in accents/lookalike letters, which the name");
        Console.WriteLine("lookup fixes. wrong: anything else. lost slots: names dropped from draft/loading screens.");
        Console.WriteLine("false shots: other screenshots that produced names anyway.");
    }

    private record Scores(
        int Right, int FoldOnly, int Wrong, int Lost, int DraftRight, int DraftNames, int LoadingRight,
        int LoadingNames, List<string> FalseShots);

    private static Scores Score(List<TruthEntry> truth, Dictionary<string, (List<string> Draft, List<string> Loading)> read) {
        int right = 0, foldOnly = 0, wrong = 0, lost = 0, draftRight = 0, draftNames = 0, loadingRight = 0, loadingNames = 0;
        List<string> falseShots = [];
        foreach (var t in truth) {
            var (draft, loading) = read[t.File];
            var names = Ocr.PickNames(draft, loading);
            if (t.Roster is null) {
                if (names.Count > 0) {
                    falseShots.Add($"{t.File} -> {string.Join(", ", names)}");
                }

                continue;
            }

            lost += t.Roster.Count - names.Count;
            var isDraft = draft.Count(w => w.Length >= 2) >= loading.Count(w => w.Length >= 2);
            var folded = t.Roster.Select(NameMatcher.Fold).ToHashSet();
            foreach (var n in names) {
                var exact = t.Roster.Any(r => r.Equals(n, StringComparison.OrdinalIgnoreCase));
                if (exact) {
                    right++;
                }
                else if (folded.Contains(NameMatcher.Fold(n))) {
                    foldOnly++;
                }
                else {
                    wrong++;
                }

                if (isDraft) {
                    draftNames++;
                    draftRight += exact ? 1 : 0;
                }
                else {
                    loadingNames++;
                    loadingRight += exact ? 1 : 0;
                }
            }
        }

        return new Scores(right, foldOnly, wrong, lost, draftRight, draftNames, loadingRight, loadingNames, falseShots);
    }

    private static string Pct(int n, int d) => d == 0 ? "-" : $"{100.0 * n / d:F1}%";

    /// <summary>Both readings of every file, by file name, on <c>parallel</c> engines at once.</summary>
    private async Task<Dictionary<string, (List<string> Draft, List<string> Loading)>> OcrAll(
        List<string> files, OcrOptions options) {
        var pool = new ConcurrentBag<Ocr>(Enumerable.Range(0, parallel).Select(_ => new Ocr(options)));
        var result = new ConcurrentDictionary<string, (List<string>, List<string>)>();
        try {
            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = parallel }, async (f, _) => {
                pool.TryTake(out var ocr);
                try {
                    List<string> draft = [], loading = [];
                    try {
                        draft = await ocr!.OcrScreenshot(f, ScreenShotKind.Draft);
                        loading = await ocr.OcrScreenshot(f, ScreenShotKind.Loading);
                    }
                    catch (Exception e) {
                        Console.Error.WriteLine($"{Path.GetFileName(f)}: {e.Message}");
                    }

                    result[Path.GetFileName(f)] = (draft, loading);
                }
                finally {
                    pool.Add(ocr!);
                }
            });
        }
        finally {
            foreach (var ocr in pool) {
                ocr.Dispose();
            }
        }

        return new Dictionary<string, (List<string> Draft, List<string> Loading)>(result);
    }

    /// <summary>Screenshots are named after the local time they were taken, e.g. "Screenshot2026-05-08 20_14_19.jpg".</summary>
    private static DateTime ScreenshotTimeUtc(string file) {
        var stem = Path.GetFileNameWithoutExtension(file)["Screenshot".Length..];
        return DateTime.ParseExact(stem, "yyyy-MM-dd HH_mm_ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal)
            .ToUniversalTime();
    }
}
