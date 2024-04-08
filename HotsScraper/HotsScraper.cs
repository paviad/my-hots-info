using System.Text.RegularExpressions;
using CascScraperCore;
using MyHotsInfo.Extensions;

namespace HotsScraper;

public partial class HotsScraper {
    private string _outputPath = null!;
    private readonly Scraper _scraper = new();

    public void Scrape(string outputPath) {
        _outputPath = outputPath;
        _scraper.FillTalentInfoList();
        SaveMapImages();
        SaveTalentImages();
        SavePortraitImages();
        GenerateOutputFiles();
        GenerateActorUnitFiles();
    }

    private void GenerateActorUnitFiles() {
        var actorDir = Path.Combine(_outputPath, "ActorUnits");
        if (!Directory.Exists(actorDir)) {
            Directory.CreateDirectory(actorDir);
        }

        foreach (var actorUnit in _scraper.ActorUnits) {
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
        var xmlText = _scraper.GenerateTalentInfoXml();
        var talentXmlFile = Path.Combine(_outputPath, "talentInfo.xml");
        if (File.Exists(talentXmlFile)) {
            File.Delete(talentXmlFile);
        }

        File.WriteAllText(talentXmlFile, xmlText);

        var csvText = _scraper.GenerateTalentInfoCsv();
        var talentCsvFile = Path.Combine(_outputPath, "talentInfo.csv");
        if (File.Exists(talentCsvFile)) {
            File.Delete(talentCsvFile);
        }

        File.WriteAllText(talentCsvFile, csvText);

        var sqlText = _scraper.GenerateTalentInfoSql();
        var sqlFileName = Path.Combine(_outputPath, "talentInfo.sql");
        if (File.Exists(sqlFileName)) {
            File.Delete(sqlFileName);
        }

        File.WriteAllText(sqlFileName, sqlText);
    }

    private string PrepareForImageUrl(string input) {
        return input.Strip();
    }

    private void SaveMapImages() {
        var targetDir = Path.Combine(_outputPath, "Maps");
        if (!Directory.Exists(targetDir)) {
            Directory.CreateDirectory(targetDir);
        }

        foreach (var mapKvp in _scraper.MapImages) {
            var ddsBytes = mapKvp.Value;
            var ddsImage = new DDSImage(ddsBytes, true);
            var mapName = mapKvp.Key;
            var rawFileName = $"{mapName}";
            var imageName = $"map_{PrepareForImageUrl(rawFileName)}.png";
            var path = Path.Combine(targetDir, imageName);
            ddsImage.Save(path);
        }
    }

    private void SaveTalentImages() {
        var targetDir = Path.Combine(_outputPath, "TalentImages");
        if (!Directory.Exists(targetDir)) {
            Directory.CreateDirectory(targetDir);
        }

        foreach (var talentInfo in _scraper.TalentInfoList) {
            var ddsBytes = talentInfo.Image;
            var ddsImage = new DDSImage(ddsBytes, true);
            var talentName = talentInfo.TalentName;
            var heroName = talentInfo.HeroName;
            var rawFileName = $"{heroName}{talentName}";
            var imageName = $"talent_{PrepareForImageUrl(rawFileName)}.png";
            var path = Path.Combine(targetDir, imageName);
            ddsImage.Save(path);
        }
    }

    private void SavePortraitImages() {
        var targetDir = Path.Combine(_outputPath, "Portraits");
        if (!Directory.Exists(targetDir)) {
            Directory.CreateDirectory(targetDir);
        }

        foreach (var heroKvp in _scraper.HeroImages) {
            var ddsBytes = heroKvp.Value;
            var ddsImage = new DDSImage(ddsBytes, true);
            var heroName = heroKvp.Key;
            var rawFileName = $"{heroName}";
            var imageName = $"portrait_{PrepareForImageUrl(rawFileName)}.png";
            var path = Path.Combine(targetDir, imageName);
            ddsImage.Save(path);
        }
    }
}
