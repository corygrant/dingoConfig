using System.Text.Json.Serialization;
using domain.Enums;

namespace domain.Devices.VehicleFunctions;

/// <summary>
/// Parking, low beam, high beam, front fog and daytime running lights from the
/// usual light switches:
///
///   low       = low switch AND ignition (when an ignition signal is given)
///   parking   = parking switch OR low
///   high      = (high switch AND low) OR flash-to-pass
///   front fog = fog switch AND parking (with the lights on, as the law wants)
///   DRL       = ignition AND NOT low
///
/// With H4-style lamps the low beam can drop out while the high beam is on. The
/// low beam and front fogs are handed to Taillights for the rear fog lamp.
/// </summary>
public sealed class Headlights : VehicleFunction
{
    public const string LowBeamSignal = "lowBeam";
    public const string FrontFogSignal = "frontFog";

    [JsonPropertyName("lowOffWithHigh")] public bool LowOffWithHigh { get; set; }

    public override string Title => "Headlights";
    public override string Summary => "Parking, low and high beam, flash to pass, front fogs and daytime running lights.";

    public override IReadOnlyList<InputSlot> InputSlots { get; } =
    [
        new("low", "Low beam switch", true, CanToggle: true),
        new("high", "High beam switch", false, CanToggle: true),
        new("flash", "Flash to pass", false),
        new("park", "Parking light switch", false, CanToggle: true),
        new("fog", "Front fog switch", false, CanToggle: true),
        new("ignition", "Only with", false,
            Help: "Low and high beam work only while this is on, usually the ignition. Daytime running lights need it.",
            Advanced: true)
    ];

    public override IReadOnlyList<OutputSlot> OutputSlots { get; } =
    [
        new("low", "Low beam", "Low beam", new OutputDefaults(12, 40, 300)),
        new("high", "High beam", "High beam", new OutputDefaults(12, 40, 300)),
        new("park", "Parking & tail", "Parking lights", new OutputDefaults(5, 12, 100)),
        new("fog", "Front fog", "Front fog", new OutputDefaults(10, 30, 300)),
        new("drl", "DRL", "DRL", new OutputDefaults(4, 10, 100))
    ];

    protected internal override void OnAdded(FwDevice device)
    {
        // The ignition, when this device runs one
        Input("ignition").Var = VehicleFunctionService.DefaultVar(device, IgnitionDefault.Ignition);
    }

    protected internal override void Build(FunctionBuilder b)
    {
        var ignition = b.In("ignition");
        var lowSwitch = b.In("low");
        var low = ignition != 0 && lowSwitch != 0
            ? b.Logic("vi:low", "low beam", lowSwitch, Conditional.And, ignition)
            : lowSwitch;

        // Parking lights, also what the front fogs need to be on
        var parking = low;
        if (b.HasOutputs("park") || b.HasOutputs("fog"))
        {
            var park = b.In("park");
            if (park != 0)
                parking = b.Logic("vi:park", "parking", park, Conditional.Or, low);
        }
        if (b.HasOutputs("park"))
            b.Drive("park", parking);

        var frontFog = 0;
        if (b.HasOutputs("fog"))
        {
            var fogSwitch = b.In("fog");
            if (fogSwitch != 0)
                frontFog = parking != 0
                    ? b.Logic("vi:fog", "front fog", fogSwitch, Conditional.And, parking)
                    : fogSwitch;
            b.Drive("fog", frontFog);
        }

        b.Share(LowBeamSignal, low);
        b.Share(FrontFogSignal, frontFog);

        var high = 0;
        if (b.HasOutputs("high") || (LowOffWithHigh && b.HasOutputs("low")))
        {
            var highSwitch = b.In("high");
            var flash = b.In("flash");
            if (highSwitch != 0 || flash != 0)
                high = b.Logic("vi:high", "high beam", highSwitch, Conditional.And, low,
                               op1: Conditional.Or, c: flash);
        }

        if (b.HasOutputs("high"))
            b.Drive("high", high);

        if (b.HasOutputs("low"))
            b.Drive("low", LowOffWithHigh && high != 0
                ? b.Logic("vi:lowOut", "low beam out", low, Conditional.And, high, notB: true)
                : low);

        if (b.HasOutputs("drl"))
        {
            if (ignition == 0)
                b.Problem("Daytime running lights need the \"Only with\" signal, usually the ignition.");
            b.Drive("drl", ignition != 0 ? b.Logic("vi:drl", "DRL", ignition, Conditional.And, low, notB: true) : 0);
        }
    }
}
