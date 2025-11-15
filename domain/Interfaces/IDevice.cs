using System.Collections.Concurrent;
using domain.Models;

namespace domain.Interfaces;

public interface IDevice
{
    Guid Id { get; }
    string Name { get; set; }
    int BaseId {get; set;}
    bool Connected {get; set;}
    TimeSpan LastRxTime {get; set;}

    void UpdateConnected();
    void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceResponse> queue);
    void Clear();
    bool InIdRange(int id);
    List<DeviceResponse> GetUploadMsgs();
    List<DeviceResponse> GetDownloadMsgs();
    List<DeviceResponse> GetUpdateMsgs(int newId);
    DeviceResponse GetBurnMsg();
    DeviceResponse GetSleepMsg();
    DeviceResponse GetVersionMsg();
}