using domain.Devices.VehicleFunctions;
using MudBlazor;

namespace web.Components.Devices.FwDevice.Functions;

public static class VehicleFunctionIcons
{
    public static string Of(VehicleFunction fn) => fn switch
    {
        Headlights => Icons.Material.Outlined.Highlight,
        Taillights => Icons.Material.Outlined.WbTwilight,
        TurnSignals => Icons.Material.Outlined.SyncAlt,
        InteriorLight => Icons.Material.Outlined.Lightbulb,
        Horn => Icons.Material.Outlined.Campaign,
        Wipers => Icons.Material.Outlined.Water,
        BlowerFan => Icons.Material.Outlined.Air,
        Ecu => Icons.Material.Outlined.Memory,
        FuelPump => Icons.Material.Outlined.LocalGasStation,
        CoolantFan => Icons.Material.Outlined.ModeFanOff,
        _ => Icons.Material.Outlined.Extension
    };
}
