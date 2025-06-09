namespace dingoConfig.Core.Models;

public class CANMessage
{
    public uint Id { get; set; }
    public byte[] Data { get; set; } = new byte[8];
    public DateTime Timestamp { get; set; }
    public bool IsExtended { get; set; }
    public bool IsRemote { get; set; }
    public int DataLength => Data?.Length ?? 0;
}