using System.Collections;
using System.Reflection;
using application.Models;
using domain.Common;
using domain.Interfaces;
using domain.Models;

namespace application.Common;

/// <summary>
/// Discovers [Plotable] properties on a device: on the device itself, on items of its List&lt;T&gt;
/// properties, and on its single nested objects (one level deep).
/// </summary>
public static class PlotReferenceFactory
{
    public static List<IPlotReference> Create(object? source)
    {
        var plotRefs = new List<IPlotReference>();

        if (source == null)
            return plotRefs;

        // Use runtime type instead of compile-time type to get concrete properties
        var properties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            // Check if this property itself is plotable
            if (GetPlotable(prop) is { } attr)
            {
                plotRefs.Add(new PlotReference(source, prop, attr.DisplayName, attr.Unit));
            }

            // Explore collections (List<T>) - ONE LEVEL DEEP ONLY
            if (prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (prop.GetValue(source) is not IEnumerable list) continue;

                var index = 0;
                foreach (var item in list)
                {
                    if (item != null)
                    {
                        foreach (var itemProp in item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (GetPlotable(itemProp) is not { } itemAttr) continue;

                            // DbcSignals are identified by their own name and unit
                            plotRefs.Add(item is DbcSignal signal
                                ? new PlotReference(item, itemProp, signal.Name, signal.Unit, prop.Name,
                                    property: () => signal.Name)
                                : new PlotReference(item, itemProp, $"{prop.Name}[{index}].{itemAttr.DisplayName}",
                                    itemAttr.Unit, prop.Name, OwnerOf(item, $"{prop.Name}[{index}]"),
                                    () => itemAttr.DisplayName));
                        }
                    }
                    index++;
                }
            }
            // Explore single complex objects (ONE LEVEL DEEP ONLY)
            else if (prop.PropertyType.IsClass &&
                     prop.PropertyType != typeof(string) &&
                     !prop.PropertyType.IsPrimitive)
            {
                var nestedObject = prop.GetValue(source);
                if (nestedObject == null) continue;

                foreach (var nestedProp in nestedObject.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (GetPlotable(nestedProp) is not { } nestedAttr) continue;

                    plotRefs.Add(new PlotReference(nestedObject, nestedProp, $"{prop.Name}.{nestedAttr.DisplayName}",
                        nestedAttr.Unit, prop.Name, OwnerOf(nestedObject, prop.Name), () => nestedAttr.DisplayName));
                }
            }
        }

        return plotRefs;
    }

    /// <summary>
    /// Returns the attribute if the property is [Plotable] and of a plottable type (bool, int, double, enum)
    /// </summary>
    private static PlotableAttribute? GetPlotable(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<PlotableAttribute>();
        if (attr?.DisplayName is not { Length: > 0 })
            return null;

        var type = prop.PropertyType;
        return type == typeof(bool) || type == typeof(int) || type == typeof(double) || type.IsEnum
            ? attr
            : null;
    }

    /// <summary>Functions are named by their (user-editable) name, anything else by its path</summary>
    private static Func<string> OwnerOf(object item, string path) =>
        item is IDeviceFunction function ? () => function.Name : () => path;
}
