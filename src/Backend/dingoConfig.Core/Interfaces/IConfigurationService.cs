using dingoConfig.Core.Models;

namespace dingoConfig.Core.Interfaces;

public interface IConfigurationService
{
    Task<DeviceConfiguration?> LoadConfigurationAsync(string configurationPath);
    Task<bool> SaveConfigurationAsync(DeviceConfiguration configuration, string configurationPath);
    Task<List<DeviceConfiguration>> LoadAllConfigurationsAsync(string configurationDirectory);
    Task<bool> DeleteConfigurationAsync(string configurationPath);
    Task<DeviceConfiguration?> GetConfigurationAsync(string deviceId);
    Task<List<DeviceConfiguration>> GetConfigurationsByTypeAsync(string deviceType);
    Task<bool> ValidateConfigurationAsync(DeviceConfiguration configuration, Device deviceCatalog);
    string GetConfigurationDirectory();
    void SetConfigurationDirectory(string directoryPath);
}