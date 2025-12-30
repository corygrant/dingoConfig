using System.Collections.Concurrent;
using domain.Devices.CanboardDevice;
using domain.Devices.dingoPdm;
using domain.Devices.dingoPdmMax;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace application.Services;

public class DeviceManager(ILogger<DeviceManager> logger)
{
    private readonly Dictionary<Guid, IDevice> _devices = new();
    private ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> _requestQueue = new();
    private Action<CanFrame>? _transmitCallback;

    public int QueueCount => _requestQueue.Count;
    
    private const int MaxRetries = 20;
    private const int TimeoutMs = 500;

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
        logger.LogInformation("Device added: {DeviceType} '{Name}' (ID: {BaseId}, Guid: {Guid})",
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
            logger.LogInformation("Device removed: {Name} (Guid: {Guid})", device.Name, deviceId);
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
        logger.LogInformation("Added {Count} devices", devices.Count);
    }

    /// <summary>
    /// Clear all devices
    /// </summary>
    public void ClearDevices()
    {
        _devices.Clear();
        _requestQueue.Clear();
        logger.LogInformation("All devices cleared");
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
    private void QueueMessage(DeviceCanFrame frame, bool sendOnly = false)
    {
        // Queue for transmission
        if (_transmitCallback != null)
        {
            _transmitCallback(frame.Frame);
        }
        else
        {
            logger.LogWarning("Transmit callback not set - message not transmitted");
            return;
        }

        //Some messages have no response, don't queue
        if (sendOnly) return;
        
        //Unique message key, used to find message in transmit queue later
        var key = (frame.DeviceBaseId, frame.Prefix, frame.Index);

        if (!_requestQueue.TryAdd(key, frame))
        {
            logger.LogWarning("Message already in queue: BaseId={BaseId}, Prefix={Prefix}, Index={Index}",
                key.Item1, key.Item2, key.Item3);
            return;
        }

        // Start timeout timer
        StartMessageTimer(key, frame);

        logger.LogDebug("Message queued: {Description} (BaseId={BaseId}, Prefix={Prefix})",
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
            logger.LogError("Message failed after {MaxRetries} retries: {Description} on {DeviceName} (ID: {BaseId})",
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

            logger.LogWarning("Message retry {Attempt}/{MaxRetries}: {Description} (BaseId={BaseId})",
                frame.RxAttempts, MaxRetries, frame.MsgDescription, key.BaseId);
        }
    }

    // ============================================
    // Device Operations (called by controllers)
    // ============================================

    /// <summary>
    /// Read configuration from device to host
    /// </summary>
    public void ReadDeviceConfig(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return;

        var readMsgs = device.GetReadMsgs();
        foreach (var msg in readMsgs)
        {
            QueueMessage(msg);
        }

        logger.LogInformation("Read started for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
    }

    /// <summary>
    /// Write configuration to device
    /// </summary>
    /// <returns>
    /// Send write config success
    /// </returns>
    public bool WriteDeviceConfig(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return false;

        var downloadMsgs = device.GetWriteMsgs();
        foreach (var msg in downloadMsgs)
        {
            QueueMessage(msg);
        }

        logger.LogInformation("Write started for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
        return true;
    }
    
    /// <summary>
    /// Modify device, name and base ID
    /// Sends modify message to device
    /// </summary>
    /// <returns>
    /// Send modify success
    /// </returns>
    public bool ModifyDeviceConfig(Guid deviceId, string newName, int newId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return false;
        
        var modifyMsgs = device.GetModifyMsgs(newId);
        foreach (var msg in modifyMsgs)
        {
            QueueMessage(msg);
        }

        logger.LogInformation("Modify started for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
        
        device.Name = newName;
        device.BaseId =  newId;
        
        return true;
    }

    /// <summary>
    /// Burn settings to device flash memory
    /// </summary>
    /// <returns>
    /// Send burn request success
    /// </returns>
    public bool BurnSettings(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return false;

        var burnMsg = device.GetBurnMsg();
        QueueMessage(burnMsg);

        logger.LogInformation("Burn initiated for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
        return true;
    }

    /// <summary>
    /// Request device enter sleep
    /// </summary>
    /// <returns>
    /// Send sleep request success
    /// </returns>
    public bool RequestSleep(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return false;

        var sleepMsg = device.GetSleepMsg();
        QueueMessage(sleepMsg);

        logger.LogInformation("Sleep requested for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
        return true;
    }

    /// <summary>
    /// Request device version/info
    /// </summary>
    /// <returns>
    /// Send request version success
    /// </returns>
    public bool RequestVersion(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return false;

        var versionMsg = device.GetVersionMsg();
        QueueMessage(versionMsg);

        logger.LogInformation("Version requested for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
        return true;
    }

    /// <summary>
    /// Request device wakeup
    /// </summary>
    /// <returns>
    /// Send wakeup success
    /// </returns>
    public bool RequestWakeup(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return false;
        
        var wakeupMsg = device.GetWakeupMsg();
        QueueMessage(wakeupMsg, true);
        
        logger.LogInformation("Wake up for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
        return true;
    }

    /// <summary>
    /// Request enter bootloader
    /// </summary>
    /// <returns>
    /// Send enter bootloader success
    /// </returns>
    public bool RequestBootloader(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device == null)
            return false;
        
        var bootloaderMsg = device.GetBootloaderMsg();
        QueueMessage(bootloaderMsg, true);
        
        logger.LogInformation("Enter bootloader on {DeviceName} (Guid: {Guid})", device.Name, deviceId);
        return true;
    }
    
}