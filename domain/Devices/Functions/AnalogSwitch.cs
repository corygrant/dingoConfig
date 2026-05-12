using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

[method: JsonConstructor]
public class AnalogSwitch(int number, string name) : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x2200;
    [JsonPropertyName("number")] public int Number { get; set; } = number;
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("mode")] public InputMode Mode { get; set; } = InputMode.Momentary;
    [JsonPropertyName("invert")] public bool Invert { get; set; }
    [JsonPropertyName("threshold")] public int Threshold { get; set; }
    
    [JsonIgnore][Plotable(displayName:"InState")] public bool State { get; set; }
    
    [JsonIgnore] public List<DeviceParameter> Params { get; } = null!;

    public List<DeviceParameter> InitParams(ref int subIndex)
    {
        return
        [
            new DeviceParameter
            {
                ParentName = Name, Name = $"analogSwitch[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"analogSwitch[{Number}].mode", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Mode, SetValue = val => Mode = (InputMode)val,
                ValueType = Mode.GetType(),
                DefaultValue = InputMode.Momentary
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"analogSwitch[{Number}].invert", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Invert, SetValue = val => Invert = (bool)val,
                ValueType = Invert.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"analogSwitch[{Number}].threshold", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Threshold, SetValue = val => Threshold = (int)val,
                ValueType = Threshold.GetType(),
                DefaultValue = 2000
            }
        ];
    }
}