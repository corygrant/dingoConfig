using System.Text.Json;
using application.Models;
using domain.Devices;
using domain.Devices.Generic;
using domain.Devices.Keypad.BlinkMarine;
using domain.Devices.Keypad.Grayhill;
using domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace application.Services;

public class ConfigFileManager(ILogger<ConfigFileManager> logger, FwDeviceDefManager fwDeviceDefManager)
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true};

    private string _workingDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "dingoConfig");

    public event Action? OnStateChanged;

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            if (_workingDirectory != value)
            {
                _workingDirectory = value;
                EnsureWorkingDirectoryExists();
                OnStateChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Full path of the currently open/saved file. Used as the target for re-saves.
    /// </summary>
    public string? CurrentFilePath
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnStateChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// File name (no directory) of the current file, for display purposes.
    /// </summary>
    public string? CurrentFileName =>
        string.IsNullOrEmpty(CurrentFilePath) ? null : Path.GetFileName(CurrentFilePath);

    private void EnsureWorkingDirectoryExists()
    {
        if (Directory.Exists(_workingDirectory)) return;
        Directory.CreateDirectory(_workingDirectory);
        logger.LogInformation($"Created working directory: {_workingDirectory}");
    }

    public List<FileInfo> ListFilesWithExtension(string extension)
    {
        EnsureWorkingDirectoryExists();
        var directory = new DirectoryInfo(_workingDirectory);
        return directory.GetFiles(extension)
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();
    }

    public bool FileExists(string fileName)
    {
        var fullPath = GetFullPath(fileName);
        return File.Exists(fullPath);
    }

    public void NewFile()
    {
        CurrentFilePath = null;
        logger.LogInformation("New file started");
    }

    /// <summary>
    /// Save devices to file, preserving all properties by grouping by concrete type
    /// </summary>
    public async Task SaveDevices(List<IDevice> devices, string? fileName = null)
    {
        var targetFileName = fileName ?? CurrentFilePath;

        if (string.IsNullOrWhiteSpace(targetFileName))
        {
            throw new InvalidOperationException("No filename specified");
        }

        var fullPath = GetFullPath(targetFileName);

        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var config = new ConfigFile()
            {
                Devices = devices.OfType<FwDevice>().ToList(),
                DbcDevices = devices.Where(d => d.GetType() == typeof(DbcDevice)).Cast<DbcDevice>().ToList(),
                BlinkMarineKeypads = devices.OfType<BlinkMarineKeypadDevice>().ToList(),
                GrayhillKeypads = devices.OfType<GrayhillKeypadDevice>().ToList()
            };

            var jsonString = JsonSerializer.Serialize(config, _options);
            await File.WriteAllTextAsync(fullPath, jsonString);

            CurrentFilePath = fullPath;

            logger.LogInformation($"Saved {devices.Count} devices to {targetFileName}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error saving devices to {targetFileName}");
            throw;
        }
    }

    /// <summary>
    /// Load devices from file, returning all devices as a single list
    /// </summary>
    public async Task<List<IDevice>?> LoadDevices(string fileName)
    {
        var fullPath = GetFullPath(fileName);

        if (!File.Exists(fullPath))
        {
            logger.LogError($"File not found: {fullPath}");
            return null;
        }

        try
        {
            var jsonString = await File.ReadAllTextAsync(fullPath);
            var config = JsonSerializer.Deserialize<ConfigFile>(jsonString, _options);

            if (config == null)
            {
                return null;
            }

            CurrentFilePath = fullPath;
            
            foreach (var device in config.Devices)
            {
                var def = fwDeviceDefManager.GetByDeviceType(device.DeviceTypeId) ?? FwDeviceDefManager.DefaultFwDevice;
                var deviceSigsConfig = fwDeviceDefManager.GetDeviceCyclicSigsConfig(def.DeviceType);
                device.ApplyDefinition(def, deviceSigsConfig);
            }

            var allDevices = new List<IDevice>();
            allDevices.AddRange(config.Devices);
            allDevices.AddRange(config.DbcDevices);
            allDevices.AddRange(config.BlinkMarineKeypads);
            allDevices.AddRange(config.GrayhillKeypads);

            logger.LogInformation($"Loaded {allDevices.Count} devices from {fileName}");
            return allDevices;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error loading devices from {fileName}");
            throw;
        }
    }

    private string GetFullPath(string fileName)
    {
        // Ensure .json extension
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".json";
        }

        // If already a full path, return it
        if (Path.IsPathRooted(fileName))
        {
            return fileName;
        }

        // Otherwise, combine with working directory
        return Path.Combine(_workingDirectory, fileName);
    }
}