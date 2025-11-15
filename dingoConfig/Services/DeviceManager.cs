using dingoConfig.Adapters;
using dingoConfig.Devices;
using Microsoft.Extensions.Logging;

namespace dingoConfig.Services;

public class DeviceManager(ILogger<DeviceManager> logger)
{
    private List<IDevice> _devices = [];

    public async Task GetAllStatus()
    {
        
    }

    public List<IDevice> GetDevices()
    {
        return _devices;
    }
    
    public async Task AddDevice(Guid deviceId)
    {
        
    }

    public void AddDevices(List<IDevice> devices)
    {
        _devices.AddRange(devices);
    }

    public async Task RemoveDevice(Guid deviceId)
    {
        
    }

    public async Task RemoveAllDevices()
    {
        
    }

    public async Task UpdateDevice(Guid deviceId)
    {
        
    }
    
    public async Task Write(Guid deviceId)
    {
        
    }
    
    public async Task Read(Guid deviceId)
    {
        
    }
    
    public async Task Burn(Guid deviceId)
    {
        
    }
    
    public async Task FwUpdate(Guid deviceId)
    {
        
    }
    
    public async Task Sleep(Guid deviceId)
    {
        
    }
    
    public async Task Wake(Guid deviceId)
    {
        
    }
}