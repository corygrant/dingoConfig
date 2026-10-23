using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;

namespace domain.Models;

public class DbcSignal
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("startBit")] public int StartBit { get; set; }
    [JsonPropertyName("length")] public int Length { get; set; }
    [JsonPropertyName("byteOrder")] public ByteOrder ByteOrder { get; set; } = ByteOrder.LittleEndian;
    [JsonPropertyName("isSigned")] public bool IsSigned { get; set; }
    [JsonPropertyName("factor")] public double Factor { get; set; } = 1.0;
    [JsonPropertyName("offset")] public double Offset { get; set; }
    [JsonPropertyName("unit")] public string Unit { get; set; } = "";
    [JsonPropertyName("min")] public double Min { get; set; }
    [JsonPropertyName("max")] public double Max { get; set; }
    [JsonPropertyName("isMultiplexor")] public bool IsMultiplexor { get; set; }
    // Only decoded when the message's multiplexor equals this value; null = not multiplexed
    [JsonPropertyName("multiplexValue")] public int? MultiplexValue { get; set; }
    // VAL_ table: raw value -> description
    [JsonPropertyName("valueTable")] public Dictionary<long, string>? ValueTable { get; set; }
    [JsonIgnore] [Plotable(displayName: "Value")] public double Value { get; set; }

    [JsonIgnore]
    public string DisplayValue =>
        ValueTable != null && Factor != 0 && ValueTable.TryGetValue((long)Math.Round((Value - Offset) / Factor), out var text)
            ? text
            : $"{Value:F2} {Unit}";
}