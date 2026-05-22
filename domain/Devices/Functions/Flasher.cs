using System.Text.Json.Serialization;
using domain.Common;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class Flasher : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1700;
    [JsonPropertyName("name")] public string Name {get; set; }
    [JsonPropertyName("number")] public int Number {get;}
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("single")] public bool Single {get; set;}
    [JsonPropertyName("input")] public int Input {get; set;}
    [JsonPropertyName("onTime")] public int OnTime { get; set; } = 500;
    [JsonPropertyName("offTime")] public int OffTime { get; set; } = 500;

    [JsonIgnore][Plotable(displayName:"State")] public bool Value {get; set;}

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public Flasher(int number, string name)
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
                ParentName = Name, Name = $"flasher[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"flasher[{Number}].input", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Input, SetValue = val => Input = (int)val,
                ValueType = Input.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"flasher[{Number}].onTime", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => OnTime, SetValue = val => OnTime = (int)val,
                ValueType = OnTime.GetType(),
                DefaultValue = 500
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"flasher[{Number}].offTime", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => OffTime, SetValue = val => OffTime = (int)val,
                ValueType = OffTime.GetType(),
                DefaultValue = 500
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"flasher[{Number}].single", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Single, SetValue = val => Single = (bool)val,
                ValueType = Single.GetType(),
                DefaultValue = false
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