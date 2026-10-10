using ModelContextProtocol;
using MyHotsObserver;

namespace MyHotsObserver.Tests;

public class ReplayControllerTests {
    private static readonly ReplaySummary Replay = new("x.StormReplay", DateTime.Now, "Map", "QuickMatch", TimeSpan.FromMinutes(20), [
        new(0, "1", "Darkness", "Tassadar", 0, false, true),
        new(1, "2", "PathU", "Nova", 0, false, true),
        new(5, "6", "Player", "Chromie", 1, true, false),
        new(6, "7", "Revbei", "Nazeebo", 1, false, false),
        new(8, "9", "ZAEHN", "Maiev", 1, false, false),
    ]);

    [Theory]
    [InlineData("me", 5)]
    [InlineData("ME", 5)]
    [InlineData("5", 5)]
    [InlineData("Chromie", 5)]
    [InlineData("player", 5)]
    [InlineData("nova", 1)]
    [InlineData("Naz", 6)]
    public void FindsPlayersByIndexNameOrHero(string query, int index) {
        Assert.Equal(index, ReplayController.ResolvePlayer(Replay, query).Index);
    }

    [Fact]
    public void RefusesMeInSomeoneElsesReplay() {
        var theirs = Replay with { Players = Replay.Players.Select(p => p with { IsMe = false }).ToList() };

        Assert.Throws<McpException>(() => ReplayController.ResolvePlayer(theirs, "me"));
    }

    [Theory]
    [InlineData("12")]
    [InlineData("Deathwing")]
    [InlineData("a")] // Darkness, PathU, Player, Nazeebo, ...
    public void RefusesPlayersItCannotPinDown(string query) {
        Assert.Throws<McpException>(() => ReplayController.ResolvePlayer(Replay, query));
    }
}
