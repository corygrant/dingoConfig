using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

/// <summary>
/// Ignition and starter. Outputs are given a role instead of an input,
/// and the firmware runs the dash shutdown on its own.
/// </summary>
public class Ignition : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1B00;
    [JsonIgnore] public const int OutputRoleSubIndex = 0x30;
    [JsonIgnore] public const int ShutdownDataLength = 8;

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

    [JsonPropertyName("buttonSource")] public IgnitionSource ButtonSource {get; set;} = IgnitionSource.Variable;
    [JsonPropertyName("buttonIde")] public bool ButtonIde {get; set;}
    [JsonPropertyName("buttonId")]
    public int ButtonId
    {
        get;
        set
        {
            field = value;
            ButtonIde = field > 2047;
        }
    } = 0x6F0;
    [JsonPropertyName("buttonByte")] public int ButtonByte {get; set;}
    [JsonPropertyName("buttonMask")] public int ButtonMask {get; set;} = 0x01;
    // Without a frame for this long the button reads released, 0 = never
    [JsonPropertyName("buttonTimeout")] public int ButtonTimeout {get; set;} = 1000;

    [JsonPropertyName("shutdownEnabled")] public bool ShutdownEnabled {get; set;}
    [JsonPropertyName("shutdownIde")] public bool ShutdownIde {get; set;}
    [JsonPropertyName("shutdownId")]
    public int ShutdownId
    {
        get;
        set
        {
            field = value;
            ShutdownIde = field > 2047;
        }
    } = 0x5AA;
    [JsonPropertyName("shutdownDlc")] public int ShutdownDlc {get; set;} = 1;
    [JsonPropertyName("shutdownData")] public List<int> ShutdownData {get; set;}
    [JsonPropertyName("shutdownInterval")] public int ShutdownInterval {get; set;} = 500;
    [JsonPropertyName("graceTime")] public int GraceTime {get; set;} = 3000;
    [JsonPropertyName("dashOffDelay")] public int DashOffDelay {get; set;} = 5000;
    [JsonPropertyName("doorInput")] public int DoorInput {get; set;}
    [JsonPropertyName("doorOnTime")] public int DoorOnTime {get; set;} = 60000;

    [JsonPropertyName("outputRoles")] public List<IgnitionOutputRole> OutputRoles {get; set;}

    [JsonIgnore][Plotable(displayName:"Ignition")] public int IgnitionOut {get; set;}
    [JsonIgnore][Plotable(displayName:"Starter")] public int StarterOut {get; set;}
    [JsonIgnore][Plotable(displayName:"Accessory")] public int AccessoryOut {get; set;}
    [JsonIgnore][Plotable(displayName:"Dash")] public int DashOut {get; set;}
    [JsonIgnore][Plotable(displayName:"State")] public int State {get; set;}

    [JsonIgnore] public List<DeviceParameter> Params { get; private set; }

    [JsonConstructor]
    public Ignition(string name, List<int>? shutdownData, List<IgnitionOutputRole>? outputRoles)
    {
        Name = name;
        ShutdownData = shutdownData ?? [1];
        while (ShutdownData.Count < ShutdownDataLength) ShutdownData.Add(0);
        OutputRoles = outputRoles ?? [];
        Params = InitParams();
    }

    public Ignition(string name, int outputCount)
        : this(name, null, [..new IgnitionOutputRole[outputCount]])
    {
    }

    /// <summary>
    /// Project files saved before output roles existed, or for another device
    /// type, carry a different number of roles than the device has outputs.
    /// </summary>
    public void SetOutputCount(int outputCount)
    {
        if (OutputRoles.Count == outputCount)
            return;

        while (OutputRoles.Count < outputCount) OutputRoles.Add(IgnitionOutputRole.None);
        if (OutputRoles.Count > outputCount) OutputRoles.RemoveRange(outputCount, OutputRoles.Count - outputCount);
        Params = InitParams();
    }

    /// <summary>The role of a 1-based output, None while the ignition is disabled.</summary>
    public IgnitionOutputRole RoleOf(int outputNumber)
    {
        if (!Enabled || outputNumber < 1 || outputNumber > OutputRoles.Count)
            return IgnitionOutputRole.None;

        return OutputRoles[outputNumber - 1];
    }

    private List<DeviceParameter> InitParams()
    {
        var subIndex = 0;
        var parameters = new List<DeviceParameter>();

        void Add(string name, Func<object> get, Action<object> set, Type type, object defaultValue, int? sub = null)
        {
            parameters.Add(new DeviceParameter
            {
                ParentName = Name, Name = $"ignition.{name}", Index = BaseIndex + (Number - 1), SubIndex = sub ?? subIndex++,
                GetValue = get, SetValue = set,
                ValueType = type,
                DefaultValue = defaultValue
            });
        }

        Add("enabled", () => Enabled, v => Enabled = (bool)v, typeof(bool), false);
        Add("mode", () => Mode, v => Mode = (IgnitionMode)v, typeof(IgnitionMode), IgnitionMode.KeySwitch);
        Add("ignInput", () => IgnInput, v => IgnInput = (int)v, typeof(int), 0);
        Add("startInput", () => StartInput, v => StartInput = (int)v, typeof(int), 0);
        Add("engineRunInput", () => EngineRunInput, v => EngineRunInput = (int)v, typeof(int), 0);
        Add("stopInput", () => StopInput, v => StopInput = (int)v, typeof(int), 0);
        Add("maxCrankTime", () => MaxCrankTime, v => MaxCrankTime = (int)v, typeof(int), 10000);

        // The rest have fixed subindexes, numbered as in the firmware
        Add("buttonSource", () => ButtonSource, v => ButtonSource = (IgnitionSource)v, typeof(IgnitionSource), IgnitionSource.Variable, 9);
        Add("buttonIde", () => ButtonIde, v => ButtonIde = (bool)v, typeof(bool), false, 10);
        Add("buttonId", () => ButtonId, v => ButtonId = (int)v, typeof(int), 0x6F0, 11);
        Add("buttonByte", () => ButtonByte, v => ButtonByte = (int)v, typeof(int), 0, 12);
        Add("buttonMask", () => ButtonMask, v => ButtonMask = (int)v, typeof(int), 0x01, 13);
        Add("buttonTimeout", () => ButtonTimeout, v => ButtonTimeout = (int)v, typeof(int), 1000, 14);

        Add("shutdownEnabled", () => ShutdownEnabled, v => ShutdownEnabled = (bool)v, typeof(bool), false, 15);
        Add("shutdownIde", () => ShutdownIde, v => ShutdownIde = (bool)v, typeof(bool), false, 16);
        Add("shutdownId", () => ShutdownId, v => ShutdownId = (int)v, typeof(int), 0x5AA, 17);
        Add("shutdownDlc", () => ShutdownDlc, v => ShutdownDlc = (int)v, typeof(int), 1, 18);
        for (var i = 0; i < ShutdownDataLength; i++)
        {
            var idx = i;
            Add($"shutdownData[{i}]", () => ShutdownData[idx], v => ShutdownData[idx] = (int)v, typeof(int), i == 0 ? 1 : 0, 19 + i);
        }
        Add("shutdownInterval", () => ShutdownInterval, v => ShutdownInterval = (int)v, typeof(int), 500, 27);
        Add("graceTime", () => GraceTime, v => GraceTime = (int)v, typeof(int), 3000, 28);
        Add("dashOffDelay", () => DashOffDelay, v => DashOffDelay = (int)v, typeof(int), 5000, 29);
        Add("doorInput", () => DoorInput, v => DoorInput = (int)v, typeof(int), 0, 30);
        Add("doorOnTime", () => DoorOnTime, v => DoorOnTime = (int)v, typeof(int), 60000, 31);

        for (var i = 0; i < OutputRoles.Count; i++)
        {
            var idx = i;
            Add($"outputRoles[{i}]", () => OutputRoles[idx], v => OutputRoles[idx] = (IgnitionOutputRole)v,
                typeof(IgnitionOutputRole), IgnitionOutputRole.None, OutputRoleSubIndex + i);
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

    /// <summary>
    /// The firmware adds these after the CAN messages, so existing indexes stay put.
    /// </summary>
    public List<DeviceVariable> GetAppendedVarMap(ref int index)
    {
        List<DeviceVariable> varMap =
        [
            new()
            {
                GetName = () => Name,
                PropertyName = "Accessory",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = true
            },
            new()
            {
                GetName = () => Name,
                PropertyName = "Dash",
                DataType = "bool",
                VariableIndex = index++,
                SingleVariable = true
            }
        ];

        return varMap;
    }
}
