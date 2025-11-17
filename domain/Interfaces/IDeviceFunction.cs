using domain.Devices.dingoPdm.Enums;
using domain.Models;

namespace domain.Interfaces;

public interface IDeviceFunction
{
    // Properties for metadata
    public int Number { get; }
    public string Name { get; }

    public static abstract int ExtractIndex(byte data, MessagePrefix prefix);
    
    // Configuration receive method
    public bool Receive(byte[] data, MessagePrefix prefix);

    // Methods to create device request/response messages
    public DeviceResponse? CreateUploadRequest(int baseId, MessagePrefix prefix);
    public DeviceResponse? CreateDownloadRequest(int baseId, MessagePrefix prefix);
}