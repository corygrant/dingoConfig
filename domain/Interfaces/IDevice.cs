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
    public void UpdateIsConnected();
    void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> queue);
    bool InIdRange(int id);

    /// <summary>
    /// Get all status message signals exposed by this device for use in CAN input configuration
    /// </summary>
    /// <returns>Enumerable of tuples containing message ID and signal</returns>
    IEnumerable<(int MessageId, DbcSignal Signal)> GetStatusSignals();
}