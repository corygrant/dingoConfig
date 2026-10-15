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
        _ => Icons.Material.Outlined.Extension
    };
}
