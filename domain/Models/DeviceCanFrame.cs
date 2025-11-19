namespace domain.Models;

public class DeviceCanFrame
{
    public int Prefix { get; set; }
    public int Index { get; set; }
    public required CanFrame Frame { get; set; }
    public bool Sent { get; set; }
    public bool Received { get; set; }
    public Timer TimeSentTimer { get; set; }
    public int RxAttempts { get; set; }
    public int DeviceBaseId { get; set; }
    public string? MsgDescription { get; set; }
}