
namespace contracts.Devices.Pdm.Functions;

public class OutputStateDto
{
    public double Current { get; set; }
    public int State { get; set; }
    public int ResetCount { get; set; }
    public double CurrentDutyCycle { get; set; }
    public double CalculatedPower { get; set; }
}