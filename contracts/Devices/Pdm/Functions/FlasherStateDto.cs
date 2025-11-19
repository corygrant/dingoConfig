
namespace contracts.Devices.Pdm.Functions;

public class FlasherStateDto
{
    public string Name { get; set; } = string.Empty;
    public int Number { get; set; }
    public bool Enabled { get; set; }
    public bool Single { get; set; }
    public int Input { get; set; }
    public int OnTime { get; set; }
    public int OffTime { get; set; }
    
    // Real-time state property
    public bool Value { get; set; }
}