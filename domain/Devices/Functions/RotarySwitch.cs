using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
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
    [JsonPropertyName("mode")] public RotaryMode Mode { get; set; } = RotaryMode.Uniform;
    [JsonPropertyName("numPos")] public int NumPos { get; set; } = 2;
    [JsonPropertyName("tolerance")] public int Tolerance { get; set; } = 200;
    [JsonPropertyName("centers")] public int[] Centers { get; set; } = new int[MaxCalibratedPos];

    [JsonIgnore] public const int MaxCalibratedPos = 10;
    // Calibrated mode reports this when the voltage is outside every window
    [JsonIgnore] public const int NoPos = 15;

    [JsonIgnore][Plotable(displayName:"Pos")] public int Pos { get; set; }
    
    [JsonIgnore] public List<DeviceParameter> Params { get; } = null!;

    public List<DeviceParameter> InitParams(ref int subIndex)
    {
        List<DeviceParameter> rotaryParams =
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
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"rotarySwitch[{Number}].mode", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Mode, SetValue = val => Mode = (RotaryMode)val,
                ValueType = Mode.GetType(),
                DefaultValue = RotaryMode.Uniform
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"rotarySwitch[{Number}].numPos", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => NumPos, SetValue = val => NumPos = (int)val,
                ValueType = NumPos.GetType(),
                DefaultValue = 2
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"rotarySwitch[{Number}].tolerance", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Tolerance, SetValue = val => Tolerance = (int)val,
                ValueType = Tolerance.GetType(),
                DefaultValue = 200
            }
        ];

        for (var k = 0; k < MaxCalibratedPos; k++)
        {
            var pos = k;
            rotaryParams.Add(new DeviceParameter
            {
                ParentName = Name, Name = $"rotarySwitch[{Number}].center[{pos}]", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Centers[pos], SetValue = val => Centers[pos] = (int)val,
                ValueType = typeof(int),
                DefaultValue = 0
            });
        }

        return rotaryParams;
    }
    
    public List<DeviceVariable> GetVarMap(ref int index)
    {
        List<DeviceVariable> varMap =
        [
            new()
            {
                GetName = () => Name,
                PropertyName = "Pos",
                DataType = "int",
                VariableIndex = index++,
                SingleVariable = false
            }

        ];

        return varMap;
    }
}