using System.Text.RegularExpressions;
using Tesseract;

namespace MyReplayLibrary;

public partial class Ocr : IDisposable {
    private TesseractEngine? _engine;

    public async Task<List<string>> OcrScreenshot(string ssName1) {
        TaskCompletionSource<List<string>> tks = new();

        var t = new Thread(() => {
            _engine ??= new(@"c:\myprojects\myhotsinfo\ocr\tessdata", "eng+ces+por+rus+hun");

            var ssName = Path.GetFileName(ssName1);
            var path = Path.GetDirectoryName(ssName1);
            var latestSs = ssName1;

            List<byte[]> encoded = [
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
            ];

            var names = new List<string>();

            foreach (var enc in encoded) {
                using var image = Pix.LoadFromMemory(enc);
                using var page = _engine.Process(image);
                var text = MyRegex().Split(page.GetText().Trim())[0];
                names.Add(text);
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
        img.FromFile();
        var angle = redTeam ? 31.1 : -31.1;
        img.Rotate(1, angle);
        var left = redTeam ? 0 : 13;
        img.Trim(left, 4, 0, 0);
        img.Scale(4);
        img.Greyscale();
        img.Threshold();
        var rc = img.GetSaveImage("f");
        return rc;
    }

    [GeneratedRegex(@"\W")]
    private static partial Regex MyRegex();

    public void Dispose() {
        _engine?.Dispose();
    }
}
