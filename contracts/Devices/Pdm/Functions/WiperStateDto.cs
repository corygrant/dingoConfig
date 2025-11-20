
namespace contracts.Devices.Pdm.Functions;

public class WiperStateDto
{
    public bool SlowState { get; set; }
    public bool FastState { get; set; }
    public int State { get; set; }
    public int Speed { get; set; }
}