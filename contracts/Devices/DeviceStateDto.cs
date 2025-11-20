namespace contracts.Devices;

public abstract class DeviceStateDto
{
    public Guid Guid { get; set; }
    public bool Connected { get; set; }
}