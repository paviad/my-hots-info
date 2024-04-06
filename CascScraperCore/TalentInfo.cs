using System.Xml.Serialization;

namespace CascScraperCore;

public class TalentInfo {
    public string HeroName { get; set; }
    public string TalentIdString { get; set; }
    public int TalentId { get; set; }
    public string TalentName { get; set; }
    public string TalentDescription { get; set; }
    public int Tier { get; set; }
    public int Column { get; set; }

    [XmlIgnore] // This was only required for import into the HotsLogs.com site.
    public byte[] Image { get; set; }
}
