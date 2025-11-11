namespace api.Models.Adapters;

public class ConnectAdapterRequest
{
    public string AdapterType { get; set; } =  string.Empty;
    public string Port { get; set; } =  string.Empty;
    public string Bitrate { get; set; } =  string.Empty;
}