using Microsoft.EntityFrameworkCore;
using MyReplayLibrary.Data;
using MyReplayLibrary.Data.Models;

namespace MyReplayLibrary;

/// <summary>The scraped talent data (HeroTalentInformations) as of a build, for browsing heroes and their talents.</summary>
public class TalentQuery(ReplayDbContext dc) {
    /// <summary>The ReplayBuildLast of talents still current as of the newest scrape.</summary>
    public const int Current = 1000000;

    public record ScrapedBuild(int Build, string? Version, DateTime? Date);

    /// <summary>
    /// Each build talents were scraped from, newest first. Every scrape starts or ends some talent's range,
    /// unless it changed nothing, in which case it would show the same talents as the one before it anyway.
    /// </summary>
    public async Task<List<ScrapedBuild>> GetBuilds() {
        var firsts = await dc.HeroTalentInformations.Select(t => t.ReplayBuildFirst).Distinct().ToListAsync();
        var lasts = await dc.HeroTalentInformations.Select(t => t.ReplayBuildLast).Where(b => b < Current).Distinct()
            .ToListAsync();
        var builds = firsts.Union(lasts).ToList();
        var names = await dc.BuildNumbers.Where(b => builds.Contains(b.Buildnumber1))
            .ToDictionaryAsync(b => b.Buildnumber1);

        return builds
            .OrderDescending()
            .Select(b => names.TryGetValue(b, out var n)
                ? new ScrapedBuild(b, n.Version, n.Builddate)
                : new ScrapedBuild(b, null, null))
            .ToList();
    }

    /// <summary>Every hero's talents as of the build.</summary>
    public Task<List<HeroTalentInformation>> GetTalents(int build) =>
        dc.HeroTalentInformations
            .Where(t => t.ReplayBuildFirst <= build && build <= t.ReplayBuildLast)
            .AsNoTracking()
            .ToListAsync();
}
