using System.Text.Json.Serialization;
using domain.Enums;

namespace domain.Models;

public class CyclicSigDef
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("startBit")] public int StartBit { get; set; }
    [JsonPropertyName("length")] public int Length { get; set; }
    [JsonPropertyName("byteOrder")] public ByteOrder ByteOrder { get; set; } = ByteOrder.LittleEndian;
    [JsonPropertyName("isSigned")] public bool IsSigned { get; set; }
    [JsonPropertyName("factor")] public double Factor { get; set; } = 1.0;
    [JsonPropertyName("unit")] public string Unit { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; } = 1;
    [JsonPropertyName("startIndex")] public int StartIndex { get; set; }
}
