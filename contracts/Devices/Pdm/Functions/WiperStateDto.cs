
namespace contracts.Devices.Pdm.Functions;

public class WiperStateDto
{
    public string Name { get; set; } = string.Empty;
    public int Number { get; set; } = 1; // Always 1 for singleton function
    public bool Enabled { get; set; }
    public int Mode { get; set; }
    public int SlowInput { get; set; }
    public int FastInput { get; set; }
    public int InterInput { get; set; }
    public int OnInput { get; set; }
    public int SpeedInput { get; set; }
    public int ParkInput { get; set; }
    public bool ParkStopLevel { get; set; }
    public int SwipeInput { get; set; }
    public int WashInput { get; set; }
    public int WashWipeCycles { get; set; }
    public int[] SpeedMap { get; set; } = new int[8];
    public double[] IntermitTime { get; set; } = new double[6];
    
    // Real-time state properties
    public bool SlowState { get; set; }
    public bool FastState { get; set; }
    public int State { get; set; }
    public int Speed { get; set; }
}