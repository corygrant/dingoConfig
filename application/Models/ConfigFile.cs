using domain.Devices;
using domain.Devices.Generic;
using domain.Devices.Keypad.BlinkMarine;
using domain.Devices.Keypad.Grayhill;

namespace application.Models;

/// <summary>
/// Configuration file model that contains separate lists for each device type
/// </summary>
public class ConfigFile
{
    public List<FwDevice> Devices { get; set; } = new();
    public List<DbcDevice> DbcDevices { get; set; } = new();
    public List<BlinkMarineKeypadDevice> BlinkMarineKeypads { get; set; } = new();
    public List<GrayhillKeypadDevice> GrayhillKeypads { get; set; } = new();
}
