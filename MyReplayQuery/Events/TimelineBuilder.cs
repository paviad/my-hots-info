using Heroes.ReplayParser;
using Heroes.ReplayParser.MPQFiles;
using MyReplayQuery.Abilities;

namespace MyReplayQuery.Events;

/// <summary>Flattens a parsed <see cref="Replay"/> into a <see cref="ReplayTimeline"/>.</summary>
public static class TimelineBuilder {
    private static readonly int[] TalentTiers = [1, 4, 7, 10, 13, 16, 20];

    /// <summary>Parse options that keep everything the timeline uses (units, positions, chat).</summary>
    public static ParseOptions ParseOptions => ParseOptions.FullParsing;

    public static ReplayTimeline Load(string path, IAbilityLabels? labels = null, string? me = null) {
        var replay = ReplayFiles.Parse(path, ParseOptions);
        return Build(replay, path, labels ?? NoAbilityLabels.Instance, ReplayFiles.MyToonId(path), me);
    }

    /// <param name="myToonId">Battle.net id of "me", normally taken from the replay's folder.</param>
    /// <param name="me">Player or hero name of "me"; overrides <paramref name="myToonId"/>.</param>
    public static ReplayTimeline Build(Replay replay, string source, IAbilityLabels labels, int? myToonId, string? me = null) {
        var players = replay.Players.Select((p, i) => new PlayerInfo(
            i, p.Name, p.Character, p.Team,
            me is not null
                ? string.Equals(p.Name, me, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Character, me, StringComparison.OrdinalIgnoreCase)
                : p.BattleNetId == myToonId,
            p.BattleNetId)).ToList();
        var byPlayer = replay.Players.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => players[x.i]);
        PlayerInfo? Info(Player? p) => p is not null && byPlayer.TryGetValue(p, out var info) ? info : null;

        var events = new List<ReplayEvent>();
        AddDeaths(replay, players, byPlayer, events);
        AddGameEvents(replay, Info, labels, events);
        AddChats(replay, Info, events);
        AddTalents(replay, players, events);
        AddUnits(replay, Info, events);
        AddLevels(replay, events);

        var positions = new Dictionary<int, PositionTrack>();
        foreach (var (player, info) in byPlayer) {
            var samples = HeroUnits(replay, player)
                .SelectMany(u => u.Positions)
                .Select(p => new PositionSample(p.TimeSpan.TotalSeconds, new MapPoint(p.Point.X, p.Point.Y), p.IsEstimated))
                .OrderBy(s => s.T)
                .ToList();
            positions[info.Index] = new PositionTrack(samples);
        }

        return new ReplayTimeline {
            Source = source,
            Map = replay.Map,
            Build = replay.ReplayBuild,
            Timestamp = replay.Timestamp,
            Length = replay.Frames / 16.0,
            Players = players,
            Events = events.OrderBy(e => e.T).ToList(),
            Positions = positions,
        };
    }

    private static IEnumerable<Unit> HeroUnits(Replay replay, Player player) =>
        player.HeroUnits.Count > 0
            ? player.HeroUnits
            : replay.Units.Where(u => u.PlayerControlledBy == player && u.Name.StartsWith("Hero"));

    private static void AddDeaths(Replay replay, List<PlayerInfo> players, Dictionary<Player, PlayerInfo> byPlayer, List<ReplayEvent> events) {
        // PlayerDeath ids are tracker player ids; PlayerSetupEvent maps them to working-set slots, as Statistics.cs does.
        var idToPlayer = new Dictionary<int, PlayerInfo>();
        foreach (var setup in replay.TrackerEvents.Where(t => t.TrackerEventType == ReplayTrackerEvents.TrackerEventType.PlayerSetupEvent)) {
            var slot = setup.Data.dictionary[3].optionalData?.vInt;
            if (slot is { } s && s < replay.ClientListByWorkingSetSlotID.Length
                && replay.ClientListByWorkingSetSlotID[s] is { } p && byPlayer.TryGetValue(p, out var info)) {
                idToPlayer[(int)setup.Data.dictionary[0].vInt!.Value] = info;
            }
        }

        PlayerInfo? ById(int id) =>
            idToPlayer.TryGetValue(id, out var p) ? p : id >= 1 && id <= players.Count ? players[id - 1] : null;

        var heroDeaths = replay.Players
            .SelectMany(p => HeroUnits(replay, p).Where(u => u.TimeSpanDied is not null).Select(u => (Info: byPlayer[p], Unit: u)))
            .ToList();

        foreach (var t in replay.TrackerEvents.Where(t => t.TrackerEventType == ReplayTrackerEvents.TrackerEventType.StatGameEvent
                                                         && t.Data.dictionary[0].blobText == "PlayerDeath")) {
            var ints = t.Data.dictionary[2].optionalData?.array;
            if (ints is null || ints.Length == 0 || ById((int)ints[0].dictionary[1].vInt!.Value) is not { } victim) {
                continue;
            }

            var credit = ints.Skip(1).Select(i => ById((int)i.dictionary[1].vInt!.Value)).OfType<PlayerInfo>().Distinct().ToList();
            var fixeds = t.Data.dictionary[3].optionalData?.array ?? [];
            double? Fixed(string key) => fixeds.FirstOrDefault(f => f.dictionary[0].dictionary[0].blobText == key)?.dictionary[1].vInt;
            MapPoint? pos = Fixed("PositionX") is { } x && Fixed("PositionY") is { } y ? new MapPoint(x, y) : null;

            var seconds = t.TimeSpan.TotalSeconds;
            var unit = heroDeaths
                .Where(d => d.Info == victim && Math.Abs(d.Unit.TimeSpanDied!.Value.TotalSeconds - seconds) <= 1)
                .Select(d => d.Unit)
                .FirstOrDefault();
            var killer = unit?.PlayerKilledBy is { } k && byPlayer.TryGetValue(k, out var ki) ? ki : null;

            events.Add(new DeathEvent(t.GameLoop / 16.0, victim, credit, killer, pos));
        }
    }

    private static void AddGameEvents(Replay replay, Func<Player?, PlayerInfo?> info, IAbilityLabels labels, List<ReplayEvent> events) {
        foreach (var e in replay.GameEvents) {
            if (info(e.player) is not { } player) {
                continue;
            }

            var t = e.ticksElapsed / 16.0;
            switch (e.eventType) {
                case GameEventType.CCmdEvent when e.data.array[1] is { } abilData: {
                    var abil = (int)abilData.array[0].unsignedInt!.Value;
                    var cmd = (int)abilData.array[1].unsignedInt!.Value;
                    var (target, unitTarget) = CmdTarget(e.data.array[2]);
                    var label = labels.Label(abil, player.Hero);
                    events.Add(new CastEvent(t, player, abil, cmd, label.Slot, label.Label, label.Name, target, unitTarget));
                    break;
                }
                case GameEventType.CTriggerKeyPressedEvent: {
                    var code = (int)e.data.array[0].vInt!.Value;
                    var flags = (int)e.data.array[1].vInt!.Value;
                    // Each press arrives as a pair: flags with bit 3 set on key down, cleared on key up.
                    events.Add(new KeyEvent(t, player, code, KeyCodes.Name(code), (flags & 8) != 0));
                    break;
                }
                case GameEventType.CTriggerPingEvent:
                    events.Add(new PingEvent(t, player));
                    break;
            }
        }
    }

    private static (MapPoint? Point, bool UnitTarget) CmdTarget(TrackerEventStructure? data) {
        static double Coord(TrackerEventStructure s) => s.unsignedInt!.Value / 4096.0;

        return data?.array switch {
            { Length: 3 } point => (new MapPoint(Coord(point[0]), Coord(point[1])), false),
            { Length: 7 } unit when unit[6].array is { } snap => (new MapPoint(Coord(snap[0]), Coord(snap[1])), true),
            _ => (null, false),
        };
    }

    private static void AddChats(Replay replay, Func<Player?, PlayerInfo?> info, List<ReplayEvent> events) {
        foreach (var m in replay.Messages.Where(m => m.MessageEventType == ReplayMessageEvents.MessageEventType.SChatMessage)) {
            events.Add(new ChatEvent(m.Timestamp.TotalSeconds, info(m.MessageSender), m.ChatMessage?.Message ?? ""));
        }
    }

    private static void AddTalents(Replay replay, List<PlayerInfo> players, List<ReplayEvent> events) {
        for (var i = 0; i < replay.Players.Length; i++) {
            var talents = replay.Players[i].Talents;
            for (var j = 0; j < talents.Length; j++) {
                var tier = j < TalentTiers.Length ? TalentTiers[j] : 0;
                events.Add(new TalentEvent(talents[j].TimeSpanSelected.TotalSeconds, players[i], tier,
                    talents[j].TalentName ?? $"#{talents[j].TalentID}"));
            }
        }
    }

    private static void AddUnits(Replay replay, Func<Player?, PlayerInfo?> info, List<ReplayEvent> events) {
        var heroUnits = replay.Players.SelectMany(p => HeroUnits(replay, p).Select(u => (u, p))).ToDictionary(x => x.u, x => x.p);

        foreach (var u in replay.Units) {
            if (heroUnits.TryGetValue(u, out var owner)) {
                if (info(owner) is { } p) {
                    events.Add(new SpawnEvent(u.TimeSpanBorn.TotalSeconds, p, u.Name, u.PointBorn is { } b ? new MapPoint(b.X, b.Y) : null));
                }

                continue;
            }

            if (u.TimeSpanDied is { } died) {
                events.Add(new UnitDiedEvent(died.TotalSeconds, u.Name, u.Group.ToString(), u.Team, info(u.PlayerKilledBy),
                    u.UnitKilledBy?.Name, u.PointDied is { } d ? new MapPoint(d.X, d.Y) : null));
            }
        }
    }

    private static void AddLevels(Replay replay, List<ReplayEvent> events) {
        for (var team = 0; team < replay.TeamLevels.Length; team++) {
            foreach (var (level, at) in replay.TeamLevels[team] ?? []) {
                events.Add(new LevelUpEvent(at.TotalSeconds, team, level));
            }
        }
    }
}
