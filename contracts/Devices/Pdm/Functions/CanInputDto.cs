using domain.Devices.dingoPdm.Enums;
using domain.Enums;

namespace contracts.Devices.Pdm.Functions;

public class CanInputDto(int number)
{
    // Config properties
    public int Number { get; } = number;
    public bool Enabled { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool TimeoutEnabled { get; set; }
    public int Timeout { get; set; }
    public bool Ide { get; set; }
    public int StartingByte { get; set; }
    public int Dlc { get; set; }
    public Operator Operator { get; set; }
    public int OnVal { get; set; }
    public InputMode Mode { get; set; }
    public int Id { get; set; }

    // State properties
    public bool Output { get; set; }
    public int Value { get; set; }
    
    public CanInputDto(CanInputDto source) : this(source.Number)
    {
        Enabled = source.Enabled;
        Name = source.Name;
        TimeoutEnabled = source.TimeoutEnabled;
        Timeout = source.Timeout;
        Ide = source.Ide;
        StartingByte = source.StartingByte;
        Dlc = source.Dlc;
        Operator = source.Operator;
        OnVal = source.OnVal;
        Mode = source.Mode;
        Id = source.Id;
        Output = source.Output;
        Value = source.Value;
    }
    
    public void CopyFrom(CanInputDto source)
    {
        Enabled = source.Enabled;
        Name = source.Name;
        TimeoutEnabled = source.TimeoutEnabled;
        Timeout = source.Timeout;
        Ide = source.Ide;
        StartingByte = source.StartingByte;
        Dlc = source.Dlc;
        Operator = source.Operator;
        OnVal = source.OnVal;
        Mode = source.Mode;
        Id = source.Id;
    }
}
