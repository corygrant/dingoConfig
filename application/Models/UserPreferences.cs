namespace application.Models;

public class UserPreferences
{
    public string WorkingDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "dingoConfig");

    public string? SelectedAdapter { get; set; }
    public string? SelectedPort { get; set; }
    public string? SelectedBitrate { get; set; }
}
