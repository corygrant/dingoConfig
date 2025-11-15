using dingoConfig.Enums;
using dingoConfig.Models;
using dingoConfig.Models;

namespace dingoConfig.Adapters;

public delegate void DataReceivedHandler(object sender, CanDataEventArgs e);
public interface ICommsAdapter
{
    string? Name { get; }
    Task<bool>  InitAsync(string port, CanBitRate bitRate, CancellationToken ct);
    Task<bool>  StartAsync(CancellationToken ct);
    Task<bool>  StopAsync();
    Task<bool>  WriteAsync(CanData data, CancellationToken ct);
    
    event DataReceivedHandler? DataReceived;

    TimeSpan RxTimeDelta { get; }
    bool IsConnected { get;}
}