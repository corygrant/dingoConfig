using domain.Devices.Functions;
using domain.Enums;
using domain.Interfaces;

namespace domain.Devices.VehicleFunctions;

/// <summary>Adds, rebuilds and removes vehicle functions on a device.</summary>
public static class VehicleFunctionService
{
    /// <summary>Everything that can be added, in the order the UI lists it.</summary>
    public static IReadOnlyList<Func<VehicleFunction>> Catalog { get; } =
    [
        () => new Headlights(),
        () => new Taillights(),
        () => new TurnSignals(),
        () => new InteriorLight(),
        () => new Horn()
    ];

    /// <summary>Rebuilds every function, for example after a project is loaded.</summary>
    public static void RebuildAll(FwDevice device)
    {
        foreach (var fn in device.VehicleFunctions.Where(f => !f.ReadsSignals).ToList())
            Build(device, fn);
        RebuildReaders(device, null);
    }

    public static VehicleFunction Add(FwDevice device, VehicleFunction fn)
    {
        device.VehicleFunctions.Add(fn);
        fn.OnAdded(device);
        Rebuild(device, fn);
        return fn;
    }

    /// <summary>
    /// Rewrites the function's blocks after any change to it, and those of the
    /// functions built on its signals.
    /// </summary>
    public static void Rebuild(FwDevice device, VehicleFunction fn)
    {
        Build(device, fn);
        RebuildReaders(device, fn);
    }

    private static void Build(FwDevice device, VehicleFunction fn)
    {
        fn.Problems.Clear();
        var builder = new FunctionBuilder(device, fn);
        fn.Build(builder);
        builder.Finish();
        fn.Signals = builder.Shared;
    }

    private static void RebuildReaders(FwDevice device, VehicleFunction? changed)
    {
        foreach (var reader in device.VehicleFunctions.Where(f => f.ReadsSignals && f != changed).ToList())
            Build(device, reader);
    }

    public static void Remove(FwDevice device, VehicleFunction fn)
    {
        foreach (var (key, number) in fn.Blocks)
            ReleaseBlock(device, key, number);
        fn.Blocks.Clear();

        foreach (var slot in fn.OutputSlots)
            foreach (var number in fn.OutputsIn(slot.Key))
                ReleaseOutput(device, number, slot);

        device.VehicleFunctions.Remove(fn);
        RebuildReaders(device, null);
    }

    /// <summary>
    /// Brings a loaded project in step with the current recipes: everything is
    /// rebuilt so signals between functions are known again.
    /// </summary>
    public static void AfterLoad(FwDevice device) => RebuildAll(device);

    public static void AddOutput(FwDevice device, VehicleFunction fn, string slotKey, int outputNumber)
    {
        var slot = fn.OutputSlots.First(s => s.Key == slotKey);
        var list = fn.OutputsIn(slotKey);
        if (list.Contains(outputNumber))
            return;
        list.Add(outputNumber);

        var output = device.Outputs[outputNumber - 1];
        slot.Defaults.ApplyTo(output);
        if (output.Name == DefaultOutputName(outputNumber))
            output.Name = list.Count > 1 ? $"{slot.OutputName} {list.Count}" : slot.OutputName;

        Rebuild(device, fn);
    }

    public static void RemoveOutput(FwDevice device, VehicleFunction fn, string slotKey, int outputNumber)
    {
        var slot = fn.OutputSlots.First(s => s.Key == slotKey);
        if (!fn.OutputsIn(slotKey).Remove(outputNumber))
            return;

        ReleaseOutput(device, outputNumber, slot);
        Rebuild(device, fn);
    }

    /// <summary>Outputs nothing else drives: not the ignition, not a function, not a paired follower.</summary>
    public static IEnumerable<Output> FreeOutputs(FwDevice device) =>
        device.Outputs.Where(o => o.PrimaryOutput < 0 && device.ManagedBy(o) == null);

    /// <summary>The function that built a virtual input, flasher or timer, or drives an output.</summary>
    public static VehicleFunction? OwnerOf(FwDevice device, IDeviceFunction block)
    {
        foreach (var fn in device.VehicleFunctions)
        {
            if (block is Output output)
            {
                if (fn.SlotOf(output.Number) != null)
                    return fn;
                continue;
            }

            var prefix = PrefixOf(block);
            if (prefix != null && fn.Blocks.Any(kv => kv.Value == block.Number && kv.Key.StartsWith(prefix)))
                return fn;
        }
        return null;
    }

    internal static void ReleaseBlock(FwDevice device, string key, int number)
    {
        if (key.StartsWith("vi:"))
        {
            var vi = device.VirtualInputs.FirstOrDefault(v => v.Number == number);
            if (vi == null) return;
            vi.Enabled = false;
            vi.Var0 = vi.Var1 = vi.Var2 = 0;
            vi.Not0 = vi.Not1 = vi.Not2 = false;
            vi.Cond0 = vi.Cond1 = Conditional.And;
            vi.Mode = InputMode.Momentary;
            vi.Name = $"virtualInput{number}";
        }
        else if (key.StartsWith("flasher:"))
        {
            var flasher = device.Flashers.FirstOrDefault(f => f.Number == number);
            if (flasher == null) return;
            flasher.Enabled = false;
            flasher.Input = 0;
            flasher.Single = false;
            flasher.OnTime = 500;
            flasher.OffTime = 500;
            flasher.Name = $"flasher{number}";
        }
        else if (key.StartsWith("timer:"))
        {
            var timer = device.Timers.FirstOrDefault(t => t.Number == number);
            if (timer == null) return;
            timer.Enabled = false;
            timer.Input = 0;
            timer.ResetInput = 0;
            timer.Mode = TimerMode.OnDelay;
            timer.Time = 1000;
            timer.Name = $"timer{number}";
        }
    }

    private static void ReleaseOutput(FwDevice device, int number, OutputSlot slot)
    {
        var output = device.Outputs[number - 1];
        output.Enabled = false;
        output.Input = 0;
        if (output.Name.StartsWith(slot.OutputName))
            output.Name = DefaultOutputName(number);
    }

    private static string DefaultOutputName(int number) => $"output{number}";

    private static string? PrefixOf(IDeviceFunction block) => block switch
    {
        VirtualInput => "vi:",
        Flasher => "flasher:",
        DeviceTimer => "timer:",
        _ => null
    };
}
