using System.Text.Json.Serialization;
using domain.Devices.Keypad.Enums;
using MudBlazor;

namespace domain.Devices.Keypad.Grayhill.Functions;

public class Button(int number, string name)
{
    [JsonPropertyName("number")] public int Number { get; } = number;
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonPropertyName("icon")] public string Icon { get; set; } = Icons.Material.Outlined.Circle;
    [JsonPropertyName("mode")] public ButtonMode Mode { get; set; }
    [JsonIgnore] public bool State { get; set; }
    [JsonIgnore] public bool[] Led { get; set; } = new bool[3];
}