using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace infrastructure.Comms.Adapters;

public class SimAdapter : ICommsAdapter
{
    private TimeSpan _rxTimeDelta;
    public string? Name { get; set; }

    public bool InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public bool StartAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public bool StopAsync()
    {
        throw new NotImplementedException();
    }

    public bool WriteAsync(CanData data, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public DataReceivedHandler DataReceived { get; set; }
    public TimeSpan RxTimeDelta()
    {
        throw new NotImplementedException();
    }

    TimeSpan ICommsAdapter.RxTimeDelta => _rxTimeDelta;

    public bool IsConnected { get; }
}