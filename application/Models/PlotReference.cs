using System.Reflection;

namespace application.Models;

public interface IPlotReference
{
    /// <summary>Stable path within the device, e.g. "Outputs[0].Current"</summary>
    string Name { get; }
    /// <summary>Human-readable name; follows renames of the owning function, e.g. "Headlights Current"</summary>
    string Label { get; }
    /// <summary>Name of the owning function, e.g. "Headlights"; empty for device-level values and DBC signals</summary>
    string Owner { get; }
    /// <summary>Name of the value within its owner, e.g. "Current"</summary>
    string Property { get; }
    /// <summary>Owning collection/property, e.g. "Outputs", or "Device" for device-level values</summary>
    string Group { get; }
    string Unit { get; }
    /// <summary>bool/int/enum values change in steps and are drawn as stepped lines</summary>
    bool IsDiscrete { get; }
    double GetValue();
    object? SourceObject { get; }
    PropertyInfo Prop { get; }
}

public class PlotReference(
    object source,
    PropertyInfo prop,
    string name,
    string unit,
    string group = "Device",
    Func<string>? owner = null,
    Func<string>? property = null)
    : IPlotReference
{
    public string Name { get; } = name;
    public string Owner => owner?.Invoke() ?? "";
    public string Property => property?.Invoke() ?? Name;
    public string Label => Owner.Length > 0 ? $"{Owner} {Property}" : Property;
    public string Group { get; } = group;
    public string Unit { get; } = unit;
    public bool IsDiscrete { get; } = prop.PropertyType != typeof(double);
    public object? SourceObject => source;
    public PropertyInfo Prop => prop;

    public double GetValue()
    {
        var value = prop.GetValue(source);

        return value switch
        {
            null => 0.0,
            bool b => b ? 1.0 : 0.0,
            int i => i,
            double d => d,
            Enum e => Convert.ToInt32(e),
            _ => 0.0
        };
    }
}
