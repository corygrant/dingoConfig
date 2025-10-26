using System.Diagnostics;
using domain.Enums;
using domain.Events;
using domain.Models;

namespace domain.Interfaces;

public delegate void DataReceivedHandler(object sender, CanDataEventArgs e);
public interface ICommsAdapter
{
    Task<(bool success, string? error)> InitAsync(string port, CanBitRate bitRate, CancellationToken ct);
    Task<(bool success, string? error)> StartAsync(CancellationToken ct);
    Task<(bool success, string? error)> StopAsync();
    Task<(bool success, string? error)> WriteAsync(CanData data, CancellationToken ct);
    
    DataReceivedHandler DataReceived { get; set; }

    TimeSpan RxTimeDelta();
    bool IsConnected { get;}
}