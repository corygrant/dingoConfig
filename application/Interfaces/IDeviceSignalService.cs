using application.Models;

namespace application.Interfaces;

public interface IDeviceSignalService
{
    /// <summary>
    /// Build hierarchical signal tree from all configured devices
    /// </summary>
    DeviceSignalHierarchy BuildSignalHierarchy();

    /// <summary>
    /// Get signals from a specific device
    /// </summary>
    DeviceSignalNode? GetDeviceSignals(Guid deviceGuid);
}
