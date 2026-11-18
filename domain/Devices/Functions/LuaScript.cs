using System.Text.Json.Serialization;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

// The stored Lua program and its outputs (setLuaOut slots), appended last to the firmware var map.
// No firmware params: the script goes over LuaProtocol, Enabled only shows the node in the flow editor.
public class LuaScript : IDeviceFunction
{
    [JsonPropertyName("name")] public string Name { get; set; } = "luaScript";
    [JsonIgnore] public int Number => 1;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("script")] public string Script { get; set; } = string.Empty;
    [JsonIgnore] public int NumOutputs { get => Values.Length; set => Values = new double[value]; }
    [JsonIgnore] public double[] Values { get; private set; } = []; // From the cyclic msgs, only sent once the script sets them
    [JsonIgnore] public List<DeviceParameter> Params { get; } = [];

    public List<DeviceVariable> GetVarMap(ref int index)
    {
        var vars = new List<DeviceVariable>();
        for (var i = 0; i < NumOutputs; i++)
        {
            vars.Add(new DeviceVariable
            {
                GetName = () => Name,
                PropertyName = $"Out {i + 1}",
                DataType = "any", // A script can output a bool or a number
                VariableIndex = index++
            });
        }
        return vars;
    }
}
