using api.Enums;
using api.Adapters;
using api.Models;

namespace api.Adapters;

public class PcanAdapter  : ICommsAdapter
{
    private TimeSpan _rxTimeDelta;
    public string? Name => "PCAN";

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