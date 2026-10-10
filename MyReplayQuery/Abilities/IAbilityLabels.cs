namespace MyReplayQuery.Abilities;

public readonly record struct AbilityLabel(string? Slot, string Label, string? Name);

/// <summary>Turns a cast's ability link id into something readable.</summary>
public interface IAbilityLabels {
    AbilityLabel Label(int abil, string hero);

    /// <summary>The heroes known to cast <paramref name="abil"/>.</summary>
    IReadOnlyCollection<string> Heroes(int abil);
}

/// <summary>No knowledge of ability ids: every cast renders as <c>Hero.#1291</c>.</summary>
public sealed class NoAbilityLabels : IAbilityLabels {
    public static readonly NoAbilityLabels Instance = new();

    public AbilityLabel Label(int abil, string hero) => new(null, $"{hero}.#{abil}", null);

    public IReadOnlyCollection<string> Heroes(int abil) => [];
}
