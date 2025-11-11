namespace api.Models;

public class CanData
{
    public int Id { get; set; }
    public int Len { get; set; }
    public required byte[] Payload { get; set; }
}