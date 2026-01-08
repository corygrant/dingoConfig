using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Devices.Canboard.Functions;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace domain.Devices.Canboard;

public class CanboardDevice : IDevice
{
    [JsonIgnore] protected ILogger<CanboardDevice> Logger = NullLogger<CanboardDevice>.Instance;

    [JsonIgnore] protected virtual int NumAnalogInputs { get; } = 5; //Also serve as rotary switches and analog/dig inputs
    [JsonIgnore] protected virtual int NumDigitalInputs { get; } = 8;
    [JsonIgnore] protected virtual int NumDigitalOutputs { get; } = 4;

    [JsonIgnore] public Guid Guid { get; }
    [JsonIgnore] public string Type => "CANBoard";
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }
    [JsonIgnore] private DateTime LastRxTime { get; set; }

    [JsonIgnore] public bool Configurable { get; }

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

    [JsonPropertyName("analogIn")] public List<AnalogInput> AnalogInputs { get; } = [];
    [JsonPropertyName("digitalIn")] public List<DigitalInput> DigitalInputs { get; } = [];
    [JsonPropertyName("digitalOut")] public List<DigitalOutput> DigitalOutputs { get; } = [];
    
    [JsonIgnore] public double BoardTempC { get; private set; }
    [JsonIgnore] public int Heartbeat { get; private set; }

    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// Name and BaseId will be set by the deserializer from JSON properties.
    /// Logger must be set via SetLogger() after deserialization.
    /// </summary>
    [JsonConstructor]
    public CanboardDevice()
    {
        Guid = Guid.NewGuid();
        Name = "";
        BaseId = 0;
        Configurable = false;

        InitializeCollections();
    }

    /// <summary>
    /// Constructor for programmatic device creation with dependency injection
    /// </summary>
    public CanboardDevice(ILogger<CanboardDevice> logger, string name, int baseId)
    {
        Logger = logger;
        Guid = Guid.NewGuid();
        Name = name;
        BaseId = baseId;

        Configurable = false;

        // ReSharper disable VirtualMemberCallInConstructor
        InitializeCollections();

        Logger.LogDebug("CANBoard {Name} created", Name);
    }

    /// <summary>
    /// Sets the logger instance (used after JSON deserialization)
    /// </summary>
    public void SetLogger(ILogger<CanboardDevice> logger)
    {
        Logger = logger;
    }

    protected virtual void InitializeCollections()
    {
        for (var i = 0; i < NumAnalogInputs; i++)
            AnalogInputs.Add(new AnalogInput(i + 1, "analogInput" + i));

        for (var i = 0; i < NumDigitalInputs; i++)
            DigitalInputs.Add(new DigitalInput(i + 1, "digitalInput" + i));

        for (var i = 0; i < NumDigitalOutputs; i++)
            DigitalOutputs.Add(new DigitalOutput(i + 1, "digitalOutput" + i));
    }

    public void UpdateIsConnected()
    {
        TimeSpan timeSpan = DateTime.Now - LastRxTime;
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
        if (id == BaseId + 0) ReadMessage0(data);
        if (id == BaseId + 1) ReadMessage1(data);
        if (id == BaseId + 2) ReadMessage2(data);

        LastRxTime = DateTime.Now;
    }

    private void ReadMessage0(byte[] data)
    {
        AnalogInputs[0].Millivolts = DbcSignalCodec.ExtractSignal(data, startBit: 0, length: 16);
        AnalogInputs[1].Millivolts = DbcSignalCodec.ExtractSignal(data, startBit: 16, length: 16);
        AnalogInputs[2].Millivolts = DbcSignalCodec.ExtractSignal(data, startBit: 32, length: 16);
        AnalogInputs[3].Millivolts = DbcSignalCodec.ExtractSignal(data, startBit: 48, length: 16);
    }

    private void ReadMessage1(byte[] data)
    {
        AnalogInputs[4].Millivolts = DbcSignalCodec.ExtractSignal(data, startBit: 0, length: 16);
        //Byte 2 empty
        //Byte 3 empty
        //Byte 4 empty
        //Byte 5 empty
        BoardTempC = DbcSignalCodec.ExtractSignal(data, startBit: 48, length: 16, factor: 0.01);
    }

    private void ReadMessage2(byte[] data)
    {
        // Rotary switch positions (4-bit fields)
        AnalogInputs[0].RotarySwitchPos = (short)DbcSignalCodec.ExtractSignalInt(data, startBit: 0, length: 4);
        AnalogInputs[1].RotarySwitchPos = (short)DbcSignalCodec.ExtractSignalInt(data, startBit: 4, length: 4);
        AnalogInputs[2].RotarySwitchPos = (short)DbcSignalCodec.ExtractSignalInt(data, startBit: 8, length: 4);
        AnalogInputs[3].RotarySwitchPos = (short)DbcSignalCodec.ExtractSignalInt(data, startBit: 12, length: 4);
        AnalogInputs[4].RotarySwitchPos = (short)DbcSignalCodec.ExtractSignalInt(data, startBit: 16, length: 4);

        // Digital inputs (1-bit fields in byte 4)
        DigitalInputs[0].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 32, length: 1) != 0;
        DigitalInputs[1].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 33, length: 1) != 0;
        DigitalInputs[2].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 34, length: 1) != 0;
        DigitalInputs[3].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 35, length: 1) != 0;
        DigitalInputs[4].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 36, length: 1) != 0;
        DigitalInputs[5].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 37, length: 1) != 0;
        DigitalInputs[6].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 38, length: 1) != 0;
        DigitalInputs[7].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 39, length: 1) != 0;

        // Analog inputs digital mode (1-bit fields in byte 5)
        AnalogInputs[0].DigitalIn = DbcSignalCodec.ExtractSignalInt(data, startBit: 40, length: 1) != 0;
        AnalogInputs[1].DigitalIn = DbcSignalCodec.ExtractSignalInt(data, startBit: 41, length: 1) != 0;
        AnalogInputs[2].DigitalIn = DbcSignalCodec.ExtractSignalInt(data, startBit: 42, length: 1) != 0;
        AnalogInputs[3].DigitalIn = DbcSignalCodec.ExtractSignalInt(data, startBit: 43, length: 1) != 0;
        AnalogInputs[4].DigitalIn = DbcSignalCodec.ExtractSignalInt(data, startBit: 44, length: 1) != 0;

        // Digital outputs (1-bit fields in byte 6)
        DigitalOutputs[0].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 48, length: 1) != 0;
        DigitalOutputs[1].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 49, length: 1) != 0;
        DigitalOutputs[2].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 50, length: 1) != 0;
        DigitalOutputs[3].State = DbcSignalCodec.ExtractSignalInt(data, startBit: 51, length: 1) != 0;

        // Heartbeat (8-bit field in byte 7)
        Heartbeat = (int)DbcSignalCodec.ExtractSignalInt(data, startBit: 56, length: 8);
    }

    public List<DeviceCanFrame> GetReadMsgs()
    {
        // TODO: Implement CANBoard upload messages
        return [];
    }

    public List<DeviceCanFrame> GetWriteMsgs()
    {
        // TODO: Implement CANBoard download messages
        return [];
    }

    public DeviceCanFrame GetBurnMsg()
    {
        // TODO: Implement CANBoard burn message
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetSleepMsg()
    {
        // TODO: Implement CANBoard sleep message
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetVersionMsg()
    {
        // TODO: Implement CANBoard version message
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetWakeupMsg()
    {
        // TODO: Implement CANBoard wakeup message
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetBootloaderMsg()
    {
        // TODO: Implement CANBoard bootloader message
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public List<DeviceCanFrame> GetModifyMsgs(int newId)
    {
        // TODO: Implement CANBoard update messages
        return [];
    }
}