using System.Text.Json.Serialization;
using domain.Enums;

namespace domain.Devices.VehicleFunctions;

/// <summary>
/// Fuel pump. With the ECU's pump request it simply follows the ECU; without one
/// it primes for a moment at ignition on and then runs only while the engine
/// runs, so a stalled or crashed engine stops getting fuel:
///
///   with ECU:    pump = ECU request AND ignition
///   without ECU: pump = (prime OR engine running) AND ignition
///                prime = a pulse of PrimeTime when the ignition comes on
/// </summary>
public sealed class FuelPump : VehicleFunction
{
    [JsonPropertyName("primeTime")] public int PrimeTime { get; set; } = 2000;

    public override string Title => "Fuel pump";
    public override string Summary => "From the ECU, or primed at ignition on and then only while the engine runs.";

    public override IReadOnlyList<InputSlot> InputSlots { get; } =
    [
        new("pump", "ECU pump request", false,
            Help: "The ECU's fuel pump output, for example FOME 0x200 FuelPumpAct. Empty: prime, then engine running."),
        new("running", "Engine running", false,
            Help: "Keeps the pump on when there is no ECU request.", Advanced: true, Default: IgnitionDefault.EngineRunning),
        new("ignition", "Only with", false,
            Help: "The pump never runs without this.", Advanced: true, Default: IgnitionDefault.Ignition)
    ];

    public override IReadOnlyList<OutputSlot> OutputSlots { get; } =
    [
        new("pump", "Pump", "Fuel pump", new OutputDefaults(15, 30, 500))
    ];

    protected internal override void Build(FunctionBuilder b)
    {
        if (!b.HasOutputs("pump"))
            return;

        var ignition = b.In("ignition");
        var request = b.In("pump");
        if (request != 0)
        {
            b.Drive("pump", b.Gate("vi:pump", "pump", request, ignition));
            return;
        }

        if (ignition == 0)
        {
            b.Problem("Without an ECU request the pump needs \"Only with\", usually the ignition.");
            b.Drive("pump", 0);
            return;
        }

        var prime = 0;
        if (PrimeTime > 0 && b.Timer("timer:prime", "prime") is { } timer)
        {
            timer.Input = ignition;
            timer.ResetInput = 0;
            timer.Mode = TimerMode.PulseOneShot;
            timer.Time = PrimeTime;
            prime = b.VarOf(timer);
        }

        var running = b.In("running");
        if (running == 0)
            b.Problem("No engine running signal, so the pump only primes. Set it in the Ignition tab or under Advanced.");

        b.Drive("pump", prime == 0 && running == 0
            ? 0
            : b.Logic("vi:pump", "pump", prime, Conditional.Or, running, op1: Conditional.And, c: ignition));
    }
}

/// <summary>
/// ECU power: on with the ignition, and held on after the ignition goes off for
/// as long as the ECU asks for its main relay, so it can finish its own shutdown
/// (save learned values, park the throttle), but never longer than HoldTime:
///
///   ECU = ignition OR (main relay request AND hold), hold = ignition plus HoldTime
/// </summary>
public sealed class Ecu : VehicleFunction
{
    [JsonPropertyName("holdTime")] public int HoldTime { get; set; } = 10000;

    public override string Title => "ECU";
    public override string Summary => "On with the ignition, held while the ECU finishes its shutdown.";

    public override IReadOnlyList<InputSlot> InputSlots { get; } =
    [
        new("ecu", "Main relay request", false,
            Help: "The ECU's main relay output, for example FOME 0x200 MainRelayAct. Empty: off with the ignition."),
        new("ignition", "Power with", false,
            Help: "What powers the ECU, usually the ignition.", Advanced: true, Default: IgnitionDefault.Ignition)
    ];

    public override IReadOnlyList<OutputSlot> OutputSlots { get; } =
    [
        new("ecu", "ECU", "ECU", new OutputDefaults(10, 20, 300))
    ];

    protected internal override void Build(FunctionBuilder b)
    {
        if (!b.HasOutputs("ecu"))
            return;

        var ignition = b.In("ignition");
        if (ignition == 0)
            b.Problem("Needs the ignition: turn the Ignition block on, or set \"Power with\" under Advanced.");

        var relay = b.In("ecu");
        if (relay == 0 || ignition == 0 || HoldTime <= 0)
        {
            b.Drive("ecu", ignition);
            return;
        }

        var hold = b.Timer("timer:hold", "hold");
        if (hold == null)
        {
            b.Drive("ecu", ignition);
            return;
        }
        hold.Input = ignition;
        hold.ResetInput = 0;
        hold.Mode = TimerMode.OffDelay;
        hold.Time = HoldTime;
        b.Drive("ecu", b.Logic("vi:ecu", "ECU", relay, Conditional.And, b.VarOf(hold), op1: Conditional.Or, c: ignition));
    }
}

/// <summary>
/// Radiator fans. Fan 1 runs on the ECU's request, a thermo switch, the A/C or a
/// coolant temperature limit, and can run on for a while after that ends; fan 2
/// is the second fan or the high speed:
///
///   fan 1 = (request OR A/C OR hot) AND ignition, then on for AfterRun
///   fan 2 = fan 2 request AND ignition
///   hot   = from OnTemp up, until the coolant cools to OffTemp
/// </summary>
public sealed class CoolantFan : VehicleFunction
{
    [JsonPropertyName("onTemp")] public double OnTemp { get; set; } = 95;
    [JsonPropertyName("offTemp")] public double OffTemp { get; set; } = 90;
    [JsonPropertyName("afterRun")] public int AfterRun { get; set; }

    public override string Title => "Coolant fan";
    public override string Summary => "From the ECU, a thermo switch, the A/C or a temperature limit.";

    public override IReadOnlyList<InputSlot> InputSlots { get; } =
    [
        new("fan", "Fan request", false, Help: "The ECU's fan output (FOME 0x200 Fan) or a thermo switch."),
        new("fan2", "Fan 2 request", false, Help: "Second fan or high speed (FOME 0x200 Fan2)."),
        new("ac", "A/C on", false, Help: "Runs fan 1 while the A/C runs."),
        new("temp", "Coolant temp", false,
            Help: "A coolant temperature in °C. Fan 1 comes on at the on temperature and stays on until the off temperature.",
            Advanced: true, Value: true),
        new("ignition", "Only with", false, Help: "The fans run only while this is on.",
            Advanced: true, Default: IgnitionDefault.Ignition)
    ];

    public override IReadOnlyList<OutputSlot> OutputSlots { get; } =
    [
        new("fan", "Fan 1", "Coolant fan", new OutputDefaults(20, 50, 1500, SoftStartMs: 1000)),
        new("fan2", "Fan 2", "Coolant fan 2", new OutputDefaults(20, 50, 1500, SoftStartMs: 1000))
    ];

    protected internal override void Build(FunctionBuilder b)
    {
        var ignition = b.In("ignition");

        if (b.HasOutputs("fan"))
        {
            var hot = 0;
            var temp = b.In("temp");
            if (temp != 0)
            {
                var on = b.Condition("cond:on", "hot", temp, Operator.GreaterThanOrEqual, OnTemp);
                var off = b.Condition("cond:off", "cool", temp, Operator.LessThanOrEqual, Math.Min(OffTemp, OnTemp));
                if (on != 0 && off != 0)
                    hot = b.Latch("vi:hot", "hot", on, off);
            }

            var wanted = b.Any("vi:fan", "fan wanted", b.In("fan"), b.In("ac"), hot);
            var fan = b.Gate("vi:fanOn", "fan 1", wanted, ignition);
            if (fan != 0 && AfterRun > 0 && b.Timer("timer:afterRun", "after-run") is { } timer)
            {
                timer.Input = fan;
                timer.ResetInput = 0;
                timer.Mode = TimerMode.OffDelay;
                timer.Time = AfterRun;
                fan = b.VarOf(timer);
            }
            b.Drive("fan", fan);
        }

        if (b.HasOutputs("fan2"))
            b.Drive("fan2", b.Gate("vi:fan2", "fan 2", b.In("fan2"), ignition));
    }
}
