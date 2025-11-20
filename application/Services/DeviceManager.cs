using System.Collections.Concurrent;
using domain.Devices.CanboardDevice;
using domain.Devices.dingoPdm;
using domain.Devices.dingoPdmMax;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace application.Services;

public class DeviceManager
{
    private readonly Dictionary<Guid, IDevice> _devices = new();
    private ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> _requestQueue = new();
    private readonly ILogger<DeviceManager> _logger;
    private Action<CanFrame>? _transmitCallback;

    private const int MaxRetries = 3;
    private const int TimeoutMs = 500;

    public DeviceManager(ILogger<DeviceManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Set the callback for transmitting frames (called by CommsDataPipeline during setup)
    /// </summary>
    public void SetTransmitCallback(Action<CanFrame> callback)
    {
        _transmitCallback = callback;
    }

    /// <summary>
    /// Create and add a device of the specified type
    /// </summary>
    public IDevice AddDevice(string deviceType, string name, int baseId)
    {
        IDevice device = deviceType.ToLower() switch
        {
            "pdm" => new PdmDevice(name, baseId),
            "pdmmax" => new PdmMaxDevice(name, baseId),
            _ => throw new ArgumentException($"Unknown device type: {deviceType}")
        };

        _devices[device.Guid] = device;
        _logger.LogInformation("Device added: {DeviceType} '{Name}' (ID: {BaseId}, Guid: {Guid})",
            deviceType, name, baseId, device.Guid);

        return device;
    }

    /// <summary>
    /// Get a device by Guid
    /// </summary>
    public IDevice? GetDevice(Guid id)
    {
        _devices.TryGetValue(id, out var device);
        return device;
    }

    /// <summary>
    /// Get a device by Guid as a specific type
    /// </summary>
    public T? GetDevice<T>(Guid id) where T : class, IDevice
    {
        return GetDevice(id) as T;
    }

    /// <summary>
    /// Get a device by BaseId (for routing CAN messages)
    /// </summary>
    private IDevice? GetDeviceByBaseId(int baseId)
    {
        return _devices.Values.FirstOrDefault(d => d.BaseId == baseId);
    }

    /// <summary>
    /// Get all devices
    /// </summary>
    public IEnumerable<IDevice> GetAllDevices() => _devices.Values;

    /// <summary>
    /// Get all devices of a specific type
    /// </summary>
    public IEnumerable<T> GetDevicesByType<T>() where T : class, IDevice
    {
        return _devices.Values.OfType<T>();
    }

    /// <summary>
    /// Remove a device
    /// </summary>
    public void RemoveDevice(Guid deviceId)
    {
        if (_devices.Remove(deviceId, out var device))
        {
            _logger.LogInformation("Device removed: {Name} (Guid: {Guid})", device.Name, deviceId);
        }
    }

    /// <summary>
    /// Add multiple devices
    /// </summary>
    public void AddDevices(List<IDevice> devices)
    {
        foreach (var device in devices)
        {
            _devices[device.Guid] = device;
        }
        _logger.LogInformation("Added {Count} devices", devices.Count);
    }

    /// <summary>
    /// Get all devices
    /// </summary>
    public List<IDevice> GetDevices() => _devices.Values.ToList();

    /// <summary>
    /// Called by CommsDataPipeline when CAN data is received
    /// Routes data to all devices so they can update their state/config
    /// </summary>
    public void OnCanDataReceived(CanFrame frame)
    {
        foreach (var device in _devices.Values)
        {
            if (device.InIdRange(frame.Id))
            {
                device.Read(frame.Id, frame.Payload, ref _requestQueue);
            }
        }
    }

    // ============================================
    // Message Queuing & Timeout Management
    // ============================================

    /// <summary>
    /// Queue a message for transmission
    /// </summary>
    private void QueueMessage(DeviceCanFrame frame)
    {
        var key = (frame.DeviceBaseId, frame.Prefix, frame.Index);

        if (!_requestQueue.TryAdd(key, frame))
        {
            _logger.LogWarning("Message already in queue: BaseId={BaseId}, Prefix={Prefix}, Index={Index}",
                key.Item1, key.Item2, key.Item3);
            return;
        }

        // Queue for transmission
        if (_transmitCallback != null)
        {
            _transmitCallback(frame.Frame);
        }
        else
        {
            _logger.LogWarning("Transmit callback not set - message not transmitted");
        }

        // Start timeout timer
        StartMessageTimer(key, frame);

        _logger.LogDebug("Message queued: {Description} (BaseId={BaseId}, Prefix={Prefix})",
            frame.MsgDescription, key.Item1, key.Item2);
    }

    private void StartMessageTimer((int, int, int) key, DeviceCanFrame frame)
    {
        frame.TimeSentTimer = new Timer(_ =>
        {
            HandleMessageTimeout(key, frame);
        }, null, TimeoutMs, Timeout.Infinite);
    }

    private void HandleMessageTimeout((int BaseId, int Prefix, int Index) key, DeviceCanFrame frame)
    {
        if (!_requestQueue.TryGetValue(key, out var queuedFrame))
            return;

        frame.RxAttempts++;

        if (frame.RxAttempts >= MaxRetries)
        {
            // Max retries exceeded - remove and log error
            _requestQueue.TryRemove(key, out _);
            frame.TimeSentTimer?.Dispose();

            var device = GetDeviceByBaseId(key.BaseId);
            _logger.LogError("Message failed after {MaxRetries} retries: {Description} on {DeviceName} (ID: {BaseId})",
                MaxRetries, frame.MsgDescription, device?.Name ?? "Unknown", key.BaseId);
        }
        else
        {
            // Retry - queue again
            if (_transmitCallback != null)
            {
                _transmitCallback(frame.Frame);
            }
            StartMessageTimer(key, frame);

            _logger.LogWarning("Message retry {Attempt}/{MaxRetries}: {Description} (BaseId={BaseId})",
                frame.RxAttempts, MaxRetries, frame.MsgDescription, key.BaseId);
        }
    }

    // ============================================
    // Device Operations (called by controllers)
    // ============================================

    /// <summary>
    /// Upload configuration from device to host
    /// </summary>
    public void UploadDeviceConfig(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return;

        var uploadMsgs = device.GetUploadMsgs();
        foreach (var msg in uploadMsgs)
        {
            QueueMessage(msg);
        }

        _logger.LogInformation("Upload started for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
    }

    /// <summary>
    /// Download configuration to device
    /// </summary>
    public void DownloadDeviceConfig(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return;

        var downloadMsgs = device.GetDownloadMsgs();
        foreach (var msg in downloadMsgs)
        {
            QueueMessage(msg);
        }

        _logger.LogInformation("Download started for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
    }

    /// <summary>
    /// Burn settings to device flash memory
    /// </summary>
    public void BurnDeviceSettings(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return;

        var burnMsg = device.GetBurnMsg();
        QueueMessage(burnMsg);

        _logger.LogInformation("Burn initiated for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
    }

    /// <summary>
    /// Put device to sleep
    /// </summary>
    public void SleepDevice(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return;

        var sleepMsg = device.GetSleepMsg();
        QueueMessage(sleepMsg);

        _logger.LogInformation("Sleep requested for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
    }

    /// <summary>
    /// Request device version/info
    /// </summary>
    public void RequestDeviceVersion(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return;

        var versionMsg = device.GetVersionMsg();
        QueueMessage(versionMsg);

        _logger.LogInformation("Version requested for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
    }

    /// <summary>
    /// Download updated configuration to device
    /// (Device properties should already be updated by controller before calling this)
    /// </summary>
    public void DownloadUpdatedConfig(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return;

        var downloadMsgs = device.GetDownloadMsgs();
        foreach (var msg in downloadMsgs)
        {
            QueueMessage(msg);
        }

        _logger.LogInformation("Configuration download initiated for {DeviceName} (Guid: {Guid})",
            device.Name, deviceId);
    }
}