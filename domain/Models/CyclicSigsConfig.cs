using System.Text.Json.Serialization;

namespace domain.Models;

public class CyclicSigsConfig
{
    [JsonPropertyName("messages")] public List<CyclicMsgDef> Messages { get; set; } = [];
}
