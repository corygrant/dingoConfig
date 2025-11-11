namespace api.Models.Adapters;

public class AdapterAvailableResponse
{
    public required string[] Adapters { get; set; }
    public required string[] Ports { get; set; }
}