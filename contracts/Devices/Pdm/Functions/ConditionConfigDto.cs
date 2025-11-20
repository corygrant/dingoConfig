namespace contracts.Devices.Pdm.Functions;

public class ConditionConfigDto
{
    public string Name { get; set; } = string.Empty;
    public int Number { get; set; }
    public bool Enabled { get; set; }
    public int Input { get; set; }
    public int Operator { get; set; }
    public int Arg { get; set; }
}