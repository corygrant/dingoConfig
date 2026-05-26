using System.Text.Json.Serialization;

namespace domain.Models;

public class CyclicMsgDef
{
    // Offset from BaseId
    [JsonPropertyName("idOffset")] public int IdOffset { get; set; }
    [JsonPropertyName("signals")] public List<CyclicSigDef> Signals { get; set; } = [];
}
