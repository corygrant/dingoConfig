using dingoConfig.Hardware.Interfaces;

namespace dingoConfig.Hardware.Tests.Mocks;

public class MockUSBCommunicator : IUSBCommunicator
{
    private bool _isConnected = false;
    private readonly List<byte[]> _sentData = new();
    private readonly Queue<byte[]> _receivedData = new();

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<string>? ConnectionStatusChanged;

    public bool IsConnected => _isConnected;
    public string? PortName { get; private set; }
    public int BaudRate { get; private set; }

    public Task<bool> ConnectAsync(string portName, int baudRate = 115200)
    {
        PortName = portName;
        BaudRate = baudRate;
        _isConnected = true;
        ConnectionStatusChanged?.Invoke(this, "Connected");
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        PortName = null;
        BaudRate = 0;
        ConnectionStatusChanged?.Invoke(this, "Disconnected");
        return Task.CompletedTask;
    }

    public Task<bool> SendDataAsync(byte[] data)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        _sentData.Add(data.ToArray());
        return Task.FromResult(true);
    }

    public Task<string[]> GetAvailablePortsAsync()
    {
        return Task.FromResult(new[] { "COM1", "COM2", "/dev/ttyUSB0", "/dev/ttyACM0" });
    }

    // Mock-specific methods for testing
    public void SimulateDataReceived(byte[] data)
    {
        if (_isConnected)
        {
            DataReceived?.Invoke(this, data);
        }
    }

    public void QueueDataForReceiving(byte[] data)
    {
        _receivedData.Enqueue(data);
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
        if (!_isConnected && !string.IsNullOrEmpty(PortName))
        {
            _isConnected = true;
            ConnectionStatusChanged?.Invoke(this, "Reconnected");
        }
    }

    public List<byte[]> GetSentData() => new(_sentData);

    public void ClearSentData() => _sentData.Clear();

    public int SentDataCount => _sentData.Count;

    public byte[]? GetLastSentData() => _sentData.LastOrDefault();

    public void ProcessQueuedData()
    {
        while (_receivedData.Count > 0 && _isConnected)
        {
            var data = _receivedData.Dequeue();
            DataReceived?.Invoke(this, data);
        }
    }

    public void SimulateSLCANResponse(string command, string response)
    {
        var responseBytes = System.Text.Encoding.ASCII.GetBytes(response + "\r");
        SimulateDataReceived(responseBytes);
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
        _sentData.Clear();
        _receivedData.Clear();
    }
}