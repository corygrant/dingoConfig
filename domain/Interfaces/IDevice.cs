using System.Collections.Concurrent;
using domain.Models;

namespace domain.Interfaces;

public interface IDevice
{
    Guid Guid { get; }
    string Type { get; }
    string Name { get; set; }
    int BaseId {get; set; }
    static int DefaultId { get; }
    bool Connected {get;}
    TimeSpan CyclicGap {get;}
    TimeSpan CyclicPause {get;}
    public bool UpdateIsConnected();
    void Read(int id, byte[] data, 
                ref ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> queue, 
                List<DeviceCanFrame> outgoing);
    bool InIdRange(int id);
    IEnumerable<(int MessageId, DbcSignal Signal)> GetStatusSigs();
    List<CanFrame> GetCyclicMsgs();
}