using domain.Enums;
using domain.Models;

namespace domain.Interfaces;

public delegate void DataReceivedHandler(object sender, CanFrameEventArgs e);
public interface ICommsAdapter
{
    string? Name { get; }
    Task<bool>  InitAsync(string port, CanBitRate bitRate, CancellationToken ct);
    Task<bool>  StartAsync(CancellationToken ct);
    Task<bool>  StopAsync();
    Task<bool>  WriteAsync(CanFrame frame, CancellationToken ct);

    event DataReceivedHandler? DataReceived;
    event EventHandler? Disconnected;
    
    bool IsConnected { get;}
}