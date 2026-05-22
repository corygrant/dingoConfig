using System.Collections.Concurrent;
using application.Models;
using domain.Devices.Canboard;
using domain.Devices.dingoPdm;
using domain.Devices.Generic;
using domain.Devices.Keypad.BlinkMarine;
using domain.Devices.Keypad.Grayhill;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace application.Services;

public class DeviceManager(ILogger<DeviceManager> logger, ILoggerFactory loggerFactory, SystemLogger systemLogger, DeviceDefinitionManager deviceDefinitionManager)
{
    private readonly Dictionary<Guid, IDevice> _devices = new();
    private ConcurrentDictionary<(int BaseId, int Index, int SubIndex), DeviceCanFrame> _requestQueue = new();

    private Action<List<DeviceCanFrame>>? _batchTransmitCallback;
    
    private readonly Dictionary<Guid, DeviceUiState> _deviceUiState = new();

    private readonly Dictionary<Guid, System.Timers.Timer> _cyclicTimers = new();

    public int QueueCount => _requestQueue.Count;

    private const int MaxRetries = 2;
    private const int TimeoutMs = 3000;

    public event EventHandler<DeviceEventArgs>? DeviceAdded;
    public event EventHandler<DeviceEventArgs>? DeviceRemoved;

    public void SetBatchTransmitCallback(Action<List<DeviceCanFrame>> callback)
    {
        _batchTransmitCallback = callback;
    }

    /// <summary>
    /// Get UI state for a device (creates device UI state if it doesn't exist)
    /// </summary>
    public DeviceUiState GetDeviceUiState(Guid deviceId)
    {
        if (_deviceUiState.TryGetValue(deviceId, out var state)) return state;

        state = new DeviceUiState();
        _deviceUiState[deviceId] = state;
        return state;
    }

    /// <summary>
    /// Create and add a device of the specified type
    /// </summary>
    public void AddDevice(string deviceType, string name, int baseId)
    {
        var parts = deviceType.ToLower().Split('-', 2); // Limit to 2 parts max
        var devType = parts[0];
        var model = parts.Length > 1 ? parts[1] : string.Empty;

        // Handle pdm:{typeId} format
        int pdmTypeId = 0;
        if (devType.Contains(':'))
        {
            var pdmParts = devType.Split(':', 2);
            devType = pdmParts[0];
            int.TryParse(pdmParts[1], out pdmTypeId);
        }

        IDevice device = devType switch
        {
            "pdm" => new PdmDevice(
                deviceDefinitionManager.GetByPdmType(pdmTypeId) ?? DeviceDefinitionManager.DefaultPdm,
                name, baseId),
            "canboard" => new CanboardDevice(name, baseId),
            "dbcdevice" => new DbcDevice(name, baseId),
            "blinkkeypad" => new BlinkMarineKeypadDevice(name, baseId, model),
            "grayhillkeypad" => new GrayhillKeypadDevice(name, baseId, model),
            _ => throw new ArgumentException($"Unknown device type: '{deviceType}'")
        };

        switch (device)
        {
            case PdmDevice pdm when deviceDefinitionManager.GetPdmCyclicSigsConfig() is { } pdmSigs:
                pdm.BindCyclicSigs(pdmSigs);
                break;
            case CanboardDevice canboard when deviceDefinitionManager.GetCanboardCyclicSigsConfig() is { } canboardSigs:
                canboard.BindCyclicSigs(canboardSigs);
                break;
        }

        SetLoggers(device);
        _devices[device.Guid] = device;

        // Keypads don't need read - they're passive reporting devices
        var needsRead = device is not BlinkMarineKeypadDevice and not GrayhillKeypadDevice;
        GetDeviceUiState(device.Guid).NeedsRead = needsRead;

        logger.LogInformation("Device added: {DeviceType} '{Name}' (ID: {BaseId}, Guid: {Guid})",
            deviceType, name, baseId, device.Guid);

        SetCyclicTimer(device);
        OnDeviceAdded(new DeviceEventArgs(device));
    }

    /// <summary>
    /// Get a device by Guid
    /// </summary>
    public IDevice? GetDevice(Guid id)
    {
        _devices.TryGetValue(id, out var device);
        if(device?.UpdateIsConnected() == true) 
            CheckConfig(id);
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
    /// Get a device by BaseId (for routing CAN message)
    /// </summary>
    private IDevice? GetDeviceByBaseId(int baseId)
    {
        return _devices.Values.FirstOrDefault(d => d.BaseId == baseId);
    }

    /// <summary>
    /// Get all devices
    /// </summary>
    public IEnumerable<IDevice> GetAllDevices()
    {
        foreach (var device in _devices.Values)
        {
            if(device.UpdateIsConnected())
                CheckConfig(device.Guid); 
        }

        return _devices.Values;
    }

    /// <summary>
    /// Get all devices of a specific type
    /// </summary>
    public IEnumerable<T> GetDevicesByType<T>() where T : class, IDevice
    {
        var devices = _devices.Values.OfType<T>().ToList();
        foreach (var device in devices)
        {
            if(device.UpdateIsConnected())
                CheckConfig(device.Guid);
        }

        return devices;
    }

    /// <summary>
    /// Remove a device
    /// </summary>
    public void RemoveDevice(Guid deviceId)
    {
        RemoveCyclicTimer(deviceId);

        if (_devices.Remove(deviceId, out var device))
        {
            logger.LogInformation("Device removed: {Name} (Guid: {Guid})", device.Name, deviceId);

            OnDeviceRemoved(new DeviceEventArgs(device));
        }
    }

    /// <summary>
    /// Add multiple devices
    /// Injects loggers into devices
    /// </summary>
    public void AddDevices(List<IDevice> devices)
    {
        foreach (var device in devices)
        {
            SetLoggers(device);

            _devices[device.Guid] = device;
            GetDeviceUiState(device.Guid).NeedsRead = true;
            SetCyclicTimer(device);
            OnDeviceAdded(new DeviceEventArgs(device));
        }

        logger.LogInformation("Added {Count} devices", devices.Count);
    }

    /// <summary>
    /// Clear all devices
    /// </summary>
    public void ClearDevices()
    {
        RemoveAllCyclicTimers();
        _devices.Clear();
        _requestQueue.Clear();
        logger.LogInformation("All devices cleared");
    }

    /// <summary>
    /// Get all devices
    /// </summary>
    public List<IDevice> GetDevices()
    {
        var devices = _devices.Values.ToList();
        foreach (var device in devices)
        {
            if(device.UpdateIsConnected())
                CheckConfig(device.Guid);
        }

        return devices;
    }

    public void CheckConfig(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device is not IDeviceConfigurable) return;
        var deviceConfigurable = (IDeviceConfigurable)device;
        var msg = deviceConfigurable.GetCheckMsg();
        QueueMessage(msg);
    }

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
                var outgoing = new List<DeviceCanFrame>();
                device.Read(frame.Id, frame.Payload, ref _requestQueue, outgoing);
                if (outgoing.Count > 0)
                    _batchTransmitCallback?.Invoke(outgoing);
            }
        }
    }

    private void SetLoggers(IDevice device)
    {
        switch (device)
        {
            case PdmDevice pdmDevice:
                pdmDevice.SetLogger(loggerFactory.CreateLogger<PdmDevice>());
                pdmDevice.SuccessNotification += msg => systemLogger.Notify(pdmDevice.Name, msg);
                break;
            case CanboardDevice canboardDevice:
                canboardDevice.SetLogger(loggerFactory.CreateLogger<CanboardDevice>());
                break;
            case DbcDevice dbcDevice:
                dbcDevice.SetLogger(loggerFactory.CreateLogger<DbcDevice>());
                dbcDevice.UpdateIdRange();
                break;
            case BlinkMarineKeypadDevice blinkKeypad:
                blinkKeypad.SetLogger(loggerFactory.CreateLogger<BlinkMarineKeypadDevice>());
                break;
            case GrayhillKeypadDevice grayhillKeypad:
                grayhillKeypad.SetLogger(loggerFactory.CreateLogger<GrayhillKeypadDevice>());
                break;
        }
    }

    private void SetCyclicTimer(IDevice device)
    {
        //Cyclic timers not used or configured
        if ((device.CyclicGap <= TimeSpan.FromMilliseconds(0)) ||
            (device.CyclicPause <= TimeSpan.FromMilliseconds(0))) return;

        var timer = new System.Timers.Timer(device.CyclicGap);
        timer.Elapsed += (_, _) => SendCyclicMessages(device);
        timer.AutoReset = true;
        timer.Start();

        _cyclicTimers[device.Guid] = timer;
    }

    private void RemoveAllCyclicTimers()
    {
        foreach (var timer in _cyclicTimers)
        {
            timer.Value.Stop();
            timer.Value.Dispose();
        }

        _cyclicTimers.Clear();
    }

    private void RemoveCyclicTimer(Guid deviceId)
    {
        if (!_cyclicTimers.TryGetValue(deviceId, out var timer)) return;
        
        timer.Stop();
        _cyclicTimers.Remove(deviceId);
    }

    private void SendCyclicMessages(IDevice device)
    {
        var msgs = device.GetCyclicMsgs();
        if (msgs.Count == 0) return;

        foreach (var msg in msgs)
        {
            var devMsg = new DeviceCanFrame()
            {
                SendOnly = true,
                Frame = msg
            };
            QueueMessage(devMsg);
            Thread.Sleep(device.CyclicPause);
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
        // Queue for transmission
        if (_batchTransmitCallback != null)
        {
            _batchTransmitCallback([frame]);
        }
        else
        {
            logger.LogWarning("Transmit callback not set - message not transmitted");
            return;
        }

        //Some messages have no response, don't queue
        if (frame.SendOnly) return;

        int index = frame.Frame.Payload[2] << 8 | frame.Frame.Payload[1];
        int subIndex = frame.Frame.Payload[3];

        //Unique message key, used to find message in transmit queue later
        var key = (frame.DeviceBaseId, index, subIndex);

        if (!_requestQueue.TryAdd(key, frame))
        {
            logger.LogWarning("Message already in queue: BaseId={BaseId}, Prefix={Prefix:X}, Index={Index}",
                key.Item1, key.Item2, key.Item3);
            return;
        }

        // NOTE: Timer starts after transmission in OnFrameTransmitted
    }

    private void StartMessageTimer((int, int, int) key, DeviceCanFrame frame)
    {
        frame.TimeSentTimer = new Timer(_ => { HandleMessageTimeout(key, frame); }, null, TimeoutMs, Timeout.Infinite);
    }

    private void HandleMessageTimeout((int BaseId, int Prefix, int Index) key, DeviceCanFrame frame)
    {
        if (!_requestQueue.TryGetValue(key, out _))
            return;

        frame.RxAttempts++;

        if (frame.RxAttempts >= MaxRetries)
        {
            // Max retries exceeded - remove and log error
            _requestQueue.TryRemove(key, out _);
            frame.TimeSentTimer?.Dispose();

            int index =  frame.Frame.Payload[2] << 8 | frame.Frame.Payload[1];
            int subIndex = frame.Frame.Payload[3];
            
            var device = GetDeviceByBaseId(key.BaseId);
            logger.LogError("Message failed after {MaxRetries} retries: {Index:X}:{SubIndex} on {DeviceName} (ID: {BaseId}) - {Name}",
                MaxRetries, index, subIndex, device?.Name ?? "Unknown", key.BaseId, frame.Name);
        }
        else
        {
            // Retry - queue again
            _batchTransmitCallback?.Invoke([frame]);

            // NOTE: Timer restarts after transmission in OnFrameTransmitted

            logger.LogWarning("Message retry {Attempt}/{MaxRetries}: (BaseId={BaseId}) - {Name}",
                frame.RxAttempts, MaxRetries, key.BaseId, frame.Name);
        }
    }

    /// <summary>
    /// Called by CommsDataPipeline after a frame has been physically transmitted.
    /// Starts the response timeout timer only after the frame is actually sent.
    /// </summary>
    public void OnFrameTransmitted(DeviceCanFrame frame)
    {
        if (frame.SendOnly) return;

        int index = frame.Frame.Payload[2] << 8 | frame.Frame.Payload[1];
        int subIndex = frame.Frame.Payload[3];
        var key = (frame.DeviceBaseId, index, subIndex);

        if (_requestQueue.TryGetValue(key, out var queuedFrame))
            StartMessageTimer(key, queuedFrame);
    }

    /// <summary>
    /// Read configuration from device to host
    /// Only modified params
    /// </summary>
    public void ReadDeviceConfig(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device is not IDeviceConfigurable configurable)
            return;

        GetDeviceUiState(deviceId).NeedsRead = false;

        var readMsgs = configurable.GetReadMsgs(allParams: false);
        foreach (var msg in readMsgs)
        {
            QueueMessage(msg);
            Thread.Sleep(1); //Slow down to give device time to respond
        }
        
        logger.LogInformation("Read started for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
    }
    
    /// <summary>
    /// Read configuration from device to host
    /// All parameters
    /// </summary>
    public void ReadAllDeviceConfig(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device is not IDeviceConfigurable configurable)
            return;

        GetDeviceUiState(deviceId).NeedsRead = false;

        var readMsgs = configurable.GetReadMsgs(allParams: true);
        foreach (var msg in readMsgs)
        {
            QueueMessage(msg);
            Thread.Sleep(1); //Slow down to give device time to respond
        }

        logger.LogInformation("Read started for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
    }

    /// <summary>
    /// Write configuration to device
    /// Only modified parameters
    /// </summary>
    /// <returns>
    /// Send write config success
    /// </returns>
    public bool WriteDeviceConfig(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device is not IDeviceConfigurable configurable)
            return false;

        var downloadMsgs = configurable.GetWriteMsgs(allParams: false);
        foreach (var msg in downloadMsgs)
        {
            QueueMessage(msg);
            Thread.Sleep(1); //Slow down to give device time to respond
        }

        logger.LogInformation("Write started for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
        return true;
    }
    
    /// <summary>
    /// Write all configuration to device
    /// Write all parameters
    /// </summary>
    /// <returns>
    /// Send write config success
    /// </returns>
    public bool WriteAllDeviceConfig(Guid deviceId)
    {
        var device = GetDevice(deviceId);
        if (device is not IDeviceConfigurable configurable)
            return false;

        var downloadMsgs = configurable.GetWriteMsgs(allParams: true);
        foreach (var msg in downloadMsgs)
        {
            QueueMessage(msg);
            Thread.Sleep(1); //Slow down to give device time to respond
        }

        logger.LogInformation("Write started for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
        return true;
    }

    /// <summary>
    /// Modify device, name and base ID
    /// Sends modify message to device
    /// </summary>
    public void ModifyDeviceConfig(Guid deviceId, string newName, int baseId)
    {
        var device = GetDevice(deviceId);
        if (device is not IDeviceConfigurable configurable)
        {
            if (device == null) return;
            
            device.Name = newName;
            device.BaseId = baseId;
            return;
        }

        var modifyMsgs = configurable.GetModifyMsgs(baseId);
        foreach (var msg in modifyMsgs)
        {
            QueueMessage(msg);
            Thread.Sleep(1); //Slow down to give device time to respond
        }

        logger.LogInformation("Modify started for {DeviceName} (Guid: {Guid})", device.Name, deviceId);

        //Wait for modify messages to be sent, then update the base ID
        Thread.Sleep(300);

        device.Name = newName;
        device.BaseId = baseId;
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
        if (device is not IDeviceConfigurable configurable)
            return false;

        var burnMsg = configurable.GetBurnMsg();
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
        if (device is not IDeviceConfigurable configurable)
            return false;

        var sleepMsg = configurable.GetSleepMsg();

        if (sleepMsg == null)
        {
            logger.LogInformation("No sleep msg for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
            return false;
        }    
            
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
        if (device is not IDeviceConfigurable configurable)
            return false;

        var versionMsg = configurable.GetVersionMsg();
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
        if (device is not IDeviceConfigurable configurable)
            return false;

        var wakeupMsg = configurable.GetWakeupMsg();

        if (wakeupMsg == null)
        {
            logger.LogInformation("No wake up msg for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
            return false;
        }    
        
        QueueMessage(wakeupMsg);

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
        if (device is not IDeviceConfigurable configurable)
            return false;

        var bootloaderMsg = configurable.GetBootloaderMsg();

        if (bootloaderMsg == null)
        {
            logger.LogInformation("No bootloader msg for {DeviceName} (Guid: {Guid})", device.Name, deviceId);
            return false;    
        }
        
        QueueMessage(bootloaderMsg);

        logger.LogInformation("Enter bootloader on {DeviceName} (Guid: {Guid})", device.Name, deviceId);
        return true;
    }

    private void OnDeviceAdded(DeviceEventArgs e)
    {
        DeviceAdded?.Invoke(this, e);
    }

    private void OnDeviceRemoved(DeviceEventArgs e)
    {
        DeviceRemoved?.Invoke(this, e);
    }
}

public class DeviceEventArgs(IDevice device) : EventArgs
{
    public IDevice Device { get; } = device;
}