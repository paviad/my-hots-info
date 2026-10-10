using MyReplayQuery.Events;

namespace MyHotsObserver;

public enum CameraMove {
    /// <summary>The hero's own camera (the player's number key), with the hero's panel.</summary>
    HeroView,

    /// <summary>Follow in the everyone view: E, Backspace, then a click on the hero's top-bar portrait.</summary>
    EveryoneFollow,

    /// <summary>E alone: the everyone view with the camera left where it is.</summary>
    StayPut,

    /// <summary>
    /// The camera to <see cref="CameraStep.At"/> by a minimap click, which also stops following.
    /// </summary>
    LookAt,

    /// <summary>
    /// Holds the left button down, still, on the minimap at <see cref="CameraStep.At"/> until the
    /// next step or the end of the clip: while it's held the camera stays put, even when the hero
    /// dies or takes off.
    /// </summary>
    HoldAt,
}

/// <param name="FireAt">Replay time to act at; before the event when the move must beat it.</param>
/// <param name="Reason">What the move is for, e.g. "takes off".</param>
/// <param name="At">The map position a <see cref="CameraMove.LookAt"/> looks at.</param>
public sealed record CameraStep(double FireAt, CameraMove Move, string Reason, MapPoint? At = null);

/// <summary>
/// Where to point the camera while following a hero in the everyone view, which the game's own
/// following gets wrong in two ways. A hero with no map presence (Deathwing flying) can't be
/// followed, so look where he will land. And at a death the everyone view jumps to the hero's
/// core, so hold the minimap down on where he falls from just before (without a calibrated
/// minimap, switch to his hero view, which mostly stays put).
/// </summary>
public static class CameraPlan {
    /// <summary>Forms that take a hero off the map.</summary>
    private static readonly HashSet<string> Airborne = ["DeathwingDragonflightUnit"];

    /// <summary>The portrait click only follows a hero back on the map, so it waits for the landing.</summary>
    public const double AfterForm = 0.2;

    /// <summary>
    /// When the holds on the landing spot are pressed, relative to the take-off event. He visibly
    /// lifts off 0.5-2s before that event (the form changes once he's in the air), and a hold works
    /// at once if pressed after the lift-off but not before. So press it again every half second
    /// across that window: the first press after the lift-off moves the camera.
    /// </summary>
    public static readonly double[] HoldsAfterTakeOff = [-2, -1.5, -1, -0.5, 0, 0.5];

    /// <summary>
    /// How long before a death the held minimap click starts: a hold pressed shortly before the
    /// death lets the camera flick to the core for a moment (measured: 1.5s before gave ~0.3s at
    /// the core), one held from well before keeps it put (reenacted by hand from 4.6s before).
    /// </summary>
    public const double HoldBeforeDeath = 4.5;

    /// <summary>
    /// In the everyone view the game moves the camera to the dead hero's core at the death,
    /// whatever was done before; in the hero view it stays where he fell. So switch to the hero
    /// view this long before he dies.
    /// </summary>
    public const double HeroViewBeforeDeath = 1;

    /// <param name="timeline">The replay's events.</param>
    /// <param name="player">Index of the hero to follow.</param>
    /// <param name="from">Replay time the clip starts at.</param>
    /// <param name="to">Replay time the clip ends at.</param>
    /// <param name="minimap">
    /// Whether the camera can be pointed at a map position (the map's minimap is calibrated); if
    /// not, flying uses the hero's own camera.
    /// </param>
    public static IReadOnlyList<CameraStep> EveryoneView(ReplayTimeline timeline, int player, double from, double to, bool minimap) {
        var events = timeline.Events
            .Where(e => e switch {
                FormEvent f => f.Player.Index == player,
                DeathEvent d => d.Victim.Index == player,
                SpawnEvent s => s.Player.Index == player,
                _ => false,
            })
            .ToList();

        // The state at the start: in the air, dead, or on the ground.
        var before = events.Where(e => e.T <= from).ToList();
        var takeOff = before.OfType<FormEvent>().LastOrDefault() is { } form && Airborne.Contains(form.Unit) ? form : null;
        var death = before.LastOrDefault(e => e is DeathEvent or SpawnEvent) as DeathEvent;

        var steps = new List<CameraStep>();
        if (death is not null) {
            // Dead at the start: look where he fell until he respawns, then follow him.
            steps.Add(minimap && death.Position is { } fell
                ? new(from, CameraMove.LookAt, "dead", fell)
                : new(from, CameraMove.StayPut, "dead"));
        }
        else {
            steps.Add(takeOff is not null ? Flying(takeOff, from, "flying") : new(from, CameraMove.EveryoneFollow, "start"));
        }

        foreach (var e in events.Where(e => e.T > from && e.T <= to)) {
            switch (e) {
                case SpawnEvent sp when steps[^1].Reason == "dead":
                    steps.Add(new(sp.T + AfterForm, CameraMove.EveryoneFollow, "respawns"));
                    break;
                case FormEvent or DeathEvent when steps[^1].Reason == "dead":
                    break;
                case FormEvent f when Airborne.Contains(f.Unit):
                    foreach (var after in minimap ? HoldsAfterTakeOff : [0.2]) {
                        steps.Add(Flying(f, Math.Max(from, f.T + after), "takes off"));
                    }

                    break;
                case FormEvent f when steps[^1].Reason is "takes off" or "flying":
                    steps.Add(new(f.T + AfterForm, CameraMove.EveryoneFollow, "lands"));
                    break;
                case DeathEvent d when minimap && d.Position is { } fell:
                    // At the death the everyone view jumps to his core; a held minimap click
                    // keeps the camera where he falls.
                    steps.Add(new(d.T - HoldBeforeDeath, CameraMove.HoldAt, "dies", fell));
                    return steps;
                case DeathEvent d:
                    steps.Add(new(d.T - HeroViewBeforeDeath, CameraMove.HeroView, "dies"));
                    return steps;
            }
        }

        return steps;

        // Look where he will land, as his player would; without a minimap, his own camera.
        CameraStep Flying(FormEvent flight, double at, string reason) =>
            minimap && LandingSpot(timeline, player, flight.T) is { } spot
                ? new(at, CameraMove.HoldAt, reason, spot)
                : new(at, CameraMove.HeroView, reason);
    }

    /// <summary>
    /// Where a flight that took off at <paramref name="takeOff"/> lands: the target of his last cast
    /// before he lands, the landing spot he chose; or else his first position after landing.
    /// </summary>
    public static MapPoint? LandingSpot(ReplayTimeline timeline, int player, double takeOff) {
        var landing = timeline.Events.OfType<FormEvent>()
            .FirstOrDefault(f => f.Player.Index == player && f.T > takeOff && !Airborne.Contains(f.Unit));
        if (landing is null) {
            return null;
        }

        var aim = timeline.Events.OfType<CastEvent>()
            .LastOrDefault(c => c.Player.Index == player && c.T > takeOff && c.T < landing.T && c.Target is not null);
        return aim?.Target ?? timeline.PositionOf(timeline.Players[player], landing.T + 1, 3)?.Point;
    }
}
