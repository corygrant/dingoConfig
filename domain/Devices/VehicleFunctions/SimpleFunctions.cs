using System.Text.Json.Serialization;
using domain.Enums;

namespace domain.Devices.VehicleFunctions;

/// <summary>One switch driving one group of outputs directly, no blocks needed.</summary>
public abstract class SwitchedFunction : VehicleFunction
{
    protected abstract InputSlot Switch { get; }
    protected abstract OutputSlot Loads { get; }

    public override IReadOnlyList<InputSlot> InputSlots => [Switch];
    public override IReadOnlyList<OutputSlot> OutputSlots => [Loads];

    protected internal override void Build(FunctionBuilder b) => b.Drive(Loads.Key, b.In(Switch.Key));
}

public sealed class Horn : SwitchedFunction
{
    public override string Title => "Horn";
    public override string Summary => "Horns while the button is held.";
    protected override InputSlot Switch { get; } = new("button", "Horn button", true);
    protected override OutputSlot Loads { get; } =
        new("horn", "Horns", "Horn", new OutputDefaults(12, 30, 200));
}

/// <summary>
/// Courtesy light: on while a door is open, then stays on for the delay after
/// the last door closes. A light switch can keep it on as well.
/// </summary>
public sealed class InteriorLight : VehicleFunction
{
    [JsonPropertyName("offDelay")] public int OffDelay { get; set; } = 10000;

    public override string Title => "Interior light";
    public override string Summary => "On with the doors, off after a delay once they close.";

    public override IReadOnlyList<InputSlot> InputSlots { get; } =
    [
        new("door1", "Door switch", true, Shared: "door"),
        new("door2", "Door switch 2", false, Shared: "door"),
        new("door3", "Door switch 3", false, Shared: "door"),
        new("manual", "Light switch", false, CanToggle: true)
    ];

    public override IReadOnlyList<OutputSlot> OutputSlots { get; } =
    [
        new("light", "Light", "Interior light", new OutputDefaults(3, 8, 100))
    ];

    protected internal override void Build(FunctionBuilder b)
    {
        // The doors are built even without a light: the ignition turns the dash on with them
        var door1 = b.In("door1");
        var door2 = b.In("door2");
        var door3 = b.In("door3");
        var doors = door2 == 0 && door3 == 0
            ? door1
            : b.Logic("vi:doors", "doors", door1, Conditional.Or, door2, op1: Conditional.Or, c: door3);
        b.Share("door", doors);

        if (!b.HasOutputs("light"))
            return;

        var lit = doors;
        var timer = b.Timer("timer:delay", "off delay");
        if (timer != null)
        {
            timer.Input = doors;
            timer.ResetInput = 0;
            timer.Mode = TimerMode.OffDelay;
            timer.Time = OffDelay;
            lit = b.VarOf(timer);
        }

        var manual = b.In("manual");
        b.Drive("light", manual != 0 ? b.Logic("vi:light", "light", lit, Conditional.Or, manual) : lit);
    }
}
