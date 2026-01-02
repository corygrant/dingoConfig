using MudBlazor;
using application.Models;
using application.Services;

namespace api.Services;

public class NotificationService
{
    private readonly ISnackbar _snackbar;
    private readonly SystemLogger _logger;

    public NotificationService(ISnackbar snackbar, SystemLogger logger)
    {
        _snackbar = snackbar;
        _logger = logger;

        // Subscribe to backend error logs
        _logger.OnError += HandleBackendError;
    }

    private void HandleBackendError(LogEntry entry)
    {
        // Don't show UI notifications if the error came from "UI" source (would be duplicate)
        if (entry.Source == "UI")
            return;

        // Format message with source and category for context
        var displayMessage = entry.Message;

        // Show snackbar only (don't log again - already logged)
        _snackbar.Add(displayMessage, Severity.Error);
    }

    public void NewInfo(string message, bool logOnly = false)
    {
        if (!logOnly)
            _snackbar.Add(message, Severity.Info);
        _logger.Log(application.Models.LogLevel.Info, "UI", message, category: "Notification");
    }

    public void NewSuccess(string message, bool logOnly = false)
    {
        if (!logOnly)
            _snackbar.Add(message, Severity.Success);
        _logger.Log(application.Models.LogLevel.Info, "UI", message, category: "Notification");
    }

    public void NewWarning(string message, bool logOnly = false)
    {
        if (!logOnly)
            _snackbar.Add(message, Severity.Warning);
        _logger.Log(application.Models.LogLevel.Warning, "UI", message, category: "Notification");
    }

    public void NewError(string message, Exception? exception = null, bool logOnly = false)
    {
        if (!logOnly)
            _snackbar.Add(message, Severity.Error);
        _logger.Log(application.Models.LogLevel.Error, "UI", message, exception?.ToString(), "Notification");
    }
}
