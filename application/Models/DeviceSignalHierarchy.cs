namespace application.Models;

/// <summary>
/// Represents the complete hierarchy of signals from all devices
/// Device → Message → Signal
/// </summary>
public class DeviceSignalHierarchy
{
    public List<DeviceSignalNode> Devices { get; set; } = new();
}

/// <summary>
/// Represents a device in the hierarchy
/// </summary>
public class DeviceSignalNode
{
    public Guid DeviceGuid { get; set; }
    public string DeviceName { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public List<MessageSignalNode> Messages { get; set; } = new();
}

/// <summary>
/// Represents a CAN message containing signals
/// </summary>
public class MessageSignalNode
{
    public int MessageId { get; set; }
    public string MessageIdHex => $"0x{MessageId:X3}";
    public List<SignalNode> Signals { get; set; } = new();
}

/// <summary>
/// Represents a signal with all data needed for CanInput population
/// </summary>
public class SignalNode
{
    public Guid SourceDeviceGuid { get; set; }
    public domain.Models.DbcSignal Signal { get; set; } = null!;

    // Computed display name
    public string DisplayName => Signal.Unit.Length == 0 ? $"{Signal.Name}".Trim() : $"{Signal.Name} ({Signal.Unit})".Trim();
}
