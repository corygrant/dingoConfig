using api.Enums;
using api.Models;
using api.Models;

namespace api.Adapters;

public delegate void DataReceivedHandler(object sender, CanDataEventArgs e);
public interface ICommsAdapter
{
    string? Name { get; }
    bool InitAsync(string port, CanBitRate bitRate, CancellationToken ct);
    bool StartAsync(CancellationToken ct);
    bool StopAsync();
    bool WriteAsync(CanData data, CancellationToken ct);
    
    DataReceivedHandler DataReceived { get; set; }

    TimeSpan RxTimeDelta { get; }
    bool IsConnected { get;}
}