using System.ComponentModel.DataAnnotations;

namespace contracts.Adapters;

public class ConnectAdapterRequest
{
    [Required(ErrorMessage = "AdapterType is required")]
    public string AdapterType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Port is required")]
    public string Port { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bitrate is required")]
    [RegularExpression("^(1000K|500K|250K|125K)$", ErrorMessage = "Bitrate must be one of: 1000K, 500K, 250K, 125K")]
    public string Bitrate { get; set; } = string.Empty;
}