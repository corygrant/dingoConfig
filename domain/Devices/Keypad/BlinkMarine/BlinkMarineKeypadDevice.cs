using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Devices.Keypad.BlinkMarine.Enums;
using domain.Devices.Keypad.BlinkMarine.Functions;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace domain.Devices.Keypad.BlinkMarine;

public class BlinkMarineKeypadDevice : IKeypadDevice
{
    [JsonIgnore] private ILogger<BlinkMarineKeypadDevice>? _logger;

    [JsonIgnore] public Guid Guid { get; }
    [JsonIgnore] public string Type => "BlinkMarineKeypad";
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("ids")] public DeviceIds Ids { get; set; }
    [JsonPropertyName("cyclicGap")] public TimeSpan CyclicGap { get; } = TimeSpan.FromSeconds(1);
    [JsonPropertyName("cyclicPause")] public TimeSpan CyclicPause { get; } = TimeSpan.FromMilliseconds(1);
    [JsonPropertyName("isSim")] public bool IsSim { get; set; }
    [JsonIgnore] private DateTime _lastRxTime = DateTime.Now;
    
    [JsonIgnore] public static DeviceIds DefaultIds { get; } = new DeviceIds(0x15, 0x0, 0x0);

    [JsonIgnore]
    public bool Connected
    {
        get;
        private set
        {
            if (field && !value)
                Clear();
            field = value;
        }
    }

    [JsonPropertyName("model")] public string Model { get; set; }
    [JsonPropertyName("numButtons")] public int NumButtons { get; set; }
    [JsonPropertyName("numDials")] public int NumDials { get; set; }
    [JsonPropertyName("numAnalogInputs")] public int NumAnalogInputs { get; set; }
    [JsonIgnore] public BacklightColor BacklightColor { get; set; }
    [JsonIgnore] public int BacklightBrightness { get; set; }
    [JsonIgnore] private int IndicatorBrightness { get; set; }
    [JsonIgnore] private byte TickTimer { get; set; }

    [JsonPropertyName("buttons")] public List<Button> Buttons { get; init; } = [];
    [JsonPropertyName("dials")] public List<Dial> Dials { get; init; } = [];
    [JsonPropertyName("analogInputs")] public List<AnalogInput> AnalogInputs { get; init; } = [];

    [JsonIgnore]
    public Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>> StatusSigs { get; set; } =
        null!;

    [JsonConstructor]
    public BlinkMarineKeypadDevice(string name, DeviceIds ids, string model)
    {
        Name = name;
        Ids = ids;
        Model = model;
        var config = BlinkMarineModels.Lookup(Model);
        NumButtons = config.numButtons;
        NumDials = config.numDials;
        NumAnalogInputs = config.numAnalogInputs;
        Guid = Guid.NewGuid();
        InitCollections();
        InitStatusSigs();
    }

    public void SetLogger(ILogger<BlinkMarineKeypadDevice> logger)
    {
        _logger = logger;
    }

    private void InitCollections()
    {
        for (var i = 0; i < NumButtons; i++)
            Buttons.Add(new Button(i + 1, $"button{i + 1}"));

        for (var i = 0; i < NumDials; i++)
            Dials.Add(new Dial(i + 1, $"dial{i + 1}"));

        for (var i = 0; i < NumAnalogInputs; i++)
            AnalogInputs.Add(new AnalogInput(i + 1, $"analogInput{i + 1}"));
    }

    private void InitStatusSigs()
    {
        StatusSigs = new Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>>();

        // Button States
        StatusSigs[(int)MessageId.ButtonState] = [];
        for (var i = 0; i < NumButtons; i++)
        {
            var button = Buttons[i];
            StatusSigs[(int)MessageId.ButtonState].Add((
                new DbcSignal { Name = $"Button{i + 1}.State", StartBit = i, Length = 1 },
                val => button.State = val != 0
            ));
        }

        if (NumDials > 0)
        {
            // Dial 1 
            StatusSigs[(int)MessageId.DialState1] =
            [
                //Direction
                (
                    new DbcSignal { Name = $"Dial1.Direction", StartBit = 7, Length = 1 },
                    val => Dials[0].Direction = (DialDirection)val
                ),
                //Ticks
                (
                    new DbcSignal { Name = $"Dial1.Ticks", StartBit = 0, Length = 7 },
                    val => Dials[0].Ticks = (int)val
                ),
                //Counter
                (
                    new DbcSignal { Name = $"Dial1.Counter", StartBit = 8, Length = 16 },
                    val => Dials[0].Counter = (int)val
                )
            ];
        }

        if (NumDials > 1)
        {
            // Dial 2 
            StatusSigs[(int)MessageId.DialState2] =
            [
                //Direction
                (
                    new DbcSignal { Name = $"Dial2.Direction", StartBit = 7, Length = 1 },
                    val => Dials[1].Direction = (DialDirection)val
                ),
                //Ticks
                (
                    new DbcSignal { Name = $"Dial2.Ticks", StartBit = 0, Length = 7 },
                    val => Dials[1].Ticks = (int)val
                ),
                //Counter
                (
                    new DbcSignal { Name = $"Dial2.Counter", StartBit = 8, Length = 16 },
                    val => Dials[1].Counter = (int)val
                )
            ];
        }

        // Analog Inputs
        StatusSigs[(int)MessageId.AnalogInput] = [];
        for (var i = 0; i < NumAnalogInputs; i++)
        {
            var input = AnalogInputs[i];
            StatusSigs[(int)MessageId.AnalogInput].Add((
                new DbcSignal
                    { Name = $"AnalogInput{i + 1}.Value", StartBit = i * 16, Length = 16, Factor = 0.01 }, // 5/500
                val => input.Voltage = val
            ));
        }
    }

    /// <remarks>
    /// Returns true only on Connected false to true transition
    /// </remarks>
    public bool UpdateIsConnected()
    {
        var lastConnected = Connected;
        var timeSpan = DateTime.Now - _lastRxTime;
        Connected = timeSpan.TotalMilliseconds < 500;
        
        return Connected & !lastConnected;
    }

    private void Clear()
    {
        foreach (var btn in Buttons)
            btn.State = false;

        foreach (var dial in Dials)
        {
            dial.Counter = 0;
            dial.Ticks = 0;
            dial.Direction = DialDirection.Clockwise;
        }

        foreach (var input in AnalogInputs)
            input.Voltage = 0;

        _logger?.LogInformation("{Name} BlinkMarine Keypad cleared", Name);
    }

    public bool InIdRange(int id)
    {
        // CANopen uses BaseId as node ID (1-127)
        // Message IDs: 0x180 + nodeId, 0x280 + nodeId, etc.
        return id == ((int)MessageId.ButtonState + Ids.Base) ||
               id == ((int)MessageId.SetLed + Ids.Base) ||
               id == ((int)MessageId.DialState1 + Ids.Base) ||
               id == ((int)MessageId.SetLedBlink + Ids.Base) ||
               id == ((int)MessageId.DialState2 + Ids.Base) ||
               id == ((int)MessageId.LedBrightness + Ids.Base) ||
               id == ((int)MessageId.AnalogInput + Ids.Base) ||
               id == ((int)MessageId.Backlight + Ids.Base) ||
               id == ((int)MessageId.Heartbeat + Ids.Base);
    }

    public void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue, List<DeviceCanFrame> outgoing)
    {
        switch ((MessageId)id - Ids.Base)
        {
            case MessageId.ButtonState:
                ParseButtonState(data);
                break;
            case MessageId.SetLed:
                ParseSetLed(data, false);
                break;
            case MessageId.DialState1:
                ParseDialState(data, index: 0);
                break;
            case MessageId.SetLedBlink:
                ParseSetLed(data, true);
                break;
            case MessageId.DialState2:
                ParseDialState(data, index: 1);
                break;
            case MessageId.LedBrightness:
                ParseLedBrightness(data);
                break;
            case MessageId.AnalogInput:
                ParseAnalogInput(data);
                break;
            case MessageId.Backlight:
                ParseBacklight(data);
                break;
            case MessageId.Heartbeat:
            case MessageId.Nmt:
            case MessageId.SdoResponse:
            case MessageId.SdoRequest:
                break;
            default:
                //Don't update LastRxTime, not a valid message
                return;
        }

        _lastRxTime = DateTime.Now;
    }

    private void ParseButtonState(byte[] data)
    {
        // Button states are packed as bits
        // Each byte contains 8 button states
        for (var i = 0; i < NumButtons && i < Buttons.Count; i++)
        {
            var button = Buttons[i];
            var byteIndex = i / 8;
            var bitIndex = i % 8;

            if (byteIndex < data.Length)
                button.State = (data[byteIndex] & (1 << bitIndex)) != 0;
        }
    }

    private void ParseSetLed(byte[] data, bool blink)
    {
        var ledRed = new int[NumButtons];
        var ledGreen = new int[NumButtons];
        var ledBlue = new int[NumButtons];

        switch (Model)
        {
            case "pkp1100" or 
                "pkp1200" or 
                "pkp1500" or 
                "pkp1600" or 
                "pkp2200" or 
                "pkp2300" or 
                "pkp2400":
                //1 to 8 buttons
                ExtractColor(data, ledRed, 0);
                ExtractColor(data, ledGreen, 8);
                ExtractColor(data, ledBlue, 16);
                break;

            case "pkp2600":
                //12 buttons
                //Uses alternative mapping
                ExtractColor(data, ledRed, 0);
                ExtractColor(data, ledGreen, 12);
                ExtractColor(data, ledBlue, 24);
                break;

            case "pkp2500" or 
                "pkp3500":
                //9 to 16 buttons
                ExtractColor(data, ledRed, 0);
                ExtractColor(data, ledGreen, 16);
                ExtractColor(data, ledBlue, 32);
                break;

            case "pkp3500mt":
                //15 buttons
                // Skip bit positions 0, 10 (red), 15-16, 26 (green), 31-32, 42 (blue)
                ExtractColorWithGaps(data, ledRed, 1, [10]);
                ExtractColorWithGaps(data, ledGreen, 17, [26]);
                ExtractColorWithGaps(data, ledBlue, 33, [42]);
                break;
            
            default:
                throw new InvalidOperationException($"Unknown model: {Model}");
        }

        for (var i = 0; i < NumButtons; i++)
        {
            if (blink)
                Buttons[i].BlinkColor = RgbToButtonColor(ledRed[i], ledGreen[i], ledBlue[i]);
            else
                Buttons[i].IndicatorColor = RgbToButtonColor(ledRed[i], ledGreen[i], ledBlue[i]);
        }
    }

    private static void ExtractColor(byte[] data, int[] led, int startBit)
    {
        for (var i = 0; i < led.Length; i++)
            led[i] = (int)DbcSignalCodec.ExtractSignalInt(data, startBit + i, 1);
    }

    private static void ExtractColorWithGaps(byte[] data, int[] led, int startBit, int[] skipBits)
    {
        var bitPos = startBit;
        for (var i = 0; i < led.Length; i++)
        {
            // Skip any gap bits
            while (skipBits.Contains(bitPos))
                bitPos++;

            led[i] = (int)DbcSignalCodec.ExtractSignalInt(data, bitPos, 1);
            bitPos++;
        }
    }

    private void ParseLedBrightness(byte[] data)
    {
        if (Model == "pkp3500mt")
        {
            for (var i = 0; i < NumDials; i++)
                for (var j = 0; j < 16; j++) 
                    Dials[i].RingLeds[j] = DbcSignalCodec.ExtractSignalInt(data, (i * 16) + j, 1) != 0;

            IndicatorBrightness = 63; //Indicator brightness not sent on this model
        }
        else
        {
            IndicatorBrightness =
                (int)DbcSignalCodec.ExtractSignalInt(data, startBit: 0, length: 8, factor: 1.58); //100% / 63 = 1.58
        }

        foreach (var button in Buttons)
            button.IndicatorBrightness = IndicatorBrightness;
    }

    private void ParseBacklight(byte[] data)
    {
        BacklightBrightness = DbcSignalCodec.ExtractSignalInt(data, startBit: 0, length: 1) > 0 ? 100 : 0;
    }

    private void ParseDialState(byte[] data, int index)
    {
        if (index >= NumDials) return;
        
        Dials[index].Ticks = (short)DbcSignalCodec.ExtractSignalInt(
            data,
            startBit: 0,
            length: 7);
        
        Dials[index].Direction = (DialDirection)DbcSignalCodec.ExtractSignalInt(
            data,
            startBit: 7,
            length: 1);
        
        Dials[index].Counter = (int)DbcSignalCodec.ExtractSignalInt(
            data,
            startBit: 8,
            length: 16);

        Dials[index].TopPosition = (int)DbcSignalCodec.ExtractSignalInt(
            data,
            startBit: 24,
            length: 8);
    }

    private void ParseAnalogInput(byte[] data)
    {
        // Each analog input is 16-bit unsigned value in millivolts
        var numInputsInMessage = Math.Min(4, NumAnalogInputs);

        for (var i = 0; i < numInputsInMessage && i < AnalogInputs.Count; i++)
        {
            var input = AnalogInputs[i];
            var startBit = i * 16;

            input.Voltage = DbcSignalCodec.ExtractSignal(
                data,
                startBit: startBit,
                length: 16);
        }
    }

    private CanFrame BuildButtonState()
    {
        var data = new byte[5];

        for (var i = 0; i < Buttons.Count; i++)
        {
            var button = Buttons[i];
            DbcSignalCodec.InsertBool(data, button.State, i);
        }

        data[4] = TickTimer;
        TickTimer++;

        return new CanFrame((int)MessageId.ButtonState + Ids.Base, 5, data);
    }

    private CanFrame BuildDialState(int index)
    {
        var data = new byte[8];
        
        var dial = Dials[index];
        
        DbcSignalCodec.InsertSignalInt(data,
            value: dial.Ticks,
            startBit: 0,
            length: 7);
        DbcSignalCodec.InsertBool(data, dial.Direction == DialDirection.CounterClockwise, startBit: 7);
        DbcSignalCodec.InsertSignalInt(data,
            value: dial.Counter,
            startBit: 8,
            length: 16);
        DbcSignalCodec.InsertSignalInt(data,
            value: dial.TopPosition,
            startBit: 24,
            length: 8);

        var id = (int)MessageId.DialState1;
        if (index == 1) id = (int)MessageId.DialState2;

        return new CanFrame(id, 8, data);
    }

    private CanFrame BuildAnalogInput()
    {
        var data = new byte[8];

        for (var i = 0; i < AnalogInputs.Count; i++)
        {
            var input = AnalogInputs[i];
            DbcSignalCodec.InsertSignal(data, input.Voltage, i * 16, 16, factor: 0.01);
        }

        return new CanFrame((int)MessageId.AnalogInput + Ids.Base, 8, data);
    }

    public IEnumerable<(int MessageId, DbcSignal Signal)> GetStatusSigs()
    {
        foreach (var kvp in StatusSigs)
        {
            // Create a copy with the ID populated
            var messageId = Ids.Base + kvp.Key;
            foreach (var (signal, _) in kvp.Value)
            {
                var signalCopy = new DbcSignal
                {
                    Name = signal.Name,
                    Id = messageId,
                    StartBit = signal.StartBit,
                    Length = signal.Length,
                    ByteOrder = signal.ByteOrder,
                    IsSigned = signal.IsSigned,
                    Factor = signal.Factor,
                    Offset = signal.Offset,
                    Unit = signal.Unit,
                    Min = signal.Min,
                    Max = signal.Max
                };
                yield return (messageId, signalCopy);
            }
        }
    }

    public List<CanFrame> GetCyclicMsgs()
    {
        if (!IsSim) return [];

        var msgs = new List<CanFrame>();

        msgs.Add(BuildButtonState());

        if (NumDials > 0)
        {
            msgs.Add(BuildDialState(0));
            msgs.Add(BuildDialState(1));
        }

        if (NumAnalogInputs > 0)
            msgs.Add(BuildAnalogInput());

        return msgs;
    }

    private static ButtonColor RgbToButtonColor(int red, int green, int blue)
    {
        return (ButtonColor)(
            (red > 0 ? 1 << 0 : 0) |
            (green > 0 ? 1 << 1 : 0) |
            (blue > 0 ? 1 << 2 : 0)
        );
    }
}