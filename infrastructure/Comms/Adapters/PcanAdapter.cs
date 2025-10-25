using domain.Enums;
using domain.Interfaces;

namespace infrastructure.Comms.Adapters;

public class PcanAdapter  : ICommsAdapter
{
    public Task<(bool success, string? error)> InitAsync(string port, CanBitRate bitRate)
    {
        throw new NotImplementedException();
    }

    public Task<(bool success, string? error)> StartAsync()
    {
        throw new NotImplementedException();
    }

    public Task<(bool success, string? error)> StopAsync()
    {
        throw new NotImplementedException();
    }

    public Task<(bool success, string? error)> WriteAsync()
    {
        throw new NotImplementedException();
    }

    public TimeSpan RxTimeDelta()
    {
        throw new NotImplementedException();
    }
}