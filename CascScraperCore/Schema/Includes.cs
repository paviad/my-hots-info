using System.Xml.Serialization;

namespace CascScraperCore.Schema;

public class Includes {
    [XmlElement("Catalog")]
    public List<ModCatalog> Catalog { get; set; }
}
