namespace dingoConfig.Models.Adapters;

public class AdapterStatusResponse
{
    public bool IsConnected { get; set; }
    public string? ActiveAdapter { get; set; } = string.Empty;
    
    public string? ActivePort { get; set; } =  string.Empty;
    
}