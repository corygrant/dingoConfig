using System.Text.Json.Serialization;
using domain.Common;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class DigitalOutput : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x2100;
    [JsonPropertyName("name")] public string Name {get; set; }
    [JsonPropertyName("number")] public int Number {get;}
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("input")] public int Input { get; set; }

    [JsonIgnore][Plotable(displayName:"State")] public bool State { get; set; }
    
    [JsonIgnore] public List<DeviceParameter> Params { get; }
    
    [JsonConstructor]
    public DigitalOutput(int number, string name)
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
                ParentName = Name, Name = $"digitalOutput[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"digitalOutput[{Number}].input", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Input, SetValue = val => Input = (int)val,
                ValueType = Input.GetType(),
                DefaultValue = 0
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