using application.Interfaces;
using application.Models;
using domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace application.Services;

public class DeviceSignalService(
    DeviceManager deviceManager,
    ILogger<DeviceSignalService> logger) : IDeviceSignalService
{
    public DeviceSignalHierarchy BuildSignalHierarchy()
    {
        var hierarchy = new DeviceSignalHierarchy();
        var allDevices = deviceManager.GetAllDevices();

        foreach (var device in allDevices)
        {
            try
            {
                var deviceNode = BuildDeviceNode(device);
                if (deviceNode.Messages.Any())
                {
                    hierarchy.Devices.Add(deviceNode);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error building signal hierarchy for device {Name}", device.Name);
            }
        }

        logger.LogDebug("Built signal hierarchy with {Count} devices", hierarchy.Devices.Count);
        return hierarchy;
    }

    public DeviceSignalNode? GetDeviceSignals(Guid deviceGuid)
    {
        var device = deviceManager.GetDevice(deviceGuid);
        if (device == null) return null;

        return BuildDeviceNode(device);
    }

    private DeviceSignalNode BuildDeviceNode(IDevice device)
    {
        var deviceNode = new DeviceSignalNode
        {
            DeviceGuid = device.Guid,
            DeviceName = device.Name,
            DeviceType = device.Type
        };

        // Group signals by message ID
        var signalsByMessage = device.GetStatusSignals()
            .GroupBy(tuple => tuple.MessageId)
            .OrderBy(g => g.Key);

        foreach (var messageGroup in signalsByMessage)
        {
            var messageNode = new MessageSignalNode
            {
                MessageId = messageGroup.Key,
                Signals = messageGroup
                    .Select(tuple => new SignalNode
                    {
                        SourceDeviceGuid = device.Guid,
                        Signal = tuple.Signal
                    })
                    .OrderBy(s => s.Signal.StartBit)
                    .ToList()
            };

            deviceNode.Messages.Add(messageNode);
        }

        return deviceNode;
    }
}
