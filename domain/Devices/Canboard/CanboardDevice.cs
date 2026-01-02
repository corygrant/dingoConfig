using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace domain.Devices.CanboardDevice;

public class CanboardDevice : IDevice
{
    private readonly ILogger<CanboardDevice> _logger;

    public Guid Guid { get; }
    [JsonIgnore] public string Type => "CANBoard";
    public string Name { get; set; }
    public int BaseId { get; set; }
    public DateTime LastRxTime { get; set; }
    public bool Connected { get; set; }

    public CanboardDevice(ILogger<CanboardDevice> logger, string name, int baseId)
    {
        _logger = logger;
        Guid = Guid.NewGuid();
        Name = name;
        BaseId = baseId;
    }

    public void UpdateIsConnected()
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        // TODO: Implement state clearing
    }

    public bool InIdRange(int id)
    {
        // TODO: Implement CANBoard ID range checking
        return false;
    }

    public void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> queue)
    {
        // TODO: Implement CANBoard message parsing
    }

    public List<DeviceCanFrame> GetReadMsgs()
    {
        // TODO: Implement CANBoard upload messages
        return [];
    }

    public List<DeviceCanFrame> GetWriteMsgs()
    {
        // TODO: Implement CANBoard download messages
        return [];
    }

    public DeviceCanFrame GetBurnMsg()
    {
        // TODO: Implement CANBoard burn message
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetSleepMsg()
    {
        // TODO: Implement CANBoard sleep message
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetVersionMsg()
    {
        // TODO: Implement CANBoard version message
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetWakeupMsg()
    {
        // TODO: Implement CANBoard wakeup message
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetBootloaderMsg()
    {
        // TODO: Implement CANBoard bootloader message
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public List<DeviceCanFrame> GetModifyMsgs(int newId)
    {
        // TODO: Implement CANBoard update messages
        return [];
    }
}