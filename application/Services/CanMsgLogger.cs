using application.Models;
using domain.Models;
using System.Collections.Concurrent;

namespace application.Services;

public class CanMsgLogger(int maxHistorySize = 100000, string logDirectory = "./logs")
    : IDisposable
{
    public bool LogToFile { get; set; }
    public NumberFormat IdFormat { get; set; }
    public NumberFormat PayloadFormat { get; set; }

    // Use ConcurrentDictionary for O(1) lookups and thread safety
    private readonly ConcurrentDictionary<int, CanLogEntry> _messageSum = new();

    // Keep full history in a separate list
    private readonly List<CanLogEntry> _fullHistory = new();
    private readonly Lock _historyLock = new();

    private StreamWriter? _logWriter;

    /// <summary>
    /// Get the message summary for each unique CAN ID (for UI grid display)
    /// </summary>
    public List<CanLogEntry> GetMessageSum() => _messageSum.Values.ToList();

    /// <summary>
    /// Get full message history (for detailed log view)
    /// </summary>
    public List<CanLogEntry> GetFullHistory()
    {
        lock (_historyLock)
        {
            return [.._fullHistory];
        }
    }

    /// <summary>
    /// Get the most recent N messages from history
    /// </summary>
    public List<CanLogEntry> GetRecentHistory(int count)
    {
        lock (_historyLock)
        {
            return _fullHistory.TakeLast(count).ToList();
        }
    }

    public void Log(DataDirection dir, CanFrame msg)
    {
        var entry = new CanLogEntry
        {
            Id = msg.Id,
            Payload = msg.Payload,
            Len = msg.Len,
            Direction = dir,
            Timestamp = DateTime.UtcNow,
            Count = 1
        };

        // Update or add to message summary dictionary (O(1) operation)
        _messageSum.AddOrUpdate(
            msg.Id,
            entry, // Add new
            (_, existing) => // Update existing
            {
                existing.Payload = msg.Payload;
                existing.Len = msg.Len;
                existing.Timestamp = DateTime.UtcNow;
                existing.Direction = dir;
                existing.Count++;
                return existing;
            });

        // Add to full history if tracking all messages
        lock (_historyLock)
        {
            _fullHistory.Add(entry);

            // Remove oldest if over limit
            if (_fullHistory.Count > maxHistorySize)
            {
                _fullHistory.RemoveAt(0);
            }
        }

        // Write to file if enabled
        if (LogToFile)
        {
            WriteToFile(entry);
        }
    }

    public void CreateLogFile()
    {
        if (!LogToFile) return;

        Directory.CreateDirectory(logDirectory);
        var fileName = $"canlog_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = Path.Combine(logDirectory, fileName);

        _logWriter = new StreamWriter(filePath, append: false);
        _logWriter.WriteLine("Timestamp,Direction,CAN ID,Length,Data");
    }

    private void WriteToFile(CanLogEntry entry)
    {
        if (_logWriter == null || entry.Payload == null) return;

        //Always write ID and payload as hex to make parsing easier later
        var idStr = $"0x{entry.Id:X}";

        var dataStr = BitConverter.ToString(entry.Payload).Replace("-", " ");

        _logWriter.WriteLine(
            $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
            $"{entry.Direction}," +
            $"{idStr}," +
            $"{entry.Len}," +
            $"{dataStr}"
        );

        _logWriter.Flush(); // Ensure data is written immediately
    }

    /// <summary>
    /// Clear all logged messages (both summary and history)
    /// </summary>
    public void Clear()
    {
        _messageSum.Clear();

        lock (_historyLock)
        {
            _fullHistory.Clear();
        }
    }

    public void Dispose()
    {
        _logWriter?.Dispose();
    }
}
