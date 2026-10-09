using MyReplayQuery.Events;
using MyReplayQuery.Query;
using Xunit.Abstractions;

namespace MyReplayQuery.Tests;

/// <summary>
/// Checks against a real replay: the 2026-10-09 Dragon Shire game where Deathwing chased Chromie twice.
/// Passes trivially (with a note) on machines that don't have the file.
/// </summary>
public class DragonShireReplayTests(ITestOutputHelper output) {
    private const string ReplayPath =
        @"C:\Users\pavia\Documents\Heroes of the Storm\Accounts\1831861\2-Hero-1-612637\Replays\Multiplayer\2026-10-09 21.33.03 Dragon Shire.StormReplay";

    private static readonly Lazy<ReplayTimeline> Timeline = new(() => TimelineBuilder.Load(ReplayPath));

    private bool Missing() {
        if (File.Exists(ReplayPath)) {
            return false;
        }

        output.WriteLine($"Skipped: {ReplayPath} not found");
        return true;
    }

    [Fact]
    public void IdentifiesMeFromTheFolder() {
        if (Missing()) {
            return;
        }

        Assert.Equal("Chromie", Timeline.Value.Me?.Hero);
    }

    [Fact]
    public void DeathwingDeathsCreditMe() {
        if (Missing()) {
            return;
        }

        var deaths = Timeline.Value.Events.OfType<DeathEvent>().Where(d => d.Victim.Hero == "Deathwing").ToList();

        Assert.Equal([11, 19, 22, 24], deaths.Select(d => (int)d.T / 60));
        Assert.All(deaths.Where(d => d.T > 19 * 60), d => Assert.Contains(d.Credit, p => p.IsMe));
        Assert.Equal("Fibak", deaths.Single(d => (int)d.T == 22 * 60 + 25).Killer?.Name);
    }

    [Fact]
    public void ChaseQueryFindsBothChases() {
        if (Missing()) {
            return;
        }

        var results = new Evaluator(Timeline.Value).Run(QueryParser.Parse("""
            let land = cast(hero: Deathwing, abil: 1315) near(me, 6)
            find land then death(victim: Deathwing, credit: me) within 45s
            """));

        var spans = results.Single().Matches.Select(m => (ResultFormatter.Time(m.Start), ResultFormatter.Time(m.End))).ToList();
        Assert.Equal([("21:59.5", "22:25.6"), ("23:40.3", "24:16.6")], spans);
    }
}
