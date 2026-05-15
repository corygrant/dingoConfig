using domain.Enums;

namespace application.Models;

public class UserPreferences
{
    public string WorkingDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "dingoConfig");

    public string? SelectedAdapter { get; set; }
    public string? SelectedPort { get; set; }
    public CanBitRate SelectedBitrate { get; set; }
    public NumberFormat IdFormat { get; set; }
}
