using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Devices.dingoPdm.Enums;
using domain.Devices.dingoPdm.Functions;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static domain.Common.DbcSignalCodec;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable VirtualMemberCallInConstructor

namespace domain.Devices.dingoPdm;

public class PdmDevice : IDeviceConfigurable
{
    [JsonIgnore] protected ILogger<PdmDevice> Logger = null!;

    [JsonIgnore] protected virtual int MinMajorVersion => 0;
    [JsonIgnore] protected virtual int MinMinorVersion => 4;
    [JsonIgnore] protected virtual int MinBuildVersion => 27;

    [JsonIgnore] protected virtual int NumDigitalInputs => 2;
    [JsonIgnore] protected virtual int NumOutputs => 8;
    [JsonIgnore] protected virtual int NumCanInputs => 32;
    [JsonIgnore] protected virtual int NumVirtualInputs => 16;
    [JsonIgnore] protected virtual int NumFlashers => 4;
    [JsonIgnore] protected virtual int NumCounters => 4;
    [JsonIgnore] protected virtual int NumConditions => 32;

    [JsonIgnore] protected virtual int PdmType => 0; //0=dingoPDM, 1=dingoPDM-Max
    [JsonIgnore] protected bool PdmTypeOk;
    
    [JsonIgnore] public Guid Guid { get; }
    [JsonIgnore] public virtual string Type => "dingoPDM";
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }

    
    [JsonIgnore][Plotable(displayName:"DevState")] public DeviceState DeviceState { get; private set; }
    [JsonIgnore][Plotable(displayName:"TotalCurrent", unit:"A")] public double TotalCurrent { get; private set; }
    [JsonIgnore][Plotable(displayName:"BatteryVoltage", unit:"V")] public double BatteryVoltage { get; private set; }
    [JsonIgnore][Plotable(displayName:"Temperature", unit:"degC")] public double BoardTempC { get; private set; }
    [JsonIgnore] public string Version { get; private set; } = "v0.0.0";
    
    [JsonIgnore] public bool SleepEnabled { get; set; }
    [JsonIgnore] public bool CanFiltersEnabled { get; set; }
    [JsonIgnore] public CanBitRate BitRate { get; set; }
    
    [JsonPropertyName("inputs")] public List<Input> Inputs { get; init; } = [];
    [JsonPropertyName("outputs")] public List<Output> Outputs { get; init; } = [];
    [JsonPropertyName("canInputs")] public List<CanInput> CanInputs { get; init; } = [];
    [JsonPropertyName("virtualInputs")] public List<VirtualInput> VirtualInputs { get; init; } = [];
    [JsonPropertyName("wipers")] public Wiper Wipers { get; protected set; } = null!;
    [JsonPropertyName("flashers")] public List<Flasher> Flashers { get; init; } = [];
    [JsonPropertyName("starterDisable")] public StarterDisable StarterDisable { get; protected set; } = null!;
    [JsonPropertyName("counters")] public List<Counter> Counters { get; init; } = [];
    [JsonPropertyName("conditions")] public List<Condition> Conditions { get; init; } = [];
    
    [JsonIgnore] private DateTime LastRxTime { get; set; }

    [JsonIgnore] private Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>> StatusMessageSignals { get; set; } = null!;

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
    
    [JsonConstructor]
    public PdmDevice(string name, int baseId)
    {
        Name = name;
        BaseId = baseId;
        Guid = Guid.NewGuid();

        InitializeCollections();
    }

    public void SetLogger(ILogger<PdmDevice> logger)
    {
        Logger = logger;
    }

    protected virtual void InitializeCollections()
    {
        for (var i = 0; i < NumDigitalInputs; i++)
            Inputs.Add(new Input(i + 1, "digitalInput" + (i + 1)));

        for (var i = 0; i < NumOutputs; i++)
            Outputs.Add(new Output(i + 1, "output" + (i + 1)));

        for (var i = 0; i < NumCanInputs; i++)
            CanInputs.Add(new CanInput(i + 1, "canInput" + (i + 1)));

        for (var i = 0; i < NumVirtualInputs; i++)
            VirtualInputs.Add(new VirtualInput(i + 1, "virtualInput" + (i + 1)));

        for (var i = 0; i < NumFlashers; i++)
            Flashers.Add(new Flasher(i + 1,  "flasher" + (i + 1)));

        for (var i = 0; i < NumCounters; i++)
            Counters.Add(new Counter(i  + 1, "counter" + (i + 1)));

        for (var i = 0; i < NumConditions; i++)
            Conditions.Add(new Condition(i + 1, "condition" + (i + 1)));
        
        StarterDisable = new StarterDisable("starterDisable", NumOutputs);

        Wipers = new Wiper("wiper");

        InitializeStatusMessageSignals();
    }

    protected virtual void InitializeStatusMessageSignals()
    {
        StatusMessageSignals = new Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>>();

        // Message 0: System status
        StatusMessageSignals[0] = new List<(DbcSignal, Action<double>)>();
        for (var i = 0; i < NumDigitalInputs; i++)
        {
            var inputIndex = i;
            StatusMessageSignals[0].Add((
                new DbcSignal { Name = $"Input{inputIndex}State", StartBit = i, Length = 1 },
                val => Inputs[inputIndex].State = val != 0
            ));
        }
        StatusMessageSignals[0].AddRange(new List<(DbcSignal, Action<double>)>
        {
            (new DbcSignal { Name = "DeviceState", StartBit = 8, Length = 4 },
                val => DeviceState = (DeviceState)val),
            (new DbcSignal { Name = "PdmType", StartBit = 12, Length = 4 },
                val => PdmTypeOk = PdmType == (int)val),
            (new DbcSignal { Name = "TotalCurrent", StartBit = 16, Length = 16, Factor = 0.1, Unit = "A" },
                val => TotalCurrent = val),
            (new DbcSignal { Name = "BatteryVoltage", StartBit = 32, Length = 16, Factor = 0.1, Unit = "V" },
                val => BatteryVoltage = val),
            (new DbcSignal { Name = "BoardTemp", StartBit = 48, Length = 16, Factor = 0.1, Unit = "°C" },
                val => BoardTempC = Math.Round(val, 1))
        });

        // Message 1: Output currents 0-3
        StatusMessageSignals[1] = [];
        for (var i = 0; i < 4 && i < NumOutputs; i++)
        {
            var outputIndex = i;
            StatusMessageSignals[1].Add((
                new DbcSignal { Name = $"Output{outputIndex}Current", StartBit = i * 16, Length = 16, Factor = 0.1, Unit = "A" },
                val => Outputs[outputIndex].Current = val
            ));
        }

        // Message 2: Output currents 4-7
        StatusMessageSignals[2] = [];
        for (var i = 4; i < NumOutputs; i++)
        {
            var outputIndex = i;
            StatusMessageSignals[2].Add((
                new DbcSignal { Name = $"Output{outputIndex}Current", StartBit = (i - 4) * 16, Length = 16, Factor = 0.1, Unit = "A" },
                val => Outputs[outputIndex].Current = val
            ));
        }

        // Message 3: Output states, wiper, flashers
        StatusMessageSignals[3] = [];
        for (var i = 0; i < NumOutputs; i++)
        {
            var outputIndex = i;
            StatusMessageSignals[3].Add((
                new DbcSignal { Name = $"Output{outputIndex}State", StartBit = i * 4, Length = 4 },
                val => Outputs[outputIndex].State = (OutState)val
            ));
        }
        StatusMessageSignals[3].AddRange(new List<(DbcSignal, Action<double>)>
        {
            (new DbcSignal { Name = "WiperSlowState", StartBit = 32, Length = 1 },
                val => Wipers.SlowState = val != 0),
            (new DbcSignal { Name = "WiperFastState", StartBit = 33, Length = 1 },
                val => Wipers.FastState = val != 0),
            (new DbcSignal { Name = "WiperSpeed", StartBit = 40, Length = 4 },
                val => Wipers.Speed = (WiperSpeed)val),
            (new DbcSignal { Name = "WiperState", StartBit = 44, Length = 4 },
                val => Wipers.State = (WiperState)val)
        });
        for (var i = 0; i < NumFlashers; i++)
        {
            var flasherIndex = i;
            StatusMessageSignals[3].Add((
                new DbcSignal { Name = $"Flasher{flasherIndex}", StartBit = 48 + i, Length = 1 },
                val => Flashers[flasherIndex].Value = val != 0 && Flashers[flasherIndex].Enabled
            ));
        }

        // Message 4: Output reset counts
        StatusMessageSignals[4] = [];
        for (var i = 0; i < NumOutputs; i++)
        {
            var outputIndex = i;
            StatusMessageSignals[4].Add((
                new DbcSignal { Name = $"Output{outputIndex}ResetCount", StartBit = i * 8, Length = 8 },
                val => Outputs[outputIndex].ResetCount = (int)val
            ));
        }

        // Message 5: CAN inputs & virtual inputs
        StatusMessageSignals[5] = [];
        for (var i = 0; i < NumCanInputs; i++)
        {
            var canInputIndex = i;
            StatusMessageSignals[5].Add((
                new DbcSignal { Name = $"CanInput{canInputIndex}", StartBit = i, Length = 1 },
                val => CanInputs[canInputIndex].Output = val != 0
            ));
        }
        for (var i = 0; i < NumVirtualInputs; i++)
        {
            var virtualInputIndex = i;
            StatusMessageSignals[5].Add((
                new DbcSignal { Name = $"VirtualInput{virtualInputIndex}", StartBit = 32 + i, Length = 1 },
                val => VirtualInputs[virtualInputIndex].Value = val != 0
            ));
        }

        // Message 6: Counters & conditions
        StatusMessageSignals[6] = [];
        for (var i = 0; i < NumCounters; i++)
        {
            var counterIndex = i;
            StatusMessageSignals[6].Add((
                new DbcSignal { Name = $"Counter{counterIndex}", StartBit = i * 8, Length = 8 },
                val => Counters[counterIndex].Value = (int)val
            ));
        }
        for (var i = 0; i < NumConditions; i++)
        {
            var conditionIndex = i;
            StatusMessageSignals[6].Add((
                new DbcSignal { Name = $"Condition{conditionIndex}", StartBit = 32 + i, Length = 1 },
                val => Conditions[conditionIndex].Value = (int)val
            ));
        }

        // Messages 7-14: CAN input values (4 per message)
        for (var msg = 7; msg <= 14; msg++)
        {
            StatusMessageSignals[msg] = [];
            for (var i = 0; i < 4; i++)
            {
                var canInputIndex = (msg - 7) * 4 + i;
                if (canInputIndex < NumCanInputs)
                {
                    StatusMessageSignals[msg].Add((
                        new DbcSignal { Name = $"CanInput{canInputIndex}Value", StartBit = i * 16, Length = 16 },
                        val => CanInputs[canInputIndex].Value = (ushort)val
                    ));
                }
            }
        }

        // Message 15: Output duty cycles
        StatusMessageSignals[15] = [];
        for (var i = 0; i < NumOutputs; i++)
        {
            var outputIndex = i;
            StatusMessageSignals[15].Add((
                new DbcSignal { Name = $"Output{outputIndex}DutyCycle", StartBit = i * 8, Length = 8, Unit = "%" },
                val => Outputs[outputIndex].CurrentDutyCycle = val
            ));
        }
    }

    private void Clear()
    {
        foreach(var input in Inputs)
            input.State = false;

        foreach(var output in Outputs)
        {
            output.Current = 0;
            output.State = OutState.Off;
        }

        foreach(var input in VirtualInputs)
            input.Value = false;

        foreach(var canInput in CanInputs)
            canInput.Output = false;
        
        Logger.LogDebug("PDM {Name} cleared", Name);
    }

    public void UpdateIsConnected()
    {
        var timeSpan = DateTime.Now - LastRxTime;
        Connected = timeSpan.TotalMilliseconds < 500;
    }
    
    public bool InIdRange(int id)
    {
        return (id >= BaseId - 1) && (id <= BaseId + 31);
    }
    
    public void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> queue)
    {
        var offset = id - BaseId;

        // Use dictionary lookup for status messages 0-15
        if (StatusMessageSignals.TryGetValue(offset, out var signals))
        {
            foreach (var (signal, setValue) in signals)
            {
                var value = ExtractSignal(data, signal);
                setValue(value);
            }
        }
        // Handle special messages with custom logic
        else
        {
            switch (offset)
            {
                case 30: ReadSettingsResponse(data, queue); break;
                case 31: ReadInfoWarnErrorMessage(data); break;
            }
        }

        LastRxTime = DateTime.Now;
    }

    public IEnumerable<(int MessageId, DbcSignal Signal)> GetStatusSignals()
    {
        foreach (var kvp in StatusMessageSignals)
        {
            int messageId = BaseId + kvp.Key;
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

    protected void ReadSettingsResponse(byte[] data, ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> queue)
    {
        //Response is prefix + 128
        if (data[0] < 128)
            return;
            
        var prefix = (MessagePrefix)(data[0] - 128);

        int index;

        //Vars used below
        (int BaseId, int, int) key;
        DeviceCanFrame canFrame;

        switch (prefix)
        {
            case MessagePrefix.Version:
                Version = $"v{data[1]}.{data[2]}.{(data[3] << 8) + (data[4])}";

                key = (BaseId, (int)MessagePrefix.Version, 0);
                if (queue.TryGetValue(key, out canFrame!))
                {
                    canFrame.TimeSentTimer?.Dispose();
                    queue.TryRemove(key, out _);
                }
                
                Logger.LogInformation("{Name} FW version received: {Version}", Name, Version);
                
                if (!CheckVersion(data[1], data[2], (data[3] << 8) + (data[4])))
                {
                    Logger.LogError("{Name} ID: {BaseId}, Firmware needs to be updated. V{MinMajorVersion}.{MinMinorVersion}.{MinBuildVersion} or greater", Name, BaseId, MinMajorVersion, MinMinorVersion, MinBuildVersion);
                }

                break;

            case MessagePrefix.Can:
                SleepEnabled = Convert.ToBoolean(data[1] & 0x01);
                CanFiltersEnabled = Convert.ToBoolean((data[1] >> 1) & 0x01);
                BaseId = (data[2] << 8) + data[3];
                BitRate = (CanBitRate)((data[1] & 0xF0)>>4);

                key = (BaseId, (int)MessagePrefix.Can, 0);
                if (queue.TryGetValue(key, out canFrame!))
                {
                    canFrame.TimeSentTimer?.Dispose();
                    queue.TryRemove(key, out _);
                }

                break;

            case MessagePrefix.Inputs:
                index = Input.ExtractIndex(data[1], prefix);
                
                if (index >= 0 && index < NumDigitalInputs)
                {
                    if (Inputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.Inputs, index);
                        if (queue.TryGetValue(key, out canFrame!))
                        {
                            canFrame.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.Outputs:
                index = Output.ExtractIndex(data[1], prefix);
                
                if (index >= 0 && index < NumOutputs)
                {
                    if (Outputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.Outputs, index);
                        if (queue.TryGetValue(key, out canFrame!))
                        {
                            canFrame.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.OutputsPwm:
                index = Output.ExtractIndex(data[1], prefix);

                if (index >= 0 && index < NumOutputs)
                {
                    if (Outputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.OutputsPwm, index);
                        if (queue.TryGetValue(key, out canFrame!))
                        {
                            canFrame.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.VirtualInputs:
                index = VirtualInput.ExtractIndex(data[2], prefix);
                
                if (index >= 0 && index < NumVirtualInputs)
                {
                    if (VirtualInputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.VirtualInputs, index);
                        if (queue.TryGetValue(key, out canFrame!))
                        {
                            canFrame.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.Flashers:
                index = Flasher.ExtractIndex(data[1], prefix);
                
                if (index >= 0 && index < NumFlashers)
                {
                    if (Flashers[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.Flashers, index);
                        if (queue.TryGetValue(key, out canFrame!))
                        {
                            canFrame.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.Wiper:
                if (Wipers.Receive(data, prefix))
                {
                    key = (BaseId, (int)MessagePrefix.Wiper, 0);
                    if (queue.TryGetValue(key, out canFrame!))
                    {
                        canFrame.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                break;

            case MessagePrefix.WiperSpeed:
                if (Wipers.Receive(data, prefix))
                {
                    key = (BaseId, (int)MessagePrefix.WiperSpeed, 0);
                    if (queue.TryGetValue(key, out canFrame!))
                    {
                        canFrame.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                break;

            case MessagePrefix.WiperDelays:
                if (Wipers.Receive(data, prefix))
                {
                    key = (BaseId, (int)MessagePrefix.WiperDelays, 0);
                    if (queue.TryGetValue(key, out canFrame!))
                    {
                        canFrame.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                break;

            case MessagePrefix.StarterDisable:
                if (StarterDisable.Receive(data, prefix))
                {
                    key = (BaseId, (int)MessagePrefix.StarterDisable, 0);
                    if (queue.TryGetValue(key, out canFrame!))
                    {
                        canFrame.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                break;

            case MessagePrefix.CanInputs:
                index = CanInput.ExtractIndex(data[1], prefix);
                if (index >= 0 && index < NumCanInputs)
                {
                    if(CanInputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.CanInputs, index);
                        if (queue.TryGetValue(key, out canFrame!))
                        {
                            canFrame.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.CanInputsId:
                index = CanInput.ExtractIndex(data[1], prefix);
                if (index >= 0 && index < NumCanInputs)
                {
                    if (CanInputs[index].Receive(data, prefix))
                    {
                        key = (BaseId, (int)MessagePrefix.CanInputsId, index);
                        if (queue.TryGetValue(key, out canFrame!))
                        {
                            canFrame.TimeSentTimer?.Dispose();
                            queue.TryRemove(key, out _);
                        }
                    }
                }

                break;

            case MessagePrefix.Counter:
				index = Counter.ExtractIndex(data[1], prefix);
				if (index >= 0 && index < NumCounters)
				{
					if (Counters[index].Receive(data, prefix))
					{
						key = (BaseId, (int)MessagePrefix.Counter, index);
						if (queue.TryGetValue(key, out canFrame!))
						{
							canFrame.TimeSentTimer?.Dispose();
							queue.TryRemove(key, out _);
						}
					}
				}

				break;

			case MessagePrefix.Conditions:
				index = Condition.ExtractIndex(data[1], prefix);
				if (index >= 0 && index < NumConditions)
				{
					if (Conditions[index].Receive(data, prefix))
					{
						key = (BaseId, (int)MessagePrefix.Conditions, index);
						if (queue.TryGetValue(key, out canFrame!))
						{
							canFrame.TimeSentTimer?.Dispose();
							queue.TryRemove(key, out _);
						}
					}
				}

				break;

			case MessagePrefix.BurnSettings:
                if (data[1] == 1) //Successful burn
                {
                    Logger.LogInformation("{Name} ID: {BaseId}, Burn Successful", Name, BaseId);

                    key = (BaseId, (int)MessagePrefix.BurnSettings, 0);
                    if (queue.TryGetValue(key, out canFrame!))
                    {
                        canFrame.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                if (data[1] == 0) //Unsuccessful burn
                    Logger.LogError("{Name} ID: {BaseId}, Burn Failed", Name, BaseId);
                
                break;

            case MessagePrefix.Sleep:
                if (data[1] == 1) //Successful sleep
                {
                    Logger.LogInformation("{Name} ID: {BaseId}, Sleep Successful", Name, BaseId);

                    key = (BaseId, (int)MessagePrefix.Sleep, 0);
                    if (queue.TryGetValue(key, out canFrame!))
                    {
                        canFrame.TimeSentTimer?.Dispose();
                        queue.TryRemove(key, out _);
                    }
                }

                if (data[1] == 0) //Unsuccessful sleep
                    Logger.LogError("{Name} ID: {BaseId}, Sleep Failed", Name, BaseId);
                
                break;
        }
    }

    protected void ReadInfoWarnErrorMessage(byte[] data)
    {
        //Response is lowercase version of set/get prefix
        var type = (MessageType)char.ToUpper(Convert.ToChar(data[0]));
        var src = (MessageSrc)data[1];

        switch (type)
        {
            case MessageType.Info:
                Logger.LogInformation("{Name} ID: {BaseId}, Src: {MessageSrc} {I} {I1} {I2}", Name, BaseId, src, (data[3] << 8) + data[2], (data[5] << 8) + data[4], (data[7] << 8) + data[6]);
                break;
            case MessageType.Warning:
                Logger.LogWarning("{Name} ID: {BaseId}, Src: {MessageSrc} {I} {I1} {I2}", Name, BaseId, src, (data[3] << 8) + data[2], (data[5] << 8) + data[4], (data[7] << 8) + data[6]);
                break;
            case MessageType.Error:
                Logger.LogError("{Name} ID: {BaseId}, Src: {MessageSrc} {I} {I1} {I2}", Name, BaseId, src, (data[3] << 8) + data[2], (data[5] << 8) + data[4], (data[7] << 8) + data[6]);
                break;
        }
    }

    public List<DeviceCanFrame> GetReadMsgs()
    {
        var id = BaseId;

        var msgs = new List<DeviceCanFrame>
        {
            //Request settings messages
            //Version
            new DeviceCanFrame
            {
                DeviceBaseId = BaseId,
                Sent = false,
                Received = false,
                Prefix = (int)MessagePrefix.Version,
                Index = 0,
                Frame = new CanFrame(Id: id - 1, Len: 1, Payload: [Convert.ToByte(MessagePrefix.Version), 0, 0, 0, 0, 0, 0, 0]),
                MsgDescription="Version"
            },
            //CAN settings
            new DeviceCanFrame
            {
                DeviceBaseId = BaseId,
                Sent = false,
                Received = false,
                Prefix = (int)MessagePrefix.Can,
                Index = 0,
                Frame = new CanFrame(Id: id - 1, Len: 1, Payload: [Convert.ToByte(MessagePrefix.Can), 0, 0, 0, 0, 0, 0, 0]),
                MsgDescription="CANSettings"
            }
        };

        //Inputs
        foreach (var function in Inputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.Inputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Outputs
        foreach (var function in Outputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.Outputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Outputs PWM
        foreach (var function in Outputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.OutputsPwm);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Virtual inputs
        foreach (var function in Inputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.VirtualInputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Flashers
        foreach (var function in Flashers)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.Flashers);
            if ( msg != null)
                msgs.Add(msg);
        }

        //CAN inputs
        foreach (var function in CanInputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.CanInputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //CAN inputs ID
        foreach (var function in CanInputs)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.CanInputsId);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Wiper
        var wiperMsg = Wipers.CreateUploadRequest(id, MessagePrefix.Wiper);
        if ( wiperMsg != null)
            msgs.Add(wiperMsg);

        //Wiper speeds
        var wiperSpeedMsg = Wipers.CreateUploadRequest(id, MessagePrefix.WiperSpeed);
        if ( wiperSpeedMsg != null)
            msgs.Add(wiperSpeedMsg);

        //Wiper delays
        var wiperDelayMsg = Wipers.CreateUploadRequest(id, MessagePrefix.WiperDelays);
        if ( wiperDelayMsg != null)
            msgs.Add(wiperDelayMsg);

        //Starter disable
        var starterDisableMsg = StarterDisable.CreateUploadRequest(id, MessagePrefix.StarterDisable);
        if ( starterDisableMsg != null)
            msgs.Add(starterDisableMsg);

		//Counter
        foreach (var function in Counters)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.Counter);
            if ( msg != null)
                msgs.Add(msg);
        }

		//Condition
        foreach (var function in Conditions)
        {
            var msg = function.CreateUploadRequest(id, MessagePrefix.Conditions);
            if ( msg != null)
                msgs.Add(msg);
        }

		return msgs;
    }

    public List<DeviceCanFrame> GetWriteMsgs()
    {
        var id = BaseId;

        List<DeviceCanFrame> msgs =
        [
            new DeviceCanFrame
            {
                DeviceBaseId = BaseId,
                Sent = false,
                Received = false,
                Prefix = (int)MessagePrefix.Can,
                Index = 0,
                Frame = new CanFrame
                (
                    Id: id - 1,
                    Len: 4,
                    Payload: [
                        Convert.ToByte(MessagePrefix.Can), //Byte 0
                        Convert.ToByte(Convert.ToByte(SleepEnabled) +
                                       (Convert.ToByte(CanFiltersEnabled) << 1) +
                                       ((Convert.ToByte(BitRate) & 0x0F) << 4)),
                        Convert.ToByte((BaseId & 0xFF00) >> 8), //Byte 2
                        Convert.ToByte(BaseId & 0x00FF), //Byte 3
                        0, 0, 0, 0
                    ]
                ),
                MsgDescription = "CANSettings"
            }
        ];

        //Inputs
        foreach(var function in Inputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.Inputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Outputs
        foreach(var function in Outputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.Outputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Outputs PWM
        foreach(var function in Outputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.OutputsPwm);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Virtual inputs
        foreach(var function in VirtualInputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.VirtualInputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Flashers
        foreach(var function in Flashers)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.Flashers);
            if ( msg != null)
                msgs.Add(msg);
        }

        //CAN inputs
        foreach(var function in CanInputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.CanInputs);
            if ( msg != null)
                msgs.Add(msg);
        }

        //CAN inputs ID
        foreach(var function in CanInputs)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.CanInputsId);
            if ( msg != null)
                msgs.Add(msg);
        }

        //Wiper
        var wiperMsg = Wipers.CreateDownloadRequest(id, MessagePrefix.Wiper);
        if ( wiperMsg != null)
            msgs.Add(wiperMsg);

        //Wiper speeds
        var wiperSpeedMsg = Wipers.CreateDownloadRequest(id, MessagePrefix.WiperSpeed);
        if ( wiperSpeedMsg != null)
            msgs.Add(wiperSpeedMsg);

        //Wiper delays
        var wiperDelayMsg = Wipers.CreateDownloadRequest(id, MessagePrefix.WiperDelays);
        if ( wiperDelayMsg != null)
            msgs.Add(wiperDelayMsg);

        //Starter disable
        var starterDisableMsg = StarterDisable.CreateDownloadRequest(id, MessagePrefix.StarterDisable);
        if ( starterDisableMsg != null)
            msgs.Add(starterDisableMsg);

		//Counter
        foreach(var function in Counters)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.Counter);
            if ( msg != null)
                msgs.Add(msg);
        }

        foreach(var function in Conditions)
        {
            var msg = function.CreateDownloadRequest(id, MessagePrefix.Conditions);
            if ( msg != null)
                msgs.Add(msg);
        }

		return msgs;
    }

    public List<DeviceCanFrame> GetModifyMsgs(int newId)
    {
        List<DeviceCanFrame> msgs =
        [
            new DeviceCanFrame
            {
                DeviceBaseId = newId, //Set msg ID to new ID so response is processed properly
                Sent = false,
                Received = false,
                Prefix = (int)MessagePrefix.Can,
                Index = 0,
                Frame = new CanFrame
                (
                    Id: BaseId - 1,
                    Len: 4,
                    Payload: [
                        Convert.ToByte(MessagePrefix.Can), //Byte 0
                        Convert.ToByte(Convert.ToByte(SleepEnabled) +
                                       (Convert.ToByte(CanFiltersEnabled) << 1) +
                                       ((Convert.ToByte(BitRate) & 0x0F) << 4)),
                        Convert.ToByte((newId & 0xFF00) >> 8), //Byte 2
                        Convert.ToByte(newId & 0x00FF), //Byte 3
                        0, 0, 0, 0
                    ]
                ),
                MsgDescription = "CANSettings"
            }

        ];

        return msgs;
    }

    public DeviceCanFrame GetBurnMsg()
    {
        return new DeviceCanFrame
        {
            DeviceBaseId = BaseId,
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.BurnSettings,
            Index = 0,
            Frame = new CanFrame
            (
                Id: BaseId - 1,
                Len: 4,
                Payload: [Convert.ToByte(MessagePrefix.BurnSettings), 1, 3, 8, 0, 0, 0, 0]
            ),
            MsgDescription = "Burn Settings"
        };
    }

    public DeviceCanFrame GetSleepMsg()
    {
        return new DeviceCanFrame
        {
            DeviceBaseId = BaseId,
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Sleep,
            Index = 0,
            Frame = new CanFrame
            (
                Id: BaseId - 1,
                Len: 5,
                Payload: [Convert.ToByte(MessagePrefix.Sleep), Convert.ToByte('Q'), Convert.ToByte('U'), Convert.ToByte('I'), Convert.ToByte('T'), 0, 0, 0
                ]
            ),
            MsgDescription = "Sleep Request"
        };
    }

    public DeviceCanFrame GetVersionMsg()
    {
        return new DeviceCanFrame
        {
            DeviceBaseId = BaseId,
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Version,
            Index = 0,
            Frame = new CanFrame
            (
                Id: BaseId - 1,
                Len: 1,
                Payload: [Convert.ToByte(MessagePrefix.Version), 0, 0, 0, 0, 0, 0, 0]
            ),
            MsgDescription = "Version"
        };
    }

    public DeviceCanFrame GetWakeupMsg()
    {
        return new DeviceCanFrame
        {
            DeviceBaseId = BaseId,
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Null, //No response = no prefix
            Index = 0,
            Frame = new CanFrame
            (
                Id: BaseId - 1,
                Len: 1,
                Payload: [Convert.ToByte('!'), 0, 0, 0, 0, 0, 0, 0]
            ),
            MsgDescription = "Wakeup"
        };
    }

    public DeviceCanFrame GetBootloaderMsg()
    {
        return new DeviceCanFrame
        {
            DeviceBaseId = BaseId,
            Sent = false,
            Received = false,
            Prefix = (int)MessagePrefix.Bootloader,
            Index = 0,
            Frame = new CanFrame
            (
                Id: BaseId - 1,
                Len: 6,
                Payload: [
                    Convert.ToByte(MessagePrefix.Bootloader), (byte)'B', (byte)'O', (byte)'O', (byte)'T', (byte)'L', 0,
                    0
                ]
            ),
            MsgDescription = "Bootloader"
        };
    }
    
    public void UpdateName(string newName) =>  Name = newName;

    // Collection accessors
    public IReadOnlyList<Input> GetInputs() => Inputs.AsReadOnly();
    public IReadOnlyList<Output> GetOutputs() => Outputs.AsReadOnly();
    public IReadOnlyList<CanInput> GetCanInputs() => CanInputs.AsReadOnly();
    public IReadOnlyList<VirtualInput> GetVirtualInputs() => VirtualInputs.AsReadOnly();
    public IReadOnlyList<Flasher> GetFlashers() => Flashers.AsReadOnly();
    public IReadOnlyList<Counter> GetCounters() => Counters.AsReadOnly();
    public IReadOnlyList<Condition> GetConditions() => Conditions.AsReadOnly();
    public Wiper GetWipers() => Wipers;
    public StarterDisable GetStarterDisable() => StarterDisable;

    protected bool CheckVersion(int major, int minor, int build)
    {
        if (major > MinMajorVersion)
            return true;

        if ((major == MinMajorVersion) && (minor > MinMinorVersion))
            return true;

        if ((major == MinMajorVersion) && (minor == MinMinorVersion) && (build >= MinBuildVersion))
            return true;

        return false;
    }
}