using System.Text.Json;
using api.Adapters;
using Microsoft.Extensions.Logging;

namespace api.Services;

public class ConfigFileManager(DeviceManager manager, ILogger<ConfigFileManager> logger)
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true};
    
    public async Task Open(string filename)
    {
        var jsonString = await File.ReadAllTextAsync(filename);

        List<IDevice>? devices = JsonSerializer.Deserialize<List<IDevice>>(jsonString, _options);

        if (devices != null)
        {
            manager.AddDevices(devices);
            logger.LogInformation($"Added {devices.Count} devices");
        }
        else
        {
            logger.LogError($"No devices found in {filename}");
        }
    }

    public async Task Save(string filename)
    {
        await File.WriteAllTextAsync(filename, JsonSerializer.Serialize(manager.GetDevices(), _options));
        
        logger.LogInformation($"Config file saved  to {filename}");
    }
}