using MyHotsObserver;
using MyHotsObserver.Tools;
using MyReplayQuery.Query;

namespace MyHotsObserver.Tests;

public class ReplayCatalogTests {
    [Theory]
    [InlineData(0, "1")]
    [InlineData(4, "5")]
    [InlineData(8, "9")]
    [InlineData(9, "0")]
    [InlineData(10, "")]
    public void FocusKeysRunOneToNineThenZero(int index, string key) {
        Assert.Equal(key, ReplayCatalog.FocusKey(index));
    }

    [Theory]
    [InlineData("find death(victim: enemy, killer: me) count >= 3 within 12s", false)]
    [InlineData("find form(hero: Deathwing)", false)]
    [InlineData("find cast(hero: Deathwing, slot: D)", true)]
    [InlineData("let land = cast (hero: Deathwing)\nfind land then death(victim: Deathwing) within 45s", true)]
    public void NeedsAbilityNamesOnlyForCasts(string query, bool casts) {
        Assert.Equal(casts, ReplayTools.UsesCasts(QueryParser.Parse(query)));
    }
}
