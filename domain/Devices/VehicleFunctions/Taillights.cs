using domain.Enums;

namespace domain.Devices.VehicleFunctions;

/// <summary>
/// The lamps at the back that have their own switches:
///
///   brake    = brake pedal switch
///   reverse  = reverse switch
///   rear fog = rear fog switch AND (low beam OR front fog)
///
/// The rear fog lamp may only work with the low beam or the front fogs on, so it
/// follows the Headlights function unless "Rear fog only with" names something
/// else. The tail (position) lamps run with the parking lights in Headlights.
/// </summary>
public sealed class Taillights : VehicleFunction
{
    public override string Title => "Taillights";
    public override string Summary => "Brake, reverse and rear fog lamps.";

    public override IReadOnlyList<InputSlot> InputSlots { get; } =
    [
        new("brake", "Brake pedal switch", false),
        new("reverse", "Reverse switch", false),
        new("fog", "Rear fog switch", false, CanToggle: true),
        new("fogWith", "Rear fog with", false,
            Help: "The rear fog lamp works only while this is on. Empty: the low beam or front fogs from Headlights.",
            Advanced: true)
    ];

    public override IReadOnlyList<OutputSlot> OutputSlots { get; } =
    [
        new("brake", "Brake lamps", "Brake light", new OutputDefaults(8, 20, 100)),
        new("reverse", "Reverse lamps", "Reverse light", new OutputDefaults(5, 15, 100)),
        new("fog", "Rear fog", "Rear fog", new OutputDefaults(4, 10, 100))
    ];

    internal override bool ReadsSignals => true;

    protected internal override void Build(FunctionBuilder b)
    {
        if (b.HasOutputs("brake"))
            b.Drive("brake", b.In("brake"));
        if (b.HasOutputs("reverse"))
            b.Drive("reverse", b.In("reverse"));

        if (!b.HasOutputs("fog"))
            return;

        var fogSwitch = b.In("fog");
        if (fogSwitch == 0)
        {
            b.Drive("fog", 0);
            return;
        }

        var with = b.In("fogWith");
        var low = with == 0 ? b.SignalOf(Headlights.LowBeamSignal) : with;
        var frontFog = with == 0 ? b.SignalOf(Headlights.FrontFogSignal) : 0;
        b.Drive("fog", low == 0 && frontFog == 0
            ? fogSwitch // nothing to follow: on its own switch
            : b.Logic("vi:fog", "rear fog", low, Conditional.Or, frontFog, op1: Conditional.And, c: fogSwitch));
    }
}
