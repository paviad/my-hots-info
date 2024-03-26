using System.Xml.Serialization;

namespace CascScraper.Schema
{
    public class ModCatalog
    {
        [XmlAttribute("path")]
        public string Path { get; set; }
    }
}
