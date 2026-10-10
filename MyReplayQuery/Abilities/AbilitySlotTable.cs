using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Heroes.ReplayParser;
using Heroes.ReplayParser.MPQFiles;
using MyReplayQuery.Events;

namespace MyReplayQuery.Abilities;

/// <summary>
/// Maps a build's ability link ids to hotkey slots by majority vote over many replays.
/// </summary>
/// <remarks>
/// Replays record physical keys, not slots, so a single player who rebinds D to F would mislabel their casts.
/// Instead each (replay, player) votes once per ability id for the default-binding slot of the key they pressed
/// before casting it; most players use default keys, so rebinders are outvoted, and every cast of that id is then
/// labelled by id regardless of who cast it.
/// </remarks>
public sealed class AbilitySlotTable : IAbilityLabels {
    /// <summary>Below this share of votes the slot is shown with a "?".</summary>
    public const double ConfidentShare = 0.6;

    /// <summary>A key press counts towards a cast only if it came at most this long before it.</summary>
    private const double MaxKeyToCastSeconds = 3.0;

    public sealed record Entry(int Abil, string Slot, int Votes, int TotalVotes, Dictionary<string, int> SlotVotes, Dictionary<string, int> HeroVotes) {
        public double Share => TotalVotes == 0 ? 0 : (double)Votes / TotalVotes;
    }

    /// <summary>A replay's vote: a player reached ability <c>Abil</c> on <c>Hero</c> most often with <c>Slot</c>.</summary>
    public sealed record Vote(int Abil, string Slot, string Hero);

    public int Build { get; init; }
    public int ReplaysUsed { get; set; }
    public Dictionary<int, Entry> Entries { get; set; } = [];

    /// <summary>
    /// Each replay's votes by file name, so replays added after the table was built can be counted in
    /// without parsing the others again. Replays that couldn't be parsed have no votes.
    /// </summary>
    public Dictionary<string, List<Vote>> ReplayVotes { get; init; } = [];

    /// <summary>Optional real names, keyed by ability id, from the user's alias file.</summary>
    [JsonIgnore]
    public Dictionary<int, string> Names { get; set; } = [];

    public AbilityLabel Label(int abil, string hero) {
        Names.TryGetValue(abil, out var name);
        if (!Entries.TryGetValue(abil, out var e)) {
            return new AbilityLabel(null, $"{hero}.#{abil}", name);
        }

        var slot = e.Share >= ConfidentShare ? e.Slot : e.Slot + "?";
        return new AbilityLabel(e.Slot, $"{hero}.{slot}", name);
    }

    public IReadOnlyCollection<string> Heroes(int abil) =>
        Entries.TryGetValue(abil, out var e) ? e.HeroVotes.Keys : [];

    public static string CacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyHotsInfo", "abil-slots");

    /// <summary>User-maintained names: <c>{ "98348": { "1315": "Dragonflight" } }</c>.</summary>
    public static string AliasFile => Path.Combine(CacheDirectory, "abil-names.json");

    private static string CacheFile(int build) => Path.Combine(CacheDirectory, $"{build}.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// The table for <paramref name="build"/>: the cached one, with any replays of the build it hasn't seen
    /// yet added in, or built from <paramref name="replayFiles"/> when there is none.
    /// </summary>
    public static AbilitySlotTable GetOrBuild(int build, IEnumerable<string> replayFiles, bool rebuild = false, Action<string>? progress = null) {
        AbilitySlotTable? cached = null;
        if (!rebuild && File.Exists(CacheFile(build))) {
            cached = JsonSerializer.Deserialize<AbilitySlotTable>(File.ReadAllText(CacheFile(build)))
                ?? throw new InvalidDataException($"Bad cache file {CacheFile(build)}");
        }

        // Tables cached before they kept per-replay votes can't be added to, so they're rebuilt.
        var table = cached is { ReplayVotes.Count: > 0 } ? cached : new AbilitySlotTable { Build = build };
        if (table.AddReplays(replayFiles, progress) || table != cached) {
            Directory.CreateDirectory(CacheDirectory);
            File.WriteAllText(CacheFile(build), JsonSerializer.Serialize(table, JsonOptions));
        }

        table.Names = LoadAliases(build);
        return table;
    }

    public static Dictionary<int, string> LoadAliases(int build) {
        if (!File.Exists(AliasFile)) {
            return [];
        }

        var all = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(AliasFile)) ?? [];
        return all.TryGetValue(build.ToString(), out var names)
            ? names.Where(kv => int.TryParse(kv.Key, out _)).ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value)
            : [];
    }

    public static AbilitySlotTable BuildFromReplays(int build, IEnumerable<string> replayFiles, Action<string>? progress = null) {
        var table = new AbilitySlotTable { Build = build };
        table.AddReplays(replayFiles, progress);
        return table;
    }

    /// <summary>Counts the votes of the build's replays this table hasn't seen; false if there were none.</summary>
    public bool AddReplays(IEnumerable<string> replayFiles, Action<string>? progress = null) {
        var candidates = replayFiles.Where(f => !ReplayVotes.ContainsKey(Path.GetFileName(f))).Where(f => {
            try {
                return ReplayFiles.ReadBuild(f) == Build;
            }
            catch (Exception) {
                return false;
            }
        }).ToList();
        if (candidates.Count == 0) {
            return false;
        }

        progress?.Invoke(ReplayVotes.Count == 0
            ? $"Building ability slot table for build {Build} from {candidates.Count} replays..."
            : $"Adding {candidates.Count} new replays to the ability slot table for build {Build}...");

        var added = new ConcurrentDictionary<string, List<Vote>>();
        var done = 0;
        var options = new ParseOptions {
            ShouldParseUnits = false,
            ShouldParseStatistics = false,
            ShouldParseMessageEvents = false,
            ShouldParseDetailedBattleLobby = false,
        };

        // Two at a time. A parsed replay is ~180 MB live and parsing makes far more garbage, so memory
        // grows with each parse running alongside: measured on 350 replays, one at a time peaked at
        // 0.6 GB in 203s and sixteen at 21 GB in 143s. Unbounded, it ran a server out of memory.
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Min(2, Environment.ProcessorCount) };
        Parallel.ForEach(candidates, parallel, file => {
            List<Vote> votes;
            try {
                votes = Votes(ReplayFiles.Parse(file, options)).Select(v => new Vote(v.Abil, v.Slot, v.Hero)).ToList();
            }
            catch (Exception) {
                // Unparseable replays just don't vote.
                votes = [];
            }

            added[Path.GetFileName(file)] = votes;
            var n = Interlocked.Increment(ref done);
            if (n % 25 == 0) {
                progress?.Invoke($"  {n}/{candidates.Count}");
            }
        });

        foreach (var (file, votes) in added) {
            ReplayVotes[file] = votes;
        }

        Tally();
        return true;
    }

    /// <summary>Recounts <see cref="Entries"/> from every replay's votes.</summary>
    private void Tally() {
        ReplaysUsed = ReplayVotes.Values.Count(v => v.Count > 0);
        Entries = ReplayVotes.Values.SelectMany(v => v).GroupBy(v => v.Abil).ToDictionary(g => g.Key, g => {
            var slotVotes = g.GroupBy(v => v.Slot).ToDictionary(s => s.Key, s => s.Count());
            var heroVotes = g.GroupBy(v => v.Hero).ToDictionary(h => h.Key, h => h.Count());
            var top = slotVotes.MaxBy(kv => kv.Value);
            return new Entry(g.Key, top.Key, top.Value, g.Count(), slotVotes, heroVotes);
        });
    }

    /// <summary>One vote per (player, ability id): the slot that player most often reached it with.</summary>
    public static IEnumerable<(int Abil, string Slot, string Hero)> Votes(Replay replay) {
        foreach (var playerEvents in replay.GameEvents.Where(e => e.player is not null).GroupBy(e => e.player)) {
            var hero = playerEvents.Key.Character;
            var perAbil = new Dictionary<int, Dictionary<string, int>>();
            (int Code, int Tick)? lastKey = null;

            foreach (var e in playerEvents.OrderBy(e => e.ticksElapsed)) {
                if (e.eventType == GameEventType.CTriggerKeyPressedEvent && (e.data.array[1].vInt!.Value & 8) != 0) {
                    lastKey = ((int)e.data.array[0].vInt!.Value, e.ticksElapsed);
                }
                else if (e.eventType == GameEventType.CCmdEvent && e.data.array[1] is { } abilData) {
                    if (lastKey is { } k && (e.ticksElapsed - k.Tick) / 16.0 <= MaxKeyToCastSeconds
                        && KeyCodes.DefaultSlot(k.Code) is { } slot) {
                        var abil = (int)abilData.array[0].unsignedInt!.Value;
                        var counts = perAbil.TryGetValue(abil, out var c) ? c : perAbil[abil] = [];
                        counts[slot] = counts.GetValueOrDefault(slot) + 1;
                    }

                    // A key press is used up by the first cast after it.
                    lastKey = null;
                }
            }

            foreach (var (abil, counts) in perAbil) {
                yield return (abil, counts.MaxBy(kv => kv.Value).Key, hero);
            }
        }
    }
}
