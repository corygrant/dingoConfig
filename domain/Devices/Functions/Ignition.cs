using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class Ignition : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1B00;
    [JsonPropertyName("name")] public string Name {get; set;}
    [JsonIgnore] public int Number => 1;
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("mode")] public IgnitionMode Mode {get; set;} = IgnitionMode.KeySwitch;

    // KeySwitch: the maintained ON position. StartButton: the button itself.
    [JsonPropertyName("ignInput")] public int IgnInput {get; set;}
    // KeySwitch: the momentary START position. StartButton: the start condition.
    [JsonPropertyName("startInput")] public int StartInput {get; set;}
    [JsonPropertyName("engineRunInput")] public int EngineRunInput {get; set;}
    [JsonPropertyName("stopInput")] public int StopInput {get; set;}
    [JsonPropertyName("maxCrankTime")] public int MaxCrankTime {get; set;} = 10000;

    [JsonIgnore][Plotable(displayName:"Ignition")] public int IgnitionOut {get; set;}
    [JsonIgnore][Plotable(displayName:"Starter")] public int StarterOut {get; set;}
    [JsonIgnore][Plotable(displayName:"State")] public int State {get; set;}

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public Ignition(string name)
    {
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
                ParentName = Name, Name = "ignition.enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "ignition.mode", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Mode, SetValue = val => Mode = (IgnitionMode)val,
                ValueType = Mode.GetType(),
                DefaultValue = IgnitionMode.KeySwitch
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "ignition.ignInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => IgnInput, SetValue = val => IgnInput = (int)val,
                ValueType = IgnInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "ignition.startInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => StartInput, SetValue = val => StartInput = (int)val,
                ValueType = StartInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "ignition.engineRunInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => EngineRunInput, SetValue = val => EngineRunInput = (int)val,
                ValueType = EngineRunInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "ignition.stopInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => StopInput, SetValue = val => StopInput = (int)val,
                ValueType = StopInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = "ignition.maxCrankTime", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => MaxCrankTime, SetValue = val => MaxCrankTime = (int)val,
                ValueType = MaxCrankTime.GetType(),
                DefaultValue = 10000
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
                PropertyName = "Ignition",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = true
            },
            new()
            {
                GetName = () => Name,
                PropertyName = "Starter",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = true
            },
            new()
            {
                GetName = () => Name,
                PropertyName = "State",
                DataType = "int",
                VariableIndex = index++,
                SingleVariable = true
            }
        ];

        return varMap;
    }
}
