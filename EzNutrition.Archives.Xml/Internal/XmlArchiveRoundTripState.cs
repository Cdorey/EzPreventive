using System.Xml.Linq;
using EzNutrition.Archives.Contracts.Serialization;

namespace EzNutrition.Archives.Xml.Internal;

internal sealed class XmlArchiveRoundTripState : ArchiveRoundTripState
{
    public XmlArchiveRoundTripState(
        XDocument source,
        string semanticFingerprint,
        bool containsUnknownContent)
        : base(XmlArchiveFormat.CodecIdentifier, containsUnknownContent)
    {
        Source = new XDocument(source);
        SemanticFingerprint = semanticFingerprint;
    }

    public XDocument Source { get; }

    public string SemanticFingerprint { get; }
}
