using domain.Devices.Canboard;
using domain.Devices.dingoPdm;
using domain.Devices.dingoPdmMax;
using domain.Devices.Generic;

namespace application.Models;

/// <summary>
/// Configuration file model that contains separate lists for each device type
/// </summary>
public class ConfigFile
{
    public List<PdmDevice> PdmDevices { get; set; } = new();
    public List<PdmMaxDevice> PdmMaxDevices { get; set; } = new();
    public List<CanboardDevice> CanboardDevices { get; set; } = new();
    public List<DbcDevice> DbcDevices { get; set; } = new();
}
