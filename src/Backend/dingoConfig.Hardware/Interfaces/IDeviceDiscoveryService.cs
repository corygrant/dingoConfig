using dingoConfig.Hardware.Models;

namespace dingoConfig.Hardware.Interfaces;

public interface IDeviceDiscoveryService : IDisposable
{
    event EventHandler<HardwareDevice>? DeviceFound;
    event EventHandler<HardwareDevice>? DeviceRemoved;
    event EventHandler<bool>? ScanningStateChanged;
    
    bool IsScanning { get; }
    
    Task StartScanningAsync();
    Task StopScanningAsync();
    Task<List<HardwareDevice>> GetAvailableDevicesAsync();
    Task<HardwareDevice?> GetDeviceAsync(string deviceId);
}