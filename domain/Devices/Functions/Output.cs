using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Enums.dingoPdm;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions;

public class Output : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x1000;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("number")] public int Number { get; }
    [JsonPropertyName("currentLimit")] public double CurrentLimit { get; set; } = 20.0;
    [JsonPropertyName("resetCountLimit")] public int ResetCountLimit { get; set; } = 3;
    [JsonPropertyName("resetMode")] public ResetMode ResetMode { get; set; } = ResetMode.None;
    [JsonPropertyName("resetTime")] public int ResetTime { get; set; } = 1000;

    [JsonPropertyName("inrushCurrentLimit")]
    public double InrushCurrentLimit { get; set; } = 50.0;

    [JsonPropertyName("inrushTime")] public int InrushTime { get; set; } = 1000;
    [JsonPropertyName("input")] public int Input { get; set; }
    [JsonPropertyName("pwmEnabled")] public bool PwmEnabled { get; set; }
    [JsonPropertyName("softStartEnabled")] public bool SoftStartEnabled { get; set; }
    [JsonPropertyName("variableDutyCycle")] public bool VariableDutyCycle { get; set; }
    [JsonPropertyName("dutyCycleInput")] public int DutyCycleInput { get; set; }
    [JsonPropertyName("fixedDutyCycle")] public int FixedDutyCycle { get; set; } = 100;
    [JsonPropertyName("frequency")] public int Frequency { get; set; } = 100;
    [JsonPropertyName("softStartRampTime")] public int SoftStartRampTime { get; set; }

    [JsonPropertyName("dutyCycleDenominator")]
    public int DutyCycleDenominator { get; set; } = 100;
    [JsonPropertyName("primaryOutput")] public int PrimaryOutput { get; set; } = -1; //-1 = pairing disabled
    
    [JsonIgnore][Plotable(displayName:"Current", unit:"A")] public double Current { get; set; }
    [JsonIgnore][Plotable(displayName:"State")] public OutState State { get; set; }
    [JsonIgnore][Plotable(displayName:"ResetCount")] public int ResetCount { get; set; }
    [JsonIgnore][Plotable(displayName:"DutyCycle", unit:"%")] public double CurrentDutyCycle
    {
        get;
        set
        {
            field = value;
            CalculatedCurrent = (field / 100.0) * Current;
        }
    }
    [JsonIgnore][Plotable(displayName:"CalcCurrent", unit:"A")] public double CalculatedCurrent { get; private set; }

    [JsonIgnore] public List<DeviceParameter> Params { get; }

    [JsonConstructor]
    public Output(int number, string name)
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
                ParentName = Name, Name = $"output[{Number}].enabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].input", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Input, SetValue = val => Input = (int)val,
                ValueType = Input.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].currentLimit", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => CurrentLimit, SetValue = val => CurrentLimit = (double)val,
                ValueType = CurrentLimit.GetType(),
                DefaultValue = 20.0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].inrushCurrentLimit", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => InrushCurrentLimit, SetValue = val => InrushCurrentLimit = (double)val,
                ValueType = InrushCurrentLimit.GetType(),
                DefaultValue = 50.0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].inrushTime", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => InrushTime, SetValue = val => InrushTime = (int)val,
                ValueType = InrushTime.GetType(),
                DefaultValue = 1000
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].resetMode", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ResetMode, SetValue = val => ResetMode = (ResetMode)val,
                ValueType = ResetMode.GetType(),
                DefaultValue = ResetMode.None
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].resetTime", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ResetTime, SetValue = val => ResetTime = (int)val,
                ValueType = ResetTime.GetType(),
                DefaultValue = 1000
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].resetCountLimit", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => ResetCountLimit, SetValue = val => ResetCountLimit = (int)val,
                ValueType = ResetCountLimit.GetType(),
                DefaultValue = 3
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].pwmEnabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => PwmEnabled, SetValue = val => PwmEnabled = (bool)val,
                ValueType = PwmEnabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].softStartEnabled", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => SoftStartEnabled, SetValue = val => SoftStartEnabled = (bool)val,
                ValueType = SoftStartEnabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].variableDutyCycle", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => VariableDutyCycle, SetValue = val => VariableDutyCycle = (bool)val,
                ValueType = VariableDutyCycle.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].dutyCycleInput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => DutyCycleInput, SetValue = val => DutyCycleInput = (int)val,
                ValueType = DutyCycleInput.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].fixedDutyCycle", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => FixedDutyCycle, SetValue = val => FixedDutyCycle = (int)val,
                ValueType = FixedDutyCycle.GetType(),
                DefaultValue = 100
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].frequency", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Frequency, SetValue = val => Frequency = (int)val,
                ValueType = Frequency.GetType(),
                DefaultValue = 100
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].softStartRampTime", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => SoftStartRampTime, SetValue = val => SoftStartRampTime = (int)val,
                ValueType = SoftStartRampTime.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].dutyCycleDenominator", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => DutyCycleDenominator, SetValue = val => DutyCycleDenominator = (int)val,
                ValueType = DutyCycleDenominator.GetType(),
                DefaultValue = 100
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"output[{Number}].primaryOutput", Index = BaseIndex + (Number - 1), SubIndex = subIndex++,
                GetValue = () => PrimaryOutput, SetValue = val => PrimaryOutput = (int)val,
                ValueType = PrimaryOutput.GetType(),
                DefaultValue = -1,
                IsSignedInt = true
            }
        ];
    }
}