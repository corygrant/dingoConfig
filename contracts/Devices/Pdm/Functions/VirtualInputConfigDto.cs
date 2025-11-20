namespace contracts.Devices.Pdm.Functions;

public class VirtualInputConfigDto
{
    public int Number { get; set; }
    public bool Enabled { get; set; }
    public string Name { get; set; } = string.Empty;
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
}