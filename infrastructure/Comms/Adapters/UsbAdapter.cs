using System.Diagnostics;
using System.IO.Ports;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace infrastructure.Comms.Adapters;

public class UsbAdapter : ICommsAdapter
{
    private static SerialPort _serial;
    private static Stopwatch _rxStopwatch;
    private readonly int _rxTimeDelta;

    public Task<(bool success, string? error)> InitAsync(string port, CanBitRate bitRate, CancellationToken ct = default)
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

    public Task<(bool success, string? error)> StartAsync(CancellationToken ct)
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

    public Task<(bool success, string? error)> WriteAsync(CanData data, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public DataReceivedHandler DataReceived { get; set; }

    public Task<(bool success, string? error)> WriteAsync()
    {
        throw new NotImplementedException();
    }

    public TimeSpan RxTimeDelta()
    {
        throw new NotImplementedException();
    }

    public bool IsConnected { get; }
}