using System.Text.Json.Serialization;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Functions.Keypad;

public class Button : IDeviceFunction
{
    [JsonIgnore] public const int BaseIndex = 0x3100;
    [JsonPropertyName("name")] public string Name {get; set; }
    [JsonPropertyName("keypadNumber")] public int KeypadNumber {get;}
    [JsonPropertyName("number")] public int Number {get;}
    [JsonPropertyName("enabled")] public bool Enabled {get; set;}
    [JsonPropertyName("mode")] public InputMode Mode {get; set;}
    [JsonPropertyName("valColors")] public int[] Colors {get; set;}
    [JsonPropertyName("faultColor")] public int FaultColor {get; set;}
    [JsonPropertyName("valVars")] public int[] Vars {get; set;}
    [JsonPropertyName("faultVar")] public int FaultVar {get; set;}
    [JsonPropertyName("valBlink")] public bool[] Blink {get; set;}
    [JsonPropertyName("faultBlink")] public bool FaultBlink {get; set;}
    [JsonPropertyName("blinkColors")] public int[] BlinkColors {get; set;}
    [JsonPropertyName("faultBlinkColor")] public int FaultBlinkColor {get; set;}
    [JsonIgnore] public List<DeviceParameter> Params { get; set; } = null!;
    
    [JsonConstructor]
    public Button(int keypadNumber, int number, string name)
    {
        KeypadNumber = keypadNumber;
        Number = number;
        Name = name;
        
        Colors = new int[4];
        Vars  = new int[4];
        BlinkColors = new int[4];
        Blink  = new bool[4];

        InitParams();
    }

    private void InitParams()
    {
        Params = new List<DeviceParameter>();
        var subIndex = 0;
        Params.AddRange(
        [
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].enabled", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Enabled, SetValue = val => Enabled = (bool)val,
                ValueType = Enabled.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].mode", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Mode, SetValue = val => Mode = (InputMode)val,
                ValueType = Mode.GetType(),
                DefaultValue = InputMode.Momentary
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valColor0", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Colors[0], SetValue = val => Colors[0] = (int)val,
                ValueType = Colors[0].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valColor1", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Colors[1], SetValue = val => Colors[1] = (int)val,
                ValueType = Colors[1].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valColor2", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Colors[2], SetValue = val => Colors[2] = (int)val,
                ValueType = Colors[2].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valColor3", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Colors[3], SetValue = val => Colors[3] = (int)val,
                ValueType = Colors[3].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].faultColor", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => FaultColor, SetValue = val => FaultColor = (int)val,
                ValueType = FaultColor.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valVar0", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Vars[0], SetValue = val => Vars[0] = (int)val,
                ValueType = Vars[0].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valVar1", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Vars[1], SetValue = val => Vars[1] = (int)val,
                ValueType = Vars[1].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valVar2", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Vars[2], SetValue = val => Vars[2] = (int)val,
                ValueType = Vars[2].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valVar3", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Vars[3], SetValue = val => Vars[3] = (int)val,
                ValueType = Vars[3].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].faultVar", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => FaultVar, SetValue = val => FaultVar = (int)val,
                ValueType = FaultVar.GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valBlink0", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Blink[0], SetValue = val => Blink[0] = (bool)val,
                ValueType = Blink[0].GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valBlink1", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Blink[1], SetValue = val => Blink[1] = (bool)val,
                ValueType = Blink[1].GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valBlink2", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Blink[2], SetValue = val => Blink[2] = (bool)val,
                ValueType = Blink[2].GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].valBlink3", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => Blink[3], SetValue = val => Blink[3] = (bool)val,
                ValueType = Blink[3].GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].faultBlink", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => FaultBlink, SetValue = val => FaultBlink = (bool)val,
                ValueType = FaultBlink.GetType(),
                DefaultValue = false
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].blinkColor0", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => BlinkColors[0], SetValue = val => BlinkColors[0] = (int)val,
                ValueType = BlinkColors[0].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].blinkColor1", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => BlinkColors[1], SetValue = val => BlinkColors[1] = (int)val,
                ValueType = BlinkColors[1].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].blinkColor2", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => BlinkColors[2], SetValue = val => BlinkColors[2] = (int)val,
                ValueType = BlinkColors[2].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].blinkColor3", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => BlinkColors[3], SetValue = val => BlinkColors[3] = (int)val,
                ValueType = BlinkColors[3].GetType(),
                DefaultValue = 0
            },
            new DeviceParameter
            {
                ParentName = Name, Name = $"keypad[{KeypadNumber}].button[{Number}].faultBlinkColor", 
                Index = BaseIndex + ((KeypadNumber - 1) * 32) + (Number - 1), SubIndex = subIndex++,
                GetValue = () => FaultBlinkColor, SetValue = val => FaultBlinkColor = (int)val,
                ValueType = FaultBlinkColor.GetType(),
                DefaultValue = 0
            },
        ]);
    }
}