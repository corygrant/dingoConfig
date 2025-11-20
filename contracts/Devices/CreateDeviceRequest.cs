namespace contracts.Devices;

public class CreateDeviceRequest
{
    public required string DeviceType { get; set; }
    public required string Name { get; set; }
    public required int BaseId { get; set; }
}
