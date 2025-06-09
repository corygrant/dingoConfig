using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace dingoConfig.Core.Models;

public class CommunicationSettings
{
    public CommunicationProtocol Protocol { get; set; }
    public int BaudRate { get; set; } = 500000;
    public uint BaseId { get; set; }
    public int MaxNodes { get; set; } = 16;
    public int TimeoutMs { get; set; } = 1000;
    public int RetryCount { get; set; } = 3;

    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (BaudRate <= 0)
            errors.Add("BaudRate must be positive");

        if (MaxNodes <= 0)
            errors.Add("MaxNodes must be positive");

        if (TimeoutMs <= 0)
            errors.Add("TimeoutMs must be positive");

        if (RetryCount < 0)
            errors.Add("RetryCount cannot be negative");

        // Validate CAN-specific settings
        if (Protocol == CommunicationProtocol.CAN)
        {
            var validBaudRates = new[] { 125000, 250000, 500000, 1000000 };
            if (!validBaudRates.Contains(BaudRate))
                errors.Add("CAN BaudRate must be one of: 125000, 250000, 500000, 1000000");

            if (BaseId > 0x7FF && BaseId <= 0x1FFFFFFF)
            {
                // Extended CAN ID range is valid
            }
            else if (BaseId > 0x7FF)
            {
                errors.Add("CAN BaseId must be within standard (0-0x7FF) or extended (0-0x1FFFFFFF) range");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum CommunicationProtocol
{
    Unknown,
    CAN,
    Serial,
    TCP,
    UDP
}