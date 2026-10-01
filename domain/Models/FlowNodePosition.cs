using System.Text.Json.Serialization;

namespace domain.Models;

public record FlowNodePosition(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);
