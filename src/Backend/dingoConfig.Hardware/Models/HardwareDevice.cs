namespace dingoConfig.Hardware.Models;

public class HardwareDevice
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HardwareDeviceType Type { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string[] Capabilities { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> Properties { get; set; } = new();
}

public enum HardwareDeviceType
{
    Unknown,
    PCAN,
    SLCAN,
    USBSerial,
    Network
}