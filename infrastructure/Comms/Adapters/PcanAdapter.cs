using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace infrastructure.Comms.Adapters;

public class PcanAdapter  : ICommsAdapter
{
    public Task<(bool success, string? error)> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<(bool success, string? error)> StartAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<(bool success, string? error)> StopAsync()
    {
        throw new NotImplementedException();
    }

    public Task<(bool success, string? error)> WriteAsync(CanData data, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public DataReceivedHandler DataReceived { get; set; }
    public TimeSpan RxTimeDelta()
    {
        throw new NotImplementedException();
    }

    public bool IsConnected { get; }
}