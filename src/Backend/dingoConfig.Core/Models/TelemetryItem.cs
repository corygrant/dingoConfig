using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace dingoConfig.Core.Models;

public class TelemetryItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TelemetryType Type { get; set; }
    public string Unit { get; set; } = string.Empty;
    public uint CanId { get; set; }
    public int ByteOffset { get; set; }
    public int ByteLength { get; set; }
    public double Scale { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public string Description { get; set; } = string.Empty;

    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
            errors.Add("Telemetry Id is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Telemetry Name is required");

        if (ByteOffset < 0)
            errors.Add("ByteOffset cannot be negative");

        if (ByteLength <= 0)
            errors.Add("ByteLength must be positive");

        if (ByteOffset + ByteLength > 8)
            errors.Add("ByteOffset + ByteLength cannot exceed 8 (CAN message size)");

        if (Scale == 0)
            errors.Add("Scale cannot be zero");

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum TelemetryType
{
    Boolean,
    Integer,
    Float,
    String
}