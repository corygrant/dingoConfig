using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.CanboardDevice;

public class CanboardDevice : IDevice
{
    public Guid Guid { get; } = Guid.NewGuid();
    [JsonIgnore] public string Type => "CANBoard";
    public string Name { get; set; } = "CANBoard";
    public int BaseId { get; set; }
    public DateTime LastRxTime { get; set; }
    public bool Connected { get; set; }

    public void UpdateConnected()
    {
        // TODO: Implement connection status update
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

    public bool Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> queue)
    {
        // TODO: Implement CANBoard message parsing
        return false;
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