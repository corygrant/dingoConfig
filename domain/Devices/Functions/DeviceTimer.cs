using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class DeviceTimer : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1A00;
    [JsonPropertyName("name")] public string Name {get; set; }
    [JsonPropertyName("number")] public int Number {get;}
    [JsonPropertyName("enabled")] public bool Enabled {get; set; }
    [JsonPropertyName("input")] public int Input { get; set; }
    [JsonPropertyName("resetInput")] public int ResetInput { get; set; }
    [JsonPropertyName("mode")] public TimerMode Mode {get; set;} = TimerMode.OnDelay;
    [JsonPropertyName("time")] public int Time {get; set;} = 1000;

    [JsonIgnore][Plotable(displayName:"State")] public int Value {get; set;}

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public DeviceTimer(int number, string name)
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
                ParentName = Name, Name = $"timer[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"timer[{Number}].input", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Input, SetValue = val => Input = (int)val,
                ValueType = Input.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"timer[{Number}].resetInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ResetInput, SetValue = val => ResetInput = (int)val,
                ValueType = ResetInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"timer[{Number}].mode", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Mode, SetValue = val => Mode = (TimerMode)val,
                ValueType = Mode.GetType(),
                DefaultValue = TimerMode.OnDelay
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"timer[{Number}].time", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Time, SetValue = val => Time = (int)val,
                ValueType = Time.GetType(),
                DefaultValue = 1000
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
