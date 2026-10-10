using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using MyReplayQuery.Query;

namespace MyHotsObserver.Tools;

[McpServerToolType]
public sealed class ReplayTools(ReplayCatalog catalog) {
    [McpServerTool(Name = "list_replays", ReadOnly = true, Idempotent = true)]
    [Description("Lists this machine's HotS replays, newest first, with map, mode, length and the ten players.")]
    public object ListReplays(
        [Description("Only replays whose map, game mode, hero or player names contain every word, e.g. \"Dragon Shire Deathwing\"")]
        string? filter = null,
        [Description("Most replays to return")] int limit = 10,
        [Description("Most replays to look at, newest first")] int scan = 300) {
        var (matches, scanned) = catalog.List(filter, Math.Clamp(limit, 1, 100), Math.Max(scan, 1));
        return new {
            scanned,
            replays = matches.Select(Describe),
        };
    }

    [McpServerTool(Name = "replay_info", ReadOnly = true, Idempotent = true)]
    [Description("A replay's map, mode, length and players. focusKey is the observer key that follows each player.")]
    public object ReplayInfo(
        [Description("Replay file path, 'latest', or N for the N-th newest")] string replay = "latest") {
        var path = ReplayCatalog.Resolve(replay);
        var summary = catalog.Summary(path) ?? throw new McpException($"Can't read {Path.GetFileName(path)}");
        return Describe(summary);
    }

    [McpServerTool(Name = "find_moments", ReadOnly = true, Idempotent = true)]
    [Description("""
        Searches a replay's events with the replay query language (the CLI's `rq find`).
        Times are seconds since the replay started.

        Query language:
          let NAME = PATTERN          name a pattern for reuse
          find PATTERN                return every span that matches

        Events and their fields:
          death(victim, hero, credit, killer, x, y)      credit = everyone credited; killer = killing blow
          cast(player, hero, abil, cmd, slot, label, name, x, y)
          key(player, hero, key, code, down)   chat(player, hero, text)   ping(player, hero)
          talent(player, hero, tier, name)     spawn(player, hero, unit)  levelup(team, level)
          unitdied(unit, group, team, killer, killerunit, x, y)

        Values: Deathwing, "E.T.C.", gg*, 20, >=16, me, ally, enemy, $var, a|b, !value
        Patterns:
          A then [all] B within 45s       A not followed by B within 10s
          A preceded by B within 5s       A not preceded by B within 5s
          A count >= 3 within 12s         A near(me, 6)       (A)

        Examples:
          find death(victim: enemy, credit: me)
          let land = cast(hero: Deathwing, slot: D) near(me, 6)
          find land then death(victim: Deathwing, credit: me) within 45s
        """)]
    public object FindMoments(
        [Description("The query, one or more statements")] string query,
        [Description("Replay file path, 'latest', or N for the N-th newest")] string replay = "latest",
        [Description("Only matches starting at or after this time (m:ss)")] string? from = null,
        [Description("Only matches starting at or before this time (m:ss)")] string? to = null,
        [Description("Most matches to return per find statement")] int limit = 50) {
        QueryProgram program;
        try {
            program = QueryParser.Parse(query);
        }
        catch (QueryException e) {
            throw new McpException(e.Render(query));
        }

        var timeline = catalog.Timeline(ReplayCatalog.Resolve(replay));
        IReadOnlyList<FindResult> results;
        try {
            results = new Evaluator(timeline).Run(program);
        }
        catch (QueryException e) {
            throw new McpException(e.Render(query));
        }

        var start = from is null ? double.NegativeInfinity : ParseTime(from);
        var end = to is null ? double.PositiveInfinity : ParseTime(to);
        return new {
            replay = timeline.Source,
            map = timeline.Map,
            length = Time(timeline.Length),
            results = results.Select(r => {
                var matches = r.Matches.Where(m => m.Start >= start && m.Start <= end).ToList();
                return new {
                    find = r.Find.Text,
                    total = matches.Count,
                    undecided = r.Truncated,
                    matches = matches.Take(Math.Max(limit, 1)).Select(m => new {
                        start = Time(m.Start),
                        end = Time(m.End),
                        startSeconds = Math.Round(m.Start, 1),
                        endSeconds = Math.Round(m.End, 1),
                        bindings = m.Bindings.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()),
                        notes = m.Notes,
                        events = m.Events.Select(e => $"{Time(e.T)}  {e.Describe()}"),
                    }),
                };
            }),
        };
    }

    internal static string Time(double seconds) => ResultFormatter.Time(seconds).Trim();

    private static double ParseTime(string text) {
        try {
            return ResultFormatter.ParseTime(text);
        }
        catch (FormatException) {
            throw new McpException($"'{text}' is not a time; use m:ss");
        }
    }

    private static object Describe(ReplaySummary s) => new {
        replay = s.Path,
        played = s.Played.ToString("yyyy-MM-dd HH:mm"),
        s.Map,
        s.Mode,
        length = Time(s.Length.TotalSeconds),
        teams = s.Players.GroupBy(p => p.Team).OrderBy(g => g.Key).Select(g => new {
            team = g.Key,
            won = g.First().Won,
            players = g.Select(p => new { p.Index, p.FocusKey, p.Hero, p.Name, p.IsMe }),
        }),
    };
}
