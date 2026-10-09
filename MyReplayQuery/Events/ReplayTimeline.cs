namespace MyReplayQuery.Events;

public readonly record struct PositionSample(double T, MapPoint Point, bool Estimated);

/// <summary>
/// A hero's known positions over time. Samples come from the parser's unit positions, most of which
/// are estimated from move commands, so every lookup reports whether it rests on an estimate.
/// </summary>
public sealed class PositionTrack(IReadOnlyList<PositionSample> samples) {
    public IReadOnlyList<PositionSample> Samples { get; } = samples;

    /// <summary>
    /// Position at <paramref name="t"/>: interpolated between the samples around it when both are within
    /// <paramref name="tolerance"/> seconds, otherwise the nearest sample within tolerance.
    /// </summary>
    public (MapPoint Point, bool Estimated)? At(double t, double tolerance = 2.0) {
        if (Samples.Count == 0) {
            return null;
        }

        var hi = LowerBound(t);
        var lo = hi - 1;
        PositionSample? before = lo >= 0 ? Samples[lo] : null;
        PositionSample? after = hi < Samples.Count ? Samples[hi] : null;

        if (before is { } b && after is { } a && t - b.T <= tolerance && a.T - t <= tolerance) {
            if (a.T - b.T < 1e-9) {
                return (a.Point, a.Estimated);
            }

            var f = (t - b.T) / (a.T - b.T);
            var p = new MapPoint(b.Point.X + (a.Point.X - b.Point.X) * f, b.Point.Y + (a.Point.Y - b.Point.Y) * f);
            return (p, a.Estimated || b.Estimated);
        }

        PositionSample? nearest = (before, after) switch {
            ({ } x, { } y) => t - x.T <= y.T - t ? x : y,
            ({ } x, null) => x,
            (null, { } y) => y,
            _ => null,
        };
        return nearest is { } n && Math.Abs(n.T - t) <= tolerance ? (n.Point, n.Estimated) : null;
    }

    private int LowerBound(double t) {
        int lo = 0, hi = Samples.Count;
        while (lo < hi) {
            var mid = (lo + hi) / 2;
            if (Samples[mid].T < t) {
                lo = mid + 1;
            }
            else {
                hi = mid;
            }
        }

        return lo;
    }
}

/// <summary>A replay flattened into time-ordered, typed events.</summary>
public sealed class ReplayTimeline {
    public required string Source { get; init; }
    public required string Map { get; init; }
    public required int Build { get; init; }
    public required DateTime Timestamp { get; init; }
    public required double Length { get; init; }
    public required IReadOnlyList<PlayerInfo> Players { get; init; }
    public required IReadOnlyList<ReplayEvent> Events { get; init; }
    public required IReadOnlyDictionary<int, PositionTrack> Positions { get; init; }

    public PlayerInfo? Me => Players.FirstOrDefault(p => p.IsMe);

    public (MapPoint Point, bool Estimated)? PositionOf(PlayerInfo player, double t, double tolerance = 2.0) =>
        Positions.TryGetValue(player.Index, out var track) ? track.At(t, tolerance) : null;
}
