using MyHotsObserver;
using MyReplayQuery.Events;
using MyReplayQuery.Query;
using Xunit.Abstractions;

namespace MyHotsObserver.Tests;

/// <summary>
/// Against the 2026-10-09 Dragon Shire replay, where Deathwing (player 9) chases Chromie: he takes
/// off at 23:39.6, lands at 23:47.9 and dies at 24:16.7. Passes trivially on machines without it.
/// </summary>
public class CameraPlanTests(ITestOutputHelper output) {
    private const string ReplayPath =
        @"C:\Users\pavia\Documents\Heroes of the Storm\Accounts\1831861\2-Hero-1-612637\Replays\Multiplayer\2026-10-09 21.33.03 Dragon Shire.StormReplay";

    private static readonly Lazy<ReplayTimeline> Timeline = new(() => TimelineBuilder.Load(ReplayPath));

    [Fact]
    public void WithoutAMinimapFlyingUsesHisCamera() {
        if (Missing()) {
            return;
        }

        var plan = CameraPlan.EveryoneView(Timeline.Value, 9, ResultFormatter.ParseTime("23:38.3"), ResultFormatter.ParseTime("24:24.7"), minimap: false);

        (double At, CameraMove Move)[] expected = [
            (1418.3, CameraMove.EveryoneFollow), // 23:38.3, the start
            (1419.8, CameraMove.HeroView), // just after the take-off event
            (1428.1, CameraMove.EveryoneFollow), // just after he lands
            (1455.7, CameraMove.HeroView), // 1s before he dies
        ];
        Assert.Equal(expected.Select(e => e.Move), plan.Select(s => s.Move));
        Assert.All(expected.Zip(plan), x => Assert.Equal(x.First.At, x.Second.FireAt, 0.1));
    }

    [Fact]
    public void LooksWhereHeWillLandAndWhereHeFell() {
        if (Missing()) {
            return;
        }

        var plan = CameraPlan.EveryoneView(Timeline.Value, 9, ResultFormatter.ParseTime("23:38.3"), ResultFormatter.ParseTime("24:24.7"), minimap: true);

        Assert.Equal([
            (CameraMove.EveryoneFollow, (MapPoint?)null),
            (CameraMove.HoldAt, new MapPoint(121.5, 100.5)), // around the lift-off, every 0.5s: his last aim before landing
            (CameraMove.HoldAt, new MapPoint(121.5, 100.5)),
            (CameraMove.HoldAt, new MapPoint(121.5, 100.5)),
            (CameraMove.HoldAt, new MapPoint(121.5, 100.5)),
            (CameraMove.HoldAt, new MapPoint(121.5, 100.5)),
            (CameraMove.HoldAt, new MapPoint(121.5, 100.5)),
            (CameraMove.EveryoneFollow, null),
            (CameraMove.HoldAt, new MapPoint(86, 67)), // 4.5s before he dies: hold the minimap where he falls
        ], plan.Select(s => (s.Move, s.At is { } p ? new MapPoint(Math.Round(p.X, 1), Math.Round(p.Y, 1)) : (MapPoint?)null)));
    }

    [Fact]
    public void WithoutAMinimapStartsOnHisCameraMidFlight() {
        if (Missing()) {
            return;
        }

        var plan = CameraPlan.EveryoneView(Timeline.Value, 9, ResultFormatter.ParseTime("23:42"), ResultFormatter.ParseTime("23:55"), minimap: false);

        Assert.Equal([CameraMove.HeroView, CameraMove.EveryoneFollow], plan.Select(s => s.Move));
    }

    [Fact]
    public void FollowsHimOnceHeRespawns() {
        if (Missing()) {
            return;
        }

        // He died at 22:25.6 and respawns at 23:35.
        var plan = CameraPlan.EveryoneView(Timeline.Value, 9, ResultFormatter.ParseTime("23:34"), ResultFormatter.ParseTime("23:45"), minimap: true);

        Assert.Equal([CameraMove.LookAt, CameraMove.EveryoneFollow, .. Enumerable.Repeat(CameraMove.HoldAt, 6)], plan.Select(s => s.Move));
        Assert.Equal("respawns", plan[1].Reason);
    }

    [Fact]
    public void StaysPutWhenTheClipStartsWhileHeIsDead() {
        if (Missing()) {
            return;
        }

        var plan = CameraPlan.EveryoneView(Timeline.Value, 9, ResultFormatter.ParseTime("24:30"), ResultFormatter.ParseTime("24:50"), minimap: false);

        Assert.Equal([CameraMove.StayPut], plan.Select(s => s.Move));
    }

    private bool Missing() {
        if (File.Exists(ReplayPath)) {
            return false;
        }

        output.WriteLine($"Skipped: {ReplayPath} not found");
        return true;
    }
}
