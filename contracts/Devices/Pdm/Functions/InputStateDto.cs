namespace contracts.Devices.Pdm.Functions;

public class InputStateDto
{
    public bool Enabled { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Number { get; set; }
    public bool State { get; set; }
    public bool Invert { get; set; }
    public int Mode { get; set; }
    public int DebounceTime { get; set; }
    public int Pull { get; set; }
}