using System.Globalization;
using application.Models;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace application.Services;

public enum PlaybackState { Idle, Playing, Paused, Stopped }

public class SimPlayback(ILogger<SimPlayback> logger)
{
    private List<PlaybackMessage> _messages = [];
    private CancellationTokenSource? _playCts;
    private readonly Lock _stateLock = new();

    public PlaybackState State { get; private set; } = PlaybackState.Idle;

    public int CurrentMessageIndex { get; private set; }

    public int TotalMessages => _messages.Count;
    public TimeSpan CurrentTime { get; private set; }
    public bool Loop { get; set; }

    public string? CurrentFileName { get; private set; }

    public event Action<CanFrame, DataDirection>? MessageReady;

    public async Task<(bool success, string? error)> LoadFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return (false, "File not found");
            }

            var lines = await File.ReadAllLinesAsync(filePath);
            if (lines.Length < 2)
            {
                return (false, "File is empty or invalid");
            }

            var messages = new List<PlaybackMessage>();
            DateTime? firstTimestamp = null;

            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 5)
                    continue;

                try
                {
                    // Parse timestamp
                    var timestamp = DateTime.ParseExact(
                        parts[0].Trim(),
                        "yyyy-MM-dd HH:mm:ss.fff",
                        CultureInfo.InvariantCulture
                    );

                    firstTimestamp ??= timestamp;

                    // Parse direction
                    var direction = parts[1].Trim().Equals("Rx", StringComparison.OrdinalIgnoreCase)
                        ? DataDirection.Rx
                        : DataDirection.Tx;

                    // Parse CAN ID (always hex)
                    var canIdStr = parts[2].Trim();
                    var canId = Convert.ToInt32(canIdStr, 16);

                    // Parse length
                    var length = byte.Parse(parts[3].Trim());

                    // Parse data bytes (always hex)
                    var dataStr = parts[4].Trim();
                    var dataBytes = dataStr.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(b => Convert.ToByte(b, 16)).ToArray();

                    // Ensure we have exactly 'length' bytes
                    if (dataBytes.Length < length)
                    {
                        var paddedData = new byte[length];
                        Array.Copy(dataBytes, paddedData, dataBytes.Length);
                        dataBytes = paddedData;
                    }

                    var frame = new CanFrame(
                        Id: canId,
                        Len: length,
                        Payload: dataBytes.Take(length).ToArray()
                    );

                    var relativeTime = timestamp - firstTimestamp.Value;

                    messages.Add(new PlaybackMessage
                    {
                        Frame = frame,
                        RelativeTime = relativeTime,
                        Direction = direction
                    });
                }
                catch
                {
                    //logger.LogWarning("Skipping invalid line {LineNumber}: {Error}", i + 1, ex.Message);
                }
            }

            if (messages.Count == 0)
            {
                return (false, "No valid CAN messages found in file");
            }

            lock (_stateLock)
            {
                _messages = messages;
                CurrentMessageIndex = 0;
                State = PlaybackState.Idle;
                CurrentTime = TimeSpan.Zero;
                CurrentFileName = Path.GetFileName(filePath);
            }

            logger.LogInformation("Loaded {Count} CAN messages from {FileName}", messages.Count, CurrentFileName);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load CAN log file: {FilePath}", filePath);
            return (false, ex.Message);
        }
    }

    public Task Play()
    {
        lock (_stateLock)
        {
            if (State == PlaybackState.Playing || _messages.Count == 0)
                return Task.CompletedTask;

            // If stopped or at end, reset to beginning
            if (State == PlaybackState.Stopped || CurrentMessageIndex >= _messages.Count)
            {
                CurrentMessageIndex = 0;
                CurrentTime = TimeSpan.Zero;
            }

            State = PlaybackState.Playing;
            _playCts?.Cancel();
            _playCts = new CancellationTokenSource();
        }

        logger.LogInformation("Starting playback from index {Index}", CurrentMessageIndex);
        _ = PlaybackLoop(_playCts.Token);
        return Task.CompletedTask;
    }

    public Task Pause()
    {
        lock (_stateLock)
        {
            if (State != PlaybackState.Playing)
                return Task.CompletedTask;

            _playCts?.Cancel();
            State = PlaybackState.Paused;
        }

        logger.LogInformation("Playback paused at index {Index}", CurrentMessageIndex);
        return Task.CompletedTask;
    }

    public Task Reset()
    {
        lock (_stateLock)
        {
            _playCts?.Cancel();
            CurrentMessageIndex = 0;
            CurrentTime = TimeSpan.Zero;
            State = PlaybackState.Idle;
        }

        logger.LogInformation("Playback reset");
        return Task.CompletedTask;
    }

    private async Task PlaybackLoop(CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var startIndex = CurrentMessageIndex;

        // Adjust start time to account for current position
        if (startIndex > 0 && startIndex < _messages.Count)
        {
            startTime -= _messages[startIndex].RelativeTime;
        }

        try
        {
            while (CurrentMessageIndex < _messages.Count && !ct.IsCancellationRequested)
            {
                var msg = _messages[CurrentMessageIndex];
                var elapsedTime = DateTime.UtcNow - startTime;
                var delay = msg.RelativeTime - elapsedTime;

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct);
                }

                MessageReady?.Invoke(msg.Frame, msg.Direction);
                CurrentTime = msg.RelativeTime;
                CurrentMessageIndex++;
            }

            // Handle completion
            lock (_stateLock)
            {
                if (ct.IsCancellationRequested)
                    return;

                if (Loop && _messages.Count > 0)
                {
                    CurrentMessageIndex = 0;
                    CurrentTime = TimeSpan.Zero;
                    logger.LogInformation("Looping playback");
                    _ = PlaybackLoop(ct);
                }
                else
                {
                    State = PlaybackState.Stopped;
                    logger.LogInformation("Playback completed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when paused or reset
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during playback");
            lock (_stateLock)
            {
                State = PlaybackState.Stopped;
            }
        }
    }

    public Task Clear()
    {
        lock (_stateLock)
        {
            _playCts?.Cancel();
            CurrentMessageIndex = 0;
            CurrentTime = TimeSpan.Zero;
            State = PlaybackState.Idle;
            
            _messages.Clear();
        }
        
        logger.LogInformation("Playback cleared");

        return Task.CompletedTask;
    }

    private class PlaybackMessage
    {
        public required CanFrame Frame { get; init; }
        public required TimeSpan RelativeTime { get; init; }
        public required DataDirection Direction { get; init; }
    }
}
