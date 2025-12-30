using System.Collections.Concurrent;
using domain.Models;

namespace domain.Interfaces;

public interface IDevice
{
    Guid Guid { get; }
    string Type { get; }
    string Name { get; set; }
    int BaseId {get; set; }
    bool Connected {get;}
    DateTime LastRxTime {get; set;}
    
    bool Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> queue);
    bool InIdRange(int id);
    List<DeviceCanFrame> GetReadMsgs();
    List<DeviceCanFrame> GetWriteMsgs();
    List<DeviceCanFrame> GetModifyMsgs(int newId);
    DeviceCanFrame GetBurnMsg();
    DeviceCanFrame GetSleepMsg();
    DeviceCanFrame GetVersionMsg();
    DeviceCanFrame GetWakeupMsg();
    DeviceCanFrame GetBootloaderMsg();
}