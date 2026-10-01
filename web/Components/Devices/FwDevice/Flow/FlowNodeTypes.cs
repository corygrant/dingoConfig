using domain.Devices.Functions;
using domain.Devices.Functions.Keypad;
using domain.Enums;
using domain.Interfaces;
using FwDeviceModel = domain.Devices.FwDevice;

namespace web.Components.Devices.FwDevice.Flow;

public enum FlowCategory { Device, Source, Logic, Sink }

/// <summary>
/// A connectable input on a function: a var map index property (e.g. Output.Input).
/// DataTypes mirror what the var selector dialog accepts for that property.
/// </summary>
public sealed record FlowInputDef(
    string Key,
    string Label,
    string[] DataTypes,
    Func<int> Get,
    Action<int> Set);

/// <summary>
/// Describes how one kind of device function appears in the flow editor.
/// Functions are fixed slots on the device; a slot appears as a node when enabled.
/// </summary>
public sealed class FlowNodeType
{
    public required string Kind { get; init; }
    public required string Label { get; init; }
    public required FlowCategory Category { get; init; }
    public required Func<FwDeviceModel, IReadOnlyList<IDeviceFunction>> Slots { get; init; }
    public required Func<IDeviceFunction, bool> GetEnabled { get; init; }
    public required Action<IDeviceFunction, bool> SetEnabled { get; init; }
    public required Func<IDeviceFunction, IReadOnlyList<FlowInputDef>> Inputs { get; init; }

    /// <summary>Live values, in the same order as the function's GetVarMap() entries. Null = no live value.</summary>
    public required Func<IDeviceFunction, object?[]> Values { get; init; }

    public bool IsSingleton { get; init; }

    public string NodeId(IDeviceFunction function) => $"{Kind}-{function.Number}";

    public string SlotLabel(IDeviceFunction function) => IsSingleton ? Label : $"{Label} {function.Number}";
}

public static class FlowNodeTypes
{
    private static readonly string[] Bool = ["bool"];
    private static readonly string[] Numeric = ["int", "float"];
    private static readonly string[] Any = ["bool", "int", "float"];

    public static IReadOnlyList<FlowNodeType> All { get; } =
    [
        Define<DigitalInput>("digitalInput", "Digital Input", FlowCategory.Source, d => d.DigitalInputs,
            x => x.Enabled, (x, v) => x.Enabled = v,
            _ => [],
            x => [x.State]),

        Define<AnalogInput>("analogInput", "Analog Input", FlowCategory.Source, d => d.AnalogInputs,
            x => x.Enabled, (x, v) => x.Enabled = v,
            _ => [],
            x => [null, x.Millivolts, x.Rotary.Pos, x.Switch.State]),

        Define<CanInput>("canInput", "CAN Input", FlowCategory.Source, d => d.CanInputs,
            x => x.Enabled, (x, v) => x.Enabled = v,
            _ => [],
            x => [x.Output, x.Value]),

        Define<KeypadMaster>("keypad", "Keypad", FlowCategory.Source, d => d.Keypads,
            x => x.Enabled, (x, v) => x.Enabled = v,
            KeypadInputs,
            _ => []),

        Define<VirtualInput>("virtualInput", "Virtual Input", FlowCategory.Logic, d => d.VirtualInputs,
            x => x.Enabled, (x, v) => x.Enabled = v,
            x =>
            [
                new("var0", "Var 0", Bool, () => x.Var0, v => x.Var0 = v),
                new("var1", "Var 1", Bool, () => x.Var1, v => x.Var1 = v),
                new("var2", "Var 2", Bool, () => x.Var2, v => x.Var2 = v)
            ],
            x => [x.Value]),

        Define<Condition>("condition", "Condition", FlowCategory.Logic, d => d.Conditions,
            x => x.Enabled, (x, v) => x.Enabled = v,
            x => [new("input", "Input", Any, () => x.Input, v => x.Input = v)],
            x => [x.Value != 0]),

        Define<Counter>("counter", "Counter", FlowCategory.Logic, d => d.Counters,
            x => x.Enabled, (x, v) => x.Enabled = v,
            x =>
            [
                new("incInput", "Increment", Bool, () => x.IncInput, v => x.IncInput = v),
                new("decInput", "Decrement", Bool, () => x.DecInput, v => x.DecInput = v),
                new("resetInput", "Reset", Bool, () => x.ResetInput, v => x.ResetInput = v)
            ],
            x => [x.Value]),

        Define<Flasher>("flasher", "Flasher", FlowCategory.Logic, d => d.Flashers,
            x => x.Enabled, (x, v) => x.Enabled = v,
            x => [new("input", "Input", Bool, () => x.Input, v => x.Input = v)],
            x => [x.Value]),

        Define<Output>("output", "Output", FlowCategory.Sink, d => d.Outputs,
            x => x.Enabled, (x, v) => x.Enabled = v,
            x =>
            [
                new("input", "Input", Bool, () => x.Input, v => x.Input = v),
                new("dutyCycleInput", "Duty Cycle", Numeric, () => x.DutyCycleInput, v => x.DutyCycleInput = v)
            ],
            x => [x.State == OutState.On, x.Current, x.State == OutState.Overcurrent, x.State == OutState.Fault]),

        Define<DigitalOutput>("digitalOutput", "Digital Output", FlowCategory.Sink, d => d.DigitalOutputs,
            x => x.Enabled, (x, v) => x.Enabled = v,
            x => [new("input", "Input", Bool, () => x.Input, v => x.Input = v)],
            x => [x.State]),

        Define<CanOutput>("canOutput", "CAN Output", FlowCategory.Sink, d => d.CanOutputs,
            x => x.Enabled, (x, v) => x.Enabled = v,
            x => [new("input", "Input", Any, () => x.Input, v => x.Input = v)],
            _ => []),

        Define<Wiper>("wiper", "Wiper", FlowCategory.Sink, d => d.Def.HasWipers ? [d.Wipers] : [],
            x => x.Enabled, (x, v) => x.Enabled = v,
            x =>
            [
                new("onInput", "On", Bool, () => x.OnInput, v => x.OnInput = v),
                new("slowInput", "Slow", Bool, () => x.SlowInput, v => x.SlowInput = v),
                new("fastInput", "Fast", Bool, () => x.FastInput, v => x.FastInput = v),
                new("interInput", "Intermittent", Bool, () => x.InterInput, v => x.InterInput = v),
                new("speedInput", "Speed", ["int"], () => x.SpeedInput, v => x.SpeedInput = v),
                new("parkInput", "Park", Bool, () => x.ParkInput, v => x.ParkInput = v),
                new("swipeInput", "Swipe", Bool, () => x.SwipeInput, v => x.SwipeInput = v),
                new("washInput", "Wash", Bool, () => x.WashInput, v => x.WashInput = v)
            ],
            x => [x.SlowState, x.FastState, null, null, null, null],
            singleton: true),

        Define<StarterDisable>("starterDisable", "Starter Disable", FlowCategory.Sink,
            d => d.Def.HasStarterDisable ? [d.StarterDisable] : [],
            x => x.Enabled, (x, v) => x.Enabled = v,
            x => [new("input", "Input", Bool, () => x.Input, v => x.Input = v)],
            _ => [],
            singleton: true)
    ];

    private static IReadOnlyList<FlowInputDef> KeypadInputs(KeypadMaster keypad)
    {
        var inputs = new List<FlowInputDef>
        {
            new("dimmingVar", "Dimming", Bool, () => keypad.DimmingVar, v => keypad.DimmingVar = v)
        };

        foreach (var button in keypad.Buttons)
        {
            for (var i = 0; i < button.Vars.Length; i++)
            {
                var index = i;
                inputs.Add(new FlowInputDef($"button{button.Number}.var{i}", $"{button.Name} val {i}", Bool,
                    () => button.Vars[index], v => button.Vars[index] = v));
            }

            inputs.Add(new FlowInputDef($"button{button.Number}.fault", $"{button.Name} fault", Bool,
                () => button.FaultVar, v => button.FaultVar = v));
        }

        return inputs;
    }

    private static FlowNodeType Define<T>(
        string kind,
        string label,
        FlowCategory category,
        Func<FwDeviceModel, IEnumerable<T>> slots,
        Func<T, bool> getEnabled,
        Action<T, bool> setEnabled,
        Func<T, IReadOnlyList<FlowInputDef>> inputs,
        Func<T, object?[]> values,
        bool singleton = false) where T : IDeviceFunction
    {
        return new FlowNodeType
        {
            Kind = kind,
            Label = label,
            Category = category,
            IsSingleton = singleton,
            Slots = d => slots(d).Cast<IDeviceFunction>().ToList(),
            GetEnabled = f => getEnabled((T)f),
            SetEnabled = (f, v) => setEnabled((T)f, v),
            Inputs = f => inputs((T)f),
            Values = f => values((T)f)
        };
    }
}
