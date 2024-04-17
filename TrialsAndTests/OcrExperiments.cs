using MyReplayLibrary;

namespace TrialsAndTests;

public class OcrExperiments {

    [Theory]
    [InlineData("Screenshot2024-03-30 20_38_10.jpg")]
    [InlineData("Screenshot2024-03-27 22_39_10.jpg")]
    [InlineData("Screenshot2024-03-26 22_24_23.jpg")]
    [InlineData("Screenshot2020-11-14 17_47_05.jpg")]
    [InlineData("Screenshot2020-09-12 20_25_56.jpg")]
    [InlineData("Screenshot2020-09-12 18_34_57.jpg")]
    [InlineData("Screenshot2024-03-31 19_03_10.jpg")]
    public async Task TryOcrDraft(string fn) {
        var basePath = @"C:\Users\USER\Documents\Heroes of the Storm\Screenshots";
        var ocr = new Ocr();
        var rc = await ocr.OcrScreenshot(Path.Combine(basePath, fn), ScreenShotKind.Draft);
    }

    [Theory]
    [InlineData("Screenshot2024-03-29 11_49_03.jpg")]
    [InlineData("Screenshot2024-03-29 11_48_55.jpg")]
    [InlineData("Screenshot2024-03-13 19_53_48.jpg")]
    [InlineData("Screenshot2023-10-01 22_17_52.jpg")]
    [InlineData("Screenshot2020-09-12 20_25_56.jpg")]
    [InlineData("Screenshot2022-07-08 13_41_21.jpg")]
    [InlineData("Screenshot2021-01-14 20_54_02.jpg")]
    public async Task TryOcrLoading(string fn) {
        var basePath = @"C:\Users\USER\Documents\Heroes of the Storm\Screenshots";
        var ocr = new Ocr();
        var rc = await ocr.OcrScreenshot(Path.Combine(basePath, fn), ScreenShotKind.Loading);
    }
}
