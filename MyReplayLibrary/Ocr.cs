using System.Text.RegularExpressions;
using Tesseract;

namespace MyReplayLibrary;

public partial class Ocr : IDisposable {
    private TesseractEngine? _engine;
    private readonly Lock _ocrLock = new();

    public async Task<List<string>> OcrScreenshot(string ssName1, ScreenShotKind ssKind) {
        TaskCompletionSource<List<string>> tks = new();

        var t = new Thread(() => {
            _engine ??= new(@"c:\myprojects\myhotsinfo\ocr\tessdata", "eng+ces+por+rus+hun+chi_sim+chi_tra");

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
                    using var page = _engine.Process(image);

                    text1 = MyRegex().Split(page.GetText().Trim());
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

            tks.SetResult(names);
            //return names;
        });

        t.Start();
        var rc = await tks.Task;
        return rc;
    }

    private static byte[] GetBytes(string latestSs, int cornerX, int cornerY, bool redTeam = false) {
        var img = new ImagePipeline(latestSs, cornerX, cornerY, redTeam);
        img.FromFile(121, 95);
        var angle = redTeam ? 31.1 : -31.1;
        img.Rotate(1, angle);
        var left = redTeam ? 0 : 13;
        img.Trim(left, 4, 0, 0, 110, 18);
        img.Scale(4);
        img.Greyscale();
        img.Threshold(redTeam ? 100 : 110);
        var rc = img.GetSaveImage("f");
        return rc;
    }

    private static byte[] GetBytes2(string latestSs, int cornerX, int cornerY, bool redTeam = false) {
        var img = new ImagePipeline(latestSs, cornerX, cornerY, redTeam);
        img.FromFile(203, 28);
        img.Scale(4);
        img.Greyscale();
        img.Threshold(redTeam ? 90 : 110);
        var rc = img.GetSaveImage("f");
        return rc;
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
