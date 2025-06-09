using dingoConfig.Core.Interfaces;
using dingoConfig.Core.Models;

namespace dingoConfig.Core.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IFileStorageService _fileStorageService;
    private readonly Dictionary<string, DeviceConfiguration> _configurationCache = new();
    private string _configurationDirectory = "configs";

    public ConfigurationService(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
    }

    public async Task<DeviceConfiguration?> LoadConfigurationAsync(string configurationPath)
    {
        if (string.IsNullOrWhiteSpace(configurationPath))
            return null;

        if (!await _fileStorageService.FileExistsAsync(configurationPath))
            return null;

        return await _fileStorageService.ReadJsonAsync<DeviceConfiguration>(configurationPath);
    }

    public async Task<bool> SaveConfigurationAsync(DeviceConfiguration configuration, string configurationPath)
    {
        if (configuration == null || string.IsNullOrWhiteSpace(configurationPath))
            return false;

        try
        {
            // Update last modified timestamp
            configuration.LastModified = DateTime.UtcNow;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(configurationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                await _fileStorageService.CreateDirectoryAsync(directory);
            }

            // Save configuration
            var success = await _fileStorageService.WriteJsonAsync(configuration, configurationPath);
            
            if (success)
            {
                // Update cache
                _configurationCache[configuration.DeviceId] = configuration;
            }

            return success;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<List<DeviceConfiguration>> LoadAllConfigurationsAsync(string configurationDirectory)
    {
        if (string.IsNullOrWhiteSpace(configurationDirectory))
            return new List<DeviceConfiguration>();

        if (!await _fileStorageService.DirectoryExistsAsync(configurationDirectory))
            return new List<DeviceConfiguration>();

        var configFiles = await _fileStorageService.GetFilesAsync(configurationDirectory, "*.json");
        var configurations = new List<DeviceConfiguration>();

        foreach (var configFile in configFiles)
        {
            try
            {
                var configuration = await LoadConfigurationAsync(configFile);
                if (configuration != null)
                {
                    configurations.Add(configuration);
                }
            }
            catch (Exception)
            {
                // Log error but continue processing other configurations
                continue;
            }
        }

        return configurations;
    }

    public async Task<bool> DeleteConfigurationAsync(string configurationPath)
    {
        if (string.IsNullOrWhiteSpace(configurationPath))
            return false;

        if (!await _fileStorageService.FileExistsAsync(configurationPath))
            return false;

        try
        {
            var success = await _fileStorageService.DeleteFileAsync(configurationPath);
            
            if (success)
            {
                // Remove from cache
                var configToRemove = _configurationCache.Values
                    .FirstOrDefault(c => configurationPath.Contains(c.DeviceId));
                
                if (configToRemove != null)
                {
                    _configurationCache.Remove(configToRemove.DeviceId);
                }
            }

            return success;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<DeviceConfiguration?> GetConfigurationAsync(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return null;

        if (_configurationCache.TryGetValue(deviceId, out var cachedConfig))
            return cachedConfig;

        // Try to find configuration in directory
        var configurations = await LoadAllConfigurationsAsync(_configurationDirectory);
        var config = configurations.FirstOrDefault(c => c.DeviceId == deviceId);
        
        if (config != null)
        {
            _configurationCache[deviceId] = config;
        }

        return config;
    }

    public async Task<List<DeviceConfiguration>> GetConfigurationsByTypeAsync(string deviceType)
    {
        if (string.IsNullOrWhiteSpace(deviceType))
            return new List<DeviceConfiguration>();

        var allConfigurations = await LoadAllConfigurationsAsync(_configurationDirectory);
        return allConfigurations.Where(c => c.DeviceType == deviceType).ToList();
    }

    public Task<bool> ValidateConfigurationAsync(DeviceConfiguration configuration, Device deviceCatalog)
    {
        if (configuration == null || deviceCatalog == null)
            return Task.FromResult(false);

        try
        {
            // Check if device type matches
            if (configuration.DeviceType != deviceCatalog.Type.ToString())
                return Task.FromResult(false);

            // Validate each configuration setting against catalog parameters
            foreach (var setting in configuration.Settings)
            {
                var parameter = deviceCatalog.Parameters.FirstOrDefault(p => p.Id == setting.Key);
                if (parameter == null)
                {
                    // Setting not found in catalog - this might be valid for optional parameters
                    continue;
                }

                // Validate parameter value type and range
                if (!ValidateParameterValue(setting.Value, parameter))
                    return Task.FromResult(false);
            }

            // Check for required parameters
            var requiredParameters = deviceCatalog.Parameters.Where(p => p.IsRequired);
            foreach (var requiredParam in requiredParameters)
            {
                if (!configuration.Settings.ContainsKey(requiredParam.Id))
                    return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }

    private bool ValidateParameterValue(object value, Parameter parameter)
    {
        try
        {
            switch (parameter.Type)
            {
                case ParameterType.Integer:
                    if (value is not int intValue)
                        return false;
                    
                    if (parameter.Min.HasValue && intValue < parameter.Min.Value)
                        return false;
                    
                    if (parameter.Max.HasValue && intValue > parameter.Max.Value)
                        return false;
                    
                    break;

                case ParameterType.Float:
                    if (value is not double doubleValue && value is not float)
                        return false;
                    
                    var numValue = Convert.ToDouble(value);
                    
                    if (parameter.Min.HasValue && numValue < parameter.Min.Value)
                        return false;
                    
                    if (parameter.Max.HasValue && numValue > parameter.Max.Value)
                        return false;
                    
                    break;

                case ParameterType.Boolean:
                    if (value is not bool)
                        return false;
                    break;

                case ParameterType.String:
                    if (value is not string)
                        return false;
                    break;

                case ParameterType.Enum:
                    if (value is not string enumValue || !parameter.Options.Contains(enumValue))
                        return false;
                    break;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string GetConfigurationDirectory()
    {
        return _configurationDirectory;
    }

    public void SetConfigurationDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

        _configurationDirectory = directoryPath;
        _configurationCache.Clear(); // Clear cache when directory changes
    }
}