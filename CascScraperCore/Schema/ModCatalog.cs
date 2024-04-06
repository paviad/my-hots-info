using System.Xml.Serialization;

namespace CascScraperCore.Schema;

public class ModCatalog {
    [XmlAttribute("path")]
    public string Path { get; set; }
}
