using System.Text.Json.Serialization;
using domain.Common;
using domain.Devices.dingoPdm.Enums;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using ByteOrder = domain.Enums.ByteOrder;

namespace domain.Devices.dingoPdm.Functions;

public class CanInput : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1300;
    [JsonPropertyName("name")] public string Name {get; set; }
    [JsonPropertyName("number")] public int Number {get;}
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("timeoutEnabled")] public bool TimeoutEnabled {get; set;}
    [JsonPropertyName("timeout")] public int Timeout { get; set; } = 1000;
    [JsonPropertyName("ide")] public bool Ide {get; set;}
    [JsonPropertyName("startBit")] public int StartBit {get; set;}
    [JsonPropertyName("bitLength")] public int BitLength { get; set; } = 8;
    [JsonPropertyName("factor")] public double Factor { get; set; } = 1.0;
    [JsonPropertyName("offset")] public double Offset {get; set;}
    [JsonPropertyName("byteOrder")] public ByteOrder ByteOrder { get; set; } = ByteOrder.LittleEndian;
    [JsonPropertyName("signed")] public bool Signed {get; set;}
    [JsonPropertyName("operator")] public Operator Operator { get; set; } = Operator.Equal;
    [JsonPropertyName("operand")] public double Operand {get; set;}
    [JsonPropertyName("mode")] public InputMode Mode { get; set; } = InputMode.Momentary;

    [JsonPropertyName("id")]
    public int Id
    {
        get;
        set
        {
            field = value;
            Ide = (field > 2047);
        }
    }

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonIgnore][Plotable(displayName:"State")] public bool Output { get; set; }
    [JsonIgnore][Plotable(displayName:"Value")] public int Value {get; set;}
    

    [JsonConstructor]
    public CanInput(int number, string name)
    {
        Number = number;
        Name = name;

        Params = InitParams();
    }

    private List<DeviceParameter> InitParams()
    {
        var subIndex = 0;
        return
        [
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].timeoutEnabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => TimeoutEnabled, SetValue = val => TimeoutEnabled = (bool)val,
                ValueType = TimeoutEnabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].timeout", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Timeout, SetValue = val => Timeout = (int)val,
                ValueType = Timeout.GetType(),
                DefaultValue = 1000
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].ide", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Ide, SetValue = val => Ide = (bool)val,
                ValueType = Ide.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].sid", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Id, SetValue = val => Id = (int)val,
                ValueType = Id.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].startBit", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => StartBit, SetValue = val => StartBit = (int)val,
                ValueType = StartBit.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].bitLength", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => BitLength, SetValue = val => BitLength = (int)val,
                ValueType = BitLength.GetType(),
                DefaultValue = 8
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].factor", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Factor, SetValue = val => Factor = (double)val,
                ValueType = Factor.GetType(),
                DefaultValue = 1.0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].offset", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Offset, SetValue = val => Offset = (double)val,
                ValueType = Offset.GetType(),
                DefaultValue = 0.0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].byteOrder", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ByteOrder, SetValue = val => ByteOrder = (ByteOrder)val,
                ValueType = ByteOrder.GetType(),
                DefaultValue = ByteOrder.LittleEndian
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].signed", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Signed, SetValue = val => Signed = (bool)val,
                ValueType = Signed.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].operator", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Operator, SetValue = val => Operator = (Operator)val,
                ValueType = Operator.GetType(),
                DefaultValue = Operator.Equal
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].operand", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Operand, SetValue = val => Operand = (double)val,
                ValueType = Operand.GetType(),
                DefaultValue = 0.0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canInput[{Number}].mode", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Mode, SetValue = val => Mode = (InputMode)val,
                ValueType = Mode.GetType(),
                DefaultValue = InputMode.Momentary
            }
        ];
    }
}