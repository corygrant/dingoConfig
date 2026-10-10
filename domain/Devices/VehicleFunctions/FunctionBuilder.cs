using domain.Devices.Functions;
using domain.Enums;
using domain.Interfaces;

namespace domain.Devices.VehicleFunctions;

/// <summary>
/// Hands a function the device blocks it asks for while it builds, reusing the
/// ones it already owns, and releases whatever it no longer used afterwards.
/// </summary>
public sealed class FunctionBuilder
{
    private readonly FwDevice _target;
    private readonly VehicleFunction _fn;
    private readonly Dictionary<string, List<int>> _outputs;
    private readonly Dictionary<string, int> _blocks;
    private readonly HashSet<string> _used = [];

    // Signals the build hands out to other functions
    internal Dictionary<string, int> Shared { get; } = new();

    internal FunctionBuilder(FwDevice target, VehicleFunction fn)
    {
        _target = target;
        _fn = fn;
        _outputs = fn.Outputs;
        _blocks = fn.Blocks;
    }

    public bool HasOutputs(string slot) => _outputs.TryGetValue(slot, out var list) && list.Count > 0;

    /// <summary>The device's Ignition block, null on a device without one.</summary>
    public Ignition? Ignition => _target.Def.HasIgnition ? _target.Ignition : null;

    /// <summary>
    /// Hands out a signal another function builds on, such as the low beam for the
    /// rear fog lamp.
    /// </summary>
    public void Share(string key, int var) => Shared[key] = var;

    /// <summary>A signal another function on the device handed out, 0 when none did.</summary>
    public int SignalOf(string key) => _target.VehicleFunctions
        .Where(fn => fn != _fn)
        .Select(fn => fn.Signals.GetValueOrDefault(key))
        .FirstOrDefault(v => v != 0);

    public void Problem(string text) => _fn.Problems.Add(text);

    /// <summary>
    /// The variable an input slot reads. A push button goes through a latching
    /// virtual input, so each press toggles it. An empty slot with a default reads
    /// the Ignition block.
    /// </summary>
    public int In(string key)
    {
        var input = _fn.Input(key);
        var slot = _fn.InputSlots.FirstOrDefault(s => s.Key == key);
        if (input.Var == 0)
            return slot is { Default: not IgnitionDefault.None }
                ? VehicleFunctionService.DefaultVar(_target, slot.Default)
                : 0;

        if (!input.Toggle)
            return input.Var;

        var label = slot?.Label ?? key;
        var vi = ClaimVirtualInput($"vi:toggle:{key}", $"{label} toggle");
        if (vi == null)
            return 0;

        SetLogic(vi, input.Var, Conditional.Or, 0, false, Conditional.And, 0, false);
        vi.Mode = InputMode.Latched;
        return VarOf(vi);
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
