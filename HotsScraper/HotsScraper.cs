using System.Text.RegularExpressions;
using CascScraper;

namespace HotsScraper;

public class HotsScraper {
    private string OutputPath;
    private readonly Scraper Scraper = new();
    private readonly string BadChars = Regex.Escape("- '‘’:,!.\"”?()");

    public async Task Scrape(string outputPath) {
        OutputPath = outputPath;
        Scraper.FillTalentInfoList();
        SaveImages();
        GenerateOutputFiles();
        GenerateActorUnitFiles();
    }

    private void GenerateActorUnitFiles() {
        var actorDir = Path.Combine(OutputPath, "ActorUnits");
        if (!Directory.Exists(actorDir)) {
            Directory.CreateDirectory(actorDir);
        }

        foreach (var actorUnit in Scraper.ActorUnits) {
            var unitName = actorUnit.Key;
            var pngPath0 = Path.Combine(actorDir, "0" + unitName + ".png");
            var pngPath1 = Path.Combine(actorDir, "1" + unitName + ".png");
            try {
                var ddsImage = new DDSImage(actorUnit.Value.MinimapIcon, true);
                ddsImage.Save(pngPath0);
                ddsImage.Save(pngPath1);
            }
            catch (Exception e) {
                File.WriteAllText(pngPath0 + "_error.txt", e.ToString());
                File.WriteAllText(pngPath1 + "_error.txt", e.ToString());
            }
        }
    }

    private void GenerateOutputFiles() {
        var xmlText = Scraper.GenerateTalentInfoXml();
        var talentXmlFile = Path.Combine(OutputPath, "talentInfo.xml");
        if (File.Exists(talentXmlFile)) {
            File.Delete(talentXmlFile);
        }

        File.WriteAllText(talentXmlFile, xmlText);

        var csvText = Scraper.GenerateTalentInfoCsv();
        var talentCsvFile = Path.Combine(OutputPath, "talentInfo.csv");
        if (File.Exists(talentCsvFile)) {
            File.Delete(talentCsvFile);
        }

        File.WriteAllText(talentCsvFile, csvText);

        var sqlText = Scraper.GenerateTalentInfoSql();
        var sqlFileName = Path.Combine(OutputPath, "talentInfo.sql");
        if (File.Exists(sqlFileName)) {
            File.Delete(sqlFileName);
        }

        File.WriteAllText(sqlFileName, sqlText);
    }

    private string PrepareForImageUrl(string input) {
        return Regex.Replace(input, $"[{BadChars}]", "");
    }

    private void SaveImages() {
        var targetDir = Path.Combine(OutputPath, "TalentImages");
        if (!Directory.Exists(targetDir)) {
            Directory.CreateDirectory(targetDir);
        }

        foreach (var talentInfo in Scraper.TalentInfoList) {
            var ddsBytes = talentInfo.Image;
            var ddsImage = new DDSImage(ddsBytes, true);
            var talentName = talentInfo.TalentName;
            var heroName = talentInfo.HeroName;
            var rawFileName = $"{heroName}{talentName}";
            var imageName = $"{PrepareForImageUrl(rawFileName)}.png";
            var path = Path.Combine(targetDir, imageName);
            ddsImage.Save(path);
        }
    }
}
