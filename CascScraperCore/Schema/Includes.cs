using System.Collections.Generic;
using System.Xml.Serialization;

namespace CascScraper.Schema
{
    public class Includes
    {
        [XmlElement("Catalog")]
        public List<ModCatalog> Catalog { get; set; }
    }
}
