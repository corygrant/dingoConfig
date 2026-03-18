using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class Counter : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1600;
    [JsonPropertyName("name")] public string Name {get; set; }
    [JsonPropertyName("number")] public int Number {get;}
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("incInput")] public int IncInput { get; set; }
    [JsonPropertyName("decInput")] public int DecInput { get; set; }
    [JsonPropertyName("resetInput")] public  int ResetInput { get; set; }
    [JsonPropertyName("minCount")] public int  MinCount {get; set;}
    [JsonPropertyName("maxCount")] public int MaxCount { get; set; } = 10;
    [JsonPropertyName("incEdge")] public InputEdge IncEdge {get; set;} =  InputEdge.Rising;
    [JsonPropertyName("decEdge")] public InputEdge DecEdge {get; set;} =  InputEdge.Rising;
    [JsonPropertyName("resetEdge")] public InputEdge ResetEdge {get; set;} = InputEdge.Rising;
    [JsonPropertyName("wrapAround")] public bool WrapAround {get; set;}
    [JsonPropertyName("holdToReset")] public bool HoldToReset {get; set;}
    [JsonPropertyName("resetTime")] public int ResetTime { get; set; } = 2000;
    
    [JsonIgnore][Plotable(displayName:"State")] public int Value {get; set;}

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public Counter(int number, string name)
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
                ParentName = Name, Name = $"counter[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].incInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => IncInput, SetValue = val => IncInput = (int)val,
                ValueType = IncInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].decInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => DecInput, SetValue = val => DecInput = (int)val,
                ValueType = DecInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].resetInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ResetInput, SetValue = val => ResetInput = (int)val,
                ValueType = ResetInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].minCount", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => MinCount, SetValue = val => MinCount = (int)val,
                ValueType = MinCount.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].maxCount", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => MaxCount, SetValue = val => MaxCount = (int)val,
                ValueType = MaxCount.GetType(),
                DefaultValue = 10
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].incEdge", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => IncEdge, SetValue = val => IncEdge = (InputEdge)val,
                ValueType = IncEdge.GetType(),
                DefaultValue = InputEdge.Rising
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].decEdge", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => DecEdge, SetValue = val => DecEdge = (InputEdge)val,
                ValueType = DecEdge.GetType(),
                DefaultValue = InputEdge.Rising
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].resetEdge", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ResetEdge, SetValue = val => ResetEdge = (InputEdge)val,
                ValueType = ResetEdge.GetType(),
                DefaultValue = InputEdge.Rising
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].wrapAround", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => WrapAround, SetValue = val => WrapAround = (bool)val,
                ValueType = WrapAround.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].holdToReset", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => HoldToReset, SetValue = val => HoldToReset = (bool)val,
                ValueType = HoldToReset.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"counter[{Number}].resetTime", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ResetTime, SetValue = val => ResetTime = (int)val,
                ValueType = ResetTime.GetType(),
                DefaultValue = 2000
            }
        ];
    }
}