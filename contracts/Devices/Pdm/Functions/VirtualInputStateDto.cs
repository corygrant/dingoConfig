
namespace contracts.Devices.Pdm.Functions;

public class VirtualInputStateDto
{
    public string Name { get; set; } = string.Empty;
    public int Number { get; set; }
    public bool Enabled { get; set; }
    public bool Not0 { get; set; }
    public int Var0 { get; set; }
    public int Cond0 { get; set; }
    public bool Not1 { get; set; }
    public int Var1 { get; set; }
    public int Cond1 { get; set; }
    public bool Not2 { get; set; }
    public int Var2 { get; set; }
    public int Cond2 { get; set; }
    public int Mode { get; set; }
    
    // Real-time state property
    public bool Value { get; set; }
}