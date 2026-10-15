using System.Text.Json.Serialization;
using domain.Enums;

namespace domain.Devices.VehicleFunctions;

/// <summary>
/// Heater blower. With a resistor pack each speed has its own output and the
/// highest speed switched on wins:
///
///   speed 3 = switch 3
///   speed 2 = switch 2 AND NOT switch 3
///   speed 1 = switch 1 AND NOT (switch 2 OR switch 3)          all AND accessory
///
/// A single PWM motor instead takes its speed from a 0–100 % value on the Speed 1
/// outputs, switched on by the Speed 1 switch (or by the accessory alone).
/// </summary>
public sealed class BlowerFan : VehicleFunction
{
    public override string Title => "Blower fan";
    public override string Summary => "Heater blower, resistor pack speeds or one PWM motor.";

    public override IReadOnlyList<InputSlot> InputSlots { get; } =
    [
        new("speed1", "Speed 1 switch", false, CanToggle: true),
        new("speed2", "Speed 2 switch", false, CanToggle: true),
        new("speed3", "Speed 3 switch", false, CanToggle: true),
        new("value", "Speed %", false,
            Help: "One PWM motor: its speed from a 0–100 % value, such as a knob on a CANboard.",
            Advanced: true, Value: true),
        new("ignition", "Only with", false, Help: "The blower runs only while this is on.",
            Advanced: true, Default: IgnitionDefault.Accessory)
    ];

    public override IReadOnlyList<OutputSlot> OutputSlots { get; } =
    [
        new("speed1", "Speed 1", "Blower", new OutputDefaults(15, 40, 500)),
        new("speed2", "Speed 2", "Blower 2", new OutputDefaults(15, 40, 500)),
        new("speed3", "Speed 3", "Blower 3", new OutputDefaults(15, 40, 500))
    ];

    protected internal override void Build(FunctionBuilder b)
    {
        var ignition = b.In("ignition");
        var s2 = b.HasOutputs("speed1") || b.HasOutputs("speed2") ? b.In("speed2") : 0;
        var s3 = b.HasOutputs("speed1") || b.HasOutputs("speed2") || b.HasOutputs("speed3") ? b.In("speed3") : 0;

        if (b.HasOutputs("speed3"))
            b.Drive("speed3", b.Gate("vi:speed3", "speed 3", s3, ignition));

        if (b.HasOutputs("speed2"))
            b.Drive("speed2", s2 == 0 ? 0
                : s3 == 0 ? b.Gate("vi:speed2", "speed 2", s2, ignition)
                : b.Logic("vi:speed2", "speed 2", s2, Conditional.And, s3, notB: true, op1: Conditional.And, c: ignition));

        if (!b.HasOutputs("speed1"))
            return;

        var s1 = b.In("speed1");
        var value = b.In("value");
        b.DriveDuty("speed1", value);
        if (value != 0)
        {
            // One PWM motor: the value sets the speed, the switch only turns it on
            b.Drive("speed1", s1 != 0 ? b.Gate("vi:speed1", "speed 1", s1, ignition) : ignition);
            return;
        }

        var higher = b.Any("vi:higher", "speed 2 or 3", s2, s3);
        b.Drive("speed1", s1 == 0 ? 0
            : higher == 0 ? b.Gate("vi:speed1", "speed 1", s1, ignition)
            : b.Logic("vi:speed1", "speed 1", s1, Conditional.And, higher, notB: true, op1: Conditional.And, c: ignition));
    }
}

/// <summary>
/// Wipers and washer on the PDM's own wiper block: slow, fast and intermittent
/// switches, a park switch so the blades stop out of sight, and a washer button
/// that also gives a few wipes. The washer pump runs while its button is held.
/// </summary>
public sealed class Wipers : VehicleFunction
{
    [JsonPropertyName("interPause")] public int InterPause { get; set; } = 4000;
    [JsonPropertyName("washWipes")] public int WashWipes { get; set; } = 3;
    [JsonPropertyName("parkedHigh")] public bool ParkedHigh { get; set; }

    public override string Title => "Wipers";
    public override string Summary => "Slow, fast and intermittent wipers, park switch and washer.";

    public override IReadOnlyList<InputSlot> InputSlots { get; } =
    [
        new("slow", "Slow switch", false),
        new("fast", "Fast switch", false),
        new("inter", "Intermittent", false),
        new("wash", "Washer button", false),
        new("park", "Park switch", false, Help: "The motor's park contact, so the blades stop out of sight."),
        new("swipe", "Single wipe", false, Help: "One wipe per press.", Advanced: true),
        new("ignition", "Only with", false,
            Help: "Wipers work only while this is on. Empty: whenever the switches are on.", Advanced: true)
    ];

    public override IReadOnlyList<OutputSlot> OutputSlots { get; } =
    [
        new("slow", "Slow", "Wiper slow", new OutputDefaults(8, 25, 500)),
        new("fast", "Fast", "Wiper fast", new OutputDefaults(8, 25, 500)),
        new("wash", "Washer", "Washer pump", new OutputDefaults(5, 15, 200))
    ];

    protected internal override void Build(FunctionBuilder b)
    {
        var ignition = b.In("ignition");
        var wipers = b.HasOutputs("slow") || b.HasOutputs("fast");
        var wash = wipers || b.HasOutputs("wash") ? b.Gate("vi:wash", "washer", b.In("wash"), ignition) : 0;

        if (b.HasOutputs("wash"))
            b.Drive("wash", wash);

        if (!wipers)
            return;

        var wiper = b.Wiper("wiper:motor", "wipers");
        if (wiper == null)
            return;

        if (b.In("park") == 0)
            b.Problem("No park switch: the blades stop wherever they are when switched off.");

        wiper.Mode = WiperMode.DigIn;
        wiper.SlowInput = b.Gate("vi:slow", "slow", b.In("slow"), ignition);
        wiper.FastInput = b.Gate("vi:fast", "fast", b.In("fast"), ignition);
        wiper.InterInput = b.Gate("vi:inter", "intermittent", b.In("inter"), ignition);
        wiper.WashInput = wash;
        wiper.SwipeInput = b.Gate("vi:swipe", "single wipe", b.In("swipe"), ignition);
        wiper.OnInput = 0;
        wiper.SpeedInput = 0;
        wiper.ParkInput = b.In("park");
        wiper.ParkStopLevel = ParkedHigh;
        wiper.WashWipeCycles = WashWipes;
        wiper.IntermitTime[0] = InterPause;

        b.Drive("slow", b.VarOf(wiper, "Slow Output"));
        b.Drive("fast", b.VarOf(wiper, "Fast Output"));
    }
}
