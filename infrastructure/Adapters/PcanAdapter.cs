using System.Diagnostics;
using domain.Enums;
using domain.Interfaces;
using domain.Models;
using Peak.Can.Basic;
using MessageType = Peak.Can.Basic.MessageType;

namespace infrastructure.Adapters;

public class PcanAdapter  : ICommsAdapter
{
    public string Name => "PCAN";
    
    private Worker? _worker;
    
    public event DataReceivedHandler? DataReceived;

    private Stopwatch? _rxStopwatch;
    public TimeSpan RxTimeDelta { get; private set; }
    public bool IsConnected => RxTimeDelta < TimeSpan.FromMilliseconds(500);

    public Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        _worker = new Worker(PcanChannel.Usb01, ConvertBaudRate(bitRate));
        _rxStopwatch = Stopwatch.StartNew();
        return Task.FromResult(true);
    }

    public Task<bool> StartAsync(CancellationToken ct)
    {
        if (_worker == null) return Task.FromResult(false);
        
        _worker.MessageAvailable += OnMessageAvailable;
        try
        {
            _worker.Start(true);
        }
        catch (PcanBasicException e)
        {
            Console.WriteLine(e.ToString());
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> StopAsync()
    {
        if (_worker == null) return Task.FromResult(false);
        
        _worker.MessageAvailable -= OnMessageAvailable;
        _worker.Stop();
        
        return Task.FromResult(true);
    }

    public Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        if (_worker == null || frame.Payload.Length != 8 || !(_worker.Active)) return Task.FromResult(false);

        var msg = new PcanMessage((uint)frame.Id, MessageType.Standard, (byte)frame.Len, frame.Payload);

        return Task.FromResult(_worker.Transmit(msg, out _));
    }

    private void OnMessageAvailable(object? sender, MessageAvailableEventArgs e)
    {
        if (_worker == null) return;
        
        if (!_worker.Dequeue(e.QueueIndex, out var msg, out _)) return;
        
        if (_rxStopwatch != null)
        {
            RxTimeDelta = new TimeSpan(_rxStopwatch.ElapsedMilliseconds);
            _rxStopwatch.Restart();
        }

        if (msg.DLC <= 0) return;

        // Only copy the actual number of bytes specified by DLC, not the full 8-byte buffer
        // The length of the payload array is checked by receiving devices
        var payload = new byte[msg.DLC];
        Array.Copy(msg.Data, payload, msg.DLC);

        var frame = new CanFrame
        (
            Id: Convert.ToInt16(msg.ID),
            Len: Convert.ToInt16(msg.DLC),
            Payload: payload
        );
        DataReceived?.Invoke(this, new CanFrameEventArgs(frame));

    }
    
    private static Bitrate ConvertBaudRate(CanBitRate baud)
    {
        return baud switch
        {
            CanBitRate.BitRate1000K => Bitrate.Pcan1000,
            CanBitRate.BitRate500K => Bitrate.Pcan500,
            CanBitRate.BitRate250K => Bitrate.Pcan250,
            CanBitRate.BitRate125K => Bitrate.Pcan125,
            _ => Bitrate.Pcan500
        };
    }
}