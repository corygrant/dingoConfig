namespace contracts.Devices;

public abstract class DeviceConfigDto
{
    public Guid Guid { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BaseId { get; set; }
    public string Version { get; set; } = string.Empty;
}