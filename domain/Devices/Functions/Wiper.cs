using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class Wiper : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1900;
    [JsonPropertyName("name")] public string Name {get; set;}
    [JsonIgnore] public int Number => 1; // Singleton function
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("mode")] public WiperMode Mode { get; set; }
    [JsonPropertyName("slowInput")] public int SlowInput { get; set; }
    [JsonPropertyName("fastInput")] public int FastInput { get; set; }
    [JsonPropertyName("interInput")] public int InterInput { get; set; }
    [JsonPropertyName("onInput")] public int OnInput { get; set; }
    [JsonPropertyName("speedInput")] public int SpeedInput { get; set; }
    [JsonPropertyName("parkInput")] public int ParkInput { get; set; }
    [JsonPropertyName("parkStopLevel")] public bool ParkStopLevel { get; set; }
    [JsonPropertyName("swipeInput")] public int SwipeInput { get; set; }
    [JsonPropertyName("washInput")] public int WashInput { get; set; }
    [JsonPropertyName("washWipeCycles")] public int WashWipeCycles { get; set; } = 3;
    [JsonPropertyName("speedMap")] public WiperSpeed[] SpeedMap { get; set; } = 
        [WiperSpeed.Inter1, WiperSpeed.Inter2, WiperSpeed.Inter3, WiperSpeed.Inter4, 
            WiperSpeed.Inter5, WiperSpeed.Inter6, WiperSpeed.Slow, WiperSpeed.Fast];
    [JsonPropertyName("intermitTime")] public int[] IntermitTime { get; set; } = [1000, 2000, 3000, 4000, 5000, 6000];
    [JsonIgnore][Plotable(displayName:"SlowState")] public bool SlowState { get; set; }
    [JsonIgnore][Plotable(displayName:"FastState")] public bool FastState { get; set; }
    [JsonIgnore][Plotable(displayName:"State")] public WiperState State { get; set; }
    [JsonIgnore][Plotable(displayName:"Speed")] public WiperSpeed Speed { get; set; }

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public Wiper(string name)
    {
        Name = name;
        Params = InitParams();
    }

    private List<DeviceParameter> InitParams()
    {
        var subIndex = 0;
        var parameters = new List<DeviceParameter>
        {
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.mode", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Mode, SetValue = val => Mode = (WiperMode)val,
                ValueType = Mode.GetType(),
                DefaultValue = WiperMode.DigIn
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.slowInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => SlowInput, SetValue = val => SlowInput = (int)val,
                ValueType = SlowInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.fastInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => FastInput, SetValue = val => FastInput = (int)val,
                ValueType = FastInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.interInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => InterInput, SetValue = val => InterInput = (int)val,
                ValueType = InterInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.onInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => OnInput, SetValue = val => OnInput = (int)val,
                ValueType = OnInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.speedInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => SpeedInput, SetValue = val => SpeedInput = (int)val,
                ValueType = SpeedInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.parkInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ParkInput, SetValue = val => ParkInput = (int)val,
                ValueType = ParkInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.parkStopLevel", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ParkStopLevel, SetValue = val => ParkStopLevel = (bool)val,
                ValueType = ParkStopLevel.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.swipeInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => SwipeInput, SetValue = val => SwipeInput = (int)val,
                ValueType = SwipeInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.washInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => WashInput, SetValue = val => WashInput = (int)val,
                ValueType = WashInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "wiper.washWipeCycles", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => WashWipeCycles, SetValue = val => WashWipeCycles = (int)val,
                ValueType = WashWipeCycles.GetType(),
                DefaultValue = 3
            }
        };

        WiperSpeed[] speedMapDefaults = [WiperSpeed.Inter1, WiperSpeed.Inter2, WiperSpeed.Inter3, WiperSpeed.Inter4, WiperSpeed.Inter5, WiperSpeed.Inter6, WiperSpeed.Slow, WiperSpeed.Fast];
        for (var i = 0; i < 8; i++)
        {
            var idx = i;
            parameters.Add(new DeviceParameter
            {
                ParentName = Name, Name = $"wiper.speedMap[{i}]", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => SpeedMap[idx], SetValue = val => SpeedMap[idx] = (WiperSpeed)val,
                ValueType = SpeedMap[idx].GetType(),
                DefaultValue = speedMapDefaults[idx]
            });
        }

        int[] intermitDefaults = [1000, 2000, 3000, 4000, 5000, 6000];
        for (var i = 0; i < 6; i++)
        {
            var idx = i;
            parameters.Add(new DeviceParameter
            {
                ParentName = Name, Name = $"wiper.intermitTime[{i}]", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => IntermitTime[idx], SetValue = val => IntermitTime[idx] = (int)val,
                ValueType = IntermitTime[idx].GetType(),
                DefaultValue = intermitDefaults[idx]
            });
        }

        return parameters;
    }
    
    public List<DeviceVariable> GetVarMap(ref int index)
    {
        List<DeviceVariable> varMap =
        [
            new()
            {
                GetName = () => Name,
                PropertyName = "Slow Output",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = true
            },
            new()
            {
                GetName = () => Name,
                PropertyName = "Fast Output",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = true
            },
            new()
            {
                GetName = () => Name,
                PropertyName = "Park Output",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = true
            },
            new()
            {
                GetName = () => Name,
                PropertyName = "Inter Output",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = true
            },
            new()
            {
                GetName = () => Name,
                PropertyName = "Wash Output",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = true
            },
            new()
            {
                GetName = () => Name,
                PropertyName = "Swipe Output",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = true
            }

        ];

        return varMap;
    }
}