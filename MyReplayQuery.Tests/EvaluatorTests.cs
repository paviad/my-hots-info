using MyReplayQuery.Events;
using MyReplayQuery.Query;

namespace MyReplayQuery.Tests;

public class EvaluatorTests {
    private static readonly PlayerInfo Me = new(0, "Player", "Chromie", 0, true, 1);
    private static readonly PlayerInfo Ally = new(1, "Tos", "Murky", 0, false, 2);
    private static readonly PlayerInfo Wing = new(2, "Epicure", "Deathwing", 1, false, 3);
    private static readonly PlayerInfo Etc = new(3, "bosko25", "E.T.C.", 1, false, 4);

    private static ReplayTimeline Timeline(double length, IEnumerable<ReplayEvent> events,
        Dictionary<int, PositionTrack>? positions = null) => new() {
        Source = "test.StormReplay",
        Map = "Test Map",
        Build = 1,
        Timestamp = DateTime.UnixEpoch,
        Length = length,
        Players = [Me, Ally, Wing, Etc],
        Events = events.OrderBy(e => e.T).ToList(),
        Positions = positions ?? [],
    };

    private static List<Match> Find(ReplayTimeline timeline, string query) =>
        new Evaluator(timeline).Run(QueryParser.Parse(query)).Last().Matches.ToList();

    private static CastEvent Cast(double t, PlayerInfo p, int abil, MapPoint? target = null) =>
        new(t, p, abil, 0, null, $"{p.Hero}.#{abil}", null, target, false);

    private static DeathEvent Death(double t, PlayerInfo victim, params PlayerInfo[] credit) =>
        new(t, victim, credit, credit.FirstOrDefault(), null);

    [Fact]
    public void ThenPairsEachBWithTheEarliestA() {
        var tl = Timeline(100, [Cast(10, Wing, 7), Cast(12, Wing, 7), Death(20, Wing, Me), Cast(50, Wing, 7), Death(90, Wing, Me)]);

        var matches = Find(tl, "find cast(abil: 7) then death(victim: Deathwing) within 15s");

        // The re-cast at 12 reaches the same death as 10; the cast at 50 has no death within 15s.
        var m = Assert.Single(matches);
        Assert.Equal((10, 20), (m.Start, m.End));
    }

    [Fact]
    public void ThenAllKeepsEveryPairing() {
        var tl = Timeline(100, [Cast(10, Wing, 7), Cast(12, Wing, 7), Death(20, Wing, Me), Death(22, Etc, Me)]);

        var matches = Find(tl, "find cast(abil: 7) then all death(credit: me) within 15s");

        Assert.Equal([(10.0, 20.0), (10.0, 22.0), (12.0, 20.0), (12.0, 22.0)], matches.Select(m => (m.Start, m.End)));
    }

    [Fact]
    public void BindingsMustUnifyAcrossASequence() {
        var tl = Timeline(100, [
            Death(10, Wing, Me),
            new SpawnEvent(15, Etc, "HeroL90ETC", null), // wrong player
            new SpawnEvent(30, Wing, "HeroDeathwing", null),
        ]);

        var m = Assert.Single(Find(tl, "find death(victim: $p) then spawn(player: $p) within 25s"));

        Assert.Equal(30, m.End);
        Assert.Equal(Wing, m.Bindings["p"]);
    }

    [Fact]
    public void VariableOverAListBindsEachElement() {
        var tl = Timeline(100, [Death(10, Wing, Me, Ally), new ChatEvent(12, Ally, "nice")]);

        var m = Assert.Single(Find(tl, "find death(credit: $k) then chat(player: $k) within 5s"));

        Assert.Equal(Ally, m.Bindings["k"]);
    }

    [Fact]
    public void NotFollowedByExcludesWindowsCutOffByTheGameEnd() {
        var tl = Timeline(100, [
            Death(10, Me, Wing), new PingEvent(12, Me), // followed: excluded
            Death(40, Me, Wing),                        // not followed: match
            Death(95, Me, Wing),                        // window runs past 100: undecided
        ]);

        var result = new Evaluator(tl).Run(QueryParser.Parse("find death(victim: me) not followed by ping(player: me) within 10s")).Single();

        Assert.Equal(40, Assert.Single(result.Matches).Start);
        Assert.Equal(1, result.Truncated);
    }

    [Fact]
    public void PrecededByStartsAtTheEarlierEvent() {
        var tl = Timeline(100, [new PingEvent(5, Me), new PingEvent(8, Me), Death(10, Wing, Me), Death(50, Etc, Me)]);

        var m = Assert.Single(Find(tl, "find death(credit: me) preceded by ping(player: me) within 4s"));

        // The closest preceding ping is used.
        Assert.Equal((8, 10), (m.Start, m.End));
    }

    [Fact]
    public void NotPrecededBy() {
        var tl = Timeline(100, [new PingEvent(8, Me), Death(10, Wing, Me), Death(50, Etc, Me)]);

        var m = Assert.Single(Find(tl, "find death(credit: me) not preceded by ping(player: me) within 4s"));

        Assert.Equal(50, m.Start);
    }

    [Fact]
    public void CountFindsNonOverlappingClusters() {
        var tl = Timeline(100, [Death(10, Wing, Me), Death(12, Etc, Me), Death(14, Wing, Me), Death(40, Etc, Me), Death(60, Wing, Me)]);

        var m = Assert.Single(Find(tl, "find death(victim: enemy, credit: me) count >= 3 within 10s"));

        Assert.Equal((10, 14), (m.Start, m.End));
        Assert.Equal(3, m.Events.Length);
    }

    [Fact]
    public void KeywordsAndNamesMatchPlayersAndHeroes() {
        var tl = Timeline(100, [Death(10, Etc, Me), Death(20, Ally, Wing), Death(30, Me, Wing)]);

        Assert.Equal([10.0], Find(tl, "find death(victim: ETC)").Select(m => m.Start));
        Assert.Equal([10.0], Find(tl, "find death(victim: bosko*)").Select(m => m.Start));
        Assert.Equal([20.0], Find(tl, "find death(victim: ally)").Select(m => m.Start));
        Assert.Equal([10.0], Find(tl, "find death(victim: enemy)").Select(m => m.Start));
        Assert.Equal([20.0, 30.0], Find(tl, "find death(victim: !enemy)").Select(m => m.Start));
        Assert.Equal([20.0, 30.0], Find(tl, "find death(credit: Deathwing|Epicure)").Select(m => m.Start));
    }

    [Fact]
    public void NumericComparisons() {
        var tl = Timeline(100, [new TalentEvent(10, Me, 13, "A"), new TalentEvent(20, Me, 16, "B"), new TalentEvent(30, Me, 20, "C")]);

        Assert.Equal(["B", "C"], Find(tl, "find talent(tier: >=16)").Select(m => ((TalentEvent)m.Events[0]).Name));
        Assert.Equal(["A"], Find(tl, "find talent(tier: <16)").Select(m => ((TalentEvent)m.Events[0]).Name));
    }

    [Fact]
    public void NearUsesTheTargetPointAndTheSubjectsTrack() {
        var positions = new Dictionary<int, PositionTrack> {
            [Me.Index] = new([new PositionSample(9, new MapPoint(100, 100), true), new PositionSample(11, new MapPoint(110, 100), true)]),
        };
        var tl = Timeline(100, [
            Cast(10, Wing, 7, new MapPoint(106, 100)), // me is interpolated to 105,100: distance 1
            Cast(10.5, Wing, 7, new MapPoint(150, 100)), // far
            Cast(30, Wing, 7, new MapPoint(110, 100)), // no position for me near t=30
        ], positions);

        var m = Assert.Single(Find(tl, "find cast(hero: Deathwing) near(me, 3)"));

        Assert.Equal(10, m.Start);
        Assert.Contains("estimated", Assert.Single(m.Notes));
    }

    [Fact]
    public void MeIsRequiredForKeywords() {
        var tl = Timeline(100, [Death(10, Etc, Me)]) is var t
            ? new ReplayTimeline {
                Source = t.Source, Map = t.Map, Build = t.Build, Timestamp = t.Timestamp, Length = t.Length,
                Players = t.Players.Select(p => p with { IsMe = false }).ToList(), Events = t.Events, Positions = t.Positions,
            }
            : null!;

        Assert.Throws<QueryException>(() => Find(tl, "find death(victim: me)"));
    }
}
