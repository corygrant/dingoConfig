using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Devices.dingoPdm.Enums;
using domain.Devices.dingoPdm.Functions;
using domain.Devices.dingoPdm.Functions.Keypad;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;
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
    [JsonIgnore] protected virtual int NumCanOutputs => 32;
    [JsonIgnore] protected virtual int NumVirtualInputs => 16;
    [JsonIgnore] protected virtual int NumFlashers => 4;
    [JsonIgnore] protected virtual int NumCounters => 4;
    [JsonIgnore] protected virtual int NumConditions => 32;
    [JsonIgnore] protected virtual int NumKeypads => 2;

    [JsonIgnore] public const int BaseIndex = 0x0000;
    [JsonIgnore] protected virtual int PdmType => 0; //0=dingoPDM, 1=dingoPDM-Max
    [JsonIgnore] protected bool PdmTypeOk;
    [JsonIgnore] public bool ConfigMismatch { get; set; } = true;

    [JsonIgnore] public Guid Guid { get; }
    [JsonIgnore] public virtual string Type => "dingoPDM";
    [JsonIgnore] public int ConfigVersion { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }
    [JsonPropertyName("paramTxId")] public int ParamTxId { get; set; } = 0x080;
    [JsonPropertyName("paramRxId")] public int ParamRxId { get; set; } = 0x081;
    
    [JsonIgnore] public List<DeviceVariable> VarMap { get; set; } = null!;
    [JsonIgnore] public List<DeviceParameter> Params { get; set; } = null!;

    
    [JsonIgnore][Plotable(displayName:"DevState")] public DeviceState DeviceState { get; private set; }
    [JsonIgnore][Plotable(displayName:"TotalCurrent", unit:"A")] public double TotalCurrent { get; private set; }
    [JsonIgnore][Plotable(displayName:"BatteryVoltage", unit:"V")] public double BatteryVoltage { get; private set; }
    [JsonIgnore][Plotable(displayName:"Temperature", unit:"degC")] public double BoardTempC { get; private set; }
    [JsonIgnore] public string Version { get; private set; } = "v0.0.0";
    public event Action<string>? SuccessNotification;
    
    [JsonPropertyName("sleepEnabled")] public bool SleepEnabled { get; set; }
    [JsonPropertyName("filtersEnabled")] public bool CanFiltersEnabled { get; set; }
    [JsonPropertyName("connectUsbToCan")] public bool ConnectUsbToCan { get; set; } = true;
    [JsonPropertyName("bitrate")] public CanBitRate BitRate { get; set; } = CanBitRate.BitRate500K;
    [JsonIgnore] public TimeSpan CyclicGap { get; } =  TimeSpan.FromSeconds(0);
    [JsonIgnore] public TimeSpan CyclicPause { get; } = TimeSpan.FromMilliseconds(0);
    
    [JsonPropertyName("inputs")] public List<Input> Inputs { get; init; } = [];
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

    [JsonIgnore] private Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>> StatusSigs { get; set; } = null!;

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
    public PdmDevice(string name, int baseId)
    {
        Name = name;
        BaseId = baseId;
        Guid = Guid.NewGuid();

        InitFunctions();
        InitVarMap();
        InitParams();
    }

    public void SetLogger(ILogger<PdmDevice> logger)
    {
        Logger = logger;
        _paramProtocol.SetLogger(logger);
    }

    protected virtual void InitFunctions()
    {
        for (var i = 0; i < NumDigitalInputs; i++)
            Inputs.Add(new Input(i + 1, "digitalInput" + (i + 1)));

        for (var i = 0; i < NumOutputs; i++)
            Outputs.Add(new Output(i + 1, "output" + (i + 1)));

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
        
        StarterDisable = new StarterDisable("starterDisable", NumOutputs);

        Wipers = new Wiper("wiper");
        
        for (var i = 0; i < NumKeypads; i++)
            Keypads.Add(new KeypadMaster(i + 1, "keypad" + (i + 1)));

        InitStatusSigs();
    }

    protected virtual void InitStatusSigs()
    {
        StatusSigs = new Dictionary<int, List<(DbcSignal Signal, Action<double> SetValue)>>();

        // Message 0: System status
        StatusSigs[0] = new List<(DbcSignal, Action<double>)>();
        for (var i = 0; i < NumDigitalInputs; i++)
        {
            var inputIndex = i;
            StatusSigs[0].Add((
                new DbcSignal { Name = $"Input{inputIndex + 1}.State", StartBit = i, Length = 1 },
                val => Inputs[inputIndex].State = val != 0
            ));
        }
        StatusSigs[0].AddRange(new List<(DbcSignal, Action<double>)>
        {
            (new DbcSignal { Name = "DeviceState", StartBit = 8, Length = 4 },
                val => DeviceState = (DeviceState)val),
            (new DbcSignal { Name = "PdmType", StartBit = 12, Length = 4 },
                val => PdmTypeOk = PdmType == (int)val),
            (new DbcSignal { Name = "TotalCurrent", StartBit = 16, Length = 16, Factor = 1.0, Unit = "A" },
                val => TotalCurrent = val),
            (new DbcSignal { Name = "BatteryVoltage", StartBit = 32, Length = 16, Factor = 0.1, Unit = "V" },
                val => BatteryVoltage = val),
            (new DbcSignal { Name = "BoardTemp", StartBit = 48, Length = 16, Factor = 0.1, Unit = "°C" },
                val => BoardTempC = Math.Round(val, 1))
        });

        // Message 1: Output currents 0-3
        StatusSigs[1] = [];
        for (var i = 0; i < 4 && i < NumOutputs; i++)
        {
            var outputIndex = i;
            StatusSigs[1].Add((
                new DbcSignal { Name = $"Output{outputIndex + 1}.Current", StartBit = i * 16, Length = 16, Factor = 1.0, Unit = "A" },
                val => Outputs[outputIndex].Current = val
            ));
        }

        // Message 2: Output currents 4-7
        StatusSigs[2] = [];
        for (var i = 4; i < NumOutputs; i++)
        {
            var outputIndex = i;
            StatusSigs[2].Add((
                new DbcSignal { Name = $"Output{outputIndex + 1}.Current", StartBit = (i - 4) * 16, Length = 16, Factor = 1.0, Unit = "A" },
                val => Outputs[outputIndex].Current = val
            ));
        }

        // Message 3: Output states, wiper, flashers
        StatusSigs[3] = [];
        for (var i = 0; i < NumOutputs; i++)
        {
            var outputIndex = i;
            StatusSigs[3].Add((
                new DbcSignal { Name = $"Output{outputIndex + 1}.State", StartBit = i * 4, Length = 4 },
                val => Outputs[outputIndex].State = (OutState)val
            ));
        }
        StatusSigs[3].AddRange(new List<(DbcSignal, Action<double>)>
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
            StatusSigs[3].Add((
                new DbcSignal { Name = $"Flasher{flasherIndex + 1}", StartBit = 48 + i, Length = 1 },
                val => Flashers[flasherIndex].Value = val != 0 && Flashers[flasherIndex].Enabled
            ));
        }

        // Message 4: Output reset counts
        StatusSigs[4] = [];
        for (var i = 0; i < NumOutputs; i++)
        {
            var outputIndex = i;
            StatusSigs[4].Add((
                new DbcSignal { Name = $"Output{outputIndex + 1}.ResetCount", StartBit = i * 8, Length = 8 },
                val => Outputs[outputIndex].ResetCount = (int)val
            ));
        }

        // Message 5: CAN inputs & virtual inputs
        StatusSigs[5] = [];
        for (var i = 0; i < NumCanInputs; i++)
        {
            var canInputIndex = i;
            StatusSigs[5].Add((
                new DbcSignal { Name = $"CanInput{canInputIndex + 1}", StartBit = i, Length = 1 },
                val => CanInputs[canInputIndex].Output = val != 0
            ));
        }
        for (var i = 0; i < NumVirtualInputs; i++)
        {
            var virtualInputIndex = i;
            StatusSigs[5].Add((
                new DbcSignal { Name = $"VirtualInput{virtualInputIndex + 1}", StartBit = 32 + i, Length = 1 },
                val => VirtualInputs[virtualInputIndex].Value = val != 0
            ));
        }

        // Message 6: Counters & conditions
        StatusSigs[6] = [];
        for (var i = 0; i < NumCounters; i++)
        {
            var counterIndex = i;
            StatusSigs[6].Add((
                new DbcSignal { Name = $"Counter{counterIndex + 1}", StartBit = i * 8, Length = 8 },
                val => Counters[counterIndex].Value = (int)val
            ));
        }
        for (var i = 0; i < NumConditions; i++)
        {
            var conditionIndex = i;
            StatusSigs[6].Add((
                new DbcSignal { Name = $"Condition{conditionIndex + 1}", StartBit = 32 + i, Length = 1 },
                val => Conditions[conditionIndex].Value = (int)val
            ));
        }

        // Messages 7-22: CAN input values (2 per message)
        for (var msg = 7; msg <= 22; msg++)
        {
            StatusSigs[msg] = [];
            for (var i = 0; i < 2; i++)
            {
                var canInputIndex = (msg - 7) * 2 + i;
                if (canInputIndex < NumCanInputs)
                {
                    StatusSigs[msg].Add((
                        new DbcSignal { Name = $"CanInput{canInputIndex + 1}.Value", StartBit = i * 32, Length = 32 },
                        val => CanInputs[canInputIndex].Value = (int)val
                    ));
                }
            }
        }

        // Message 23: Output duty cycles
        StatusSigs[23] = [];
        for (var i = 0; i < NumOutputs; i++)
        {
            var outputIndex = i;
            StatusSigs[23].Add((
                new DbcSignal { Name = $"Output{outputIndex + 1}.DutyCycle", StartBit = i * 8, Length = 8, Unit = "%" },
                val => Outputs[outputIndex].CurrentDutyCycle = val
            ));
        }
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
        
        if (NumDigitalInputs > 0)
        {
            for (var i = 0; i < NumDigitalInputs; i++)
            {
                var num = i;
                VarMap.Add(new DeviceVariable
                {
                    GetName  = () => Inputs[num].Name,
                    PropertyName = "State",
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
        
        if (NumOutputs > 0)
        {
            for (var i = 0; i < NumOutputs; i++)
            {
                var num = i;
                VarMap.Add(new DeviceVariable
                {
                    GetName = () => Outputs[num].Name,
                    PropertyName = "On",
                    DataType = "bool",
                    VariableIndex = index++,
                    SingleVariable = false
                });
                VarMap.Add(new DeviceVariable
                {
                    GetName = () => Outputs[num].Name,
                    PropertyName = "Current",
                    DataType = "float",
                    VariableIndex = index++,
                    SingleVariable = false
                });
                VarMap.Add(new DeviceVariable
                {
                    GetName = () => Outputs[num].Name,
                    PropertyName = "Overcurrent",
                    DataType = "bool",
                    VariableIndex = index++,
                    SingleVariable = false
                });
                VarMap.Add(new DeviceVariable
                {
                    GetName = () => Outputs[num].Name,
                    PropertyName = "Fault",
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
        
        VarMap.Add(new DeviceVariable
        {
            GetName = () => Wipers.Name,
            PropertyName = "Slow Output",
            DataType = "bool",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        VarMap.Add(new DeviceVariable
        {
            GetName = () => Wipers.Name,
            PropertyName = "Fast Output",
            DataType = "bool",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        VarMap.Add(new DeviceVariable
        {
            GetName = () => Wipers.Name,
            PropertyName = "Park Output",
            DataType = "bool",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        VarMap.Add(new DeviceVariable
        {
            GetName = () => Wipers.Name,
            PropertyName = "Inter Output",
            DataType = "bool",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        VarMap.Add(new DeviceVariable
        {
            GetName = () => Wipers.Name,
            PropertyName = "Wash Output",
            DataType = "bool",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        VarMap.Add(new DeviceVariable
        {
            GetName = () => Wipers.Name,
            PropertyName = "Swipe Output",
            DataType = "bool",
            VariableIndex = index++,
            SingleVariable = true
        });
        
        if (NumKeypads > 0)
        {
            for (var i = 0; i < NumKeypads; i++)
            {
                var kp = i;
                for (var j = 0; j < KeypadMaster.MaxButtons; j++)
                {
                    var num = j;
                    VarMap.Add(new DeviceVariable
                    {
                        GetName = () => $"{Keypads[kp].Name} - {Keypads[kp].Buttons[num].Name}",
                        PropertyName = "State",
                        DataType = "bool",
                        VariableIndex = index++,
                        SingleVariable = false
                    });
                }
                
                for (var j = 0; j < KeypadMaster.MaxDials; j++)
                {
                    var num = j;
                    VarMap.Add(new DeviceVariable
                    {
                        GetName = () => $"{Keypads[kp].Name} - {Keypads[kp].Dials[num].Name}",
                        PropertyName = "Position",
                        DataType = "int",
                        VariableIndex = index++,
                        SingleVariable = false
                    });
                }
                
                for (var j = 0; j < KeypadMaster.MaxAnalogInputs; j++)
                {
                    var num = j;
                    VarMap.Add(new DeviceVariable
                    {
                        GetName = () => $"{Keypads[kp].Name} - analogIn{num}",
                        PropertyName = "Value",
                        DataType = "float",
                        VariableIndex = index++,
                        SingleVariable = false
                    });
                }
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
                ParentName = Name, Name = "device.configVersion", Index = BaseIndex, SubIndex = subIndex++,
                GetValue = () => ConfigVersion, SetValue = val => ConfigVersion = (int)val,
                ValueType = ConfigVersion.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "device.baseId", Index = BaseIndex, SubIndex = subIndex++,
                GetValue = () => BaseId, SetValue = val => BaseId = (int)val,
                ValueType = BaseId.GetType(),
                DefaultValue = 0x7D0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "device.paramTxId", Index = BaseIndex, SubIndex = subIndex++,
                GetValue = () => ParamTxId, SetValue = val => ParamTxId = (int)val,
                ValueType = ParamTxId.GetType(),
                DefaultValue = 0x080
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "device.paramRxId", Index = BaseIndex, SubIndex = subIndex++,
                GetValue = () => ParamRxId, SetValue = val => ParamRxId = (int)val,
                ValueType = ParamRxId.GetType(),
                DefaultValue = 0x081
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
        
        foreach (var canOutput in CanOutputs) allParams.AddRange(canOutput.Params);
        foreach (var output in Outputs) allParams.AddRange(output.Params);
        foreach (var input in Inputs) allParams.AddRange(input.Params);
        foreach (var canInput in CanInputs) allParams.AddRange(canInput.Params);
        foreach (var virtualInput in VirtualInputs) allParams.AddRange(virtualInput.Params);
        foreach (var condition in Conditions) allParams.AddRange(condition.Params);
        foreach (var counter in Counters) allParams.AddRange(counter.Params);
        foreach (var flasher in Flashers) allParams.AddRange(flasher.Params);
        allParams.AddRange(StarterDisable.Params);
        allParams.AddRange(Wipers.Params);
        // Firmware groups all keypad base params first, then all buttons, then all dials
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
        return ((id >= BaseId - 1) && (id <= BaseId + 31)) || (id == ParamRxId);
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

            if (id == ParamRxId)
            {
                if (((MessageCommand)data[0]) == MessageCommand.Version)
                    ReadVersion(BaseId, Name, data, queue);
                    
                _paramProtocol.HandleMessage(BaseId, ParamTxId, Name, data, queue, outgoing);
            }
        }

        LastRxTime = DateTime.Now;
    }

    public IEnumerable<(int MessageId, DbcSignal Signal)> GetStatusSigs()
    {
        foreach (var kvp in StatusSigs)
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
        var id = BaseId;

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
                    Id: ParamTxId,
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
                    Id: ParamTxId,
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
                Id: ParamTxId,
                Len: 8,
                Payload: [Convert.ToByte(MessageCommand.CheckCrc), 0, 0, 0, 0, 0, 0, 0]
            ),
            Name = "Check"
        };
    }

    public List<DeviceCanFrame> GetModifyMsgs(int newId)
    {
        
        //Copy params:
        //ID: 0x0000, Subindex: 1, Base ID
        //ID: 0x0000, Subindex: 2, ParamTxId
        //ID: 0x0000, Subindex: 3, ParamRxId
        var modifyParams = Params.Where(p => p is { Index: 0x0000, SubIndex: 1 or 2 or 3 }).ToList();

        List<DeviceCanFrame> msgs = [];

        foreach (var parameter in modifyParams)
        {
            msgs.Add(new DeviceCanFrame
            {
                SendOnly = true,
                DeviceBaseId = newId,
                Frame = ParamCodec.ToFrame(MessageCommand.Write, parameter, ParamTxId),
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
                Id: ParamTxId,
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
                Id: ParamTxId,
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
                Id: ParamTxId,
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
                Id: ParamTxId,
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
                Id: ParamTxId,
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
        if (index > NumKeypads - 1) return false;
        
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
    public IReadOnlyList<Input> GetInputs() => Inputs.AsReadOnly();
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