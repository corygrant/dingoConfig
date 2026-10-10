using domain.Common;
using domain.Devices.Functions;
using domain.Enums;
using domain.Interfaces;

namespace domain.Devices.VehicleFunctions;

/// <summary>
/// Adds, rebuilds and removes vehicle functions. A function lives on one PDM, its
/// home, and may also drive outputs of the other PDMs in the project.
/// </summary>
public static class VehicleFunctionService
{
    /// <summary>Everything that can be added, in the order the UI lists it.</summary>
    public static IReadOnlyList<Func<VehicleFunction>> Catalog { get; } =
    [
        () => new Headlights(),
        () => new Taillights(),
        () => new TurnSignals(),
        () => new InteriorLight(),
        () => new Horn(),
        () => new Wipers(),
        () => new BlowerFan(),
        () => new Ecu(),
        () => new FuelPump(),
        () => new CoolantFan()
    ];

    /// <summary>The Ignition block signal an empty slot falls back to, 0 without an ignition.</summary>
    public static int DefaultVar(FwDevice device, IgnitionDefault source)
    {
        if (!device.Def.HasIgnition || !device.Ignition.Enabled)
            return 0;

        int Var(string name) => device.VarMap
            .FirstOrDefault(v => v.Owner == device.Ignition && v.PropertyName == name)?.VariableIndex ?? 0;

        return source switch
        {
            IgnitionDefault.Ignition => Var("Ignition"),
            IgnitionDefault.Accessory => Var("Accessory"),
            IgnitionDefault.EngineRunning => device.Ignition.EngineRunInput,
            _ => 0
        };
    }

    /// <summary>Rebuilds every function, for example after a project is loaded.</summary>
    public static void RebuildAll(FwDevice device)
    {
        foreach (var fn in device.VehicleFunctions.Where(f => !f.ReadsSignals).ToList())
            Build(device, fn);
        RebuildReaders(device, null);
    }

    /// <summary>Every function in the project with the PDM it lives on.</summary>
    public static IEnumerable<(FwDevice Home, VehicleFunction Fn)> AllFunctions(FwDevice device) =>
        device.Project.SelectMany(d => d.VehicleFunctions.Select(fn => (d, fn))).ToList();

    /// <summary>The PDM a function lives on.</summary>
    public static FwDevice? HomeOf(FwDevice device, VehicleFunction fn) =>
        device.Project.FirstOrDefault(d => d.VehicleFunctions.Contains(fn));

    /// <summary>The PDM with this base ID in the project, null when it is not there.</summary>
    public static FwDevice? DeviceAt(FwDevice device, int baseId) =>
        device.Project.FirstOrDefault(d => d.BaseId == baseId);

    /// <summary>The outputs of a slot on one PDM: the home's own, or those of a remote part.</summary>
    public static List<int> OutputsOn(FwDevice home, VehicleFunction fn, FwDevice target, string slot) =>
        target == home ? fn.OutputsIn(slot) : Part(fn, target).OutputsIn(slot);

    /// <summary>The PDMs a function drives outputs on, its home first.</summary>
    public static IEnumerable<FwDevice> TargetsOf(FwDevice home, VehicleFunction fn) =>
        new[] { home }.Concat(fn.Remote.Where(p => !p.Value.Empty)
                                       .Select(p => DeviceAt(home, p.Key))
                                       .OfType<FwDevice>()
                                       .Where(d => d != home));

    private static RemotePart Part(VehicleFunction fn, FwDevice target)
    {
        if (!fn.Remote.TryGetValue(target.BaseId, out var part))
            fn.Remote[target.BaseId] = part = new RemotePart();
        return part;
    }

    /// <summary>
    /// Lets each PDM see the others in the project and builds the functions again,
    /// now that the PDMs they drive outputs on are known.
    /// </summary>
    public static void LinkProject(IReadOnlyList<FwDevice> devices, Func<IEnumerable<FwDevice>> project)
    {
        foreach (var device in devices)
            device.Peers = project;
        foreach (var device in devices)
            RebuildAll(device);
    }

    /// <summary>Keeps the functions pointing at a PDM whose base ID changed.</summary>
    public static void BaseIdChanged(FwDevice device, int oldBaseId)
    {
        if (oldBaseId == device.BaseId)
            return;
        foreach (var (_, fn) in AllFunctions(device))
            if (fn.Remote.Remove(oldBaseId, out var part))
                fn.Remote[device.BaseId] = part;
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
        var builder = new FunctionBuilder(device, device, fn, fn.Outputs, fn.Blocks);
        if (fn.Remote.Values.Any(p => !p.Empty))
            builder.ClaimToggles();
        fn.Build(builder);
        builder.Finish();
        fn.Signals = builder.Shared;

        // The same recipe again on every other PDM it drives outputs on. One that is
        // not in the project right now keeps what it was given last time.
        foreach (var (baseId, part) in fn.Remote.ToList())
        {
            var target = DeviceAt(device, baseId);
            if (target == null || target == device)
                continue;

            if (part.Empty)
            {
                foreach (var (key, number) in part.Blocks)
                    ReleaseBlock(target, key, number);
                fn.Remote.Remove(baseId);
                continue;
            }

            var remote = new FunctionBuilder(device, target, fn, part.Outputs, part.Blocks);
            fn.Build(remote);
            remote.Finish();
        }
    }

    private static void RebuildReaders(FwDevice device, VehicleFunction? changed)
    {
        foreach (var (home, reader) in AllFunctions(device).Where(f => f.Fn.ReadsSignals && f.Fn != changed))
            Build(home, reader);
    }

    public static void Remove(FwDevice device, VehicleFunction fn)
    {
        foreach (var (key, number) in fn.Blocks)
            ReleaseBlock(device, key, number);
        fn.Blocks.Clear();

        foreach (var slot in fn.OutputSlots)
            foreach (var number in fn.OutputsIn(slot.Key))
                ReleaseOutput(device, number, slot);

        foreach (var (baseId, part) in fn.Remote)
        {
            if (DeviceAt(device, baseId) is not { } target || target == device)
                continue;
            foreach (var (key, number) in part.Blocks)
                ReleaseBlock(target, key, number);
            foreach (var slot in fn.OutputSlots)
                foreach (var number in part.OutputsIn(slot.Key))
                    ReleaseOutput(target, number, slot);
        }
        fn.Remote.Clear();

        device.VehicleFunctions.Remove(fn);
        RebuildReaders(device, null);
    }

    /// <summary>
    /// Brings a loaded project in step with the current recipes: everything is
    /// rebuilt so signals between functions are known again.
    /// </summary>
    public static void AfterLoad(FwDevice device) => RebuildAll(device);

    public static void AddOutput(FwDevice device, VehicleFunction fn, string slotKey, int outputNumber) =>
        AddOutput(device, fn, slotKey, device, outputNumber);

    /// <summary>Adds an output of the home PDM or of another PDM in the project to a slot.</summary>
    public static void AddOutput(FwDevice home, VehicleFunction fn, string slotKey, FwDevice target, int outputNumber)
    {
        var slot = fn.OutputSlots.First(s => s.Key == slotKey);
        var list = OutputsOn(home, fn, target, slotKey);
        if (list.Contains(outputNumber))
            return;
        list.Add(outputNumber);

        var output = target.Outputs[outputNumber - 1];
        slot.Defaults.ApplyTo(output);
        if (output.Name == DefaultOutputName(outputNumber))
            output.Name = list.Count > 1 ? $"{slot.OutputName} {list.Count}" : slot.OutputName;

        Rebuild(home, fn);
    }

    public static void RemoveOutput(FwDevice device, VehicleFunction fn, string slotKey, int outputNumber) =>
        RemoveOutput(device, fn, slotKey, device, outputNumber);

    public static void RemoveOutput(FwDevice home, VehicleFunction fn, string slotKey, FwDevice target, int outputNumber)
    {
        var slot = fn.OutputSlots.First(s => s.Key == slotKey);
        if (!OutputsOn(home, fn, target, slotKey).Remove(outputNumber))
            return;

        ReleaseOutput(target, outputNumber, slot);
        Rebuild(home, fn);
    }

    /// <summary>
    /// Points an input at a bit learned from the bus. A CAN input already reading
    /// exactly that bit is reused, otherwise a free one is set up for it.
    /// Returns null when every CAN input is taken.
    /// </summary>
    public static CanInput? UseLearnedInput(FwDevice device, VehicleFunction fn, string slotKey,
                                            CanBitCandidate bit, string name)
    {
        var operand = bit.ActiveHigh ? 1.0 : 0.0;
        var input = device.CanInputs.FirstOrDefault(c => ReadsBit(c, bit))
                    ?? device.CanInputs.FirstOrDefault(c => !c.Enabled && device.UsesOf(c).Count == 0);
        if (input == null)
            return null;

        if (!input.Enabled)
        {
            input.Enabled = true;
            input.Name = name;
            input.Id = bit.Id;
            input.StartBit = bit.Bit;
            input.BitLength = 1;
            input.Factor = 1.0;
            input.Offset = 0.0;
            input.ByteOrder = ByteOrder.LittleEndian;
            input.Signed = false;
            input.Operator = Operator.Equal;
            input.Operand = operand;
            input.Mode = InputMode.Momentary;

            // A message sent all the time can time out to "off" if its sender dies.
            // One sent only on change must not, or a held switch would drop out.
            input.TimeoutEnabled = bit.PeriodMs > 0 && bit.PeriodMs <= 1000;
            input.Timeout = Math.Clamp(bit.PeriodMs * 5, 500, 5000);
        }

        fn.Input(slotKey).Var = device.VarMap
            .First(v => v.Owner == input && v.PropertyName == "State").VariableIndex;
        Rebuild(device, fn);
        return input;
    }

    /// <summary>
    /// An enabled CAN input that is on exactly when the bit reads its active value,
    /// however it is written (= 1, > 0, != 0, AND 1 ...).
    /// </summary>
    private static bool ReadsBit(CanInput c, CanBitCandidate bit)
    {
        if (!c.Enabled || c.Id != bit.Id || c.StartBit != bit.Bit || c.BitLength != 1 ||
            c.ByteOrder != ByteOrder.LittleEndian || c.Mode != InputMode.Momentary)
            return false;

        bool On(int raw) => Compare(c.Operator, raw * c.Factor + c.Offset, c.Operand);
        return On(bit.ActiveHigh ? 1 : 0) && !On(bit.ActiveHigh ? 0 : 1);
    }

    // As the firmware's CAN input compares
    private static bool Compare(Operator op, double value, double operand) => op switch
    {
        Operator.Equal => value == operand,
        Operator.NotEqual => value != operand,
        Operator.GreaterThan => value > operand,
        Operator.LessThan => value < operand,
        Operator.GreaterThanOrEqual => value >= operand,
        Operator.LessThanOrEqual => value <= operand,
        Operator.BitwiseAnd => ((uint)value & (uint)operand) > 0,
        Operator.BitwiseNand => ((uint)value & (uint)operand) == 0,
        _ => false
    };

    /// <summary>Outputs nothing else drives: not the ignition, not a function, not a paired follower.</summary>
    public static IEnumerable<Output> FreeOutputs(FwDevice device) =>
        device.Outputs.Where(o => o.PrimaryOutput < 0 && device.ManagedBy(o) == null);

    /// <summary>
    /// The function that built a block of this PDM or drives one of its outputs,
    /// whichever PDM the function lives on.
    /// </summary>
    public static VehicleFunction? OwnerOf(FwDevice device, IDeviceFunction block)
    {
        foreach (var (home, fn) in AllFunctions(device))
        {
            Dictionary<string, List<int>> outputs;
            Dictionary<string, int> blocks;
            if (home == device)
                (outputs, blocks) = (fn.Outputs, fn.Blocks);
            else if (fn.Remote.TryGetValue(device.BaseId, out var part))
                (outputs, blocks) = (part.Outputs, part.Blocks);
            else
                continue;

            if (block is Output output)
            {
                if (outputs.Values.Any(list => list.Contains(output.Number)))
                    return fn;
                continue;
            }

            var prefix = PrefixOf(block);
            if (prefix != null && blocks.Any(kv => kv.Value == block.Number && kv.Key.StartsWith(prefix)))
                return fn;
        }
        return null;
    }

    /// <summary>The slot an output of this PDM is in, for the function that drives it.</summary>
    public static OutputSlot? SlotOn(FwDevice device, VehicleFunction fn, int outputNumber)
    {
        var outputs = HomeOf(device, fn) == device ? fn.Outputs
            : fn.Remote.TryGetValue(device.BaseId, out var part) ? part.Outputs : null;
        return outputs == null ? null
            : fn.OutputSlots.FirstOrDefault(s => outputs.TryGetValue(s.Key, out var list) && list.Contains(outputNumber));
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
        else if (key.StartsWith("cond:"))
        {
            var condition = device.Conditions.FirstOrDefault(c => c.Number == number);
            if (condition == null) return;
            condition.Enabled = false;
            condition.Input = 0;
            condition.Operator = Operator.Equal;
            condition.Arg = 0;
            condition.Name = $"condition{number}";
        }
        else if (key.StartsWith("wiper:"))
        {
            var wiper = device.Wipers;
            wiper.Enabled = false;
            wiper.SlowInput = wiper.FastInput = wiper.InterInput = wiper.OnInput = 0;
            wiper.SpeedInput = wiper.ParkInput = wiper.SwipeInput = wiper.WashInput = 0;
            wiper.Name = "wiper";
        }
        else if (key.StartsWith("can:"))
        {
            var input = device.CanInputs.FirstOrDefault(c => c.Number == number);
            if (input == null) return;
            input.Enabled = false;
            input.TimeoutEnabled = false;
            input.Timeout = 1000;
            input.Id = 0;
            input.StartBit = 0;
            input.BitLength = 8;
            input.Factor = 1.0;
            input.Offset = 0;
            input.ByteOrder = ByteOrder.LittleEndian;
            input.Signed = false;
            input.Operator = Operator.Equal;
            input.Operand = 0;
            input.Mode = InputMode.Momentary;
            input.Name = $"canInput{number}";
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
        output.VariableDutyCycle = false;
        output.DutyCycleInput = 0;
        if (output.Name.StartsWith(slot.OutputName))
            output.Name = DefaultOutputName(number);
    }

    private static string DefaultOutputName(int number) => $"output{number}";

    private static string? PrefixOf(IDeviceFunction block) => block switch
    {
        VirtualInput => "vi:",
        Flasher => "flasher:",
        DeviceTimer => "timer:",
        Condition => "cond:",
        Wiper => "wiper:",
        CanInput => "can:",
        _ => null
    };
}
