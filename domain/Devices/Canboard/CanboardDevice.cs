using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Devices.Canboard.Functions;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace domain.Devices.Canboard;

public class CanboardDevice : IDevice
{
    [JsonIgnore] protected ILogger<CanboardDevice> Logger = null!;

    [JsonIgnore] protected virtual int NumAnalogInputs { get; } = 5; //Also serve as rotary switches and analog/dig inputs
    [JsonIgnore] protected virtual int NumDigitalInputs { get; } = 8;
    [JsonIgnore] protected virtual int NumDigitalOutputs { get; } = 4;

    [JsonIgnore] public Guid Guid { get; }
    [JsonIgnore] public string Type => "CANBoard";
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }
    [JsonIgnore] private DateTime LastRxTime { get; set; }

    [JsonIgnore]
    public bool Connected
    {
        get;
        private set
        {
            if (field && !value)
            {
                Clear();
            }

            field = value;
        }
    }

    [JsonPropertyName("analogIn")] public List<AnalogInput> AnalogInputs { get; init; } = [];
    [JsonPropertyName("digitalIn")] public List<DigitalInput> DigitalInputs { get; init; } = [];
    [JsonPropertyName("digitalOut")] public List<DigitalOutput> DigitalOutputs { get; init; } = [];

    [JsonIgnore] public double BoardTempC { get; private set; }
    [JsonIgnore] public int Heartbeat { get; private set; }

    [JsonIgnore] private Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>> StatusMessageSignals { get; set; } = null!;

    [JsonConstructor]
    public CanboardDevice(string name, int baseId)
    {
        Guid = Guid.NewGuid();
        Name = name;
        BaseId = baseId;

        // ReSharper disable VirtualMemberCallInConstructor
        InitializeCollections();
        InitializeStatusMessageSignals();
    }
    
    public void SetLogger(ILogger<CanboardDevice> logger)
    {
        Logger = logger;
    }

    protected virtual void InitializeCollections()
    {
        for (var i = 0; i < NumAnalogInputs; i++)
            AnalogInputs.Add(new AnalogInput(i + 1, "analogInput" + (i + 1)));

        for (var i = 0; i < NumDigitalInputs; i++)
            DigitalInputs.Add(new DigitalInput(i + 1, "digitalInput" + (i + 1)));

        for (var i = 0; i < NumDigitalOutputs; i++)
            DigitalOutputs.Add(new DigitalOutput(i + 1, "digitalOutput" + (i + 1)));
    }

    protected virtual void InitializeStatusMessageSignals()
    {
        StatusMessageSignals = new Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>>();

        // Message 0 (BaseId + 0): Analog inputs 0-3 millivolts
        StatusMessageSignals[0] = new List<(DbcSignal, Action<double>)>();
        for (var i = 0; i < 4 && i < NumAnalogInputs; i++)
        {
            var index = i;
            StatusMessageSignals[0].Add((
                new DbcSignal { Name = $"AnalogInput{index}Millivolts", StartBit = index * 16, Length = 16, Unit = "mV"},
                val => AnalogInputs[index].Millivolts = val
            ));
        }

        // Message 1 (BaseId + 1): Analog input 4 millivolts + board temperature
        StatusMessageSignals[1] =
        [
            (new DbcSignal { Name = "AnalogInput4Millivolts", StartBit = 0, Length = 16, Unit = "mV"},
                val => AnalogInputs[4].Millivolts = val),


            (new DbcSignal { Name = "BoardTempC", StartBit = 48, Length = 16, Factor = 0.01, Unit = "degC"},
                val => BoardTempC = val)
        ];

        // Message 2 (BaseId + 2): Complex message with rotary switches, digital I/O, heartbeat
        StatusMessageSignals[2] = [];

        // Rotary switch positions (4-bit each)
        for (var i = 0; i < NumAnalogInputs; i++)
        {
            var index = i;
            StatusMessageSignals[2].Add((
                new DbcSignal { Name = $"RotarySwitch{index}Pos", StartBit = index * 4, Length = 4 },
                val => AnalogInputs[index].RotarySwitchPos = (short)val
            ));
        }

        // Digital inputs (1-bit each starting at bit 32)
        for (var i = 0; i < NumDigitalInputs; i++)
        {
            var index = i;
            StatusMessageSignals[2].Add((
                new DbcSignal { Name = $"DigitalInput{index}State", StartBit = 32 + index, Length = 1 },
                val => DigitalInputs[index].State = val != 0
            ));
        }

        // Analog inputs digital mode (1-bit each starting at bit 40)
        for (var i = 0; i < NumAnalogInputs; i++)
        {
            var index = i;
            StatusMessageSignals[2].Add((
                new DbcSignal { Name = $"AnalogInput{index}DigitalMode", StartBit = 40 + index, Length = 1 },
                val => AnalogInputs[index].DigitalIn = val != 0
            ));
        }

        // Digital outputs (1-bit each starting at bit 48)
        for (var i = 0; i < NumDigitalOutputs; i++)
        {
            var index = i;
            StatusMessageSignals[2].Add((
                new DbcSignal { Name = $"DigitalOutput{index}State", StartBit = 48 + index, Length = 1 },
                val => DigitalOutputs[index].State = val != 0
            ));
        }

        // Heartbeat (8-bit at bit 56)
        StatusMessageSignals[2].Add((
            new DbcSignal { Name = "Heartbeat", StartBit = 56, Length = 8 },
            val => Heartbeat = (int)val
        ));
    }

    public void UpdateIsConnected()
    {
        var timeSpan = DateTime.Now - LastRxTime;
        Connected = timeSpan.TotalMilliseconds < 500;
    }

    private void Clear()
    {
        foreach (var input in AnalogInputs)
        {
            input.DigitalIn = false;
            input.Millivolts = 0.0;
        }

        foreach (var input in DigitalInputs)
            input.State = false;

        foreach (var output in DigitalOutputs)
            output.State = false;

        Logger.LogDebug("CANBoard {Name} cleared", Name);
    }

    public bool InIdRange(int id)
    {
        return (id >= BaseId) && (id <= BaseId + 2);
    }

    public void Read(int id, byte[] data,
        ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> queue)
    {
        var offset = id - BaseId;
        if (StatusMessageSignals.TryGetValue(offset, out var signals))
        {
            foreach (var (signal, setValue) in signals)
            {
                var value = DbcSignalCodec.ExtractSignal(data, signal);
                setValue(value);
            }
        }

        LastRxTime = DateTime.Now;
    }

    public IEnumerable<(int MessageId, DbcSignal Signal)> GetStatusSignals()
    {
        foreach (var kvp in StatusMessageSignals)
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
}