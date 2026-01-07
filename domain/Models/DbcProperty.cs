using System.Text.Json.Serialization;
using domain.Enums;

namespace domain.Models;

public class DbcProperty(string name)
{
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("startBit")] public int StartBit { get; set; }
    [JsonPropertyName("length")] public int Length { get; set; }
    [JsonPropertyName("byteOrder")] public ByteOrder ByteOrder { get; set; } = ByteOrder.LittleEndian;
    [JsonPropertyName("isSigned")] public bool IsSigned { get; set; }
    [JsonPropertyName("factor")] public double Factor { get; set; } = 1.0;
    [JsonPropertyName("offset")] public double Offset { get; set; }
    [JsonIgnore] public double Value { get; set; }
}