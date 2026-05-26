using System.Text.Json.Serialization;
using domain.Enums;

namespace domain.Models;

public class CyclicSigDef
{
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; } = 1;
    [JsonPropertyName("startIndex")] public int StartIndex { get; set; }
    [JsonPropertyName("dbc")] public DbcSignal Dbc { get; set; } = new();
}
