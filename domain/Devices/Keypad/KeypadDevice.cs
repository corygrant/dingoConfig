using System.Collections.Concurrent;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices.Keypad;

public class KeypadDevice : IDevice 
{
    public Guid Guid { get; }
    public string Type { get; }
    public string Name { get; set; }
    public int BaseId { get; set; }
    public bool Connected { get; }
    public void UpdateIsConnected()
    {
        throw new NotImplementedException();
    }

    public void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> queue)
    {
        throw new NotImplementedException();
    }

    public bool InIdRange(int id)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<(int MessageId, DbcSignal Signal)> GetStatusSignals()
    {
        throw new NotImplementedException();
    }
}