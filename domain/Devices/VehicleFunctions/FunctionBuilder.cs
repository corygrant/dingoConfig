using domain.Devices.Functions;
using domain.Enums;
using domain.Interfaces;

namespace domain.Devices.VehicleFunctions;

/// <summary>
/// Hands a function the device blocks it asks for while it builds, reusing the
/// ones it already owns, and releases whatever it no longer used afterwards.
///
/// A function lives on one PDM, its home, but may drive outputs on others on the
/// same bus. It is then built once per PDM: each one gets its own blocks and its
/// own copy of the logic, and reads the switches off the bus (see
/// <see cref="RemoteSignals"/>). So each PDM still runs its lamps by itself.
/// </summary>
public sealed class FunctionBuilder
{
    private readonly FwDevice _home;
    private readonly FwDevice _target;
    private readonly VehicleFunction _fn;
    private readonly Dictionary<string, List<int>> _outputs;
    private readonly Dictionary<string, int> _blocks;
    private readonly HashSet<string> _used = [];

    // Signals the build hands out to other functions
    internal Dictionary<string, int> Shared { get; } = new();

    internal FunctionBuilder(FwDevice home, FwDevice target, VehicleFunction fn,
                             Dictionary<string, List<int>> outputs, Dictionary<string, int> blocks)
    {
        _home = home;
        _target = target;
        _fn = fn;
        _outputs = outputs;
        _blocks = blocks;
    }

    private bool IsHome => _target == _home;

    public bool HasOutputs(string slot) => _outputs.TryGetValue(slot, out var list) && list.Count > 0;

    /// <summary>The Ignition block of the PDM being built, null on a device without one.</summary>
    public Ignition? Ignition => _target.Def.HasIgnition ? _target.Ignition : null;

    /// <summary>
    /// Hands out a signal another function builds on, such as the low beam for the
    /// rear fog lamp. Only the function's own PDM hands them out; the others read
    /// them from there.
    /// </summary>
    public void Share(string key, int var)
    {
        if (IsHome)
            Shared[key] = var;
    }

    /// <summary>A signal another function handed out, as this PDM reads it; 0 when none did.</summary>
    public int SignalOf(string key)
    {
        foreach (var (home, fn) in VehicleFunctionService.AllFunctions(_home))
        {
            var signal = fn == _fn ? 0 : fn.Signals.GetValueOrDefault(key);
            if (signal != 0)
                return Read(home, signal, key);
        }
        return 0;
    }

    public void Problem(string text) => _fn.Problems.Add(IsHome ? text : $"{_target.Name}: {text}");

    /// <summary>
    /// The variable an input slot reads. A push button goes through a latching
    /// virtual input, so each press toggles it. An empty slot with a default reads
    /// the Ignition block of the PDM being built.
    /// </summary>
    public int In(string key)
    {
        var input = _fn.Input(key);
        var slot = _fn.InputSlots.FirstOrDefault(s => s.Key == key);
        if (input.Var == 0)
            return slot is { Default: not IgnitionDefault.None }
                ? VehicleFunctionService.DefaultVar(_target, slot.Default)
                : 0;

        var label = slot?.Label ?? key;
        if (!IsHome)
        {
            // The switch is read off the bus. A push button is toggled on the home
            // PDM only and followed from there, so every PDM agrees on its state.
            var source = input.Toggle ? HomeToggle(key, label) : input.Var;
            return source == 0 ? 0 : Read(_home, source, label);
        }

        if (!input.Toggle)
            return input.Var;

        var vi = ClaimVirtualInput($"vi:toggle:{key}", $"{label} toggle");
        if (vi == null)
            return 0;

        SetLogic(vi, input.Var, Conditional.Or, 0, false, Conditional.And, 0, false);
        vi.Mode = InputMode.Latched;
        return VarOf(vi);
    }

    // On the home PDM: claims the push button toggles the other PDMs follow
    internal void ClaimToggles()
    {
        foreach (var slot in _fn.InputSlots.Where(s => s.CanToggle))
            if (_fn.Input(slot.Key) is { Var: not 0, Toggle: true })
                In(slot.Key);
    }

    private int HomeToggle(string key, string label)
    {
        if (_fn.Blocks.TryGetValue($"vi:toggle:{key}", out var number) &&
            _home.VirtualInputs.FirstOrDefault(v => v.Number == number) is { } vi)
            return _home.VarMap.FirstOrDefault(v => v.Owner == vi)?.VariableIndex ?? 0;

        Problem($"\"{label}\" is a push button, but {_home.Name} has no virtual input left to toggle it.");
        return 0;
    }

    /// <summary>
    /// A variable of another PDM as this one reads it: its own ignition, or a CAN
    /// input on the same frame. 0 and a problem when it cannot be read over the bus.
    /// </summary>
    private int Read(FwDevice source, int var, string label)
    {
        if (source == _target || var == 0)
            return var;

        var variable = source.VarMap.FirstOrDefault(v => v.VariableIndex == var);
        if (variable == null)
            return 0;

        // Device level, such as Always On: the same on every PDM
        if (variable.Owner == null)
            return _target.VarMap.FirstOrDefault(v => v.Owner == null && v.GetName() == variable.GetName())
                       ?.VariableIndex ?? 0;

        // Every PDM runs its own ignition, kept in step by the ignition sync
        if (variable.Owner is Ignition && _target.Def.HasIgnition && _target.Ignition.Enabled &&
            VarOf(_target.Ignition, variable.PropertyName) is var own and not 0)
            return own;

        if (variable.Owner is CanInput { Enabled: false } off)
        {
            Problem($"\"{label}\" reads {off.Name} on {source.Name}, which is switched off.");
            return 0;
        }

        var frame = RemoteSignals.Describe(source, variable);
        if (frame == null)
        {
            Problem($"\"{label}\" on {source.Name} is not sent on the bus, so {_target.Name} cannot read it.");
            return 0;
        }

        // A CAN input of this PDM's own that already reads it, or one set up for it
        var input = _target.CanInputs.FirstOrDefault(c => frame.Matches(c) && VehicleFunctionService.OwnerOf(_target, c) == null);
        if (input == null)
        {
            input = Claim($"can:{frame.Key}", $"{label} from {source.Name}", _target.CanInputs, "CAN input",
                          c => c.Enabled || _target.UsesOf(c).Count > 0, c => c.Enabled = true,
                          c => c.Name = Name($"{label} from {source.Name}"));
            if (input == null)
                return 0;
            frame.ApplyTo(input);
        }
        return VarOf(input, frame.Property);
    }

    /// <summary>A virtual input owned by the function: ((a op0 b) op1 c). A c of 0 is left out.</summary>
    public int Logic(string key, string label, int a, Conditional op0, int b, bool notB = false,
                     Conditional op1 = Conditional.And, int c = 0, bool notC = false)
    {
        var vi = ClaimVirtualInput(key, label);
        if (vi == null)
            return 0;

        SetLogic(vi, a, op0, b, notB, op1, c, notC);
        vi.Mode = InputMode.Momentary;
        return VarOf(vi);
    }

    /// <summary>signal AND gate, or the signal alone when there is no gate.</summary>
    public int Gate(string key, string label, int signal, int gate) =>
        signal == 0 ? 0 : gate == 0 ? signal : Logic(key, label, signal, Conditional.And, gate);

    /// <summary>Any of up to three signals; one alone needs no block.</summary>
    public int Any(string key, string label, params int[] vars)
    {
        var set = vars.Where(v => v != 0).ToList();
        if (set.Count > 3)
            throw new ArgumentException("A virtual input takes three signals at most");
        return set.Count switch
        {
            0 => 0,
            1 => set[0],
            _ => Logic(key, label, set[0], Conditional.Or, set[1], op1: Conditional.Or, c: set.ElementAtOrDefault(2))
        };
    }

    /// <summary>
    /// On from <paramref name="set"/> until <paramref name="reset"/>, a latch made
    /// of a virtual input that reads its own output: (set OR itself) AND NOT reset.
    /// </summary>
    public int Latch(string key, string label, int set, int reset)
    {
        var vi = ClaimVirtualInput(key, label);
        if (vi == null)
            return 0;

        var self = VarOf(vi);
        SetLogic(vi, set, Conditional.Or, self, false, Conditional.And, reset, true);
        vi.Mode = InputMode.Momentary;
        return self;
    }

    /// <summary>A condition owned by the function: the variable compared with a value.</summary>
    public int Condition(string key, string label, int var, Operator op, double value)
    {
        var condition = Claim(key, label, _target.Conditions, "condition", c => c.Enabled, c => c.Enabled = true,
                              c => c.Name = Name(label));
        if (condition == null)
            return 0;

        condition.Input = var;
        condition.Operator = op;
        condition.Arg = value;
        return VarOf(condition);
    }

    /// <summary>The device's wiper block, when no one else runs it.</summary>
    public Wiper? Wiper(string key, string label)
    {
        if (!_target.Def.HasWipers)
        {
            _used.Add(key);
            Problem("This device has no wiper control.");
            return null;
        }
        return Claim(key, label, [_target.Wipers], "wiper block", w => w.Enabled, w => w.Enabled = true,
                     w => w.Name = Name(label));
    }

    public Flasher? Flasher(string key, string label) =>
        Claim(key, label, _target.Flashers, "flasher", f => f.Enabled, f => f.Enabled = true, f => f.Name = Name(label));

    public DeviceTimer? Timer(string key, string label) =>
        Claim(key, label, _target.Timers, "timer", t => t.Enabled, t => t.Enabled = true, t => t.Name = Name(label));

    /// <summary>Points every output in the slot at a variable.</summary>
    public void Drive(string slot, int var)
    {
        foreach (var output in OutputsIn(slot))
        {
            output.Enabled = true;
            output.Input = var;
        }
    }

    /// <summary>
    /// Runs the slot's outputs on PWM with the duty from a 0–100 % variable, or back
    /// to their fixed duty with 0.
    /// </summary>
    public void DriveDuty(string slot, int var)
    {
        foreach (var output in OutputsIn(slot))
        {
            output.VariableDutyCycle = var != 0;
            output.DutyCycleInput = var;
            if (var != 0)
                output.DutyCycleDenominator = 1;
            output.PwmEnabled = var != 0 || output.SoftStartEnabled || output.FixedDutyCycle < 100;
        }
    }

    private IEnumerable<Output> OutputsIn(string slot) =>
        _outputs.TryGetValue(slot, out var list)
            ? list.Where(n => n >= 1 && n <= _target.Outputs.Count).Select(n => _target.Outputs[n - 1])
            : [];

    public int VarOf(IDeviceFunction block) =>
        _target.VarMap.FirstOrDefault(v => v.Owner == block)?.VariableIndex ?? 0;

    public int VarOf(IDeviceFunction block, string property) =>
        _target.VarMap.FirstOrDefault(v => v.Owner == block && v.PropertyName == property)?.VariableIndex ?? 0;

    private VirtualInput? ClaimVirtualInput(string key, string label) =>
        Claim(key, label, _target.VirtualInputs, "virtual input", v => v.Enabled, v => v.Enabled = true, v => v.Name = Name(label));

    private string Name(string label) => $"{_fn.Title}: {label}";

    private static void SetLogic(VirtualInput vi, int a, Conditional op0, int b, bool notB,
                                 Conditional op1, int c, bool notC)
    {
        vi.Var0 = a;
        vi.Not0 = false;
        vi.Cond0 = op0;
        vi.Var1 = b;
        vi.Not1 = notB;
        vi.Cond1 = op1;
        vi.Var2 = c;
        vi.Not2 = c != 0 && notC;
    }

    private T? Claim<T>(string key, string label, List<T> pool, string kind,
                        Func<T, bool> isEnabled, Action<T> enable, Action<T> name)
        where T : class, IDeviceFunction
    {
        _used.Add(key);

        T? block = null;
        if (_blocks.TryGetValue(key, out var number))
            block = pool.FirstOrDefault(b => b.Number == number);

        // Free = switched off and not built by another function
        block ??= pool.FirstOrDefault(b => !isEnabled(b) && VehicleFunctionService.OwnerOf(_target, b) == null);

        if (block == null)
        {
            _blocks.Remove(key);
            Problem($"No free {kind} left for \"{label}\". Free one up in the Configuration tab.");
            return null;
        }

        _blocks[key] = block.Number;
        enable(block);
        name(block);
        return block;
    }

    /// <summary>Releases the blocks this build did not ask for.</summary>
    internal void Finish()
    {
        foreach (var key in _blocks.Keys.Where(k => !_used.Contains(k)).ToList())
        {
            VehicleFunctionService.ReleaseBlock(_target, key, _blocks[key]);
            _blocks.Remove(key);
        }
    }
}
