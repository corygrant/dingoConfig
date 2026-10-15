using System.Text.Json.Serialization;
using domain.Enums;

namespace domain.Devices.VehicleFunctions;

/// <summary>
/// Left and right indicators plus hazards. One flasher is the clock for both
/// sides, so the hazards flash left and right in step and a side switched on
/// while the other is flashing joins the same rhythm:
///
///   request = left OR right OR hazard  -> flasher
///   left    = (left OR hazard) AND flasher
///   right   = (right OR hazard) AND flasher
/// </summary>
public sealed class TurnSignals : VehicleFunction
{
    // About 85 flashes a minute, inside the 60-120 the regulations allow
    [JsonPropertyName("onTime")] public int OnTime { get; set; } = 350;
    [JsonPropertyName("offTime")] public int OffTime { get; set; } = 350;

    private static readonly OutputDefaults Lamp = new(CurrentLimit: 5, InrushLimit: 15, InrushTime: 100);

    public override string Title => "Turn signals & hazards";
    public override string Summary => "Indicators flash in step, hazards flash both sides.";

    public override IReadOnlyList<InputSlot> InputSlots { get; } =
    [
        new("left", "Left switch", true, CanToggle: true),
        new("right", "Right switch", true, CanToggle: true),
        new("hazard", "Hazards", false, CanToggle: true)
    ];

    public override IReadOnlyList<OutputSlot> OutputSlots { get; } =
    [
        new("left", "Left", "Indicator L", Lamp),
        new("right", "Right", "Indicator R", Lamp)
    ];

    protected internal override void Build(FunctionBuilder b)
    {
        if (!b.HasOutputs("left") && !b.HasOutputs("right"))
            return;

        var left = b.In("left");
        var right = b.In("right");
        var hazard = b.In("hazard");

        var request = b.Logic("vi:request", "request", left, Conditional.Or, right,
                              op1: Conditional.Or, c: hazard);

        var flasher = b.Flasher("flasher:flash", "flash");
        var flash = 0;
        if (flasher != null)
        {
            flasher.Input = request;
            flasher.OnTime = OnTime;
            flasher.OffTime = OffTime;
            flasher.Single = false;
            flash = b.VarOf(flasher);
        }

        if (b.HasOutputs("left"))
            b.Drive("left", b.Logic("vi:left", "left", left, Conditional.Or, hazard,
                                    op1: Conditional.And, c: flash));
        if (b.HasOutputs("right"))
            b.Drive("right", b.Logic("vi:right", "right", right, Conditional.Or, hazard,
                                     op1: Conditional.And, c: flash));
    }
}
