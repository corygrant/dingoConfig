using application.Common;
using application.Models;
using Microsoft.Extensions.Logging;

namespace application.Services;

/// <summary>
/// Recording state for device plots
/// </summary>
public enum RecordingState
{
    Stopped,   // No timer, no data collection
    Recording, // Timer active, collecting data
    Paused     // Timer stopped, data preserved
}

/// <summary>
/// Singleton service that manages plot data collection for multiple devices.
/// Each device has independent recording controls and maintains its own plot data.
/// </summary>
public class DevicePlotService : IDisposable
{
    private readonly DeviceManager _deviceManager;
    private readonly ILogger<DevicePlotService> _logger;

    private readonly Dictionary<Guid, DevicePlotData> _devicePlots = new();
    private readonly Dictionary<Guid, object> _deviceLocks = new();

    private const int SamplesPerSecond = 20;
    private const int WindowSeconds = 60;
    private const int BufferCapacity = SamplesPerSecond * WindowSeconds; // 1200

    private static readonly string[] ColorPalette =
    {
        "#2196F3", // Blue
        "#F44336", // Red
        "#4CAF50", // Green
        "#FF9800", // Orange
        "#9C27B0", // Purple
        "#00BCD4", // Cyan
        "#FFEB3B", // Yellow
        "#795548", // Brown
        "#607D8B", // Blue Grey
        "#E91E63"  // Pink
    };

    public DevicePlotService(DeviceManager deviceManager, ILogger<DevicePlotService> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    /// <summary>
    /// Adds a signal to plot for a device
    /// </summary>
    public void AddSignal(Guid deviceId, PlotSignalDescriptor descriptor)
    {
        var lockObj = GetOrCreateLock(deviceId);
        lock (lockObj)
        {
            var plotData = GetOrCreatePlotData(deviceId);

            if (plotData.Signals.ContainsKey(descriptor.Key))
            {
                _logger.LogWarning("Signal {Signal} already being plotted for device {DeviceId}",
                    descriptor.GetDisplayName(), deviceId);
                return;
            }

            // Assign color from palette
            int colorIndex = plotData.Signals.Count % ColorPalette.Length;

            var timeSeries = new SignalTimeSeries
            {
                Descriptor = descriptor,
                DataPoints = new CircularBuffer<PlotDataPoint>(BufferCapacity),
                Color = ColorPalette[colorIndex]
            };

            plotData.Signals[descriptor.Key] = timeSeries;

            // Start sampling timer if state is Recording and this is first signal
            if (plotData.State == RecordingState.Recording && plotData.Signals.Count == 1)
            {
                StartSamplingTimer(deviceId, plotData);
            }

            _logger.LogInformation("Added signal {Signal} to plot for device {DeviceId}",
                descriptor.GetDisplayName(), deviceId);
        }
    }

    /// <summary>
    /// Removes a signal from plot
    /// </summary>
    public void RemoveSignal(Guid deviceId, PlotSignalDescriptor descriptor)
    {
        var lockObj = GetOrCreateLock(deviceId);
        lock (lockObj)
        {
            if (!_devicePlots.TryGetValue(deviceId, out var plotData))
                return;

            plotData.Signals.Remove(descriptor.Key);

            // Stop timer if no more signals and state is Recording
            if (plotData.Signals.Count == 0 && plotData.State == RecordingState.Recording)
            {
                StopSamplingTimer(plotData);
                _logger.LogInformation("Removed last signal, stopped sampling for device {DeviceId}", deviceId);
            }
        }
    }

    /// <summary>
    /// Gets active signals for a device
    /// </summary>
    public List<PlotSignalDescriptor> GetActiveSignals(Guid deviceId)
    {
        var lockObj = GetOrCreateLock(deviceId);
        lock (lockObj)
        {
            if (!_devicePlots.TryGetValue(deviceId, out var plotData))
                return new List<PlotSignalDescriptor>();

            return plotData.Signals.Values
                .Select(s => s.Descriptor)
                .ToList();
        }
    }

    /// <summary>
    /// Gets plot data for a specific signal
    /// </summary>
    public List<(DateTime Timestamp, double Value)> GetPlotData(Guid deviceId, PlotSignalDescriptor descriptor)
    {
        var lockObj = GetOrCreateLock(deviceId);
        lock (lockObj)
        {
            if (!_devicePlots.TryGetValue(deviceId, out var plotData))
                return new List<(DateTime, double)>();

            if (!plotData.Signals.TryGetValue(descriptor.Key, out var timeSeries))
                return new List<(DateTime, double)>();

            var dataPoints = timeSeries.DataPoints.GetAll();
            return dataPoints.Select(p => (p.Timestamp, p.Value)).ToList();
        }
    }

    /// <summary>
    /// Gets the color assigned to a signal
    /// </summary>
    public string? GetSignalColor(Guid deviceId, PlotSignalDescriptor descriptor)
    {
        var lockObj = GetOrCreateLock(deviceId);
        lock (lockObj)
        {
            if (!_devicePlots.TryGetValue(deviceId, out var plotData))
                return null;

            if (!plotData.Signals.TryGetValue(descriptor.Key, out var timeSeries))
                return null;

            return timeSeries.Color;
        }
    }

    /// <summary>
    /// Gets current recording state
    /// </summary>
    public RecordingState GetRecordingState(Guid deviceId)
    {
        var lockObj = GetOrCreateLock(deviceId);
        lock (lockObj)
        {
            if (!_devicePlots.TryGetValue(deviceId, out var plotData))
                return RecordingState.Stopped;

            return plotData.State;
        }
    }

    /// <summary>
    /// Starts recording (data collection)
    /// </summary>
    public void StartRecording(Guid deviceId)
    {
        var lockObj = GetOrCreateLock(deviceId);
        lock (lockObj)
        {
            var plotData = GetOrCreatePlotData(deviceId);

            if (plotData.State == RecordingState.Recording)
            {
                _logger.LogWarning("Recording already active for device {DeviceId}", deviceId);
                return;
            }

            plotData.State = RecordingState.Recording;

            // Start timer if we have signals
            if (plotData.Signals.Count > 0)
            {
                StartSamplingTimer(deviceId, plotData);
            }

            _logger.LogInformation("Started recording for device {DeviceId}", deviceId);
        }
    }

    /// <summary>
    /// Pauses recording (stops timer, preserves data)
    /// </summary>
    public void PauseRecording(Guid deviceId)
    {
        var lockObj = GetOrCreateLock(deviceId);
        lock (lockObj)
        {
            if (!_devicePlots.TryGetValue(deviceId, out var plotData))
                return;

            if (plotData.State != RecordingState.Recording)
            {
                _logger.LogWarning("Cannot pause - not currently recording for device {DeviceId}", deviceId);
                return;
            }

            plotData.State = RecordingState.Paused;
            StopSamplingTimer(plotData);

            _logger.LogInformation("Paused recording for device {DeviceId}", deviceId);
        }
    }

    /// <summary>
    /// Stops recording (stops timer, clears all data)
    /// </summary>
    public void StopRecording(Guid deviceId)
    {
        var lockObj = GetOrCreateLock(deviceId);
        lock (lockObj)
        {
            if (!_devicePlots.TryGetValue(deviceId, out var plotData))
                return;

            plotData.State = RecordingState.Stopped;
            StopSamplingTimer(plotData);

            // Clear all signal data
            foreach (var signal in plotData.Signals.Values)
            {
                signal.DataPoints.Clear();
            }

            _logger.LogInformation("Stopped recording for device {DeviceId}", deviceId);
        }
    }

    private void StartSamplingTimer(Guid deviceId, DevicePlotData plotData)
    {
        if (plotData.SamplingTimer != null)
        {
            _logger.LogWarning("Sampling timer already exists for device {DeviceId}", deviceId);
            return;
        }

        plotData.SamplingTimer = new Timer(_ =>
        {
            SampleAllSignals(deviceId);
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(50)); // 20 Hz

        _logger.LogInformation("Started sampling timer for device {DeviceId}", deviceId);
    }

    private void StopSamplingTimer(DevicePlotData plotData)
    {
        plotData.SamplingTimer?.Dispose();
        plotData.SamplingTimer = null;
    }

    private void SampleAllSignals(Guid deviceId)
    {
        var device = _deviceManager.GetDevice(deviceId);
        if (device == null)
            return;

        var lockObj = GetOrCreateLock(deviceId);
        lock (lockObj)
        {
            if (!_devicePlots.TryGetValue(deviceId, out var plotData))
                return;

            // Only sample if we're in Recording state
            if (plotData.State != RecordingState.Recording)
                return;

            var timestamp = DateTime.UtcNow;

            foreach (var signal in plotData.Signals.Values)
            {
                try
                {
                    double value = SignalValueExtractor.GetValue(device, signal.Descriptor);

                    var dataPoint = new PlotDataPoint
                    {
                        Timestamp = timestamp,
                        Value = value
                    };

                    signal.DataPoints.Add(dataPoint);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sampling signal {Signal} for device {DeviceId}",
                        signal.Descriptor.GetDisplayName(), deviceId);
                }
            }
        }
    }

    private DevicePlotData GetOrCreatePlotData(Guid deviceId)
    {
        if (!_devicePlots.TryGetValue(deviceId, out var plotData))
        {
            plotData = new DevicePlotData
            {
                DeviceId = deviceId,
                State = RecordingState.Stopped
            };
            _devicePlots[deviceId] = plotData;
        }
        return plotData;
    }

    private object GetOrCreateLock(Guid deviceId)
    {
        if (!_deviceLocks.TryGetValue(deviceId, out var lockObj))
        {
            lockObj = new object();
            _deviceLocks[deviceId] = lockObj;
        }
        return lockObj;
    }

    public void Dispose()
    {
        foreach (var plotData in _devicePlots.Values)
        {
            plotData.SamplingTimer?.Dispose();
        }
        _devicePlots.Clear();
        _deviceLocks.Clear();
    }
}

// Internal data structures
internal class DevicePlotData
{
    public Guid DeviceId { get; set; }
    public Dictionary<string, SignalTimeSeries> Signals { get; set; } = new();
    public Timer? SamplingTimer { get; set; }
    public RecordingState State { get; set; }
}

internal class SignalTimeSeries
{
    public PlotSignalDescriptor Descriptor { get; set; } = null!;
    public CircularBuffer<PlotDataPoint> DataPoints { get; set; } = null!;
    public string Color { get; set; } = string.Empty;
}

internal class PlotDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}
