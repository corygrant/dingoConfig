using System.Collections.Concurrent;
using application.Models;

namespace application.Services;

public class GlobalLogger : IDisposable
{
    private const int MaxBufferSize = 50000;
    private readonly ConcurrentQueue<LogEntry> _logBuffer = new();
    private readonly object _fileLock = new();
    private readonly string _logDirectory;
    private StreamWriter? _logWriter;

    public bool LogToFile { get; set; }
    public LogLevel MinimumDisplayLevel { get; set; } = LogLevel.Info;

    public GlobalLogger(string logDirectory = "./logs")
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public void Log(LogLevel level, string source, string message, string? exception = null, string? category = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Source = source,
            Message = message,
            Exception = exception,
            Category = category ?? string.Empty
        };

        // Add to buffer (drop oldest if full)
        _logBuffer.Enqueue(entry);
        while (_logBuffer.Count > MaxBufferSize)
        {
            _logBuffer.TryDequeue(out _);
        }

        // Write to file if enabled
        if (LogToFile)
        {
            WriteToFile(entry);
        }
    }

    public List<LogEntry> GetLogs(LogLevel? filterLevel = null, string? filterSource = null)
    {
        var logs = _logBuffer.ToList();

        if (filterLevel.HasValue)
        {
            logs = logs.Where(l => l.Level == filterLevel.Value).ToList();
        }

        if (!string.IsNullOrEmpty(filterSource))
        {
            logs = logs.Where(l => l.Source == filterSource).ToList();
        }

        return logs.OrderByDescending(l => l.Timestamp).ToList();
    }

    public List<LogEntry> GetRecentLogs(int count)
    {
        return _logBuffer.TakeLast(count).OrderByDescending(l => l.Timestamp).ToList();
    }

    public void Clear()
    {
        _logBuffer.Clear();
    }

    public void CreateLogFile()
    {
        lock (_fileLock)
        {
            _logWriter?.Dispose();

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"globallog_{timestamp}.csv";
            var filePath = Path.Combine(_logDirectory, fileName);

            _logWriter = new StreamWriter(filePath, append: false) { AutoFlush = true };

            // Write CSV header
            _logWriter.WriteLine("Timestamp,Level,Source,Category,Message,Exception");
        }
    }

    private void WriteToFile(LogEntry entry)
    {
        lock (_fileLock)
        {
            if (_logWriter == null)
            {
                CreateLogFile();
            }

            var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = entry.Level.ToString();
            var source = EscapeCsv(entry.Source);
            var category = EscapeCsv(entry.Category);
            var message = EscapeCsv(entry.Message);
            var exception = EscapeCsv(entry.Exception ?? "");

            _logWriter?.WriteLine($"{timestamp},{level},{source},{category},{message},{exception}");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    public void Dispose()
    {
        lock (_fileLock)
        {
            _logWriter?.Dispose();
            _logWriter = null;
        }
    }
}
