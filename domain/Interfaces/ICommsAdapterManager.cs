using domain.Enums;
using domain.Events;

namespace domain.Interfaces;

public interface ICommsAdapterManager
{
    ICommsAdapter? ActiveAdapter { get; }
    bool IsConnected { get; }
    
    Task<bool> ConnectAsync(ICommsAdapter commsAdapter, string port, CanBitRate bitRate,  CancellationToken ct = default);
    Task DisconnectAsync();
    
    event EventHandler<CanDataEventArgs>? DataReceived;
}