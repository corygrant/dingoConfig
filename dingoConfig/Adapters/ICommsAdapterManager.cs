using dingoConfig.Models.Adapters;
using dingoConfig.Enums;
using dingoConfig.Models;

namespace dingoConfig.Adapters;

public interface ICommsAdapterManager
{
    public AdapterAvailableResponse GetAvailable();
    public AdapterStatusResponse GetStatus();
    public ICommsAdapter ToAdapter(string adapterName);
    ICommsAdapter? ActiveAdapter { get; }
    bool IsConnected { get; }
    Task<bool> ConnectAsync(ICommsAdapter commsAdapter, string port, CanBitRate bitRate,  CancellationToken ct = default);
    Task<bool> DisconnectAsync();
    
    event EventHandler<CanDataEventArgs>? DataReceived;
}