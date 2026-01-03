using application.Models;
using domain.Devices.dingoPdm;
using domain.Interfaces;

namespace application.Services;

/// <summary>
/// Dynamically extracts signal values from devices using collection accessors.
/// Works with any device type without hardcoded function counts.
/// </summary>
public static class SignalValueExtractor
{
    /// <summary>
    /// Extracts the current value of a signal from a device.
    /// Converts all values to double for plotting.
    /// </summary>
    /// <param name="device">The device to extract from</param>
    /// <param name="descriptor">The signal descriptor</param>
    /// <returns>The signal value as a double</returns>
    public static double GetValue(IDevice device, PlotSignalDescriptor descriptor)
    {
        // Currently only supports PdmDevice and its subclasses
        if (device is not PdmDevice pdmDevice)
            return 0.0;

        return descriptor.Source switch
        {
            // Output signals
            SignalSource.Output when descriptor.Property == SignalProperty.Current
                => GetOutputCurrent(pdmDevice, descriptor.Index!.Value),

            SignalSource.Output when descriptor.Property == SignalProperty.DutyCycle
                => GetOutputDutyCycle(pdmDevice, descriptor.Index!.Value),

            SignalSource.Output when descriptor.Property == SignalProperty.State
                => GetOutputState(pdmDevice, descriptor.Index!.Value),

            // Input signals
            SignalSource.Input when descriptor.Property == SignalProperty.State
                => GetInputState(pdmDevice, descriptor.Index!.Value),

            // CAN Input signals
            SignalSource.CanInput when descriptor.Property == SignalProperty.State
                => GetCanInputState(pdmDevice, descriptor.Index!.Value),

            SignalSource.CanInput when descriptor.Property == SignalProperty.Value
                => GetCanInputValue(pdmDevice, descriptor.Index!.Value),

            // Virtual Input signals
            SignalSource.VirtualInput when descriptor.Property == SignalProperty.State
                => GetVirtualInputState(pdmDevice, descriptor.Index!.Value),

            // Counter signals
            SignalSource.Counter when descriptor.Property == SignalProperty.Value
                => GetCounterValue(pdmDevice, descriptor.Index!.Value),

            // Condition signals
            SignalSource.Condition when descriptor.Property == SignalProperty.State
                => GetConditionState(pdmDevice, descriptor.Index!.Value),

            // Flasher signals
            SignalSource.Flasher when descriptor.Property == SignalProperty.State
                => GetFlasherState(pdmDevice, descriptor.Index!.Value),

            // Wiper signals
            SignalSource.Wiper when descriptor.Property == SignalProperty.State
                => GetWiperState(pdmDevice, descriptor.Index!.Value),

            // Device properties
            SignalSource.DeviceProperty when descriptor.Property == SignalProperty.BatteryVoltage
                => pdmDevice.BatteryVoltage,

            SignalSource.DeviceProperty when descriptor.Property == SignalProperty.TotalCurrent
                => pdmDevice.TotalCurrent,

            SignalSource.DeviceProperty when descriptor.Property == SignalProperty.BoardTemperature
                => pdmDevice.BoardTempC,

            // Default
            _ => 0.0
        };
    }

    private static double GetOutputCurrent(PdmDevice device, int index)
    {
        if (index < 0 || index >= device.Outputs.Count)
            return 0.0;
        return device.Outputs[index].Current;
    }

    private static double GetOutputDutyCycle(PdmDevice device, int index)
    {
        if (index < 0 || index >= device.Outputs.Count)
            return 0.0;
        return device.Outputs[index].CurrentDutyCycle;
    }

    private static double GetOutputState(PdmDevice device, int index)
    {
        if (index < 0 || index >= device.Outputs.Count)
            return 0.0;
        return (double)device.Outputs[index].State;
    }

    private static double GetInputState(PdmDevice device, int index)
    {
        if (index < 0 || index >= device.Inputs.Count)
            return 0.0;
        return device.Inputs[index].State ? 1.0 : 0.0;
    }

    private static double GetCanInputState(PdmDevice device, int index)
    {
        if (index < 0 || index >= device.CanInputs.Count)
            return 0.0;
        return device.CanInputs[index].Output ? 1.0 : 0.0;
    }

    private static double GetCanInputValue(PdmDevice device, int index)
    {
        if (index < 0 || index >= device.CanInputs.Count)
            return 0.0;
        return device.CanInputs[index].Value;
    }

    private static double GetVirtualInputState(PdmDevice device, int index)
    {
        if (index < 0 || index >= device.VirtualInputs.Count)
            return 0.0;
        return device.VirtualInputs[index].Value ? 1.0 : 0.0;
    }

    private static double GetCounterValue(PdmDevice device, int index)
    {
        if (index < 0 || index >= device.Counters.Count)
            return 0.0;
        return device.Counters[index].Value;
    }

    private static double GetConditionState(PdmDevice device, int index)
    {
        if (index < 0 || index >= device.Conditions.Count)
            return 0.0;
        return device.Conditions[index].Value;
    }

    private static double GetFlasherState(PdmDevice device, int index)
    {
        if (index < 0 || index >= device.Flashers.Count)
            return 0.0;
        return device.Flashers[index].Value ? 1.0 : 0.0;
    }

    private static double GetWiperState(PdmDevice device, int index)
    {
        // Wiper has two states: Slow and Fast
        // Index 0 = Slow, Index 1 = Fast
        return index switch
        {
            0 => device.Wipers.SlowState ? 1.0 : 0.0,
            1 => device.Wipers.FastState ? 1.0 : 0.0,
            _ => 0.0
        };
    }
}
