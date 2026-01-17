using System.IO.Ports;
using domain.Enums;
using domain.Models;
using domain.Interfaces;
using infrastructure.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace infrastructure.Comms;

public class CommsAdapterManager(IServiceProvider serviceProvider, ILogger<CommsAdapterManager> logger) : ICommsAdapterManager
{
    private ICommsAdapter? _activeAdapter;
    
    public ICommsAdapter?  ActiveAdapter => _activeAdapter;

    private string? _activePort;

    public bool IsConnected => _activeAdapter?.IsConnected ?? false;

    public event EventHandler<CanFrameEventArgs>? DataReceived;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    

    public (string[] adapters, string[] ports) GetAvailable()
    {
        var serialPorts = SerialPort.GetPortNames();

        // Discover CAN interfaces (Linux only)
        string[] canIfaces = [];
        try
        {
            canIfaces = Directory.GetDirectories("/sys/class/net")
                .Select(Path.GetFileName)
                .Where(n => n.StartsWith("can") || n.StartsWith("vcan"))
                .ToArray();
        }
        catch
        {
            // Windows: ignore
        }

        // Adapter list with platform guard
        var adapters = new List<string> { "USB", "SLCAN", "PCAN", "Sim" };

        #if LINUX
        #warning Building with LINUX define enabled
        adapters.Add("SocketCAN");
        #endif

        return (
            adapters: adapters.ToArray(),
            ports: serialPorts.Concat(canIfaces).ToArray()
        );
    }        

    public (bool isConnected, string? activeAdapter, string? activePort) GetStatus()
    {
        return (
            isConnected: IsConnected,
            activeAdapter: ActiveAdapter?.Name,
            activePort: _activePort
        );
    }

    public ICommsAdapter ToAdapter(string adapterName)
    {
        return adapterName switch
        {
            "USB" => serviceProvider.GetRequiredService<UsbAdapter>(),
            "SLCAN" => serviceProvider.GetRequiredService<SlcanAdapter>(),
            #if LINUX
            #warning Building with LINUX define enabled
            "SocketCAN" => serviceProvider.GetRequiredService<SocketCanAdapter>(),
            #endif
            "PCAN" => serviceProvider.GetRequiredService<PcanAdapter>(),
            "Sim" => serviceProvider.GetRequiredService<SimAdapter>(),
            _ => throw new ArgumentException($"Unknown adapter type: {adapterName}")
        };
    }
    
    public async Task<bool> ConnectAsync(ICommsAdapter commsAdapter, string port, CanBitRate bitRate, CancellationToken ct = default)
    {
        if (_activeAdapter is { IsConnected: true })
        {
            await DisconnectAsync();
        }
        
        _activeAdapter = commsAdapter;
        _activePort = port;

        OnConnect(commsAdapter);

        _activeAdapter.DataReceived += OnDataReceived;
        _activeAdapter.Disconnected += OnDisconnected;

        var result = await _activeAdapter.InitAsync(port, bitRate, ct);

        if (result == false)
        {
            _activeAdapter.DataReceived -= OnDataReceived;
            _activeAdapter.Disconnected -= OnDisconnected;
            _activeAdapter = null;
            logger.LogError("Failed to initialize comms adapter");
            return await Task.FromResult(false);
        }

        result = await _activeAdapter.StartAsync(ct);

        if (!result)
        {
            _activeAdapter.DataReceived -= OnDataReceived;
            _activeAdapter.Disconnected -= OnDisconnected;
            _activeAdapter = null;
            logger.LogError("Failed to start comms adapter");
            return await Task.FromResult(false);
        }

        logger.LogInformation($"Adapter connected: {_activeAdapter.Name}");
        return await Task.FromResult(true);
    }

    public async Task<bool> DisconnectAsync()
    {
        if (_activeAdapter == null) return await Task.FromResult(false);

        if (_activeAdapter != null)
        {
            _activeAdapter.DataReceived -= OnDataReceived;
            _activeAdapter.Disconnected -= OnDisconnected;
            await _activeAdapter.StopAsync();

            logger.LogInformation("Adapter disconnected: {AdapterName}", _activeAdapter.Name);
        }

        return await Task.FromResult(true);
    }

    private void OnDataReceived(object sender, CanFrameEventArgs e)
    {
        DataReceived?.Invoke(this, e);
    }

    private void OnConnect(object sender)
    {
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        _ = DisconnectAsync();
        logger.LogWarning("Adapter disconnected: {AdapterName}", _activeAdapter?.Name ?? "Unknown");
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

}
