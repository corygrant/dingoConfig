using System.Diagnostics;
using System.IO.Ports;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace infrastructure.Adapters;

public class UsbAdapter : ICommsAdapter
{
    private static SerialPort _serial;
    private static Stopwatch _rxStopwatch;
    private readonly int _rxTimeDelta;
    private TimeSpan _rxTimeDelta1;

    public string? Name => "USB";

    public Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct = default)
    {
        try
        {
            _serial = new SerialPort(port, 115200, Parity.None, 8, StopBits.One);
            _serial.Handshake = Handshake.None;
            _serial.NewLine = "\r";
        }
        catch (Exception e)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> StartAsync(CancellationToken ct)
    {
        try
        {
            _serial.Open();

            _rxStopwatch = Stopwatch.StartNew();
        }
        catch (Exception e)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
        //return Task.FromResult<(bool success, string? error)>((_serial.IsOpen(), null));
    }

    public Task<bool> StopAsync()
    {
        throw new NotImplementedException();
    }

    public Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public event DataReceivedHandler? DataReceived;

    public Task<(bool success, string? error)> WriteAsync()
    {
        throw new NotImplementedException();
    }

    public TimeSpan RxTimeDelta()
    {
        throw new NotImplementedException();
    }

    TimeSpan ICommsAdapter.RxTimeDelta => _rxTimeDelta1;

    public bool IsConnected { get; }
}