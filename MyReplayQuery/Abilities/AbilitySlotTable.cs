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

    public int Build { get; init; }
    public int ReplaysUsed { get; init; }
    public Dictionary<int, Entry> Entries { get; init; } = [];

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

    public static string CacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyHotsInfo", "abil-slots");

    /// <summary>User-maintained names: <c>{ "98348": { "1315": "Dragonflight" } }</c>.</summary>
    public static string AliasFile => Path.Combine(CacheDirectory, "abil-names.json");

    private static string CacheFile(int build) => Path.Combine(CacheDirectory, $"{build}.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// The cached table for <paramref name="build"/>, building it from <paramref name="replayFiles"/> if needed.
    /// </summary>
    public static AbilitySlotTable GetOrBuild(int build, IEnumerable<string> replayFiles, bool rebuild = false, Action<string>? progress = null) {
        AbilitySlotTable table;
        if (!rebuild && File.Exists(CacheFile(build))) {
            table = JsonSerializer.Deserialize<AbilitySlotTable>(File.ReadAllText(CacheFile(build)))
                ?? throw new InvalidDataException($"Bad cache file {CacheFile(build)}");
        }
        else {
            table = BuildFromReplays(build, replayFiles, progress);
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
        var candidates = replayFiles.Where(f => {
            try {
                return ReplayFiles.ReadBuild(f) == build;
            }
            catch (Exception) {
                return false;
            }
        }).ToList();
        progress?.Invoke($"Building ability slot table for build {build} from {candidates.Count} replays...");

        var votes = new ConcurrentBag<(int Abil, string Slot, string Hero)>();
        var done = 0;
        var used = 0;
        var options = new ParseOptions {
            ShouldParseUnits = false,
            ShouldParseStatistics = false,
            ShouldParseMessageEvents = false,
            ShouldParseDetailedBattleLobby = false,
        };

        Parallel.ForEach(candidates, file => {
            try {
                var replay = ReplayFiles.Parse(file, options);
                foreach (var vote in Votes(replay)) {
                    votes.Add(vote);
                }

                Interlocked.Increment(ref used);
            }
            catch (Exception) {
                // Unparseable replays just don't vote.
            }

            var n = Interlocked.Increment(ref done);
            if (n % 25 == 0) {
                progress?.Invoke($"  {n}/{candidates.Count}");
            }
        });

        var entries = votes.GroupBy(v => v.Abil).ToDictionary(g => g.Key, g => {
            var slotVotes = g.GroupBy(v => v.Slot).ToDictionary(s => s.Key, s => s.Count());
            var heroVotes = g.GroupBy(v => v.Hero).ToDictionary(h => h.Key, h => h.Count());
            var top = slotVotes.MaxBy(kv => kv.Value);
            return new Entry(g.Key, top.Key, top.Value, g.Count(), slotVotes, heroVotes);
        });

        return new AbilitySlotTable { Build = build, ReplaysUsed = used, Entries = entries };
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
