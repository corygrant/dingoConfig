namespace dingoConfig.Core.Models;

public class DeviceConfiguration
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}