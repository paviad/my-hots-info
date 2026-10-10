using System.Collections.Concurrent;
using Heroes.ReplayParser;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using MyReplayQuery.Abilities;
using MyReplayQuery.Events;

namespace MyHotsObserver;

public sealed record PlayerSummary(int Index, string FocusKey, string Name, string Hero, int Team, bool IsMe, bool Won);

public sealed record ReplaySummary(
    string Path,
    DateTime Played,
    string Map,
    string Mode,
    TimeSpan Length,
    IReadOnlyList<PlayerSummary> Players);

/// <summary>The replays on this machine: summaries for listing, and full timelines for queries.</summary>
public sealed class ReplayCatalog(ILogger<ReplayCatalog> logger) {
    private const int TimelinesToKeep = 3;

    private readonly ConcurrentDictionary<(string, DateTime), ReplaySummary?> _summaries = new();
    private readonly ConcurrentDictionary<int, Lazy<IAbilityLabels>> _labels = new();
    private readonly Lock _buildLock = new();
    private readonly List<(string Path, DateTime Written, bool Named, ReplayTimeline Timeline)> _timelines = [];
    private readonly Lock _timelinesLock = new();

    /// <summary>Resolves a path, <c>latest</c>, or <c>N</c> for the N-th newest replay.</summary>
    public static string Resolve(string spec) {
        try {
            return ReplayFiles.Resolve(spec);
        }
        catch (ArgumentException e) {
            throw new McpException(e.Message);
        }
    }

    /// <summary>
    /// The newest replays whose map, mode, hero or player names contain every word of
    /// <paramref name="filter"/>, looking at no more than <paramref name="scanLimit"/> replays.
    /// </summary>
    public (IReadOnlyList<ReplaySummary> Matches, int Scanned) List(string? filter, int limit, int scanLimit) {
        var words = (filter ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var files = ReplayFiles.AllReplays().Take(scanLimit).ToList();
        var matches = new List<ReplaySummary>();
        var scanned = 0;
        foreach (var chunk in files.Chunk(16)) {
            var summaries = chunk.AsParallel().AsOrdered().Select(f => Summary(f.FullName)).ToList();
            scanned += chunk.Length;
            matches.AddRange(summaries.OfType<ReplaySummary>().Where(s => words.All(w => Matches(s, w))));
            if (matches.Count >= limit) {
                break;
            }
        }

        return (matches.Take(limit).ToList(), scanned);
    }

    /// <summary>The replay's header facts and players; null when it can't be parsed.</summary>
    public ReplaySummary? Summary(string path) {
        var key = (path, File.GetLastWriteTimeUtc(path));
        return _summaries.GetOrAdd(key, _ => {
            try {
                var replay = ReplayFiles.Parse(path, ParseOptions.MinimalParsing);
                var me = ReplayFiles.MyToonId(path);
                return new ReplaySummary(
                    path,
                    replay.Timestamp.ToLocalTime(),
                    replay.Map,
                    replay.GameMode.ToString(),
                    replay.ReplayLength,
                    replay.Players.Select((p, i) => new PlayerSummary(
                        i, FocusKey(i), p.Name, p.Character, p.Team, p.BattleNetId == me, p.IsWinner)).ToList());
            }
            catch (Exception e) {
                logger.LogInformation(e, "Couldn't summarize {path}", path);
                return null;
            }
        });
    }

    /// <summary>The replay's full event timeline.</summary>
    /// <param name="abilityNames">
    /// Label casts with ability slots and names. That needs the build's ability table, which takes
    /// minutes to build from all replays of the build when it isn't cached; without it, casts show
    /// as Hero.#id.
    /// </param>
    public ReplayTimeline Timeline(string path, bool abilityNames = false) {
        var written = File.GetLastWriteTimeUtc(path);
        lock (_timelinesLock) {
            var cached = _timelines.FirstOrDefault(t => t.Path == path && t.Written == written && (t.Named || !abilityNames));
            if (cached.Timeline is not null) {
                return cached.Timeline;
            }
        }

        ReplayTimeline timeline;
        try {
            timeline = TimelineBuilder.Load(path, abilityNames ? Labels(path) : NoAbilityLabels.Instance);
        }
        catch (InvalidDataException e) {
            throw new McpException(e.Message);
        }

        lock (_timelinesLock) {
            _timelines.RemoveAll(t => t.Path == path);
            _timelines.Insert(0, (path, written, abilityNames, timeline));
            if (_timelines.Count > TimelinesToKeep) {
                _timelines.RemoveAt(_timelines.Count - 1);
            }
        }

        return timeline;
    }

    /// <summary>The key that follows player <paramref name="index"/> in the observer UI: 1…9, then 0.</summary>
    public static string FocusKey(int index) => index is >= 0 and <= 9 ? ((index + 1) % 10).ToString() : "";

    /// <summary>
    /// The ability names of the replay's build, from its cached table or built from every replay of
    /// that build. One build at a time: each parses many replays, and several at once ran the
    /// server out of memory.
    /// </summary>
    private IAbilityLabels Labels(string path) {
        var build = ReplayFiles.ReadBuild(path);
        return _labels.GetOrAdd(build, b => new Lazy<IAbilityLabels>(() => {
            lock (_buildLock) {
                return AbilitySlotTable.GetOrBuild(
                    b,
                    ReplayFiles.AllReplays().Select(f => f.FullName).ToList(),
                    progress: line => logger.LogInformation("Ability table {build}: {line}", b, line));
            }
        })).Value;
    }

    private static bool Matches(ReplaySummary s, string word) {
        bool Has(string value) => value.Contains(word, StringComparison.OrdinalIgnoreCase);
        return Has(s.Map) || Has(s.Mode) || s.Players.Any(p => Has(p.Hero) || Has(p.Name));
    }
}
