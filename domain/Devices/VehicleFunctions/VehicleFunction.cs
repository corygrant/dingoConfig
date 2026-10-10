using System.Text.Json.Serialization;
using domain.Devices.Functions;
using domain.Enums;

namespace domain.Devices.VehicleFunctions;

/// <summary>An input a function asks for, filled with any device variable.</summary>
/// <param name="CanToggle">The input may be a push button that toggles the function on and off.</param>
/// <param name="Advanced">Rarely changed, shown under Advanced.</param>
public record InputSlot(string Key, string Label, bool Required, bool CanToggle = false, string Help = "",
                        bool Advanced = false);

/// <summary>Electrical defaults an output gets when it is added to a slot.</summary>
public record OutputDefaults(double CurrentLimit, double InrushLimit, int InrushTime,
                             ResetMode ResetMode = ResetMode.Count, int ResetCount = 3, int ResetTime = 1000)
{
    public void ApplyTo(Output output)
    {
        output.CurrentLimit = CurrentLimit;
        output.InrushCurrentLimit = InrushLimit;
        output.InrushTime = InrushTime;
        output.ResetMode = ResetMode;
        output.ResetCountLimit = ResetCount;
        output.ResetTime = ResetTime;
    }
}

/// <summary>A group of outputs a function drives together, for example the left indicators.</summary>
/// <param name="OutputName">Name given to an output added here while it still has its default name.</param>
public record OutputSlot(string Key, string Label, string OutputName, OutputDefaults Defaults);

public class FunctionInput
{
    [JsonPropertyName("var")] public int Var { get; set; }
    [JsonPropertyName("toggle")] public bool Toggle { get; set; }
}

/// <summary>
/// A ready-made car function, such as turn signals or headlights. It is only a
/// recipe: it builds its logic from the PDM's existing virtual inputs, flashers
/// and timers and points its outputs at the result, so the firmware needs nothing
/// new. The project file keeps the recipe; the device keeps the blocks it built.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TurnSignals), "turnSignals")]
[JsonDerivedType(typeof(Headlights), "headlights")]
[JsonDerivedType(typeof(Taillights), "taillights")]
[JsonDerivedType(typeof(InteriorLight), "interiorLight")]
[JsonDerivedType(typeof(Horn), "horn")]
public abstract class VehicleFunction
{
    [JsonPropertyName("inputs")] public Dictionary<string, FunctionInput> Inputs { get; set; } = new();

    // Slot key -> output numbers (1-based)
    [JsonPropertyName("outputs")] public Dictionary<string, List<int>> Outputs { get; set; } = new();

    // Blocks this function built, by its own key: "vi:left", "flasher:flash", "timer:delay"
    [JsonPropertyName("blocks")] public Dictionary<string, int> Blocks { get; set; } = new();

    [JsonIgnore] public abstract string Title { get; }
    [JsonIgnore] public abstract string Summary { get; }
    [JsonIgnore] public abstract IReadOnlyList<InputSlot> InputSlots { get; }
    [JsonIgnore] public abstract IReadOnlyList<OutputSlot> OutputSlots { get; }

    // What the last build could not do, for the UI
    [JsonIgnore] public List<string> Problems { get; } = [];

    // Signals the last build handed out, for other functions: "lowBeam"
    [JsonIgnore] public IReadOnlyDictionary<string, int> Signals { get; internal set; } = new Dictionary<string, int>();

    /// <summary>Builds from another function's signals, so it is rebuilt after the others.</summary>
    internal virtual bool ReadsSignals => false;

    public FunctionInput Input(string key)
    {
        if (!Inputs.TryGetValue(key, out var input))
            Inputs[key] = input = new FunctionInput();
        return input;
    }

    public List<int> OutputsIn(string slot)
    {
        if (!Outputs.TryGetValue(slot, out var list))
            Outputs[slot] = list = [];
        return list;
    }

    public bool HasOutputs(string slot) => Outputs.TryGetValue(slot, out var list) && list.Count > 0;

    public OutputSlot? SlotOf(int outputNumber) =>
        OutputSlots.FirstOrDefault(s => Outputs.TryGetValue(s.Key, out var list) && list.Contains(outputNumber));

    /// <summary>First fill when the function is added, for example the ignition signal.</summary>
    protected internal virtual void OnAdded(FwDevice device) { }

    /// <summary>Writes the blocks the function owns and points its outputs at them.</summary>
    protected internal abstract void Build(FunctionBuilder b);
}
