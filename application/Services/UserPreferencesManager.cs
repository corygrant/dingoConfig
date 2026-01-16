using System.Text.Json;
using application.Models;
using Microsoft.Extensions.Logging;

namespace application.Services;

public class UserPreferencesManager(ILogger<UserPreferencesManager> logger)
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly string _prefsPath = Path.Combine(
        AppContext.BaseDirectory,
        ".user-preferences.json");

    public UserPreferences Preferences { get; private set; } = new();

    public void Initialize()
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_prefsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Load preferences if file exists
            if (File.Exists(_prefsPath))
            {
                var json = File.ReadAllText(_prefsPath);
                var loadedPrefs = JsonSerializer.Deserialize<UserPreferences>(json, _options);
                if (loadedPrefs != null)
                {
                    Preferences = loadedPrefs;
                    logger.LogInformation("Loaded user preferences from {Path}", _prefsPath);
                }
            }
            else
            {
                // Create default preferences file
                Save();
                logger.LogInformation("Created default user preferences at {Path}", _prefsPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading user preferences from {Path}", _prefsPath);
            // Continue with default preferences
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Preferences, _options);
            File.WriteAllText(_prefsPath, json);
            logger.LogDebug("Saved user preferences to {Path}", _prefsPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving user preferences to {Path}", _prefsPath);
        }
    }
}
