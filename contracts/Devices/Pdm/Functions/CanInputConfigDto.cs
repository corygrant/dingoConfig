namespace contracts.Devices.Pdm.Functions;

public class CanInputConfigDto
{
    public int Number { get; set; }
    public bool Enabled { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool TimeoutEnabled { get; set; }
    public int Timeout { get; set; }
    public bool Ide { get; set; }
    public int StartingByte { get; set; }
    public int Dlc { get; set; }
    public int Operator { get; set; }
    public int OnVal { get; set; }
    public int Mode { get; set; }
    public int Id { get; set; }
}