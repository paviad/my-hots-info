namespace MyHotsInfo.Pages;

public record PrematchRecord(string Name, double WinRate, List<PrematchHeroRecord> Heroes);

public record PrematchHeroRecord(string Hero, double WinRate);
