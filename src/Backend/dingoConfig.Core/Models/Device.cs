using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace dingoConfig.Core.Models;

public class Device
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DeviceType Type { get; set; }
    public List<Parameter> Parameters { get; set; } = new();
    public CommunicationSettings? Communication { get; set; }
    public List<TelemetryItem> Telemetry { get; set; } = new();
    public List<DeviceCommand> Commands { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
            errors.Add("Device Id is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Device Name is required");

        if (Type == DeviceType.Unknown)
            errors.Add("Device Type must be specified");

        // Check for duplicate parameter IDs
        var duplicateParameterIds = Parameters
            .GroupBy(p => p.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicateId in duplicateParameterIds)
        {
            errors.Add($"Duplicate Parameter ID found: {duplicateId}");
        }

        // Check for duplicate telemetry IDs
        var duplicateTelemetryIds = Telemetry
            .GroupBy(t => t.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicateId in duplicateTelemetryIds)
        {
            errors.Add($"Duplicate Telemetry ID found: {duplicateId}");
        }

        // Validate child objects
        foreach (var parameter in Parameters)
        {
            var paramValidation = parameter.Validate();
            if (!paramValidation.IsValid)
            {
                errors.AddRange(paramValidation.Errors.Select(e => $"Parameter {parameter.Id}: {e}"));
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
public enum DeviceType
{
    Unknown,
    DingoPDM,
    DingoPDMMax,
    CANBoard
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}