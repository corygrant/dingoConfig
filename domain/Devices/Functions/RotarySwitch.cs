using System.Text.Json.Serialization;
using domain.Common;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class RotarySwitch(int number, string name) : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x2200;
    [JsonPropertyName("number")] public int Number { get; set; } = number;
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("invert")] public bool Invert { get; set; }
    [JsonPropertyName("fOffset")] public double Offset { get; set; }
    [JsonPropertyName("fStep")] public double Step { get; set; }
    [JsonPropertyName("fMaxPos")] public double MaxPos { get; set; }
    
    [JsonIgnore][Plotable(displayName:"Pos")] public int Pos { get; set; }
    
    [JsonIgnore] public List<DeviceParameter> Params { get; } = null!;

    public List<DeviceParameter> InitParams(ref int subIndex)
    {
        return
        [
            new DeviceParameter
            {
                ParentName = Name, Name = $"rotarySwitch[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"rotarySwitch[{Number}].invert", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Invert, SetValue = val => Invert = (bool)val,
                ValueType = Invert.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"rotarySwitch[{Number}].offset", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Offset, SetValue = val => Offset = (double)val,
                ValueType = Offset.GetType(),
                DefaultValue = 0.0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"rotarySwitch[{Number}].step", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Step, SetValue = val => Step = (double)val,
                ValueType = Step.GetType(),
                DefaultValue = 100.0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"rotarySwitch[{Number}].maxpos", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => MaxPos, SetValue = val => MaxPos = (double)val,
                ValueType = MaxPos.GetType(),
                DefaultValue = 10.0
            }
        ];
    }
}