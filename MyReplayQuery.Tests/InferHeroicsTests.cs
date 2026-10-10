using MyReplayQuery.Abilities;
using MyReplayQuery.Events;

namespace MyReplayQuery.Tests;

public class InferHeroicsTests {
    private static readonly PlayerInfo Falstad = new(0, "Leanderthal", "Falstad", 0, false, 1);
    private static readonly PlayerInfo Tassadar = new(1, "Darkness", "Tassadar", 1, false, 2);

    /// <summary>
    /// Falstad's ids in build 98348: 350 is a known heroic, 351 isn't in the table. 603 is Tassadar's
    /// heroic, next to 604 which nobody has reached with a key.
    /// </summary>
    private sealed class Table : IAbilityLabels {
        public AbilityLabel Label(int abil, string hero) => abil switch {
            349 => new("W", $"{hero}.W", null),
            350 or 603 => new("R", $"{hero}.R", null),
            _ => new(null, $"{hero}.#{abil}", null),
        };

        public IReadOnlyCollection<string> Heroes(int abil) => abil switch {
            349 or 350 => ["Falstad"],
            603 => ["Tassadar"],
            _ => [],
        };
    }

    private static CastEvent Cast(double t, int abil, PlayerInfo? player = null) {
        player ??= Falstad;
        var label = new Table().Label(abil, player.Hero);
        return new(t, player, abil, 0, label.Slot, label.Label, label.Name, null, false);
    }

    private static List<CastEvent> Infer(params ReplayEvent[] events) {
        var list = events.ToList();
        TimelineBuilder.InferHeroics(list, new Table());
        return list.OfType<CastEvent>().ToList();
    }

    [Fact]
    public void UnknownIdNextToAHeroicCastOnlyAfterLevelTenIsAHeroic() {
        var casts = Infer(new TalentEvent(427, Falstad, 10, "FalstadHeroicAbilityHinterlandBlast"), Cast(428.7, 351), Cast(568.5, 351));

        Assert.All(casts, c => {
            Assert.Equal("R", c.Slot);
            Assert.Equal("Falstad.R?", c.Label);
            Assert.Equal("HinterlandBlast", c.Name);
        });
    }

    [Fact]
    public void UnknownIdCastBeforeLevelTenStaysUnknown() {
        var casts = Infer(Cast(300, 351), new TalentEvent(427, Falstad, 10, "FalstadHeroicAbilityHinterlandBlast"), Cast(568.5, 351));

        Assert.All(casts, c => Assert.Null(c.Slot));
    }

    [Fact]
    public void UnknownIdAwayFromTheHeroicsStaysUnknown() {
        var casts = Infer(new TalentEvent(427, Falstad, 10, "FalstadHeroicAbilityHinterlandBlast"), Cast(500, 360));

        Assert.Null(Assert.Single(casts).Slot);
    }

    [Fact]
    public void UnknownIdNextToAnotherHerosHeroicStaysUnknown() {
        var casts = Infer(new TalentEvent(427, Falstad, 10, "FalstadHeroicAbilityHinterlandBlast"), Cast(500, 604));

        Assert.Null(Assert.Single(casts).Slot);
    }

    [Fact]
    public void UnknownIdSeveralHeroesCastStaysUnknown() {
        var casts = Infer(new TalentEvent(427, Falstad, 10, "FalstadHeroicAbilityHinterlandBlast"),
            new TalentEvent(430, Tassadar, 10, "TassadarHeroicAbilityArchon"), Cast(500, 351), Cast(510, 351, Tassadar));

        Assert.All(casts, c => Assert.Null(c.Slot));
    }

    [Fact]
    public void UnknownIdCastEveryFewSecondsStaysUnknown() {
        // Like Odin's abilities: only after level 10, but far more often than any heroic.
        var casts = Infer(new TalentEvent(427, Falstad, 10, "FalstadHeroicAbilityHinterlandBlast"),
            Cast(500, 351), Cast(500.4, 351), Cast(506, 351), Cast(512, 351));

        Assert.All(casts, c => Assert.Null(c.Slot));
    }
}
