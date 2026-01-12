using application.Models;
using application.Services;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace infrastructure.Adapters;

public class SimAdapter(SimPlayback playback) : ICommsAdapter
{
    public string Name => "Sim";

    public Task<bool> InitAsync(string port, CanBitRate bitRate, CancellationToken ct)
    {
        IsConnected = false;
        return Task.FromResult(true);
    }

    public Task<bool> StartAsync(CancellationToken ct)
    {
        IsConnected = true;
        playback.MessageReady += OnMessageReady;
        return Task.FromResult(true);
    }

    public Task<bool> StopAsync()
    {
        playback.MessageReady -= OnMessageReady;
        playback.Clear();
        IsConnected = false;
        return Task.FromResult(true);
    }

    public Task<bool> WriteAsync(CanFrame frame, CancellationToken ct)
    {
        return Task.FromResult(true);
    }

    private void OnMessageReady(CanFrame frame, DataDirection direction)
    {
        DataReceived?.Invoke(this, new CanFrameEventArgs(frame));
    }

    public event DataReceivedHandler? DataReceived;
    public event EventHandler? Disconnected;

    public bool IsConnected { get; private set; }
}
