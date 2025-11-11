using api.Models.Adapters;
using api.Enums;
using api.Models;

namespace api.Adapters;

public interface ICommsAdapterManager
{
    public AdapterAvailableResponse GetAvailable();
    public AdapterStatusResponse GetStatus();
    public ICommsAdapter ToAdapter(string adapterName);
    ICommsAdapter? ActiveAdapter { get; }
    bool IsConnected { get; }
    Task<bool> ConnectAsync(ICommsAdapter commsAdapter, string port, CanBitRate bitRate,  CancellationToken ct = default);
    Task DisconnectAsync();
    
    event EventHandler<CanDataEventArgs>? DataReceived;
}