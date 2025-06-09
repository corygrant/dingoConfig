using dingoConfig.Core.Interfaces;
using dingoConfig.Core.Models;

namespace dingoConfig.Core.Services;

public class CatalogService : ICatalogService
{
    private readonly IFileStorageService _fileStorageService;
    private readonly Dictionary<string, Device> _catalogCache = new();
    private string _catalogDirectory = "catalogs";

    public CatalogService(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
    }

    public async Task<Device?> LoadCatalogAsync(string catalogPath)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
            throw new ArgumentException("Catalog path cannot be null or empty", nameof(catalogPath));

        if (!await _fileStorageService.FileExistsAsync(catalogPath))
            throw new FileNotFoundException($"Catalog file not found: {catalogPath}");

        var device = await _fileStorageService.ReadJsonAsync<Device>(catalogPath);
        
        if (device == null)
            throw new InvalidDataException($"Failed to deserialize catalog from: {catalogPath}");

        return device;
    }

    public async Task<List<Device>> LoadAllCatalogsAsync(string catalogDirectory)
    {
        if (string.IsNullOrWhiteSpace(catalogDirectory))
            throw new ArgumentException("Catalog directory cannot be null or empty", nameof(catalogDirectory));

        if (!await _fileStorageService.DirectoryExistsAsync(catalogDirectory))
            return new List<Device>();

        var catalogFiles = await _fileStorageService.GetFilesAsync(catalogDirectory, "*.json");
        var devices = new List<Device>();

        foreach (var catalogFile in catalogFiles)
        {
            try
            {
                var device = await LoadCatalogAsync(catalogFile);
                if (device != null)
                {
                    devices.Add(device);
                }
            }
            catch (Exception)
            {
                // Log error but continue processing other catalogs
                continue;
            }
        }

        return devices;
    }

    public async Task ReloadCatalogsAsync()
    {
        _catalogCache.Clear();
        
        var devices = await LoadAllCatalogsAsync(_catalogDirectory);
        
        foreach (var device in devices)
        {
            _catalogCache[device.Type.ToString()] = device;
        }
    }

    public async Task<Device?> GetDeviceCatalogAsync(string deviceType)
    {
        if (string.IsNullOrWhiteSpace(deviceType))
            return null;

        if (_catalogCache.TryGetValue(deviceType, out var cachedDevice))
            return cachedDevice;

        // Try to find device in directory if not cached
        await ReloadCatalogsAsync();
        
        _catalogCache.TryGetValue(deviceType, out cachedDevice);
        return cachedDevice;
    }

    public async Task<List<string>> GetAvailableDeviceTypesAsync()
    {
        if (_catalogCache.Count == 0)
            await ReloadCatalogsAsync();

        return _catalogCache.Keys.ToList();
    }

    public async Task<bool> ValidateCatalogAsync(string catalogPath)
    {
        try
        {
            var device = await LoadCatalogAsync(catalogPath);
            if (device == null)
                return false;

            var validationResult = device.Validate();
            return validationResult.IsValid;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string GetCatalogDirectory()
    {
        return _catalogDirectory;
    }

    public void SetCatalogDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

        _catalogDirectory = directoryPath;
        _catalogCache.Clear(); // Clear cache when directory changes
    }
}