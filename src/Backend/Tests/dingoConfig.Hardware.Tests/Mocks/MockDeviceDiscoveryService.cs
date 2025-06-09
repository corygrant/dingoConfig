using dingoConfig.Hardware.Interfaces;
using dingoConfig.Hardware.Models;

namespace dingoConfig.Hardware.Tests.Mocks;

public class MockDeviceDiscoveryService : IDeviceDiscoveryService
{
    private readonly List<HardwareDevice> _availableDevices = new();
    private bool _isScanning = false;

    public event EventHandler<HardwareDevice>? DeviceFound;
    public event EventHandler<HardwareDevice>? DeviceRemoved;
    public event EventHandler<bool>? ScanningStateChanged;

    public bool IsScanning => _isScanning;

    public Task StartScanningAsync()
    {
        if (!_isScanning)
        {
            _isScanning = true;
            ScanningStateChanged?.Invoke(this, true);
        }
        return Task.CompletedTask;
    }

    public Task StopScanningAsync()
    {
        if (_isScanning)
        {
            _isScanning = false;
            ScanningStateChanged?.Invoke(this, false);
        }
        return Task.CompletedTask;
    }

    public Task<List<HardwareDevice>> GetAvailableDevicesAsync()
    {
        return Task.FromResult(new List<HardwareDevice>(_availableDevices));
    }

    public Task<HardwareDevice?> GetDeviceAsync(string deviceId)
    {
        var device = _availableDevices.FirstOrDefault(d => d.DeviceId == deviceId);
        return Task.FromResult(device);
    }

    // Mock-specific methods for testing
    public void AddMockDevice(HardwareDevice device)
    {
        if (!_availableDevices.Any(d => d.DeviceId == device.DeviceId))
        {
            _availableDevices.Add(device);
            
            if (_isScanning)
            {
                DeviceFound?.Invoke(this, device);
            }
        }
    }

    public void RemoveMockDevice(string deviceId)
    {
        var device = _availableDevices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (device != null)
        {
            _availableDevices.Remove(device);
            
            if (_isScanning)
            {
                DeviceRemoved?.Invoke(this, device);
            }
        }
    }

    public void SimulateDeviceFound(HardwareDevice device)
    {
        AddMockDevice(device);
    }

    public void SimulateDeviceRemoved(string deviceId)
    {
        RemoveMockDevice(deviceId);
    }

    public void ClearDevices()
    {
        var devicesToRemove = _availableDevices.ToList();
        _availableDevices.Clear();
        
        if (_isScanning)
        {
            foreach (var device in devicesToRemove)
            {
                DeviceRemoved?.Invoke(this, device);
            }
        }
    }

    public static HardwareDevice CreateMockPCANDevice()
    {
        return new HardwareDevice
        {
            DeviceId = "PCAN_USB_001",
            Name = "Peak PCAN-USB",
            Type = HardwareDeviceType.PCAN,
            ConnectionString = "PCAN_USBBUS1",
            IsConnected = false,
            Capabilities = new[] { "CAN", "USB" }
        };
    }

    public static HardwareDevice CreateMockSLCANDevice()
    {
        return new HardwareDevice
        {
            DeviceId = "SLCAN_001",
            Name = "SLCAN USB-CAN Adapter",
            Type = HardwareDeviceType.SLCAN,
            ConnectionString = "COM3",
            IsConnected = false,
            Capabilities = new[] { "CAN", "USB", "Serial" }
        };
    }

    public static HardwareDevice CreateMockUSBSerialDevice()
    {
        return new HardwareDevice
        {
            DeviceId = "USB_SERIAL_001",
            Name = "USB Serial Device",
            Type = HardwareDeviceType.USBSerial,
            ConnectionString = "/dev/ttyUSB0",
            IsConnected = false,
            Capabilities = new[] { "USB", "Serial" }
        };
    }

    public void Dispose()
    {
        _ = StopScanningAsync();
        _availableDevices.Clear();
    }
}