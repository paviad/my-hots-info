namespace CascScraperCore;

/// <summary>
/// The layout of one game mod in CASC: <c>base.stormdata</c> holds GameData, <c>enus.stormdata</c> the English
/// strings and <c>base.stormassets</c> the textures and other assets.
/// </summary>
public sealed record StormMod(string Root) {
    public string Name => CascFileSystem.GetFileName(Root);
    public string BaseData => $"{Root}/base.stormdata";
    public string GameData => $"{BaseData}/GameData";

    /// <summary>Lists the mod's catalogs (<c>GameData/...</c> paths, relative to <see cref="BaseData"/>).</summary>
    public string GameDataConfig => $"{BaseData}/GameData.xml";

    public string GameStrings => $"{Root}/enus.stormdata/LocalizedData/GameStrings.txt";

    /// <summary>Resolves an asset reference as written in game data, e.g. <c>Assets\Textures\foo.dds</c>.</summary>
    public string Asset(string assetRef) => CascFileSystem.Combine(Root, "base.stormassets", assetRef);
}

public static class Mods {
    public const string HeroModsDir = "mods/heromods";

    public static readonly StormMod Core = new("mods/core.stormmod");
    public static readonly StormMod HeroesData = new("mods/heroesdata.stormmod");
    public static readonly StormMod Heroes = new("mods/heroes.stormmod");
}
