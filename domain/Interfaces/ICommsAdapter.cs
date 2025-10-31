using domain.Enums;
using domain.Events;
using domain.Models;

namespace domain.Interfaces;

public delegate void DataReceivedHandler(object sender, CanDataEventArgs e);
public interface ICommsAdapter
{
    string? Name { get; set; }
    bool InitAsync(string port, CanBitRate bitRate, CancellationToken ct);
    bool StartAsync(CancellationToken ct);
    bool StopAsync();
    bool WriteAsync(CanData data, CancellationToken ct);
    
    DataReceivedHandler DataReceived { get; set; }

    TimeSpan RxTimeDelta { get; }
    bool IsConnected { get;}
}