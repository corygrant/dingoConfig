using application.Services;
using Microsoft.Extensions.Logging;

namespace infrastructure.Logging;

public class GlobalLoggerProvider : ILoggerProvider
{
    private readonly GlobalLogger _globalLogger;

    public GlobalLoggerProvider(GlobalLogger globalLogger)
    {
        _globalLogger = globalLogger;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new GlobalLoggerAdapter(categoryName, _globalLogger);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}

internal class GlobalLoggerAdapter : ILogger
{
    private readonly string _categoryName;
    private readonly GlobalLogger _globalLogger;

    public GlobalLoggerAdapter(string categoryName, GlobalLogger globalLogger)
    {
        _categoryName = categoryName;
        _globalLogger = globalLogger;
    }

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        // Map Microsoft.Extensions.Logging.LogLevel to application.Models.LogLevel
        var mappedLevel = MapLogLevel(logLevel);

        // Extract source from category (e.g., "application.Services.DeviceManager" → "DeviceManager")
        var source = ExtractSourceFromCategory(_categoryName);

        var message = formatter(state, exception);
        var exceptionStr = exception?.ToString();

        _globalLogger.Log(mappedLevel, source, message, exceptionStr, _categoryName);
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return logLevel != Microsoft.Extensions.Logging.LogLevel.None;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    private static application.Models.LogLevel MapLogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return logLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => application.Models.LogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Debug => application.Models.LogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => application.Models.LogLevel.Info,
            Microsoft.Extensions.Logging.LogLevel.Warning => application.Models.LogLevel.Warning,
            Microsoft.Extensions.Logging.LogLevel.Error => application.Models.LogLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => application.Models.LogLevel.Error,
            _ => application.Models.LogLevel.Info
        };
    }

    private static string ExtractSourceFromCategory(string categoryName)
    {
        // Extract the last segment from the category name
        // e.g., "application.Services.DeviceManager" → "DeviceManager"
        var segments = categoryName.Split('.');
        return segments.Length > 0 ? segments[^1] : categoryName;
    }
}
