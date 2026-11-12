namespace application.Models;

/// <summary>
/// A config file that was opened or saved, for the recent projects list on the home page.
/// </summary>
public class RecentProject
{
    public string Path { get; set; } = "";
    public DateTime LastUsed { get; set; }

    /// <summary>
    /// "name (type)" of each device in the file when it was last opened or saved, so the
    /// list can say what a project is without reading every file.
    /// </summary>
    public List<string> Devices { get; set; } = [];
}
