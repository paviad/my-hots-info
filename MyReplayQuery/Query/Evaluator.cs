using System.Collections.Immutable;
using System.Text.RegularExpressions;
using MyReplayQuery.Events;

namespace MyReplayQuery.Query;

/// <summary>A span of the replay that matched a pattern.</summary>
public sealed record Match(
    double Start,
    double End,
    ImmutableArray<ReplayEvent> Events,
    ImmutableDictionary<string, object> Bindings,
    ImmutableList<string> Notes) {
    public Match Then(Match next) => new(Start, next.End, Events.AddRange(next.Events), next.Bindings, Notes.AddRange(next.Notes));
}

public sealed record FindResult(FindStatement Find, IReadOnlyList<Match> Matches, int Truncated);

/// <summary>Runs a parsed query against one replay timeline.</summary>
public sealed class Evaluator {
    /// <summary>How far from a sample a hero position may be looked up for 'near'.</summary>
    private const double PositionTolerance = 2.0;

    private readonly ReplayTimeline _timeline;
    private readonly Dictionary<string, (ReplayEvent[] Events, double[] Times)> _byKind;
    private readonly Dictionary<string, Pattern> _lets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(double, double)> _truncated = [];

    public Evaluator(ReplayTimeline timeline) {
        _timeline = timeline;
        _byKind = timeline.Events.GroupBy(e => e.Kind).ToDictionary(
            g => g.Key,
            g => {
                var events = g.OrderBy(e => e.T).ToArray();
                return (events, events.Select(e => e.T).ToArray());
            });
    }

    public IReadOnlyList<FindResult> Run(QueryProgram program) {
        var results = new List<FindResult>();
        foreach (var statement in program.Statements) {
            switch (statement) {
                case LetStatement let:
                    _lets[let.Name] = let.Pattern;
                    break;
                case FindStatement find:
                    _truncated.Clear();
                    var matches = Eval(find.Pattern, ImmutableDictionary<string, object>.Empty, double.NegativeInfinity, double.PositiveInfinity)
                        .OrderBy(m => m.Start)
                        .ThenBy(m => m.End)
                        .ToList();
                    results.Add(new FindResult(find, matches, _truncated.Count));
                    break;
            }
        }

        return results;
    }

    /// <summary>Matches of <paramref name="p"/> consistent with <paramref name="env"/> whose start is in [from, to].</summary>
    private IEnumerable<Match> Eval(Pattern p, ImmutableDictionary<string, object> env, double from, double to) => p switch {
        AtomPattern atom => EvalAtom(atom, env, from, to),
        RefPattern r => Eval(_lets[r.Name], env, from, to),
        SeqPattern seq => EvalSeq(seq, env, from, to),
        CountPattern count => EvalCount(count, env, from, to),
        _ => throw new InvalidOperationException(p.GetType().Name),
    };

    private IEnumerable<Match> EvalSeq(SeqPattern seq, ImmutableDictionary<string, object> env, double from, double to) {
        switch (seq.Op) {
            case SeqOp.Then: {
                // Without 'all', each B pairs with the earliest A that reaches it, so repeated A's
                // (e.g. re-aiming an ability) before the same B give one span, not several.
                var used = new HashSet<(double, ReplayEvent)>();
                foreach (var a in Eval(seq.Left, env, from, to).OrderBy(m => m.Start)) {
                    var next = Eval(seq.Right, a.Bindings, a.End, a.End + seq.Within)
                        .Where(b => b.Start > a.End || (b.Start == a.End && !b.Events.Contains(a.Events[^1])))
                        .OrderBy(b => b.Start);
                    foreach (var b in seq.All ? next : next.Take(1)) {
                        if (seq.All || used.Add((b.Start, b.Events[^1]))) {
                            yield return a.Then(b);
                        }
                    }
                }

                break;
            }

            case SeqOp.NotFollowedBy:
                foreach (var a in Eval(seq.Left, env, from, to)) {
                    if (a.End + seq.Within > _timeline.Length) {
                        // The game ended before the window closed, so "not followed" can't be decided.
                        _truncated.Add((a.Start, a.End));
                        continue;
                    }

                    if (!Eval(seq.Right, a.Bindings, a.End, a.End + seq.Within)
                            .Any(b => b.Start > a.End || (b.Start == a.End && !b.Events.Contains(a.Events[^1])))) {
                        yield return a;
                    }
                }

                break;

            case SeqOp.PrecededBy:
                // The combined match starts at the earlier event, so widen the left range and filter after.
                foreach (var a in Eval(seq.Left, env, from, to + seq.Within)) {
                    var before = Eval(seq.Right, a.Bindings, a.Start - seq.Within, a.Start)
                        .Where(b => b.End < a.Start || (b.End == a.Start && !b.Events.Contains(a.Events[0])))
                        .OrderByDescending(b => b.Start);
                    foreach (var b in seq.All ? before : before.Take(1)) {
                        var combined = new Match(b.Start, a.End, b.Events.AddRange(a.Events), b.Bindings, b.Notes.AddRange(a.Notes));
                        if (combined.Start >= from && combined.Start <= to) {
                            yield return combined;
                        }
                    }
                }

                break;

            case SeqOp.NotPrecededBy:
                foreach (var a in Eval(seq.Left, env, from, to)) {
                    if (!Eval(seq.Right, a.Bindings, a.Start - seq.Within, a.Start)
                            .Any(b => b.End < a.Start || (b.End == a.Start && !b.Events.Contains(a.Events[0])))) {
                        yield return a;
                    }
                }

                break;
        }
    }

    /// <summary>
    /// Clusters of at least Min matches (with the same bindings) whose starts fit in the window.
    /// Clusters are taken greedily from the earliest match and don't overlap.
    /// </summary>
    private IEnumerable<Match> EvalCount(CountPattern count, ImmutableDictionary<string, object> env, double from, double to) {
        var groups = Eval(count.Inner, env, from, to + count.Within)
            .GroupBy(m => BindingKey(m.Bindings))
            .Select(g => g.OrderBy(m => m.Start).ToList());

        foreach (var group in groups) {
            var i = 0;
            while (i < group.Count) {
                var j = i;
                while (j + 1 < group.Count && group[j + 1].Start - group[i].Start <= count.Within) {
                    j++;
                }

                if (j - i + 1 >= count.Min && group[i].Start >= from && group[i].Start <= to) {
                    var cluster = group.GetRange(i, j - i + 1);
                    yield return new Match(
                        cluster[0].Start,
                        cluster.Max(m => m.End),
                        [.. cluster.SelectMany(m => m.Events)],
                        cluster[0].Bindings,
                        [$"x{cluster.Count}", .. cluster.SelectMany(m => m.Notes)]);
                    i = j + 1;
                }
                else {
                    i++;
                }
            }
        }
    }

    private static string BindingKey(ImmutableDictionary<string, object> bindings) =>
        string.Join("|", bindings.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

    private IEnumerable<Match> EvalAtom(AtomPattern atom, ImmutableDictionary<string, object> env, double from, double to) {
        if (!_byKind.TryGetValue(atom.Kind, out var kind)) {
            yield break;
        }

        var start = double.IsNegativeInfinity(from) ? 0 : LowerBound(kind.Times, from);
        for (var i = start; i < kind.Events.Length && kind.Times[i] <= to; i++) {
            var e = kind.Events[i];
            foreach (var bindings in MatchFilters(e, atom.Filters, 0, env)) {
                if (MatchNear(e, atom.Near, bindings) is { } notes) {
                    yield return new Match(e.T, e.T, [e], bindings, notes);
                }
            }
        }
    }

    private static int LowerBound(double[] times, double t) {
        int lo = 0, hi = times.Length;
        while (lo < hi) {
            var mid = (lo + hi) / 2;
            if (times[mid] < t) {
                lo = mid + 1;
            }
            else {
                hi = mid;
            }
        }

        return lo;
    }

    private IEnumerable<ImmutableDictionary<string, object>> MatchFilters(
        ReplayEvent e, IReadOnlyList<FieldFilter> filters, int index, ImmutableDictionary<string, object> env) {
        if (index == filters.Count) {
            yield return env;
            yield break;
        }

        var filter = filters[index];
        foreach (var b in MatchValue(e.Fields.GetValueOrDefault(filter.Field), filter.Value, env).Distinct(BindingsComparer.Instance)) {
            foreach (var rest in MatchFilters(e, filters, index + 1, b)) {
                yield return rest;
            }
        }
    }

    private IEnumerable<ImmutableDictionary<string, object>> MatchValue(object? field, ValueExpr value, ImmutableDictionary<string, object> env) {
        switch (value) {
            case AltValue alt:
                foreach (var option in alt.Options) {
                    foreach (var b in MatchValue(field, option, env)) {
                        yield return b;
                    }
                }

                yield break;

            case NotValue not:
                if (!MatchValue(field, not.Inner, env).Any()) {
                    yield return env;
                }

                yield break;
        }

        if (field is IReadOnlyList<PlayerInfo> list) {
            foreach (var p in list) {
                foreach (var b in MatchValue(p, value, env)) {
                    yield return b;
                }
            }

            yield break;
        }

        if (field is null) {
            yield break;
        }

        if (value is VarValue v) {
            if (!env.TryGetValue(v.Name, out var bound)) {
                yield return env.Add(v.Name, field);
            }
            else if (SameValue(bound, field)) {
                yield return env;
            }

            yield break;
        }

        if (Matches(field, value)) {
            yield return env;
        }
    }

    private static bool SameValue(object a, object b) => (a, b) switch {
        (string x, string y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase),
        (PlayerInfo x, PlayerInfo y) => x.Index == y.Index,
        (PlayerInfo x, string y) => NameMatches(y, x.Name) || NameMatches(y, x.Hero),
        (string x, PlayerInfo y) => NameMatches(x, y.Name) || NameMatches(x, y.Hero),
        _ => Equals(a, b),
    };

    private bool Matches(object field, ValueExpr value) => (field, value) switch {
        (_, KeywordValue k) => MatchesKeyword(field, k),
        (PlayerInfo p, NameValue n) => NameMatches(n.Text, p.Name) || NameMatches(n.Text, p.Hero),
        (string s, NameValue n) => NameMatches(n.Text, s),
        (TeamRef t, NameValue n) => NameMatches(n.Text, t.ToString()) || n.Text == t.Team.ToString(),
        (TeamRef t, NumberValue n) => t.Team == n.Value,
        (bool b, NameValue n) => bool.TryParse(n.Text, out var nb) && nb == b,
        (double d, NumberValue n) => Math.Abs(d - n.Value) < 1e-9,
        (double d, CompareValue c) => c.Op switch {
            ">=" => d >= c.Value,
            "<=" => d <= c.Value,
            ">" => d > c.Value,
            "<" => d < c.Value,
            _ => false,
        },
        (string s, NumberValue n) => s == n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => false,
    };

    private PlayerInfo RequireMe(SourcePos pos) =>
        _timeline.Me ?? throw new QueryException("The query uses me/ally/enemy but this replay has no known 'me' (use --me)", pos);

    private bool MatchesKeyword(object field, KeywordValue k) {
        var me = RequireMe(k.Pos);
        return (field, k.Keyword) switch {
            (PlayerInfo p, "me") => p.Index == me.Index,
            (PlayerInfo p, "ally") => p.Team == me.Team && p.Index != me.Index,
            (PlayerInfo p, "enemy") => p.Team != me.Team,
            (TeamRef t, "me" or "ally") => t.Team == me.Team,
            (TeamRef t, "enemy") => t.Team != me.Team,
            (string s, "me") => NameMatches(me.Hero, s),
            _ => false,
        };
    }

    /// <summary>
    /// Case-insensitive; ignores punctuation and spaces ("ETC" = "E.T.C.", "kelthuzad" = "Kel'Thuzad");
    /// <c>*</c> and <c>?</c> are wildcards.
    /// </summary>
    public static bool NameMatches(string pattern, string? value) {
        if (value is null) {
            return false;
        }

        if (pattern.Contains('*') || pattern.Contains('?')) {
            var regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        return string.Equals(pattern, value, StringComparison.OrdinalIgnoreCase)
               || (Normalize(pattern) is { Length: > 0 } np && np == Normalize(value));
    }

    private static string Normalize(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Checks every near clause; returns notes describing the distances, or null if any clause fails.</summary>
    private ImmutableList<string>? MatchNear(ReplayEvent e, IReadOnlyList<NearClause> clauses, ImmutableDictionary<string, object> bindings) {
        var notes = ImmutableList<string>.Empty;
        foreach (var clause in clauses) {
            (MapPoint Point, bool Estimated)? at = e.Point is { } point ? (point, false)
                : e.Actor is { } actor ? _timeline.PositionOf(actor, e.T, PositionTolerance)
                : null;
            if (at is not { } here) {
                return null;
            }

            var best = Subjects(clause.Subject, bindings)
                .Select(s => (Subject: s, Pos: _timeline.PositionOf(s, e.T, PositionTolerance)))
                .Where(x => x.Pos is not null)
                .Select(x => (x.Subject, Distance: x.Pos!.Value.Point.DistanceTo(here.Point), Estimated: x.Pos.Value.Estimated || here.Estimated))
                .Where(x => x.Distance <= clause.Distance)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();
            if (best.Subject is null) {
                return null;
            }

            notes = notes.Add($"{best.Distance:0.#} from {best.Subject.Name}{(best.Estimated ? " (estimated position)" : "")}");
        }

        return notes;
    }

    private IEnumerable<PlayerInfo> Subjects(ValueExpr subject, ImmutableDictionary<string, object> bindings) => subject switch {
        VarValue v when bindings.TryGetValue(v.Name, out var bound) => bound switch {
            PlayerInfo p => [p],
            string s => _timeline.Players.Where(p => NameMatches(s, p.Name) || NameMatches(s, p.Hero)),
            _ => [],
        },
        VarValue v => throw new QueryException($"${v.Name} must be bound before it is used in near()", v.Pos),
        AltValue alt => alt.Options.SelectMany(o => Subjects(o, bindings)),
        _ => _timeline.Players.Where(p => Matches(p, subject)),
    };

    private sealed class BindingsComparer : IEqualityComparer<ImmutableDictionary<string, object>> {
        public static readonly BindingsComparer Instance = new();

        public bool Equals(ImmutableDictionary<string, object>? x, ImmutableDictionary<string, object>? y) =>
            x is not null && y is not null && BindingKey(x) == BindingKey(y);

        public int GetHashCode(ImmutableDictionary<string, object> obj) => BindingKey(obj).GetHashCode();
    }
}
