namespace dingoConfig.Hardware.Interfaces;

public interface IUSBCommunicator : IDisposable
{
    event EventHandler<byte[]>? DataReceived;
    event EventHandler<string>? ConnectionStatusChanged;
    
    bool IsConnected { get; }
    string? PortName { get; }
    int BaudRate { get; }
    
    Task<bool> ConnectAsync(string portName, int baudRate = 115200);
    Task DisconnectAsync();
    Task<bool> SendDataAsync(byte[] data);
    Task<string[]> GetAvailablePortsAsync();
}