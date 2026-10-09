namespace MyReplayQuery.Events;

/// <summary>A point on the map, in map units.</summary>
public readonly record struct MapPoint(double X, double Y) {
    public double DistanceTo(MapPoint other) => Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
    public override string ToString() => $"{X:0.#},{Y:0.#}";
}

/// <summary>A team, relative to nobody; queries resolve <c>ally</c>/<c>enemy</c> against "me".</summary>
public readonly record struct TeamRef(int Team) {
    public override string ToString() => $"team{Team}";
}

/// <summary>A player in the replay. <see cref="Index"/> is the 0-based position in <c>Replay.Players</c>.</summary>
public sealed record PlayerInfo(int Index, string Name, string Hero, int Team, bool IsMe, int BattleNetId) {
    public override string ToString() => $"{Name} ({Hero})";
}

/// <summary>
/// A normalized replay event. <see cref="T"/> is game time in seconds (game loop / 16).
/// <see cref="Fields"/> is what the query language filters on.
/// </summary>
public abstract record ReplayEvent(double T) {
    public abstract string Kind { get; }

    /// <summary>The player who did or suffered the event, used for proximity when there is no <see cref="Point"/>.</summary>
    public virtual PlayerInfo? Actor => null;

    /// <summary>Where the event happened, when the replay says so.</summary>
    public virtual MapPoint? Point => null;

    private IReadOnlyDictionary<string, object?>? _fields;

    public IReadOnlyDictionary<string, object?> Fields => _fields ??= BuildFields();

    protected abstract IReadOnlyDictionary<string, object?> BuildFields();

    public abstract string Describe();
}

public sealed record DeathEvent(double T, PlayerInfo Victim, IReadOnlyList<PlayerInfo> Credit, PlayerInfo? Killer, MapPoint? Position)
    : ReplayEvent(T) {
    public override string Kind => "death";
    public override PlayerInfo Actor => Victim;
    public override MapPoint? Point => Position;

    protected override IReadOnlyDictionary<string, object?> BuildFields() => new Dictionary<string, object?> {
        ["victim"] = Victim,
        ["hero"] = Victim.Hero,
        ["credit"] = Credit,
        ["killer"] = Killer,
        ["x"] = Position?.X,
        ["y"] = Position?.Y,
    };

    public override string Describe() {
        var credit = Credit.Count == 0 ? "nobody" : string.Join(", ", Credit.Select(p => p == Killer ? $"*{p.Name}" : p.Name));
        return $"death  {Victim} by {credit}{(Position is { } p ? $" @{p}" : "")}";
    }
}

public sealed record CastEvent(
    double T,
    PlayerInfo Player,
    int Abil,
    int CmdIndex,
    string? Slot,
    string Label,
    string? Name,
    MapPoint? Target,
    bool HasUnitTarget) : ReplayEvent(T) {
    public override string Kind => "cast";
    public override PlayerInfo Actor => Player;
    public override MapPoint? Point => Target;

    protected override IReadOnlyDictionary<string, object?> BuildFields() => new Dictionary<string, object?> {
        ["player"] = Player,
        ["hero"] = Player.Hero,
        ["abil"] = (double)Abil,
        ["cmd"] = (double)CmdIndex,
        ["slot"] = Slot,
        ["label"] = Label,
        ["name"] = Name,
        ["x"] = Target?.X,
        ["y"] = Target?.Y,
    };

    public override string Describe() {
        var name = Name is null ? "" : $" \"{Name}\"";
        var target = Target is { } t ? $" @{t}" : HasUnitTarget ? " @unit" : "";
        return $"cast   {Player} {Label}{name} [#{Abil}/{CmdIndex}]{target}";
    }
}

public sealed record KeyEvent(double T, PlayerInfo Player, int Code, string Key, bool Down) : ReplayEvent(T) {
    public override string Kind => "key";
    public override PlayerInfo Actor => Player;

    protected override IReadOnlyDictionary<string, object?> BuildFields() => new Dictionary<string, object?> {
        ["player"] = Player,
        ["hero"] = Player.Hero,
        ["key"] = Key,
        ["code"] = (double)Code,
        ["down"] = Down,
    };

    public override string Describe() => $"key    {Player} {Key}{(Down ? "" : " (up)")}";
}

public sealed record ChatEvent(double T, PlayerInfo? Player, string Text) : ReplayEvent(T) {
    public override string Kind => "chat";
    public override PlayerInfo? Actor => Player;

    protected override IReadOnlyDictionary<string, object?> BuildFields() => new Dictionary<string, object?> {
        ["player"] = Player,
        ["hero"] = Player?.Hero,
        ["text"] = Text,
    };

    public override string Describe() => $"chat   {Player?.ToString() ?? "?"}: {Text}";
}

public sealed record PingEvent(double T, PlayerInfo? Player) : ReplayEvent(T) {
    public override string Kind => "ping";
    public override PlayerInfo? Actor => Player;

    protected override IReadOnlyDictionary<string, object?> BuildFields() => new Dictionary<string, object?> {
        ["player"] = Player,
        ["hero"] = Player?.Hero,
    };

    public override string Describe() => $"ping   {Player?.ToString() ?? "?"}";
}

public sealed record TalentEvent(double T, PlayerInfo Player, int Tier, string Name) : ReplayEvent(T) {
    public override string Kind => "talent";
    public override PlayerInfo Actor => Player;

    protected override IReadOnlyDictionary<string, object?> BuildFields() => new Dictionary<string, object?> {
        ["player"] = Player,
        ["hero"] = Player.Hero,
        ["tier"] = (double)Tier,
        ["name"] = Name,
    };

    public override string Describe() => $"talent {Player} L{Tier} {Name}";
}

public sealed record SpawnEvent(double T, PlayerInfo Player, string Unit, MapPoint? Position) : ReplayEvent(T) {
    public override string Kind => "spawn";
    public override PlayerInfo Actor => Player;
    public override MapPoint? Point => Position;

    protected override IReadOnlyDictionary<string, object?> BuildFields() => new Dictionary<string, object?> {
        ["player"] = Player,
        ["hero"] = Player.Hero,
        ["unit"] = Unit,
    };

    public override string Describe() => $"spawn  {Player} {Unit}{(Position is { } p ? $" @{p}" : "")}";
}

/// <summary>A non-hero unit dying: camps, structures, minions, summons, objectives.</summary>
public sealed record UnitDiedEvent(double T, string Unit, string Group, int? Team, PlayerInfo? Killer, string? KillerUnit, MapPoint? Position)
    : ReplayEvent(T) {
    public override string Kind => "unitdied";
    public override PlayerInfo? Actor => Killer;
    public override MapPoint? Point => Position;

    protected override IReadOnlyDictionary<string, object?> BuildFields() => new Dictionary<string, object?> {
        ["unit"] = Unit,
        ["group"] = Group,
        ["team"] = Team is { } t ? new TeamRef(t) : null,
        ["killer"] = Killer,
        ["killerunit"] = KillerUnit,
        ["x"] = Position?.X,
        ["y"] = Position?.Y,
    };

    public override string Describe() =>
        $"died   {Unit} [{Group}] by {Killer?.ToString() ?? KillerUnit ?? "?"}{(Position is { } p ? $" @{p}" : "")}";
}

public sealed record LevelUpEvent(double T, int Team, int Level) : ReplayEvent(T) {
    public override string Kind => "levelup";

    protected override IReadOnlyDictionary<string, object?> BuildFields() => new Dictionary<string, object?> {
        ["team"] = new TeamRef(Team),
        ["level"] = (double)Level,
    };

    public override string Describe() => $"level  team{Team} -> {Level}";
}

public static class EventSchema {
    /// <summary>Field names per event kind, for query validation and help text.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Fields = new Dictionary<string, string[]> {
        ["death"] = ["victim", "hero", "credit", "killer", "x", "y"],
        ["cast"] = ["player", "hero", "abil", "cmd", "slot", "label", "name", "x", "y"],
        ["key"] = ["player", "hero", "key", "code", "down"],
        ["chat"] = ["player", "hero", "text"],
        ["ping"] = ["player", "hero"],
        ["talent"] = ["player", "hero", "tier", "name"],
        ["spawn"] = ["player", "hero", "unit"],
        ["unitdied"] = ["unit", "group", "team", "killer", "killerunit", "x", "y"],
        ["levelup"] = ["team", "level"],
    };
}
