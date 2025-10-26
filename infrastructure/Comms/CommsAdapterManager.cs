using System.Diagnostics;
using domain.Enums;
using domain.Events;
using domain.Interfaces;

namespace infrastructure.Comms;

public class CommsAdapterManager(ILogger<CommsAdapterManager> logger) : ICommsAdapterManager
{
    private readonly ILogger<CommsAdapterManager> _logger = logger;
    private ICommsAdapter? _activeAdapter;
    
    public ICommsAdapter?  ActiveAdapter => _activeAdapter;
    public bool IsConnected => _activeAdapter?.IsConnected ?? false;
    
    public event EventHandler<CanDataEventArgs>? DataReceived;
    
    public async Task<bool> ConnectAsync(ICommsAdapter commsAdapter, string port, CanBitRate bitRate, CancellationToken ct = default)
    {
        if (_activeAdapter != null)
        {
            await DisconnectAsync();
        }
        
        _activeAdapter = commsAdapter;
        
        _activeAdapter.DataReceived += OnDataReceived;

        var result = await _activeAdapter.InitAsync(port, bitRate, ct);

        if (!result.success)
        {
            _activeAdapter.DataReceived -= OnDataReceived;
            _activeAdapter = null;
            _logger.LogError($"Failed to initialize comms adapter: {result.error}");
            return false;
        }
        
        result = await _activeAdapter.StartAsync(ct);

        if (!result.success)
        {
            _activeAdapter.DataReceived -= OnDataReceived;
            _activeAdapter = null;
            _logger.LogError($"Failed to start comms adapter: {result.error}");
            return false;
        }

        _logger.LogInformation($"Adapter connected: {nameof(_activeAdapter)}");
        return true;
    }

    public Task<bool> ConnectAsync(ICommsAdapter commsAdapter, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public async Task DisconnectAsync()
    {
        if (_activeAdapter == null) return;
        
        _activeAdapter.DataReceived -= OnDataReceived;
        await _activeAdapter.StopAsync();

        _logger.LogInformation("Adapter disconnected: {AdapterName}", nameof(_activeAdapter));
        _activeAdapter = null;
    }

    private void OnDataReceived(object sender, CanDataEventArgs e)
    {
        DataReceived?.Invoke(this, e);
    }
    
}