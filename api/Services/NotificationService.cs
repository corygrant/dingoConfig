using MudBlazor;
using application.Models;
using application.Services;

namespace api.Services;

public class NotificationService(ISnackbar snackbar, GlobalLogger logger)
{
    public void NewInfo(string message, bool logOnly = false)
    {
        if (!logOnly)
            snackbar.Add(message, Severity.Info);
        logger.Log(application.Models.LogLevel.Info, "UI", message, category: "Notification");
    }

    public void NewSuccess(string message, bool logOnly = false)
    {
        if (!logOnly)
            snackbar.Add(message, Severity.Success);
        logger.Log(application.Models.LogLevel.Info, "UI", message, category: "Notification");
    }

    public void NewWarning(string message, bool logOnly = false)
    {
        if (!logOnly)
            snackbar.Add(message, Severity.Warning);
        logger.Log(application.Models.LogLevel.Warning, "UI", message, category: "Notification");
    }

    public void NewError(string message, Exception? exception = null, bool logOnly = false)
    {
        if (!logOnly)
            snackbar.Add(message, Severity.Error);
        logger.Log(application.Models.LogLevel.Error, "UI", message, exception?.ToString(), "Notification");
    }
}
