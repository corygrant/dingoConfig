using domain.Devices.dingoPdm.Enums;
using domain.Models;

namespace domain.Interfaces;

public interface IDeviceFunction
{
    public int Number { get; }
    public string Name { get; }

    public static abstract int ExtractIndex(byte data, MessagePrefix prefix);
    
    public bool Receive(byte[] data, MessagePrefix prefix);

    public DeviceCanFrame? CreateUploadRequest(int baseId, MessagePrefix prefix);
    public DeviceCanFrame? CreateDownloadRequest(int baseId, MessagePrefix prefix);
}