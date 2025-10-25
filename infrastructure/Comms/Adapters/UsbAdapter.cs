using System.Diagnostics;
using System.IO.Ports;
using domain.Enums;
using domain.Interfaces;

namespace infrastructure.Comms.Adapters;

public class UsbAdapter : ICommsAdapter
{
    private static SerialPort _serial;
    private static Stopwatch _rxStopwatch;
    private readonly int _rxTimeDelta;

    public Task<(bool success, string? error)> InitAsync(string port, CanBitRate bitRate)
    {
        try
        {
            _serial = new SerialPort(port, 115200, Parity.None, 8, StopBits.One);
            _serial.Handshake = Handshake.None;
            _serial.NewLine = "\r";
        }
        catch (Exception e)
        {
            return Task.FromResult<(bool success, string? error)>((false, e.Message));
        }

        return Task.FromResult<(bool success, string? error)>((true, null));
    }

    public Task<(bool success, string? error)> StartAsync()
    {
        try
        {
            _serial.Open();

            _rxStopwatch = Stopwatch.StartNew();
        }
        catch (Exception e)
        {
            return Task.FromResult<(bool success, string? error)>((false, e.Message));
        }
        
        return Task.FromResult<(bool success, string? error)>((true, null));
        //return Task.FromResult<(bool success, string? error)>((_serial.IsOpen(), null));
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