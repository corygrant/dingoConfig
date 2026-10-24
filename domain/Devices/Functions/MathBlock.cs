using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class MathBlock : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1C00;
    [JsonPropertyName("name")] public string Name {get; set; }
    [JsonPropertyName("number")] public int Number {get;}
    [JsonPropertyName("enabled")] public bool Enabled {get; set; }
    [JsonPropertyName("operation")] public MathOp Operation {get; set;} = MathOp.Add;
    [JsonPropertyName("inputA")] public int InputA { get; set; }
    [JsonPropertyName("inputB")] public int InputB { get; set; } // 0 = use ConstB
    [JsonPropertyName("constB")] public double ConstB {get; set;}
    [JsonPropertyName("factor")] public double Factor {get; set;} = 1.0;
    [JsonPropertyName("offset")] public double Offset {get; set;}

    [JsonIgnore][Plotable(displayName:"Value")] public double Value {get; set;}

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public MathBlock(int number, string name)
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
                ParentName = Name, Name = $"math[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"math[{Number}].operation", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Operation, SetValue = val => Operation = (MathOp)val,
                ValueType = Operation.GetType(),
                DefaultValue = MathOp.Add
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"math[{Number}].inputA", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => InputA, SetValue = val => InputA = (int)val,
                ValueType = InputA.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"math[{Number}].inputB", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => InputB, SetValue = val => InputB = (int)val,
                ValueType = InputB.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"math[{Number}].constB", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ConstB, SetValue = val => ConstB = (double)val,
                ValueType = ConstB.GetType(),
                DefaultValue = 0.0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"math[{Number}].factor", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Factor, SetValue = val => Factor = (double)val,
                ValueType = Factor.GetType(),
                DefaultValue = 1.0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"math[{Number}].offset", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Offset, SetValue = val => Offset = (double)val,
                ValueType = Offset.GetType(),
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
                DataType = "float",
                VariableIndex = index++,
                SingleVariable = false
            }
        ];

        return varMap;
    }
}
