
namespace contracts.Devices.Pdm.Functions;

public class CounterStateDto
{
    public string Name { get; set; } = string.Empty;
    public int Number { get; set; }
    public bool Enabled { get; set; }
    public int IncInput { get; set; }
    public int DecInput { get; set; }
    public int ResetInput { get; set; }
    public int MinCount { get; set; }
    public int MaxCount { get; set; }
    public int IncEdge { get; set; }
    public int DecEdge { get; set; }
    public int ResetEdge { get; set; }
    public bool WrapAround { get; set; }
    
    // Real-time state property
    public int Value { get; set; }
}