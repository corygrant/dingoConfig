using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class DigitalInput : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1200;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("number")] public int Number { get; }
    [JsonPropertyName("invert")] public bool Invert { get; set; }
    [JsonPropertyName("mode")] public InputMode Mode { get; set; } = InputMode.Momentary;
    [JsonPropertyName("debounceTime")] public int DebounceTime { get; set; } = 20;
    [JsonPropertyName("pull")] public InputPull Pull { get; set; } = InputPull.NoPull;

    [JsonIgnore][Plotable(displayName:"State")] public bool State { get; set; }

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public DigitalInput(int number, string name)
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
                ParentName = Name, Name = $"input[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"input[{Number}].mode", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Mode, SetValue = val => Mode = (InputMode)val,
                ValueType = Mode.GetType(),
                DefaultValue = InputMode.Momentary
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"input[{Number}].invert", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Invert, SetValue = val => Invert = (bool)val,
                ValueType = Invert.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"input[{Number}].debounceTime", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => DebounceTime, SetValue = val => DebounceTime = (int)val,
                ValueType = DebounceTime.GetType(),
                DefaultValue = 20
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"input[{Number}].pull", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Pull, SetValue = val => Pull = (InputPull)val,
                ValueType = Pull.GetType(),
                DefaultValue = InputPull.NoPull
            }
        ];
    }
}