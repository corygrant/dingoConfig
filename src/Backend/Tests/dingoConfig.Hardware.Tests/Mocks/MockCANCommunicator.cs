using dingoConfig.Core.Models;
using dingoConfig.Hardware.Interfaces;

namespace dingoConfig.Hardware.Tests.Mocks;

public class MockCANCommunicator : ICANCommunicator
{
    private bool _isConnected = false;
    private readonly List<CANMessage> _sentMessages = new();
    private readonly Queue<CANMessage> _receivedMessages = new();

    public event EventHandler<CANMessage>? MessageReceived;
    public event EventHandler<string>? ConnectionStatusChanged;

    public bool IsConnected => _isConnected;
    public string? ConnectionString { get; private set; }

    public Task<bool> ConnectAsync(string connectionString)
    {
        ConnectionString = connectionString;
        _isConnected = true;
        ConnectionStatusChanged?.Invoke(this, "Connected");
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        ConnectionString = null;
        ConnectionStatusChanged?.Invoke(this, "Disconnected");
        return Task.CompletedTask;
    }

    public Task<bool> SendMessageAsync(CANMessage message)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        _sentMessages.Add(message);
        return Task.FromResult(true);
    }

    // Mock-specific methods for testing
    public void SimulateMessageReceived(CANMessage message)
    {
        if (_isConnected)
        {
            MessageReceived?.Invoke(this, message);
        }
    }

    public void QueueMessageForReceiving(CANMessage message)
    {
        _receivedMessages.Enqueue(message);
    }

    public void SimulateConnectionLoss()
    {
        if (_isConnected)
        {
            _isConnected = false;
            ConnectionStatusChanged?.Invoke(this, "Connection Lost");
        }
    }

    public void SimulateReconnection()
    {
        if (!_isConnected && !string.IsNullOrEmpty(ConnectionString))
        {
            _isConnected = true;
            ConnectionStatusChanged?.Invoke(this, "Reconnected");
        }
    }

    public List<CANMessage> GetSentMessages() => new(_sentMessages);

    public void ClearSentMessages() => _sentMessages.Clear();

    public int SentMessageCount => _sentMessages.Count;

    public CANMessage? GetLastSentMessage() => _sentMessages.LastOrDefault();

    public void ProcessQueuedMessages()
    {
        while (_receivedMessages.Count > 0 && _isConnected)
        {
            var message = _receivedMessages.Dequeue();
            MessageReceived?.Invoke(this, message);
        }
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
        _sentMessages.Clear();
        _receivedMessages.Clear();
    }
}