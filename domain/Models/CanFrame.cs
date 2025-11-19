namespace domain.Models;

public class CanFrame
{
    public int Id { get; set; }
    public int Len { get; set; }
    public required byte[] Payload { get; set; }
}