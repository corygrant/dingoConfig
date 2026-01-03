namespace application.Models;

/// <summary>
/// Describes the source of a plottable signal (output, input, CAN input, etc.)
/// </summary>
public enum SignalSource
{
    Output,
    Input,
    CanInput,
    VirtualInput,
    Counter,
    Condition,
    Flasher,
    Wiper,
    DeviceProperty  // For battery voltage, total current, board temperature
}

/// <summary>
/// Describes the property of a signal to plot
/// </summary>
public enum SignalProperty
{
    State,           // Boolean/enum value (default for most)
    Current,         // For outputs (Amps)
    DutyCycle,       // For outputs (%)
    Value,           // For CAN inputs, counters (numeric value)
    BatteryVoltage,  // Device property
    TotalCurrent,    // Device property
    BoardTemperature // Device property
}

/// <summary>
/// Dynamic signal descriptor that works with any device type.
/// Uses (Source, Index, Property) tuple instead of hardcoded enums.
/// </summary>
public class PlotSignalDescriptor
{
    /// <summary>
    /// The source collection (e.g., Output, Input, CanInput)
    /// </summary>
    public SignalSource Source { get; set; }

    /// <summary>
    /// Index into the source collection (0-based).
    /// Null for DeviceProperty signals.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// The property to extract (e.g., Current, DutyCycle, State)
    /// </summary>
    public SignalProperty Property { get; set; }

    /// <summary>
    /// Unique key for tracking this signal.
    /// Format: "Source_Index_Property" or "Source_Property" for device properties.
    /// </summary>
    public string Key => Index.HasValue
        ? $"{Source}_{Index}_{Property}"
        : $"{Source}_{Property}";

    /// <summary>
    /// User-friendly display name for the signal.
    /// </summary>
    public string GetDisplayName()
    {
        if (Index.HasValue)
        {
            // "Output 1 Current", "Input 2 State", etc.
            return $"{Source} {Index.Value + 1} {Property}";
        }
        else
        {
            // "Battery Voltage", "Total Current", "Board Temperature"
            return Property switch
            {
                SignalProperty.BatteryVoltage => "Battery Voltage",
                SignalProperty.TotalCurrent => "Total Current",
                SignalProperty.BoardTemperature => "Board Temperature",
                _ => Property.ToString()
            };
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is PlotSignalDescriptor other && Key == other.Key;
    }

    public override int GetHashCode()
    {
        return Key.GetHashCode();
    }

    public override string ToString()
    {
        return GetDisplayName();
    }
}
