namespace contracts.Devices;

public abstract class DeviceStateDto
{
    public Guid Guid { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BaseId { get; set; }
    public bool Connected { get; set; }
    public DateTime LastRxTime { get; set; }
    public int DeviceState { get; set; }
    public string Version { get; set; } = string.Empty;
}