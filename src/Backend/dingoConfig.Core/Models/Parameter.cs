using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace dingoConfig.Core.Models;

public class Parameter
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ParameterType Type { get; set; }
    public object? DefaultValue { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public bool IsRequired { get; set; } = true;

    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
            errors.Add("Parameter Id is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Parameter Name is required");

        // Validate enum type has options
        if (Type == ParameterType.Enum && (Options == null || Options.Count == 0))
            errors.Add("Enum parameter must have options defined");

        // Validate min/max ranges
        if (Min.HasValue && Max.HasValue && Min.Value > Max.Value)
            errors.Add("Min value cannot be greater than Max value");

        // Validate default value type
        if (DefaultValue != null)
        {
            var defaultValueValidation = ValidateValueType(DefaultValue, Type);
            if (!defaultValueValidation.IsValid)
            {
                errors.AddRange(defaultValueValidation.Errors);
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private ValidationResult ValidateValueType(object value, ParameterType expectedType)
    {
        var errors = new List<string>();

        switch (expectedType)
        {
            case ParameterType.Boolean:
                if (!(value is bool))
                    errors.Add("Default value must be boolean for Boolean parameter type");
                break;
            case ParameterType.Integer:
                if (!(value is int || value is long))
                    errors.Add("Default value must be integer for Integer parameter type");
                break;
            case ParameterType.Float:
                if (!(value is float || value is double || value is decimal))
                    errors.Add("Default value must be numeric for Float parameter type");
                break;
            case ParameterType.String:
                if (!(value is string))
                    errors.Add("Default value must be string for String parameter type");
                break;
            case ParameterType.Enum:
                if (!(value is string enumValue) || !Options.Contains(enumValue))
                    errors.Add("Default value must be one of the defined options for Enum parameter type");
                break;
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ParameterType
{
    Boolean,
    Integer,
    Float,
    String,
    Enum
}