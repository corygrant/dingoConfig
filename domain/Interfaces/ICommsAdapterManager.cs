using domain.Enums;
using domain.Models;

namespace domain.Interfaces;

public interface ICommsAdapterManager
{
    (string[] adapters, string[] ports) GetAvailable();
    (bool isConnected, string? activeAdapter, string? activePort) GetStatus();
    ICommsAdapter ToAdapter(string adapterName);
    ICommsAdapter? ActiveAdapter { get; }
    bool IsConnected { get; }
    Task<bool> ConnectAsync(ICommsAdapter commsAdapter, string port, CanBitRate bitRate,  CancellationToken ct = default);
    Task<bool> DisconnectAsync();

    event EventHandler<CanFrameEventArgs>? DataReceived;
}