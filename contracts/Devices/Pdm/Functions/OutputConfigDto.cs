namespace contracts.Devices.Pdm.Functions;

public class OutputConfigDto
{
    public int Number { get; set; }
    public bool Enabled { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrentLimit { get; set; }
    public int ResetCountLimit { get; set; }
    public int ResetMode { get; set; }
    public double ResetTime { get; set; }
    public int InrushCurrentLimit { get; set; }
    public double InrushTime { get; set; }
    public int Input { get; set; }
    public bool SoftStartEnabled { get; set; }
    public bool VariableDutyCycle { get; set; }
    public int DutyCycleInput { get; set; }
    public int FixedDutyCycle { get; set; }
    public int Frequency { get; set; }
    public int SoftStartRampTime { get; set; }
    public int DutyCycleDenominator { get; set; }
}