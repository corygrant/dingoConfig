using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace infrastructure.Adapters;

public class SimAdapter : ICommsAdapter
{
    private TimeSpan _rxTimeDelta;
    public string? Name => "Sim";

    public Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<bool> StartAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<bool> StopAsync()
    {
        throw new NotImplementedException();
    }

    public Task<bool> WriteAsync(CanData data, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public event DataReceivedHandler? DataReceived;
    public TimeSpan RxTimeDelta()
    {
        throw new NotImplementedException();
    }

    TimeSpan ICommsAdapter.RxTimeDelta => _rxTimeDelta;

    public bool IsConnected { get; }
}