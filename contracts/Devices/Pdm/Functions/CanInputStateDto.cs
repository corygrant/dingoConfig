
namespace contracts.Devices.Pdm.Functions;

public class CanInputStateDto
{
    public string Name { get; set; } = string.Empty;
    public int Number { get; set; }
    public bool Enabled { get; set; }
    public bool TimeoutEnabled { get; set; }
    public int Timeout { get; set; }
    public bool Ide { get; set; }
    public int StartingByte { get; set; }
    public int Dlc { get; set; }
    public int Operator { get; set; }
    public int OnVal { get; set; }
    public int Mode { get; set; }
    public int Id { get; set; }
    
    // Real-time state properties
    public bool Output { get; set; }
    public int Value { get; set; }
}