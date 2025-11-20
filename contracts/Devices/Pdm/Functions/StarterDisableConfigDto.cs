namespace contracts.Devices.Pdm.Functions;

public class StarterDisableConfigDto
{
    public int Number { get; set; } = 1; // Always 1 for singleton function
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Input { get; set; }
    public bool Output1 { get; set; }
    public bool Output2 { get; set; }
    public bool Output3 { get; set; }
    public bool Output4 { get; set; }
    public bool Output5 { get; set; }
    public bool Output6 { get; set; }
    public bool Output7 { get; set; }
    public bool Output8 { get; set; }
}