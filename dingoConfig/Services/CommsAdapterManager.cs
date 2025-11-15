using System.IO.Ports;
using dingoConfig.Models.Adapters;
using dingoConfig.Enums;
using dingoConfig.Models;
using dingoConfig.Adapters;
using dingoConfig.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace dingoConfig.Services;

public class CommsAdapterManager(IServiceProvider serviceProvider, ILogger<CommsAdapterManager> logger) : ICommsAdapterManager
{
    private ICommsAdapter? _activeAdapter;
    
    public ICommsAdapter?  ActiveAdapter => _activeAdapter;

    private string? _activePort;

    public bool IsConnected => _activeAdapter?.IsConnected ?? false;

    public event EventHandler<CanDataEventArgs>? DataReceived;

    public AdapterAvailableResponse GetAvailable()
    {
        return new AdapterAvailableResponse
        {
            Adapters = ["USB", "SLCAN", "PCAN", "Sim"],
            Ports = SerialPort.GetPortNames(),
        };
    }

    public AdapterStatusResponse GetStatus()
    {
        return new AdapterStatusResponse
        {
            IsConnected = IsConnected,
            ActiveAdapter = ActiveAdapter?.Name,
            ActivePort = _activePort,
        };
    }

    public ICommsAdapter ToAdapter(string adapterName)
    {
        return adapterName switch
        {
            "USB" => serviceProvider.GetRequiredService<UsbAdapter>(),
            "SLCAN" => serviceProvider.GetRequiredService<SlcanAdapter>(),
            "PCAN" => serviceProvider.GetRequiredService<PcanAdapter>(),
            "Sim" => serviceProvider.GetRequiredService<SimAdapter>(),
            _ => throw new ArgumentException($"Unknown adapter type: {adapterName}")
        };
    }
    
    public async Task<bool> ConnectAsync(ICommsAdapter commsAdapter, string port, CanBitRate bitRate, CancellationToken ct = default)
    {
        if (_activeAdapter != null)
        {
            await DisconnectAsync();
        }
        
        _activeAdapter = commsAdapter;
        _activePort = port;
        
        _activeAdapter.DataReceived += OnDataReceived;

        var result = await _activeAdapter.InitAsync(port, bitRate, ct);

        if (result == false)
        {
            _activeAdapter.DataReceived -= OnDataReceived;
            _activeAdapter = null;
            logger.LogError("Failed to initialize comms adapter");
            return await Task.FromResult(false);
        }
        
        result = await _activeAdapter.StartAsync(ct);

        if (!result)
        {
            _activeAdapter.DataReceived -= OnDataReceived;
            _activeAdapter = null;
            logger.LogError("Failed to start comms adapter");
            return await Task.FromResult(false);
        }

        logger.LogInformation($"Adapter connected: {nameof(_activeAdapter)}");
        return await Task.FromResult(true);
    }

    public async Task<bool> DisconnectAsync()
    {
        if (_activeAdapter == null) return await Task.FromResult(false);

        if (_activeAdapter != null)
        {
            _activeAdapter.DataReceived -= OnDataReceived;
            await _activeAdapter.StopAsync();

            logger.LogInformation("Adapter disconnected: {AdapterName}", nameof(_activeAdapter));
        }

        _activeAdapter = null;

        return await Task.FromResult(true);
    }

    private void OnDataReceived(object sender, CanDataEventArgs e)
    {
        DataReceived?.Invoke(this, e);
    }
    
}