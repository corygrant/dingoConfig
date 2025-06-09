namespace dingoConfig.Core.Models;

public class DeviceCommand
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public uint CanId { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public List<CommandParameter> Parameters { get; set; } = new();

    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
            errors.Add("Command Id is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Command Name is required");

        if (Data.Length > 8)
            errors.Add("Command data cannot exceed 8 bytes");

        // Validate parameters
        foreach (var parameter in Parameters)
        {
            var paramValidation = parameter.Validate();
            if (!paramValidation.IsValid)
            {
                errors.AddRange(paramValidation.Errors.Select(e => $"Parameter {parameter.Name}: {e}"));
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

public class CommandParameter
{
    public string Name { get; set; } = string.Empty;
    public ParameterType Type { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public bool IsRequired { get; set; } = true;

    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Parameter Name is required");

        if (Min.HasValue && Max.HasValue && Min.Value > Max.Value)
            errors.Add("Min value cannot be greater than Max value");

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}