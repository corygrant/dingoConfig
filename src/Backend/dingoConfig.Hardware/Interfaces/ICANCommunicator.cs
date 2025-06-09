using dingoConfig.Core.Models;

namespace dingoConfig.Hardware.Interfaces;

public interface ICANCommunicator : IDisposable
{
    event EventHandler<CANMessage>? MessageReceived;
    event EventHandler<string>? ConnectionStatusChanged;
    
    bool IsConnected { get; }
    string? ConnectionString { get; }
    
    Task<bool> ConnectAsync(string connectionString);
    Task DisconnectAsync();
    Task<bool> SendMessageAsync(CANMessage message);
}