using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Devices.Functions;
using domain.Devices.Functions.Keypad;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;
using static domain.Common.DbcSignalCodec;
// ReSharper disable MemberCanBePrivate.Global

namespace domain.Devices;

public class FwDevice : IDeviceConfigurable
{
    [JsonIgnore] protected ILogger<FwDevice> Logger = null!;
    
    [JsonIgnore] public FwDeviceDef Def { get; private set; }

    [JsonIgnore] public const int BaseIndex = 0x0000;
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonIgnore] protected bool DeviceTypeOk;
    [JsonIgnore] public bool ConfigMismatch { get; set; } = true;

    [JsonIgnore] public Guid Guid { get; }
    [JsonIgnore] public int ConfigVersion { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }
    [JsonIgnore] public const int ConfigRxOffset = 0;
    [JsonIgnore] public const int ConfigTxOffset = 1;
    [JsonIgnore] public const int CyclicRxOffset = 2;
    [JsonIgnore] public int MaxCyclicId { get; private set; }

    [JsonIgnore] public List<DeviceVariable> VarMap { get; set; } = null!;
    [JsonIgnore] public List<DeviceParameter> Params { get; set; } = null!;
    
    [JsonIgnore][Plotable(displayName:"DevState")] public DeviceState DeviceState { get; private set; }
    [JsonIgnore][Plotable(displayName:"TotalCurrent", unit:"A")] public double TotalCurrent { get; private set; }
    [JsonIgnore][Plotable(displayName:"BatteryVoltage", unit:"V")] public double BatteryVoltage { get; private set; }
    [JsonIgnore][Plotable(displayName:"Temperature", unit:"degC")] public double BoardTempC { get; private set; }
    [JsonIgnore][Plotable(displayName:"Heartbeat")] public int Heartbeat { get; private set; }
    [JsonIgnore] public string Version { get; private set; } = "v0.0.0";
    public event Action<string>? SuccessNotification;
    
    [JsonPropertyName("sleepEnabled")] public bool SleepEnabled { get; set; }
    [JsonPropertyName("filtersEnabled")] public bool CanFiltersEnabled { get; set; }
    [JsonPropertyName("connectUsbToCan")] public bool ConnectUsbToCan { get; set; } = true;
    [JsonPropertyName("bitrate")] public CanBitRate BitRate { get; set; } = CanBitRate.BitRate500K;
    [JsonIgnore] public TimeSpan CyclicGap { get; } =  TimeSpan.FromSeconds(0);
    [JsonIgnore] public TimeSpan CyclicPause { get; } = TimeSpan.FromMilliseconds(0);
    
    [JsonPropertyName("analogIn")] public List<AnalogInput> AnalogInputs { get; init; } = [];
    [JsonPropertyName("digitalIn")] public List<DigitalInput> DigitalInputs { get; init; } = [];
    [JsonPropertyName("digitalOut")] public List<DigitalOutput> DigitalOutputs { get; init; } = [];
    [JsonPropertyName("outputs")] public List<Output> Outputs { get; init; } = [];
    [JsonPropertyName("canInputs")] public List<CanInput> CanInputs { get; init; } = [];
    [JsonPropertyName("canOutputs")] public List<CanOutput> CanOutputs { get; init; } = [];
    [JsonPropertyName("virtualInputs")] public List<VirtualInput> VirtualInputs { get; init; } = [];
    [JsonPropertyName("wipers")] public Wiper Wipers { get; protected set; } = null!;
    [JsonPropertyName("flashers")] public List<Flasher> Flashers { get; init; } = [];
    [JsonPropertyName("starterDisable")] public StarterDisable StarterDisable { get; protected set; } = null!;
    [JsonPropertyName("counters")] public List<Counter> Counters { get; init; } = [];
    [JsonPropertyName("conditions")] public List<Condition> Conditions { get; init; } = [];
    [JsonPropertyName("keypads")] public List<KeypadMaster> Keypads { get; init; } = [];
    
    [JsonIgnore] private DateTime LastRxTime { get; set; }

    [JsonIgnore] private Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>> CyclicSigs { get; set; } = new();
    [JsonIgnore] private Dictionary<string, Action<double>> _setters = new();
    [JsonIgnore] private Dictionary<string, Action<int, double>> _indexedSetters = new();

    [JsonIgnore] private ParamProtocol _paramProtocol = null!;

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
    public FwDevice(string name, int baseId)
    {
        Name = name;
        BaseId = baseId;
        Guid = Guid.NewGuid();

        InitFunctions();
        InitVarMap();
        InitParams();
    }

    public FwDevice(string name, int id, FwDeviceDef def, CyclicSigsConfig? cyclicSigsConfig = null )
    {
        Name = name;
        BaseId = id;
        Guid = Guid.NewGuid();
        Def = def;
        InitFunctions();
        
        if (cyclicSigsConfig != null)
            BindCyclicSigs(cyclicSigsConfig);
        InitVarMap();
        InitParams();
    }
    
    public void SetLogger(ILogger<FwDevice> logger)
    {
        Logger = logger;
        _paramProtocol.SetLogger(logger);
    }

    private void RegisterAccessors()
    {
        _setters["DeviceState"]      = val => DeviceState = (DeviceState)val;
        _setters["PdmTypeCheck"]     = val => DeviceTypeOk = Def.DeviceType == (int)val;
        _setters["TotalCurrent"]     = val => TotalCurrent = val;
        _setters["BatteryVoltage"]   = val => BatteryVoltage = val;
        _setters["BoardTempC"]       = val => BoardTempC = val;
        _setters["Heartbeat"]        = val => Heartbeat = (int)val;
        _setters["Wiper.SlowState"]  = val => Wipers.SlowState = val != 0;
        _setters["Wiper.FastState"]  = val => Wipers.FastState = val != 0;
        _setters["Wiper.Speed"]      = val => Wipers.Speed = (WiperSpeed)val;
        _setters["Wiper.State"]      = val => Wipers.State = (WiperState)val;

        _indexedSetters["AnalogInput.Millivolts"]   = (i, val) => AnalogInputs[i].Millivolts = val;
        _indexedSetters["AnalogInput.RotaryPos"]    = (i, val) => AnalogInputs[i].Rotary.Pos = (short)val;
        _indexedSetters["AnalogInput.DigitalMode"]  = (i, val) => AnalogInputs[i].Switch.State = val != 0;
        _indexedSetters["DigitalInput.State"]       = (i, val) => DigitalInputs[i].State = val != 0;
        _indexedSetters["DigitalOutput.State"]      = (i, val) => DigitalOutputs[i].State = val != 0;
        _indexedSetters["Output.Current"]     = (i, val) => Outputs[i].Current = val;
        _indexedSetters["Output.State"]       = (i, val) => Outputs[i].State = (OutState)val;
        _indexedSetters["Output.ResetCount"]  = (i, val) => Outputs[i].ResetCount = (int)val;
        _indexedSetters["Output.DutyCycle"]   = (i, val) => Outputs[i].CurrentDutyCycle = val;
        _indexedSetters["CanInput.Output"]    = (i, val) => CanInputs[i].Output = val != 0;
        _indexedSetters["CanInput.Value"]     = (i, val) => CanInputs[i].Value = (int)val;
        _indexedSetters["VirtualInput.Value"] = (i, val) => VirtualInputs[i].Value = val != 0;
        _indexedSetters["Flasher.Value"]      = (i, val) => Flashers[i].Value = val != 0;
        _indexedSetters["Counter.Value"]      = (i, val) => Counters[i].Value = (int)val;
        _indexedSetters["Condition.Value"]    = (i, val) => Conditions[i].Value = (int)val;
    }

    public void BindCyclicSigs(CyclicSigsConfig config)
    {
        RegisterAccessors();
        CyclicSigs = new Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>>();

        foreach (var msgDef in config.Messages)
        {
            CyclicSigs[msgDef.IdOffset - CyclicRxOffset] = [];

            foreach (var sigDef in msgDef.Signals)
            {
                for (var i = 0; i < sigDef.Count; i++)
                {
                    var globalIdx = sigDef.StartIndex + i;
                    var name = sigDef.Dbc.Name.Replace("{n}", $"{globalIdx + 1}");

                    CyclicSigs[msgDef.IdOffset - CyclicRxOffset].Add((new DbcSignal
                    {
                        Name      = name,
                        StartBit  = sigDef.Dbc.StartBit + i * sigDef.Dbc.Length,
                        Length    = sigDef.Dbc.Length,
                        ByteOrder = sigDef.Dbc.ByteOrder,
                        IsSigned  = sigDef.Dbc.IsSigned,
                        Factor    = sigDef.Dbc.Factor,
                        Unit      = sigDef.Dbc.Unit
                    }, ResolveSetter(sigDef.Target, globalIdx)));
                }
            }

            if ((msgDef.IdOffset + BaseId ) > MaxCyclicId)
                MaxCyclicId = msgDef.IdOffset + BaseId;
        }
    }

    private Action<double> ResolveSetter(string target, int index)
    {
        if (_indexedSetters.TryGetValue(target, out var indexed))
            return val => indexed(index, val);
        if (_setters.TryGetValue(target, out var scalar))
            return scalar;
        throw new InvalidOperationException($"Unknown CyclicSig target: '{target}'");
    }

    private void InitFunctions()
    {
        for (var i = 0; i < Def.NumDigitalInputs; i++)
            DigitalInputs.Add(new DigitalInput(i + 1, "digitalInput" + (i + 1)));
        
        for (var i = 0; i < Def.NumDigitalOutputs; i++)
            DigitalOutputs.Add(new DigitalOutput(i + 1, "digitalOutput" + (i + 1)));
        
        for (var i = 0; i < Def.NumAnalogInputs; i++)
            AnalogInputs.Add(new AnalogInput(i + 1, "analogInput" + (i + 1)));
        
        for (var i = 0; i < Def.NumOutputs; i++)
            Outputs.Add(new Output(i + 1, "output" + (i + 1)));

        for (var i = 0; i < Def.NumCanInputs; i++)
            CanInputs.Add(new CanInput(i + 1, "canInput" + (i + 1)));
        
        for (var i = 0; i < Def.NumCanOutputs; i++)
            CanOutputs.Add(new CanOutput(i + 1, "canOutput" + (i + 1)));

        for (var i = 0; i < Def.NumVirtualInputs; i++)
            VirtualInputs.Add(new VirtualInput(i + 1, "virtualInput" + (i + 1)));

        for (var i = 0; i < Def.NumFlashers; i++)
            Flashers.Add(new Flasher(i + 1,  "flasher" + (i + 1)));

        for (var i = 0; i < Def.NumCounters; i++)
            Counters.Add(new Counter(i  + 1, "counter" + (i + 1)));

        for (var i = 0; i < Def.NumConditions; i++)
            Conditions.Add(new Condition(i + 1, "condition" + (i + 1)));
        
        StarterDisable = new StarterDisable("starterDisable", Def.NumOutputs);

        Wipers = new Wiper("wiper");
        
        for (var i = 0; i < Def.NumKeypads; i++)
            Keypads.Add(new KeypadMaster(i + 1, "keypad" + (i + 1)));
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
        
        VarMap.Add(new DeviceVariable
        {
            GetName = () => "State",
            PropertyName = "Value",
            DataType = "int",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        VarMap.Add(new DeviceVariable
        {
            GetName = () => "Temperature",
            PropertyName = "Value",
            DataType = "float",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        VarMap.Add(new DeviceVariable
        {
            GetName =  () => "Battery Voltage",
            PropertyName = "Value",
            DataType = "float",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        for (var i = 0; i < Def.NumDigitalInputs; i++)
            VarMap.AddRange(DigitalInputs[i].GetVarMap(ref index));
        
        for (var i = 0; i < Def.NumCanInputs; i++)
            VarMap.AddRange(CanInputs[i].GetVarMap(ref index));
        
        for(var i=0; i< Def.NumVirtualInputs; i++)
            VarMap.AddRange(VirtualInputs[i].GetVarMap(ref index));
        
        for (var i = 0; i < Def.NumOutputs; i++)
            VarMap.AddRange(Outputs[i].GetVarMap(ref index));
        
        for (var i = 0; i < Def.NumFlashers; i++)
            VarMap.AddRange(Flashers[i].GetVarMap(ref index));
        
        for (var i = 0; i < Def.NumConditions; i++)
            VarMap.AddRange(Conditions[i].GetVarMap(ref index));
        
        for (var i = 0; i < Def.NumCounters; i++)
            VarMap.AddRange(Counters[i].GetVarMap(ref index));
        
        VarMap.AddRange(Wipers.GetVarMap(ref index));
        
        for (var i = 0; i < Def.NumKeypads; i++)
            VarMap.AddRange(Keypads[i].GetVarMap(ref index));
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
                GetValue = () => BaseId, SetValue = val => BaseId = val is string s
                    ? int.Parse(s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s, System.Globalization.NumberStyles.HexNumber)
                    : Convert.ToInt32(val),
                ValueType = BaseId.GetType(),
                DefaultValue = Def.DefaultId
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
                ParentName = Name, Name = "device.sleepEnabled", Index = BaseIndex, SubIndex = subIndex++,
                GetValue = () => SleepEnabled, SetValue = val => SleepEnabled = (bool)val,
                ValueType = SleepEnabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "device.canFiltersEnabled", Index = BaseIndex, SubIndex = subIndex++,
                GetValue = () => CanFiltersEnabled, SetValue = val => CanFiltersEnabled = (bool)val,
                ValueType = CanFiltersEnabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "device.connectUsbToCan", Index = BaseIndex, SubIndex = subIndex++,
                GetValue = () => ConnectUsbToCan, SetValue = val => ConnectUsbToCan = (bool)val,
                ValueType = ConnectUsbToCan.GetType(),
                DefaultValue = true
            }
        ]);
        
        foreach (var output in Outputs) allParams.AddRange(output.Params);
        foreach (var input in DigitalInputs) allParams.AddRange(input.Params);
        foreach (var canInput in CanInputs) allParams.AddRange(canInput.Params);
        foreach (var virtualInput in VirtualInputs) allParams.AddRange(virtualInput.Params);
        foreach (var condition in Conditions) allParams.AddRange(condition.Params);
        foreach (var counter in Counters) allParams.AddRange(counter.Params);
        foreach (var flasher in Flashers) allParams.AddRange(flasher.Params);
        allParams.AddRange(StarterDisable.Params);
        allParams.AddRange(Wipers.Params);
        foreach (var canOutput in CanOutputs) allParams.AddRange(canOutput.Params);
        foreach (var keypad in Keypads) allParams.AddRange(keypad.BaseParams);
        foreach (var keypad in Keypads) allParams.AddRange(keypad.ButtonParams);
        foreach (var keypad in Keypads) allParams.AddRange(keypad.DialParams);
        Params = allParams;

        _paramProtocol = new ParamProtocol(this, Params)
        {
            NotifySuccess = msg => SuccessNotification?.Invoke(msg)
        };
    }

    private void Clear()
    {
        foreach(var input in DigitalInputs)
            input.State = false;

        foreach(var output in DigitalOutputs)
            output.State = false;
        
        foreach (var input in AnalogInputs)
        {
            input.Switch.State = false;
            input.Millivolts = 0.0;
            input.Rotary.Pos = 0;
        }
            
        foreach(var output in Outputs)
        {
            output.Current = 0;
            output.State = OutState.Off;
        }

        foreach(var input in VirtualInputs)
            input.Value = false;

        foreach(var canInput in CanInputs)
            canInput.Output = false;
        
        Logger.LogDebug("Device {Name} cleared", Name);
    }
    
    public bool UpdateIsConnected()
    {
        var lastConnected = Connected;
        var timeSpan = DateTime.Now - LastRxTime;
        Connected = timeSpan.TotalMilliseconds < 500;
        
        return Connected & !lastConnected;
    }
    
    public bool InIdRange(int id)
    {
        return (id >= BaseId) && (id <= MaxCyclicId);
    }
    
    public void Read(int id, byte[] data, 
            ref ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue, 
            List<DeviceCanFrame> outgoing)
    {
        var offset = id - BaseId;

        // Use dictionary lookup for status messages
        if (CyclicSigs.TryGetValue(offset - CyclicRxOffset, out var signals))
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
            if (id == BaseId + ConfigRxOffset)
            {
                _paramProtocol.HandleMessage(BaseId,BaseId + ConfigTxOffset, Name, data, queue, outgoing);
                
                ReadInfoWarnErrorMessage(data);
                
                if (((MessageCommand)data[0]) == MessageCommand.Version)
                    ReadVersion(BaseId, Name, data, queue);
            }
        }

        LastRxTime = DateTime.Now;
    }

    public IEnumerable<(int MessageId, DbcSignal Signal)> GetCyclicSigs()
    {
        foreach (var kvp in CyclicSigs)
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

    public DeviceCanFrame GetSleepMsg()
    {
        return new DeviceCanFrame
        {
            SendOnly = true,
            DeviceBaseId = BaseId,
            Frame = new CanFrame
            (
                Id: BaseId + ConfigTxOffset,
                Len: 8,
                Payload: [Convert.ToByte(MessageCommand.Sleep), Convert.ToByte('Q'), Convert.ToByte('U'), 
                            Convert.ToByte('I'), Convert.ToByte('T'), 0, 0, 0
                ]
            ),
            Name = "Sleep"
        };
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

    public DeviceCanFrame GetWakeupMsg()
    {
        return new DeviceCanFrame
        {
            SendOnly = true,
            DeviceBaseId = BaseId,
            Frame = new CanFrame
            (
                Id: BaseId + ConfigTxOffset,
                Len: 8,
                Payload: [Convert.ToByte('!'), 0, 0, 0, 0, 0, 0, 0]
            ),
            Name = "Wakeup"
        };
    }

    public DeviceCanFrame GetBootloaderMsg()
    {
        return new DeviceCanFrame
        {
            SendOnly = true,
            DeviceBaseId = BaseId,
            Frame = new CanFrame
            (
                Id: BaseId + ConfigTxOffset,
                Len: 8,
                Payload: [
                    Convert.ToByte(MessageCommand.Bootloader), (byte)'B', (byte)'O', (byte)'O', (byte)'T', (byte)'L', 0,
                    0
                ]
            ),
            Name = "Bootloader"
        };
    }
    
    public List<CanFrame> GetCyclicMsgs()
    {
        return [];
    }

    public bool SetKeypad(int index, int id, KeypadModel model)
    {
        if (index > Def.NumKeypads - 1) return false;
        
        Keypads[index].Id = id;
        Keypads[index].Model = model;
        
        return true;
    }

    private void ReadVersion(int baseId, string name, byte[] data,
        ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue)
    {
        if (data.Length != 8) return;

        var version = $"v{data[4]}.{data[5]}.{(data[6] << 8) + (data[7])}";

        if (!CheckVersion(data[4], data[5], (data[6] << 8) + (data[7])))
        {
            Logger.LogError("{Name} ID: {BaseId}, Firmware needs to be updated. V{MinMajorVersion}.{MinMinorVersion}.{MinBuildVersion} or greater",
                name, baseId, Def.MinMajorVersion, Def.MinMinorVersion, Def.MinBuildVersion);
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
        if (major > Def.MinMajorVersion)
            return true;

        if ((major == Def.MinMajorVersion) && (minor > Def.MinMinorVersion))
            return true;

        if ((major == Def.MinMajorVersion) && (minor == Def.MinMinorVersion) && (build >= Def.MinBuildVersion))
            return true;

        return false;
    }

    // Collection accessors
    public IReadOnlyList<DigitalInput> GetDigitalInputs() => DigitalInputs.AsReadOnly();
    public IReadOnlyList<DigitalOutput> GetDigitalOutputs() => DigitalOutputs.AsReadOnly();
    public IReadOnlyList<AnalogInput> GetAnalogInputs() => AnalogInputs.AsReadOnly();
    public IReadOnlyList<Output> GetOutputs() => Outputs.AsReadOnly();
    public IReadOnlyList<CanInput> GetCanInputs() => CanInputs.AsReadOnly();
    public IReadOnlyList<CanOutput> GetCanOutputs() => CanOutputs.AsReadOnly();
    public IReadOnlyList<VirtualInput> GetVirtualInputs() => VirtualInputs.AsReadOnly();
    public IReadOnlyList<Flasher> GetFlashers() => Flashers.AsReadOnly();
    public IReadOnlyList<Counter> GetCounters() => Counters.AsReadOnly();
    public IReadOnlyList<Condition> GetConditions() => Conditions.AsReadOnly();
    public Wiper GetWipers() => Wipers;
    public StarterDisable GetStarterDisable() => StarterDisable;
    public IReadOnlyList<KeypadMaster> GetKeypads() => Keypads.AsReadOnly();
}