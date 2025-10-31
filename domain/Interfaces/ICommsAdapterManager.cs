using contracts.Adapters;
using domain.Enums;
using domain.Events;

namespace domain.Interfaces;

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