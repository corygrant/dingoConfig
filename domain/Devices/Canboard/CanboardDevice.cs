using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Enums.Canboard;
using domain.Devices.Functions;
using domain.Devices.Functions.Canboard;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;
using static domain.Common.DbcSignalCodec;
// ReSharper disable MemberCanBePrivate.Global

namespace domain.Devices.Canboard;

public class CanboardDevice : IDeviceConfigurable
{
    [JsonIgnore] protected ILogger<CanboardDevice> Logger = null!;

    [JsonIgnore] protected int MinMajorVersion { get; private set; } = 3;
    [JsonIgnore] protected int MinMinorVersion { get; private set; } = 0;
    [JsonIgnore] protected int MinBuildVersion { get; private set; } = 0;

    [JsonIgnore] protected int NumAnalogInputs { get; private set; } = 5; //Also serve as rotary switches and analog/dig inputs
    [JsonIgnore] protected int NumDigitalInputs { get; private set; } = 8;
    [JsonIgnore] protected int NumDigitalOutputs { get; private set; } = 4;
    [JsonIgnore] protected int NumCanInputs { get; private set; } = 8;
    [JsonIgnore] protected int NumCanOutputs { get; private set; } = 8;
    [JsonIgnore] protected int NumVirtualInputs { get; private set; } = 8;
    [JsonIgnore] protected int NumFlashers { get; private set; } = 4;
    [JsonIgnore] protected int NumCounters { get; private set; } = 4;
    [JsonIgnore] protected int NumConditions { get; private set; } = 8;
    
    [JsonIgnore] public bool CanSleep { get; } = false;
    [JsonIgnore] public bool CanBootloader { get; } = false;

    [JsonIgnore] public const int BaseIndex = 0x0000;
    [JsonPropertyName("canboardType")] public int CanboardType { get; set; }
    [JsonIgnore] protected bool CanboardTypeOk;
    [JsonIgnore] public bool ConfigMismatch { get; set; } = true;
    
    [JsonIgnore] public Guid Guid { get; }
    [JsonIgnore] public string Type  { get; private set; } = "CANBoard";
    [JsonIgnore] public string Icon { get; private set; } = string.Empty;
    [JsonIgnore] public int ConfigVersion { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }
    [JsonIgnore] public static int DefaultId { get; } = 0x640;
    [JsonIgnore] public const int ConfigRxOffset = 0;
    [JsonIgnore] public const int ConfigTxOffset = 1;
    [JsonIgnore] public int MaxCyclicId { get; private set; }
    
    [JsonIgnore] public List<DeviceVariable> VarMap { get; set; } = null!;
    [JsonIgnore] public List<DeviceParameter> Params { get; set; } = null!;
    
    [JsonIgnore][Plotable(displayName:"Temperature", unit:"degC")] public double BoardTempC { get; private set; }
    [JsonIgnore][Plotable(displayName:"Heartbeat", unit:" ")] public int Heartbeat { get; private set; }
    
    [JsonIgnore] public string Version { get; private set; } = "v0.0.0";
    public event Action<string>? SuccessNotification;
    
    [JsonIgnore] private DateTime LastRxTime { get; set; }
    [JsonPropertyName("filtersEnabled")] public bool CanFiltersEnabled { get; set; }
    [JsonPropertyName("bitrate")] public CanBitRate BitRate { get; set; } = CanBitRate.BitRate500K;
    [JsonIgnore] public TimeSpan CyclicGap { get; } =  TimeSpan.FromSeconds(0);
    [JsonIgnore] public TimeSpan CyclicPause { get; } = TimeSpan.FromMilliseconds(0);

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
    [JsonPropertyName("canInputs")] public List<CanInput> CanInputs { get; init; } = [];
    [JsonPropertyName("canOutputs")] public List<CanOutput> CanOutputs { get; init; } = [];
    [JsonPropertyName("virtualInputs")] public List<VirtualInput> VirtualInputs { get; init; } = [];
    [JsonPropertyName("flashers")] public List<Flasher> Flashers { get; init; } = [];
    [JsonPropertyName("counters")] public List<Counter> Counters { get; init; } = [];
    [JsonPropertyName("conditions")] public List<Condition> Conditions { get; init; } = [];

    [JsonIgnore] private Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>> StatusSigs { get; set; } = null!;

    [JsonIgnore] private ParamProtocol _paramProtocol = null!;
    
    [JsonConstructor]
    public CanboardDevice(string name, int baseId)
    {
        Guid = Guid.NewGuid();
        Name = name;
        BaseId =  baseId;

        // ReSharper disable VirtualMemberCallInConstructor
        InitFunctions();
        InitVarMap();
        InitParams();
    }
    
    public CanboardDevice(CanboardDeviceDefinition definition, string name, int baseId)
    {
        Guid = Guid.NewGuid();
        Name = name;
        BaseId =  baseId;

        NumDigitalInputs = definition.NumDigitalInputs;
        NumDigitalOutputs = definition.NumOutputs;
        NumAnalogInputs = definition.NumAnalogInputs;
        NumCanInputs = definition.NumCanInputs;
        NumCanOutputs = definition.NumCanOutputs;
        NumVirtualInputs = definition.NumVirtualInputs;
        NumFlashers = definition.NumFlashers;
        NumCounters = definition.NumCounters;
        NumConditions = definition.NumConditions;

        InitFunctions();
        ApplyDefinition(definition);
    }
    
    public void SetLogger(ILogger<CanboardDevice> logger)
    {
        Logger = logger;
        _paramProtocol.SetLogger(logger);
    }

    public void ApplyDefinition(CanboardDeviceDefinition definition)
    {
        CanboardType = definition.CanboardType;
        Type = definition.TypeName;
        Icon = definition.Icon;
        MinMajorVersion = definition.MinMajorVersion;
        MinMinorVersion = definition.MinMinorVersion;
        MinBuildVersion = definition.MinBuildVersion;
        NumDigitalInputs = definition.NumDigitalInputs;
        NumDigitalOutputs = definition.NumOutputs;
        NumAnalogInputs = definition.NumAnalogInputs;
        NumCanInputs = definition.NumCanInputs;
        NumCanOutputs = definition.NumCanOutputs;
        NumVirtualInputs = definition.NumVirtualInputs;
        NumFlashers = definition.NumFlashers;
        NumCounters = definition.NumCounters;
        NumConditions = definition.NumConditions;
        
        InitStatusSigs();
        InitVarMap();
        InitParams();
    }

    private void InitFunctions()
    {
        for (var i = 0; i < NumDigitalInputs; i++)
            DigitalInputs.Add(new DigitalInput(i + 1, "digitalInput" + (i + 1)));
        
        for (var i = 0; i < NumDigitalOutputs; i++)
            DigitalOutputs.Add(new DigitalOutput(i + 1, "digitalOutput" + (i + 1)));
        
        for (var i = 0; i < NumAnalogInputs; i++)
            AnalogInputs.Add(new AnalogInput(i + 1, "analogInput" + (i + 1)));
        
        for (var i = 0; i < NumCanInputs; i++)
            CanInputs.Add(new CanInput(i + 1, "canInput" + (i + 1)));
        
        for (var i = 0; i < NumCanOutputs; i++)
            CanOutputs.Add(new CanOutput(i + 1, "canOutput" + (i + 1)));

        for (var i = 0; i < NumVirtualInputs; i++)
            VirtualInputs.Add(new VirtualInput(i + 1, "virtualInput" + (i + 1)));

        for (var i = 0; i < NumFlashers; i++)
            Flashers.Add(new Flasher(i + 1,  "flasher" + (i + 1)));

        for (var i = 0; i < NumCounters; i++)
            Counters.Add(new Counter(i  + 1, "counter" + (i + 1)));

        for (var i = 0; i < NumConditions; i++)
            Conditions.Add(new Condition(i + 1, "condition" + (i + 1)));
        
        InitStatusSigs();
    }

    protected virtual void InitStatusSigs()
    {
        StatusSigs = new Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>>();

        var cyclicIndex = 0;
        
        // Message 0 (BaseId + 0): Analog inputs 0-3 millivolts
        StatusSigs[cyclicIndex] = new List<(DbcSignal, Action<double>)>();
        for (var i = 0; i < 4 && i < NumAnalogInputs; i++)
        {
            var index = i;
            StatusSigs[cyclicIndex].Add((
                new DbcSignal { Name = $"AnalogInput{index + 1}.Millivolts", StartBit = index * 16, Length = 16, Unit = "mV"},
                val => AnalogInputs[index].Millivolts = val
            ));
        }

        cyclicIndex++;

        // Message 1 (BaseId + 1): Analog input 4 millivolts + board temperature
        StatusSigs[cyclicIndex] =
        [
            (new DbcSignal { Name = "AnalogInput5.Millivolts", StartBit = 0, Length = 16, Unit = "mV"},
                val => AnalogInputs[4].Millivolts = val),


            (new DbcSignal { Name = "BoardTempC", StartBit = 48, Length = 16, Factor = 0.01, Unit = "degC"},
                val => BoardTempC = val)
        ];
        cyclicIndex++;

        // Message 2 (BaseId + 2): Rotary switches, digital I/O, heartbeat
        StatusSigs[cyclicIndex] = [];

        // Rotary switch positions (4-bit each)
        for (var i = 0; i < NumAnalogInputs; i++)
        {
            var index = i;
            StatusSigs[cyclicIndex].Add((
                new DbcSignal { Name = $"RotarySwitch{index + 1}.Pos", StartBit = index * 4, Length = 4 },
                val => AnalogInputs[index].Rotary.Pos = (short)val
            ));
        }

        // Digital inputs (1-bit each starting at bit 32)
        for (var i = 0; i < NumDigitalInputs; i++)
        {
            var index = i;
            StatusSigs[cyclicIndex].Add((
                new DbcSignal { Name = $"DigitalInput{index + 1}.State", StartBit = 32 + index, Length = 1 },
                val => DigitalInputs[index].State = val != 0
            ));
        }

        // Analog inputs digital mode (1-bit each starting at bit 40)
        for (var i = 0; i < NumAnalogInputs; i++)
        {
            var index = i;
            StatusSigs[cyclicIndex].Add((
                new DbcSignal { Name = $"AnalogInput{index + 1}.DigitalMode", StartBit = 40 + index, Length = 1 },
                val => AnalogInputs[index].Switch.State = val != 0
            ));
        }

        // Digital outputs (1-bit each starting at bit 48)
        for (var i = 0; i < NumDigitalOutputs; i++)
        {
            var index = i;
            StatusSigs[cyclicIndex].Add((
                new DbcSignal { Name = $"DigitalOutput{index + 1}.State", StartBit = 48 + index, Length = 1 },
                val => DigitalOutputs[index].State = val != 0
            ));
        }

        // Heartbeat (8-bit at bit 56)
        StatusSigs[cyclicIndex].Add((
            new DbcSignal { Name = "Heartbeat", StartBit = 56, Length = 8 },
            val => Heartbeat = (int)val
        ));

        MaxCyclicId = cyclicIndex;
    }
    
    private void InitVarMap()
    {
        VarMap = [];
        
        var index = 0;

        VarMap.Add(new DeviceVariable
        {
            GetName = () => "None",
            PropertyName = "Value",
            DataType = "bool",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        VarMap.Add(new DeviceVariable
        {
            GetName = () => "Always On",
            PropertyName = "Value",
            DataType = "bool",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        if (NumDigitalInputs > 0)
        {
            for (var i = 0; i < NumDigitalInputs; i++)
            {
                var num = i;
                VarMap.Add(new DeviceVariable
                {
                    GetName  = () => DigitalInputs[num].Name,
                    PropertyName = "State",
                    DataType = "bool",
                    VariableIndex = index++,
                    SingleVariable = false
                });
            }
        }
        
        if (NumDigitalOutputs > 0)
        {
            for (var i = 0; i < NumDigitalOutputs; i++)
            {
                var num = i;
                VarMap.Add(new DeviceVariable
                {
                    GetName  = () => DigitalOutputs[num].Name,
                    PropertyName = "State",
                    DataType = "bool",
                    VariableIndex = index++,
                    SingleVariable = false
                });
            }
        }
        
        if (NumAnalogInputs > 0)
        {
            for (var i = 0; i < NumAnalogInputs; i++)
            {
                var num = i;
                VarMap.Add(new DeviceVariable
                {
                    GetName  = () => AnalogInputs[num].Name,
                    PropertyName = "Value",
                    DataType = "float",
                    VariableIndex = index++,
                    SingleVariable = false
                });
                VarMap.Add(new DeviceVariable
                {
                    GetName  = () => AnalogInputs[num].Name,
                    PropertyName = "Value Millivolts",
                    DataType = "float",
                    VariableIndex = index++,
                    SingleVariable = false
                });
                VarMap.Add(new DeviceVariable
                {
                    GetName  = () => AnalogInputs[num].Name,
                    PropertyName = "Rotary Position",
                    DataType = "int",
                    VariableIndex = index++,
                    SingleVariable = false
                });
                VarMap.Add(new DeviceVariable
                {
                    GetName  = () => AnalogInputs[num].Name,
                    PropertyName = "Switch Value",
                    DataType = "bool",
                    VariableIndex = index++,
                    SingleVariable = false
                });
            }
        }
        
        if (NumCanInputs > 0)
        {
            for (var i = 0; i < NumCanInputs; i++)
            {
                var num = i;
                VarMap.Add(new DeviceVariable
                {
                    GetName = () => CanInputs[num].Name,
                    PropertyName = "State",
                    DataType = "bool",
                    VariableIndex = index++,
                    SingleVariable = false
                });
                VarMap.Add(new DeviceVariable
                {
                    GetName = () => CanInputs[num].Name,
                    PropertyName = "Value",
                    DataType = "float",
                    VariableIndex = index++,
                    SingleVariable = false
                });
            }
        }
        
        if (NumVirtualInputs > 0)
        {
            for(var i=0; i< NumVirtualInputs; i++)
            {
                var num = i;
                VarMap.Add(new DeviceVariable
                {
                    GetName = () => VirtualInputs[num].Name,
                    PropertyName = "State",
                    DataType = "bool",
                    VariableIndex = index++,
                    SingleVariable = false
                });
            }  
        }
        
        if (NumFlashers > 0)
        {
            for (var i = 0; i < NumFlashers; i++)
            {
                var num = i;
                VarMap.Add(new DeviceVariable
                {
                    GetName = () => Flashers[num].Name,
                    PropertyName = "State",
                    DataType = "bool",
                    VariableIndex = index++,
                    SingleVariable = false
                });
            }
        }
        
        if (NumConditions > 0)
        {
            for (var i = 0; i < NumConditions; i++)
            {
                var num = i;
                VarMap.Add(new DeviceVariable
                {
                    GetName = () => Conditions[num].Name,
                    PropertyName = "Value",
                    DataType = "bool",
                    VariableIndex = index++,
                    SingleVariable = false
                });
            }
        }
        
        if (NumCounters > 0)
        {
            for (var i = 0; i < NumCounters; i++)
            {
                var num = i;
                VarMap.Add(new DeviceVariable
                {
                    GetName = () => Counters[num].Name,
                    PropertyName = "Value",
                    DataType = "int",
                    VariableIndex = index++,
                    SingleVariable = false
                });
            }
        }
    }

    private void InitParams()
    {
        var allParams = new List<DeviceParameter>();
        var subIndex = 0;
        allParams.AddRange(
        [
            new DeviceParameter
            {
                ParentName = Name, Name = "device.baseId", Index = BaseIndex, SubIndex = subIndex++,
                GetValue = () => BaseId, SetValue = val => BaseId = (int)val,
                ValueType = BaseId.GetType(),
                DefaultValue = DefaultId
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "device.canSpeed", Index = BaseIndex, SubIndex = subIndex++,
                GetValue = () => BitRate, SetValue = val => BitRate = (CanBitRate)val,
                ValueType = BitRate.GetType(),
                DefaultValue = CanBitRate.BitRate500K
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "device.canFiltersEnabled", Index = BaseIndex, SubIndex = subIndex++,
                GetValue = () => CanFiltersEnabled, SetValue = val => CanFiltersEnabled = (bool)val,
                ValueType = CanFiltersEnabled.GetType(),
                DefaultValue = false
            }
        ]);
        
        foreach (var input in DigitalInputs) allParams.AddRange(input.Params);
        foreach (var canInput in CanInputs) allParams.AddRange(canInput.Params);
        foreach (var virtualInput in VirtualInputs) allParams.AddRange(virtualInput.Params);
        foreach (var condition in Conditions) allParams.AddRange(condition.Params);
        foreach (var counter in Counters) allParams.AddRange(counter.Params);
        foreach (var flasher in Flashers) allParams.AddRange(flasher.Params);
        foreach (var canOutput in CanOutputs) allParams.AddRange(canOutput.Params);
        foreach (var digitalOutput in DigitalOutputs) allParams.AddRange(digitalOutput.Params);
        foreach (var analogInput in AnalogInputs) allParams.AddRange(analogInput.Params);
        Params = allParams;

        _paramProtocol = new ParamProtocol(this, Params)
        {
            NotifySuccess = msg => SuccessNotification?.Invoke(msg)
        };
    }

    private void Clear()
    {
        foreach (var input in AnalogInputs)
        {
            input.Switch.State = false;
            input.Millivolts = 0.0;
            input.Rotary.Pos = 0;
        }

        foreach (var input in DigitalInputs)
            input.State = false;

        foreach (var output in DigitalOutputs)
            output.State = false;

        Logger.LogDebug("CANBoard {Name} cleared", Name);
    }
    
    /// <remarks>
    /// Returns true only on Connected false to true transition
    /// </remarks>
    public bool UpdateIsConnected()
    {
        var lastConnected = Connected;
        var timeSpan = DateTime.Now - LastRxTime;
        Connected = timeSpan.TotalMilliseconds < 500;
        
        return Connected & !lastConnected;
    }

    public bool InIdRange(int id)
    {
        return (id >= BaseId) && (id <= BaseId + 4);
    }

    public void Read(int id, byte[] data, 
                     ref ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue, 
                     List<DeviceCanFrame> outgoing)
    {
        var offset = id - BaseId;

        // Use dictionary lookup for status messages
        if (StatusSigs.TryGetValue(offset, out var signals))
        {
            foreach (var (signal, setValue) in signals)
            {
                var value = ExtractSignal(data, signal);
                setValue(value);
            }
        }
        // Handle param, version and info/warn/error messages
        else
        {
            if (offset == 31)
            {
                ReadInfoWarnErrorMessage(data);
            }

            if (id == BaseId + ConfigRxOffset)
            {
                if (((MessageCommand)data[0]) == MessageCommand.Version)
                    ReadVersion(BaseId, Name, data, queue);
                    
                _paramProtocol.HandleMessage(BaseId, BaseId + ConfigTxOffset, Name, data, queue, outgoing);
            }
        }

        LastRxTime = DateTime.Now;
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
    
    protected void ReadInfoWarnErrorMessage(byte[] data)
    {
        //Response is lowercase version of set/get prefix
        var type = (MessageType)char.ToUpper(Convert.ToChar(data[0]));
        var src = (MessageSrc)data[1];

        switch (type)
        {
            case MessageType.Info:
                Logger.LogInformation("{Name} ID: {BaseId}, Src: {MessageSrc} {I} {I1} {I2}", 
                    Name, BaseId, src, (data[3] << 8) + data[2], (data[5] << 8) + data[4], (data[7] << 8) + data[6]);
                break;
            case MessageType.Warning:
                Logger.LogWarning("{Name} ID: {BaseId}, Src: {MessageSrc} {I} {I1} {I2}", 
                    Name, BaseId, src, (data[3] << 8) + data[2], (data[5] << 8) + data[4], (data[7] << 8) + data[6]);
                break;
            case MessageType.Error:
                Logger.LogError("{Name} ID: {BaseId}, Src: {MessageSrc} {I} {I1} {I2}", 
                    Name, BaseId, src, (data[3] << 8) + data[2], (data[5] << 8) + data[4], (data[7] << 8) + data[6]);
                break;
        }
    }

    public List<DeviceCanFrame> GetReadMsgs(bool allParams)
    {
        var cmd = allParams ? MessageCommand.ReadAll : MessageCommand.ReadAllModified;
        var name = allParams ? "ReadAll" : "ReadAllModified";
        
        List<DeviceCanFrame>  msgs =
        [
            GetVersionMsg(),
            new()
            {
                DeviceBaseId = BaseId,
                SendOnly = true,
                Frame = new CanFrame(
                    Id: BaseId + ConfigTxOffset,
                    Len: 8,
                    Payload: [Convert.ToByte(cmd), 0, 0, 0, 0, 0, 0, 0]),
                Name = name
            }
        ];

		return msgs;
    }

    public List<DeviceCanFrame> GetWriteMsgs(bool allParams)
    {
        var cmd = allParams ? MessageCommand.WriteAll : MessageCommand.WriteAllModified;
        var name = allParams ? "WriteAll" : "WriteAllModified";
        
        List<DeviceCanFrame> msgs =
        [
            new()
            {
                DeviceBaseId = BaseId,
                Frame = new CanFrame
                (
                    Id: BaseId + ConfigTxOffset,
                    Len: 8,
                    Payload: [Convert.ToByte(cmd), 0, 0, 0, 0, 0, 0, 0]
                ),
                Name = name
            }
        ];

        return msgs;
    }
    
    public DeviceCanFrame GetCheckMsg()
    {
        return new DeviceCanFrame
        {
            DeviceBaseId = BaseId,
            SendOnly = true,
            Frame = new CanFrame
            (
                Id: BaseId + ConfigTxOffset,
                Len: 8,
                Payload: [Convert.ToByte(MessageCommand.CheckCrc), 0, 0, 0, 0, 0, 0, 0]
            ),
            Name = "Check"
        };
    }
    
    public List<DeviceCanFrame> GetModifyMsgs(int newId)
    {
        List<DeviceParameter> modifyParams = [];
        
        //Copy params:
        //ID: 0x0000, Subindex: 0, Base ID
        var baseIdParam = Params.First(p => p is { Index: 0x0000, SubIndex: 0});
        baseIdParam.SetValue(newId);
        modifyParams.Add(baseIdParam);
        
        List<DeviceCanFrame> msgs = [];

        foreach (var parameter in modifyParams)
        {
            msgs.Add(new DeviceCanFrame
            {
                SendOnly = true,
                DeviceBaseId = newId,
                Frame = ParamCodec.ToFrame(MessageCommand.Write, parameter, BaseId),
                Name = $"Modify {parameter.Index}:{parameter.SubIndex}"
            });
        }
        
        return msgs;
    }
    
    public DeviceCanFrame GetBurnMsg()
    {
        return new DeviceCanFrame
        {
            DeviceBaseId = BaseId,
            Frame = new CanFrame
            (
                Id: BaseId + ConfigTxOffset,
                Len: 8,
                Payload: [Convert.ToByte(MessageCommand.BurnParams), 1, 3, 8, 0, 0, 0, 0]
            ),
            Name = "Burn"
        };
    }

    public DeviceCanFrame? GetSleepMsg()
    {
        return null;
    }
    
    public DeviceCanFrame GetVersionMsg()
    {
        return new DeviceCanFrame
        {
            DeviceBaseId = BaseId,
            Frame = new CanFrame
            (
                Id: BaseId + ConfigTxOffset,
                Len: 8,
                Payload: [Convert.ToByte(MessageCommand.Version), 0, 0, 0, 0, 0, 0, 0]
            ),
            Name = "Version"
        };
    }
    
    public DeviceCanFrame? GetWakeupMsg()
    {
        return null;
    }

    public DeviceCanFrame? GetBootloaderMsg()
    {
        return null;
    }

    public List<CanFrame> GetCyclicMsgs()
    {
        return [];
    }
    
    private void ReadVersion(int baseId, string name, byte[] data,
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue)
    {
        if (data.Length != 8) return;

        var version = $"v{data[4]}.{data[5]}.{(data[6] << 8) + (data[7])}";

        if (!CheckVersion(data[4], data[5], (data[6] << 8) + (data[7])))
        {
            Logger.LogError("{Name} ID: {BaseId}, Firmware needs to be updated. V{MinMajorVersion}.{MinMinorVersion}.{MinBuildVersion} or greater",
                name, baseId, MinMajorVersion, MinMinorVersion, MinBuildVersion);
        }
        
        (int BaseId, int, int) key = (baseId, 0, 0); //Version request message index =0, subindex = 0
        if (queue.TryGetValue(key, out var canFrame))
        {
            canFrame.TimeSentTimer?.Dispose();
            queue.TryRemove(key, out _);
        }

        Logger.LogInformation("{Name} FW version received: {Version}", name, version);

        Version = version;
    }
    
    private bool CheckVersion(int major, int minor, int build)
    {
        if (major > MinMajorVersion)
            return true;

        if ((major == MinMajorVersion) && (minor > MinMinorVersion))
            return true;

        if ((major == MinMajorVersion) && (minor == MinMinorVersion) && (build >= MinBuildVersion))
            return true;

        return false;
    }
    
    // Collection accessors
    public IReadOnlyList<DigitalInput> GetDigitalInputs() => DigitalInputs.AsReadOnly();
    public IReadOnlyList<DigitalOutput> GetDigitalOutputs() => DigitalOutputs.AsReadOnly();
    public IReadOnlyCollection<AnalogInput> GetAnalogInputs() => AnalogInputs.AsReadOnly();
    public IReadOnlyList<CanInput> GetCanInputs() => CanInputs.AsReadOnly();
    public IReadOnlyList<CanOutput> GetCanOutputs() => CanOutputs.AsReadOnly();
    public IReadOnlyList<VirtualInput> GetVirtualInputs() => VirtualInputs.AsReadOnly();
    public IReadOnlyList<Flasher> GetFlashers() => Flashers.AsReadOnly();
    public IReadOnlyList<Counter> GetCounters() => Counters.AsReadOnly();
    public IReadOnlyList<Condition> GetConditions() => Conditions.AsReadOnly();

}