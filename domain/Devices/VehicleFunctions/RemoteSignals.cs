using domain.Devices.Functions;
using domain.Enums;
using domain.Models;

namespace domain.Devices.VehicleFunctions;

/// <summary>
/// How one PDM reads another PDM's variable, using only what is already on the
/// bus. A CAN input is copied, since every PDM hears the same frame at the same
/// moment. Anything else is read from the other PDM's own status messages, which
/// it sends every 100 ms (digital inputs, virtual inputs, flashers, outputs,
/// conditions, counters, the wiper and the ignition outputs).
/// </summary>
public static class RemoteSignals
{
    // Status messages come every 100 ms; five missed and the input drops out
    private const int StatusTimeout = 500;

    /// <summary>A CAN input setting that reads the variable, and which of its variables to use.</summary>
    public sealed record Frame(int Id, int StartBit, int BitLength, double Factor, double Offset, ByteOrder Order,
                               bool Signed, Operator Op, double Operand, InputMode Mode, bool TimeoutEnabled,
                               int Timeout, string Property)
    {
        // Same frame, same bits, same test: one CAN input serves everyone who reads this
        public string Key => $"{Id:X3}:{StartBit}:{BitLength}:{Op}:{Operand}:{Mode}";

        public bool Matches(CanInput c) =>
            c.Enabled && c.Id == Id && c.StartBit == StartBit && c.BitLength == BitLength && c.Factor == Factor &&
            c.Offset == Offset && c.ByteOrder == Order && c.Signed == Signed && c.Operator == Op &&
            c.Operand == Operand && c.Mode == Mode;

        public void ApplyTo(CanInput c)
        {
            c.Id = Id;
            c.StartBit = StartBit;
            c.BitLength = BitLength;
            c.Factor = Factor;
            c.Offset = Offset;
            c.ByteOrder = Order;
            c.Signed = Signed;
            c.Operator = Op;
            c.Operand = Operand;
            c.Mode = Mode;
            c.TimeoutEnabled = TimeoutEnabled;
            c.Timeout = Timeout;
        }
    }

    /// <summary>Where on the bus the source PDM's variable can be read, null when it cannot.</summary>
    public static Frame? Describe(FwDevice source, DeviceVariable variable) => variable.Owner switch
    {
        CanInput c => new Frame(c.Id, c.StartBit, c.BitLength, c.Factor, c.Offset, c.ByteOrder, c.Signed, c.Operator,
                                c.Operand, c.Mode, c.TimeoutEnabled, c.Timeout, variable.PropertyName),
        DigitalInput d => Status(source, "DigitalInput.State", d.Number - 1),
        VirtualInput v => Status(source, "VirtualInput.Value", v.Number - 1),
        Flasher f => Status(source, "Flasher.Value", f.Number - 1),
        Condition c => Status(source, "Condition.Value", c.Number - 1),
        Counter c => Status(source, "Counter.Value", c.Number - 1, number: true),
        Output o when variable.PropertyName == "On" => Status(source, "Output.State", o.Number - 1, onValue: (int)OutState.On),
        Wiper when variable.PropertyName == "Slow Output" => Status(source, "Wiper.SlowState", 0),
        Wiper when variable.PropertyName == "Fast Output" => Status(source, "Wiper.FastState", 0),
        Ignition => IgnitionFlag(source, variable.PropertyName),
        _ => null
    };

    private static Frame? Status(FwDevice source, string target, int index, bool number = false, int onValue = 1)
    {
        if (source.StatusSignal(target, index) is not { } found)
            return null;
        var (id, signal) = found;

        return new Frame(id, signal.StartBit, signal.Length, number ? signal.Factor : 1.0, 0, signal.ByteOrder,
                         signal.IsSigned, Operator.Equal, onValue, InputMode.Momentary, true, StatusTimeout,
                         number ? "Value" : "State");
    }

    // The ignition status message packs its outputs as flags: ignition, accessory, dash, starter
    private static Frame? IgnitionFlag(FwDevice source, string property)
    {
        var bit = property switch { "Ignition" => 0, "Accessory" => 1, "Dash" => 2, "Starter" => 3, _ => -1 };
        if (bit < 0 || source.StatusSignal("Ignition.OutputFlags", 0) is not { } found)
            return null;
        var (id, signal) = found;

        return new Frame(id, signal.StartBit + bit, 1, 1.0, 0, signal.ByteOrder, false, Operator.Equal, 1,
                         InputMode.Momentary, true, StatusTimeout, "State");
    }
}
