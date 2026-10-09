using System.CommandLine;
using System.Text.Json;
using MyReplayQuery.Abilities;
using MyReplayQuery.Events;
using MyReplayQuery.Query;

namespace MyHotsCli;

/// <summary>
/// <c>rq</c>: search replays for event patterns with the replay query language (see <see cref="QueryParser"/>).
/// </summary>
public static class ReplayQueryCommand {
    private const string QueryHelp = """
        Query language:
          let NAME = PATTERN          name a pattern for reuse
          find PATTERN                print every span that matches

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

        Example:
          let land = cast(hero: Deathwing, slot: D) near(me, 6)
          find land then death(victim: Deathwing, credit: me) within 45s
        """;

    public static void Setup(RootCommand rootCommand) {
        var rq = new Command("rq", "Search replays for event patterns");

        var replayOption = new Option<string[]>("--replay", "-r") {
            Description = "Replay file, 'latest', or N for the N-th newest (repeatable)",
            DefaultValueFactory = _ => ["latest"],
            // One value per -r, so a positional query after it isn't taken for a replay.
            Recursive = true,
        };
        var lastOption = new Option<int?>("--last") {
            Description = "Search the K newest replays instead of --replay",
            Recursive = true,
        };
        var meOption = new Option<string?>("--me") {
            Description = "Player or hero name of 'me' (default: the toon whose folder holds the replay)",
            Recursive = true,
        };
        var fromOption = new Option<string?>("--from") { Description = "Only events/matches at or after m:ss", Recursive = true };
        var toOption = new Option<string?>("--to") { Description = "Only events/matches at or before m:ss", Recursive = true };
        var noTableOption = new Option<bool>("--no-abil-table") {
            Description = "Don't build or use the ability slot table (casts show as Hero.#id)",
            Recursive = true,
        };
        rq.Options.Add(replayOption);
        rq.Options.Add(lastOption);
        rq.Options.Add(meOption);
        rq.Options.Add(fromOption);
        rq.Options.Add(toOption);
        rq.Options.Add(noTableOption);

        // rq find
        var find = new Command("find", "Run a query.\n\n" + QueryHelp);
        var queryArgument = new Argument<string?>("query") { Description = "Query text", Arity = ArgumentArity.ZeroOrOne };
        var fileOption = new Option<string?>("--file", "-f") { Description = "Read the query from a file" };
        var jsonOption = new Option<bool>("--json") { Description = "Print matches as JSON" };
        find.Arguments.Add(queryArgument);
        find.Options.Add(fileOption);
        find.Options.Add(jsonOption);
        find.Validators.Add(r => {
            if (r.GetValue(queryArgument) is null == r.GetValue(fileOption) is null) {
                r.AddError("Give either a query or --file.");
            }
        });
        find.SetAction(r => {
            var source = r.GetValue(queryArgument) ?? File.ReadAllText(r.GetValue(fileOption)!);
            QueryProgram program;
            try {
                program = QueryParser.Parse(source);
            }
            catch (QueryException e) {
                Console.Error.WriteLine(e.Render(source));
                return 2;
            }

            var (from, to) = Window(r.GetValue(fromOption), r.GetValue(toOption));
            var json = r.GetValue(jsonOption);
            var output = new List<object>();
            foreach (var timeline in Timelines(r.GetValue(replayOption)!, r.GetValue(lastOption), r.GetValue(meOption), r.GetValue(noTableOption))) {
                IReadOnlyList<FindResult> results;
                try {
                    results = new Evaluator(timeline).Run(program);
                }
                catch (QueryException e) {
                    Console.Error.WriteLine($"{Path.GetFileName(timeline.Source)}: {e.Render(source)}");
                    continue;
                }

                results = results.Select(x => x with { Matches = x.Matches.Where(m => m.Start >= from && m.Start <= to).ToList() }).ToList();
                if (json) {
                    output.Add(ToJson(timeline, results));
                    continue;
                }

                Console.WriteLine(ResultFormatter.Header(timeline));
                foreach (var result in results) {
                    Console.Write(ResultFormatter.Result(result));
                }

                Console.WriteLine();
            }

            if (json) {
                Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
            }

            return 0;
        });
        rq.Subcommands.Add(find);

        // rq events
        var events = new Command("events", "Print a replay's normalized event timeline");
        var kindOption = new Option<string[]>("--kind", "-k") {
            Description = $"Event kinds to show ({string.Join(", ", EventSchema.Fields.Keys)}); default all but key and unitdied",
            AllowMultipleArgumentsPerToken = true,
        };
        var playerOption = new Option<string[]>("--player", "-p") {
            Description = "Only events involving these players or heroes ('me' allowed)",
            AllowMultipleArgumentsPerToken = true,
        };
        events.Options.Add(kindOption);
        events.Options.Add(playerOption);
        events.SetAction(r => {
            var (from, to) = Window(r.GetValue(fromOption), r.GetValue(toOption));
            var kinds = Split(r.GetValue(kindOption));
            var players = Split(r.GetValue(playerOption));
            foreach (var timeline in Timelines(r.GetValue(replayOption)!, r.GetValue(lastOption), r.GetValue(meOption), r.GetValue(noTableOption))) {
                Console.WriteLine(ResultFormatter.Header(timeline));
                foreach (var p in timeline.Players) {
                    Console.WriteLine($"  team{p.Team}  {p.Name,-16} {p.Hero}{(p.IsMe ? "  (me)" : "")}");
                }

                foreach (var e in timeline.Events.Where(e => e.T >= from && e.T <= to)) {
                    if (kinds.Count > 0 ? !kinds.Contains(e.Kind) : e.Kind is "key" or "unitdied") {
                        continue;
                    }

                    if (players.Count > 0 && !Involved(e).Any(p => players.Any(q =>
                            q.Equals("me", StringComparison.OrdinalIgnoreCase) ? p.IsMe : Evaluator.NameMatches(q, p.Name) || Evaluator.NameMatches(q, p.Hero)))) {
                        continue;
                    }

                    Console.WriteLine(ResultFormatter.Event(e));
                }

                Console.WriteLine();
            }
        });
        rq.Subcommands.Add(events);

        // rq abil-table
        var abilTable = new Command("abil-table", "Show (or rebuild) the ability slot table for a build");
        var buildOption = new Option<int?>("--build") { Description = "Build number (default: the build of --replay)" };
        var rebuildOption = new Option<bool>("--rebuild") { Description = "Rebuild from replays even if cached" };
        var heroOption = new Option<string?>("--hero") { Description = "Only abilities this hero voted for" };
        abilTable.Options.Add(buildOption);
        abilTable.Options.Add(rebuildOption);
        abilTable.Options.Add(heroOption);
        abilTable.SetAction(r => {
            var build = r.GetValue(buildOption) ?? ReplayFiles.ReadBuild(ReplayFiles.Resolve(r.GetValue(replayOption)![0]));
            var table = AbilitySlotTable.GetOrBuild(build, AllReplayPaths(), r.GetValue(rebuildOption), Console.Error.WriteLine);
            var hero = r.GetValue(heroOption);
            Console.WriteLine($"Build {table.Build}: {table.Entries.Count} ability ids from {table.ReplaysUsed} replays");
            Console.WriteLine($"Names from {AbilitySlotTable.AliasFile}: {table.Names.Count}");
            Console.WriteLine();
            Console.WriteLine($"{"id",6}  {"slot",-8} {"share",5} {"votes",5}  heroes / name");
            foreach (var e in table.Entries.Values.OrderBy(e => e.Abil)) {
                if (hero is not null && !e.HeroVotes.Keys.Any(h => Evaluator.NameMatches(hero, h))) {
                    continue;
                }

                var heroes = string.Join(", ", e.HeroVotes.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key} {kv.Value}"))
                             + (e.HeroVotes.Count > 4 ? $", +{e.HeroVotes.Count - 4}" : "");
                var name = table.Names.TryGetValue(e.Abil, out var n) ? $"  \"{n}\"" : "";
                Console.WriteLine($"{e.Abil,6}  {e.Slot,-8} {e.Share,5:P0} {e.TotalVotes,5}  {heroes}{name}");
            }
        });
        rq.Subcommands.Add(abilTable);

        rootCommand.Subcommands.Add(rq);
    }

    private static List<string> AllReplayPaths() => ReplayFiles.AllReplays().Select(f => f.FullName).ToList();

    private static IEnumerable<ReplayTimeline> Timelines(string[] specs, int? last, string? me, bool noTable) {
        var paths = last is { } k
            ? ReplayFiles.AllReplays().Take(k).Select(f => f.FullName).ToList()
            : specs.Select(ReplayFiles.Resolve).ToList();
        var tables = new Dictionary<int, IAbilityLabels>();
        List<string>? allPaths = null;

        IAbilityLabels Labels(string path) {
            if (noTable) {
                return NoAbilityLabels.Instance;
            }

            var build = ReplayFiles.ReadBuild(path);
            if (!tables.TryGetValue(build, out var labels)) {
                allPaths ??= AllReplayPaths();
                tables[build] = labels = AbilitySlotTable.GetOrBuild(build, allPaths, progress: Console.Error.WriteLine);
            }

            return labels;
        }

        // Ability tables first (they may take a while to build), then parse the replays in parallel.
        var labelsByPath = paths.ToDictionary(p => p, Labels);
        var loaded = paths.AsParallel().AsOrdered().Select(path => {
            try {
                return TimelineBuilder.Load(path, labelsByPath[path], me);
            }
            catch (Exception e) {
                Console.Error.WriteLine($"{Path.GetFileName(path)}: {e.Message}");
                return null;
            }
        });
        return loaded.OfType<ReplayTimeline>();
    }

    private static (double From, double To) Window(string? from, string? to) =>
        (from is null ? double.NegativeInfinity : ResultFormatter.ParseTime(from),
         to is null ? double.PositiveInfinity : ResultFormatter.ParseTime(to));

    private static HashSet<string> Split(string[]? values) =>
        (values ?? []).SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<PlayerInfo> Involved(ReplayEvent e) => e switch {
        DeathEvent d => [d.Victim, .. d.Credit],
        _ => e.Actor is { } a ? [a] : [],
    };

    private static object ToJson(ReplayTimeline timeline, IReadOnlyList<FindResult> results) => new {
        replay = timeline.Source,
        map = timeline.Map,
        build = timeline.Build,
        results = results.Select(r => new {
            find = r.Find.Text,
            undecided = r.Truncated,
            matches = r.Matches.Select(m => new {
                start = Math.Round(m.Start, 2),
                end = Math.Round(m.End, 2),
                bindings = m.Bindings.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()),
                notes = m.Notes,
                events = m.Events.Select(e => new { t = Math.Round(e.T, 2), kind = e.Kind, text = e.Describe() }),
            }),
        }),
    };
}
