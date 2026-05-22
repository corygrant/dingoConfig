using System.Text.Json.Serialization;

namespace domain.Models;

public class CyclicMsgDef
{
    // Offset from BaseId for the first (or only) message in this group.
    [JsonPropertyName("offset")] public int Offset { get; set; }

    [JsonPropertyName("signals")] public List<CyclicSigDef> Signals { get; set; } = [];
}
