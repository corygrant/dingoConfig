using System.Text.Json.Serialization;
using domain.Devices.Keypad.BlinkMarine.Enums;
using domain.Devices.Keypad.Enums;

namespace domain.Devices.Keypad.BlinkMarine.Functions;

public class Button(int number, string name)
{
    [JsonPropertyName("number")] public int Number { get; } = number;
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonPropertyName("icon")] public string Icon { get; set; } = "";
    [JsonPropertyName("mode")] public ButtonMode Mode { get; set; } //For sim
    [JsonIgnore] public bool State { get; set; }
    
    [JsonIgnore] public ButtonColor IndicatorColor { get; set; }
    [JsonIgnore] public ButtonColor BlinkColor { get; set; }
    [JsonIgnore] public int IndicatorBrightness { get; set; }
}