namespace MyHotsInfo.Pages;

public record PrematchRecord(string Name, int NumGames, double WinRate, List<PrematchHeroRecord> Heroes);

public record PrematchHeroRecord(string Hero, int NumGames, double WinRate);
