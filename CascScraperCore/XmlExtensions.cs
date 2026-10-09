using System.Xml;

namespace CascScraperCore;

/// <summary>
/// Shorthands for reading game data XML, where a property is usually a child element carrying its value in a
/// <c>value</c> attribute: <c>&lt;HeroIcon value="Assets\Textures\foo.dds"/&gt;</c>.
/// </summary>
public static class XmlExtensions {
    /// <summary>The attribute's value, or null if the node doesn't have it.</summary>
    public static string? Attr(this XmlNode node, string name) => node.Attributes?[name]?.Value;

    /// <summary>The attribute's value; throws naming the element if it is missing.</summary>
    public static string RequiredAttr(this XmlNode node, string name) =>
        node.Attr(name) ?? throw new InvalidDataException($"<{node.Name}> has no '{name}' attribute");

    /// <summary>The <c>value</c> attribute of the first node matching <paramref name="xpath"/>, or null.</summary>
    public static string? ValueOf(this XmlNode node, string xpath) => node.SelectSingleNode(xpath)?.Attr("value");
}
