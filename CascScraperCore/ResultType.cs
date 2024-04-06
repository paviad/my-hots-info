using System.Xml;

namespace CascScraperCore;

internal class ResultType {
    public ResultType(decimal value) {
        IsDecimal = true;
        Value = value;
    }

    public ResultType(XmlNode obj) {
        Node = obj;
    }

    public bool IsDecimal { get; set; }
    public decimal Value { get; set; }

    public XmlNode Node { get; set; }
}
