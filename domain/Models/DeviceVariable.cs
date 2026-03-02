namespace domain.Models;

public class DeviceVariable
{
    public Func<string> GetName { get; init; } = null!;
    public string PropertyName { get; set; } = string.Empty;
    public int VariableIndex { get; set; }
    public bool SingleVariable { get; set; }
    public string DataType { get; set; } = string.Empty;
}