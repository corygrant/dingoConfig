using application.Models;
using application.Services;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace infrastructure.Adapters;

public class SimAdapter : ICommsAdapter
{
    private readonly SimPlayback _playback;
    private TimeSpan _rxTimeDelta = TimeSpan.FromMilliseconds(50);

    public string? Name => "Sim";

    public SimAdapter(SimPlayback playback)
    {
        _playback = playback;
    }

    public Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        IsConnected = false;
        return Task.FromResult(true);
    }

    public Task<bool> StartAsync(CancellationToken ct)
    {
        IsConnected = true;
        _playback.MessageReady += OnMessageReady;
        return Task.FromResult(true);
    }

    public Task<bool> StopAsync()
    {
        _playback.MessageReady -= OnMessageReady;
        IsConnected = false;
        return Task.FromResult(true);
    }

    public Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        return Task.FromResult(true);
    }

    private void OnMessageReady(CanFrame frame, DataDirection direction)
    {
        _rxTimeDelta = TimeSpan.FromMilliseconds(50); // Simulate 20 Hz
        DataReceived?.Invoke(this, new CanFrameEventArgs(frame));
    }

    public event DataReceivedHandler? DataReceived;

    public TimeSpan RxTimeDelta()
    {
        return _rxTimeDelta;
    }

    TimeSpan ICommsAdapter.RxTimeDelta => _rxTimeDelta;

    public bool IsConnected { get; set; }
}
