using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class Condition : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1500;
    [JsonPropertyName("name")] public string Name {get; set; }
    [JsonPropertyName("number")] public int Number {get;}
    [JsonPropertyName("enabled")] public bool Enabled {get; set; }
    [JsonPropertyName("input")] public int Input { get; set; }
    [JsonPropertyName("operator")] public Operator Operator {get; set;} = Operator.Equal;
    [JsonPropertyName("arg")] public double Arg {get; set;}

    [JsonIgnore][Plotable(displayName:"State")] public int Value {get; set;}

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public Condition(int number, string name)
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
                ParentName = Name, Name = $"condition[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"condition[{Number}].input", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Input, SetValue = val => Input = (int)val,
                ValueType = Input.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"condition[{Number}].operator", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Operator, SetValue = val => Operator = (Operator)val,
                ValueType = Operator.GetType(),
                DefaultValue = Operator.Equal
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"condition[{Number}].arg", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Arg, SetValue = val => Arg = (double)val,
                ValueType = Arg.GetType(),
                DefaultValue = 0.0
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
                PropertyName = "Value",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = false
            }

        ];

        return varMap;
    }
}