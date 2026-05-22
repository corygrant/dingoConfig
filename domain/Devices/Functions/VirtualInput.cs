using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class VirtualInput : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1400;
    [JsonPropertyName("name")] public string Name {get; set; }
    [JsonPropertyName("number")] public int Number {get; }
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("not0")] public bool Not0 {get; set;}
    [JsonPropertyName("var0")] public int Var0 { get; set; }
    [JsonPropertyName("cond0")] public Conditional Cond0 { get; set; } = Conditional.And;
    [JsonPropertyName("not1")] public bool Not1 {get; set;}
    [JsonPropertyName("var1")] public int Var1 { get; set; }
    [JsonPropertyName("cond1")] public Conditional Cond1 { get; set; } = Conditional.And;
    [JsonPropertyName("not2")] public bool Not2 {get; set;}
    [JsonPropertyName("var2")] public int Var2 { get; set; }
    [JsonPropertyName("mode")] public InputMode Mode {get; set;} = InputMode.Momentary;

    [JsonIgnore][Plotable(displayName:"State")] public bool Value {get; set;}

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public VirtualInput(int number, string name)
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
                ParentName = Name, Name = $"virtualInput[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"virtualInput[{Number}].not0", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Not0, SetValue = val => Not0 = (bool)val,
                ValueType = Not0.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"virtualInput[{Number}].var0", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Var0, SetValue = val => Var0 = (int)val,
                ValueType = Var0.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"virtualInput[{Number}].cond0", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Cond0, SetValue = val => Cond0 = (Conditional)val,
                ValueType = Cond0.GetType(),
                DefaultValue = Conditional.And
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"virtualInput[{Number}].not1", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Not1, SetValue = val => Not1 = (bool)val,
                ValueType = Not1.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"virtualInput[{Number}].var1", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Var1, SetValue = val => Var1 = (int)val,
                ValueType = Var1.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"virtualInput[{Number}].cond1", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Cond1, SetValue = val => Cond1 = (Conditional)val,
                ValueType = Cond1.GetType(),
                DefaultValue = Conditional.And
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"virtualInput[{Number}].not2", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Not2, SetValue = val => Not2 = (bool)val,
                ValueType = Not2.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"virtualInput[{Number}].var2", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Var2, SetValue = val => Var2 = (int)val,
                ValueType = Var2.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"virtualInput[{Number}].mode", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Mode, SetValue = val => Mode = (InputMode)val,
                ValueType = Mode.GetType(),
                DefaultValue = InputMode.Momentary
            }
        ];
    }
    
    public List<DeviceVariable> GetVarMap(ref int index)
    {
        List<DeviceVariable> varMap =
        [
            new()
            {
                GetName = () => Name,
                PropertyName = "State",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = false
            }

        ];

        return varMap;
    }
}