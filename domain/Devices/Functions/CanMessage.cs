using System.Text.Json.Serialization;
using domain.Common;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

/// <summary>
/// A whole CAN frame with a fixed, user defined payload, sent while an input is
/// true. A CAN output writes one variable into a bit field and cannot express an
/// arbitrary payload, which is what a command frame to another device usually
/// needs - for example the frame that tells a dashboard to shut down.
/// </summary>
public class CanMessage : IDeviceFunction
{
    public const int DataLength = 8;

    [JsonIgnore] public const int BaseIndex = 0x1C00;
    [JsonPropertyName("name")] public string Name {get; set; }
    [JsonPropertyName("number")] public int Number {get;}
    [JsonPropertyName("enabled")] public bool Enabled {get; set; }
    [JsonPropertyName("input")] public int Input { get; set; }
    [JsonPropertyName("ide")] public bool Ide { get; set; }

    [JsonPropertyName("id")]
    public int Id
    {
        get;
        set
        {
            field = value;
            Ide = (field > 2047);
        }
    }
    [JsonPropertyName("dlc")] public int Dlc { get; set; } = 8;
    [JsonPropertyName("data")] public List<int> Data { get; set; }
    // Gap between repeats while the input stays true. Receivers that debounce a
    // command over several seconds need the frame to keep arriving. 0 sends a
    // single frame on each rising edge instead.
    [JsonPropertyName("interval")] public int Interval {get; set;} = 100;

    [JsonIgnore][Plotable(displayName:"Sending")] public int Value {get; set;}

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public CanMessage(int number, string name, List<int> data)
    {
        Number = number;
        Name = name;
        Data = data ?? [];
        while (Data.Count < DataLength) Data.Add(0);
        Params = InitParams();
    }

    public CanMessage(int number, string name)
        : this(number, name, [..new int[DataLength]])
    {
    }

    private List<DeviceParameter> InitParams()
    {
        var subIndex = 0;
        var parameters = new List<DeviceParameter>
        {
            new DeviceParameter
            {
                ParentName = Name, Name = $"canMessage[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canMessage[{Number}].input", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Input, SetValue = val => Input = (int)val,
                ValueType = Input.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canMessage[{Number}].ide", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Ide, SetValue = val => Ide = (bool)val,
                ValueType = Ide.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canMessage[{Number}].id", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Id, SetValue = val => Id = (int)val,
                ValueType = Id.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"canMessage[{Number}].dlc", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Dlc, SetValue = val => Dlc = (int)val,
                ValueType = Dlc.GetType(),
                DefaultValue = 8
            }
        };

        for (var i = 0; i < DataLength; i++)
        {
            var index = i; // capture for the closures
            parameters.Add(new DeviceParameter
            {
                ParentName = Name, Name = $"canMessage[{Number}].data{index}", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Data[index], SetValue = val => Data[index] = (int)val,
                ValueType = Data[index].GetType(),
                DefaultValue = 0
            });
        }

        parameters.Add(new DeviceParameter
        {
            ParentName = Name, Name = $"canMessage[{Number}].interval", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
            GetValue = () => Interval, SetValue = val => Interval = (int)val,
            ValueType = Interval.GetType(),
            DefaultValue = 100
        });

        return parameters;
    }

    public List<DeviceVariable> GetVarMap(ref int index)
    {
        List<DeviceVariable> varMap =
        [
            new()
            {
                GetName = () => Name,
                PropertyName = "Sending",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = false
            }
        ];

        return varMap;
    }
}
