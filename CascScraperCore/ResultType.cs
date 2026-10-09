using System.Xml;

namespace CascScraperCore;

internal class ResultType {
    public ResultType(decimal value) {
        IsDecimal = true;
        Value = value;
    }

    public ResultType(XmlNode? obj) {
        Node = obj;
    }

    public bool IsDecimal { get; set; }
    public decimal Value { get; set; }

    /// <summary>The object an object spec resolved to; null for numbers and for objects that weren't found.</summary>
    public XmlNode? Node { get; set; }
}
