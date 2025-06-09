using dingoConfig.Core.Models;

namespace dingoConfig.Core.Interfaces;

public interface ICatalogService
{
    Task<Device?> LoadCatalogAsync(string catalogPath);
    Task<List<Device>> LoadAllCatalogsAsync(string catalogDirectory);
    Task ReloadCatalogsAsync();
    Task<Device?> GetDeviceCatalogAsync(string deviceType);
    Task<List<string>> GetAvailableDeviceTypesAsync();
    Task<bool> ValidateCatalogAsync(string catalogPath);
    string GetCatalogDirectory();
    void SetCatalogDirectory(string directoryPath);
}