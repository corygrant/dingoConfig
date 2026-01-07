using System.Reflection;

namespace application.Models;

public interface IPlotReference
{
    string Name { get; }
    string Unit { get; }
    double GetValue();
    object? SourceObject { get; }
    PropertyInfo Prop { get; }
}

public class PlotReference<T>(
    T source,
    PropertyInfo prop,
    string name,
    string unit)
    : IPlotReference
{
    public string Name { get; } = name;
    public string Unit { get; } = unit;
    public object? SourceObject => source;
    public PropertyInfo Prop => prop;

    public double GetValue()
    {
        var value = prop.GetValue(source);

        if (value == null)
            return 0.0;

        if (prop.PropertyType == typeof(bool))
            return (bool)value ? 1.0 : 0.0;

        if (prop.PropertyType == typeof(int))
            return (int)value;

        if (prop.PropertyType == typeof(double))
            return (double)value;

        if (prop.PropertyType.IsEnum)
            return Convert.ToInt32(value);

        return 0.0;
    }
}