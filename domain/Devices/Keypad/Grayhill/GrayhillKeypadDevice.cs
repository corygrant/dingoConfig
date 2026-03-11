using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Devices.Keypad.Grayhill.Enums;
using domain.Devices.Keypad.Grayhill.Functions;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace domain.Devices.Keypad.Grayhill;

public class GrayhillKeypadDevice : IKeypadDevice
{
    [JsonIgnore] private ILogger<GrayhillKeypadDevice>? _logger;

    [JsonIgnore] public Guid Guid { get; }
    [JsonIgnore] public string Type => "GrayhillKeypad";
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }
    [JsonPropertyName("cyclicGap")] public TimeSpan CyclicGap { get; } = TimeSpan.FromSeconds(1);
    [JsonPropertyName("cyclicPause")] public TimeSpan CyclicPause { get; } = TimeSpan.FromMilliseconds(1);
    [JsonPropertyName("isSim")] public bool IsSim { get; set; }
    [JsonIgnore] private DateTime _lastRxTime = DateTime.Now;

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
    [JsonIgnore] public int BacklightBrightness { get; set; }
    [JsonIgnore] public int IndicatorBrightness { get; set; }

    [JsonPropertyName("buttons")] public List<Button> Buttons { get; init; } = [];

    [JsonIgnore] public Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>> StatusSigs { get; set; } = null!;

    [JsonConstructor]
    public GrayhillKeypadDevice(string name, int baseId, string model)
    {
        Name = name;
        BaseId = baseId;
        Model = model;
        NumButtons = GrayhillModels.Lookup(model);
        Guid = Guid.NewGuid();
        InitCollections();
        InitStatusSigs();
    }

    public void SetLogger(ILogger<GrayhillKeypadDevice> logger)
    {
        _logger = logger;
    }

    private void InitCollections()
    {
        for (var i = 0; i < NumButtons; i++)
            Buttons.Add(new Button(i + 1, $"button{i + 1}"));
    }

    private void InitStatusSigs()
    {
        StatusSigs = new Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>>();

        // Button States
        StatusSigs[0] = [];
        for (var i = 0; i < NumButtons; i++)
        {
            var button = Buttons[i]; // No cast needed - strongly typed!
            StatusSigs[0].Add((
                new DbcSignal { Name = $"Button{i + 1}.State", StartBit = i, Length = 1},
                val => button.State = val != 0
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

        _logger?.LogInformation("{Name} Grayhill Keypad cleared", Name);
    }

    public bool InIdRange(int id)
    {
        // CANopen uses BaseId as node ID (1-127)
        // Message IDs: 0x180 + nodeId, 0x200 + nodeId, etc.
        return id == ((int)MessageId.ButtonState + BaseId) ||
               id == ((int)MessageId.LedControl + BaseId);
    }

    public void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue, List<DeviceCanFrame> outgoing)
    {
        switch ((MessageId)id - BaseId)
        {
            case MessageId.ButtonState:
                //Only read button state when not sim
                //Sim maintains its own button state
                if(!IsSim)
                    ParseButtonState(data);
                break;
            case MessageId.LedControl:
                ParseLedControl(data);
                break;
            case MessageId.BrightnessControl:
                ParseBrightnessControl(data);
                break;
            default:
                //Don't update LastRxTime - invalid message
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

    private void ParseLedControl(byte[] data)
    {
        // LEDs are packed as 3 bits per button
        for (var i = 0; i < NumButtons && i < Buttons.Count; i++)
        {
            var button = Buttons[i];
            
            for (var j = 0; j < Buttons[i].Led.Length; j++)
            {
                var bitPos = (i * Buttons[i].Led.Length) + j;
                var byteIndex = bitPos / 8;
                var bitIndex = bitPos % 8;
                
                if (byteIndex < data.Length)
                    button.Led[j] = (data[byteIndex] & (1 << bitIndex)) != 0;
                else
                    button.Led[j] = false;
            }
        }
    }

    private void ParseBrightnessControl(byte[] data)
    {
        if (data.Length < 3) return;

        // Brightness is scaled 0% (0), 50% (128), 100% (255)
        IndicatorBrightness = (int)DbcSignalCodec.ExtractSignalInt(data, 0, 16, factor: 0.392);
        BacklightBrightness = (int)DbcSignalCodec.ExtractSignalInt(data, 16, 16, factor: 0.392);
    }

    private CanFrame BuildButtonState()
    {
        var data = new byte[3];

        for (var i = 0; i < Buttons.Count; i++)
        {
            var button = Buttons[i];
            DbcSignalCodec.InsertBool(data, button.State, i);
        }
    
        return new CanFrame((int)MessageId.ButtonState + BaseId, 3, data);
    }

    public IEnumerable<(int MessageId, DbcSignal Signal)> GetStatusSigs()
    {
        foreach (var kvp in StatusSigs)
        {
            var messageId = BaseId + kvp.Key;
            foreach (var (signal, _) in kvp.Value)
            {
                // Create a copy with the ID populated
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
        
        //Transmit button states
        return
        [
            BuildButtonState(),
        ];
    }
}