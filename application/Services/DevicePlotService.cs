using application.Common;
using application.Models;
using domain.Common;
using domain.Devices.Generic;
using Microsoft.Extensions.Logging;

namespace application.Services;

/*
 * DevicePlotService->DevicePlots->DevicePlot->Signals->Signal->Reference->GetValue()
 */

public class DevicePlotService : IDisposable
{
    private readonly DeviceManager _deviceManager;
    private readonly ILogger<DevicePlotService> _logger;

    private readonly Dictionary<Guid, DevicePlotData> _devicePlots = new();
    private readonly Dictionary<Guid, List<IPlotReference>> _deviceProps = new();
    private readonly Dictionary<Guid, EventHandler> _signalsChangedHandlers = new();

    private const int SamplesPerSecond = 20;
    private const int WindowSeconds = 60;
    private const int BufferCapacity = SamplesPerSecond * WindowSeconds; // 1200

    public DevicePlotService(DeviceManager deviceManager, ILogger<DevicePlotService> logger)
    {
        _deviceManager =  deviceManager;
        _logger = logger;
        
        _deviceManager.DeviceAdded += DeviceAddedHandler;
        _deviceManager.DeviceRemoved += DeviceRemovedHandler;
    }

    private void DeviceAddedHandler(object? sender, DeviceEventArgs e)
    {
        if (_deviceProps.ContainsKey(e.Device.Guid))
        {
            _logger.LogWarning("Device {Device} props already added for {DeviceId}",
                e.Device.Name, e.Device.Guid);
            return;
        }

        _deviceProps[e.Device.Guid] = CreatePlotReferencesFromObject(e.Device, e.Device.Name);

        // Subscribe to SignalsChanged event for DbcDevice
        if (e.Device is DbcDevice dbcDevice)
        {
            EventHandler handler = (s, args) =>
            {
                RefreshPlotReferences(dbcDevice.Guid, dbcDevice, dbcDevice.Name);
            };

            _signalsChangedHandlers[e.Device.Guid] = handler;
            dbcDevice.SignalsChanged += handler;
        }
    }

    private void DeviceRemovedHandler(object? sender, DeviceEventArgs e)
    {
        _deviceProps.Remove(e.Device.Guid);

        // Unsubscribe from SignalsChanged event for DbcDevice
        if (e.Device is DbcDevice dbcDevice && _signalsChangedHandlers.TryGetValue(e.Device.Guid, out var handler))
        {
            dbcDevice.SignalsChanged -= handler;
            _signalsChangedHandlers.Remove(e.Device.Guid);
        }
    }

    /// <summary>
    /// Refreshes plot references for a device (e.g., after DbcSignals are added)
    /// </summary>
    public void RefreshPlotReferences(Guid deviceId, object device, string deviceName)
    {
        _deviceProps[deviceId] = CreatePlotReferencesFromObject(device, deviceName);
        _logger.LogInformation("Refreshed plot references for device {DeviceName} ({DeviceId})",
            deviceName, deviceId);
    }

    public List<IPlotReference> GetAvailableProps(Guid deviceId)
    {
        return !_deviceProps.TryGetValue(deviceId, out var propData) ? [] : propData;
    }
    
    /// <summary>
    /// Adds a signal to plot for a device
    /// </summary>
    public void AddSignal(Guid deviceId, IPlotReference reference, string color)
    {
        var plotData = GetOrCreatePlotData(deviceId);

        if (plotData.Signals.ContainsKey(reference.Name))
        {
            _logger.LogWarning("Signal {Signal} already being plotted for device {DeviceId}",
                reference.Name, deviceId);
            return;
        }

        var timeSeries = new SignalTimeSeries
        {
            Reference = reference,
            DataPoints = new CircularBuffer<PlotDataPoint>(BufferCapacity),
            Color = color
        };

        plotData.Signals[reference.Name] = timeSeries;

        // Start sampling timer if state is Recording and this is first signal
        if (plotData is { State: RecordingState.Recording, Signals.Count: 1 })
        {
            StartSamplingTimer(deviceId, plotData);
        }

        _logger.LogInformation("Added signal {Signal} with color {Color} to plot for device {DeviceId}",
            reference.Name, color, deviceId);
    }

    /// <summary>
    /// Removes a signal from plot
    /// </summary>
    public void RemoveSignal(Guid deviceId, IPlotReference reference)
    {
        if (!_devicePlots.TryGetValue(deviceId, out var plotData))
            return;

        plotData.Signals.Remove(reference.Name);

        // Stop timer if no more signals and state is Recording
        if (plotData.Signals.Count == 0 && plotData.State == RecordingState.Recording)
        {
            StopSamplingTimer(plotData);
            _logger.LogInformation("Removed last signal, stopped sampling for device {DeviceId}", deviceId);
        }
    }

    /// <summary>
    /// Gets active signals for a device
    /// </summary>
    public List<IPlotReference> GetActiveSignals(Guid deviceId)
    {
        if (!_devicePlots.TryGetValue(deviceId, out var plotData))
            return new List<IPlotReference>();

        return plotData.Signals.Values
            .Select(s => s.Reference)
            .ToList();
    }

    /// <summary>
    /// Gets plot data for a specific signal
    /// </summary>
    public List<(DateTime Timestamp, double Value)> GetPlotData(Guid deviceId, IPlotReference reference)
    {
        if (!_devicePlots.TryGetValue(deviceId, out var plotData))
            return new List<(DateTime, double)>();

        if (!plotData.Signals.TryGetValue(reference.Name, out var timeSeries))
            return new List<(DateTime, double)>();

        var dataPoints = timeSeries.DataPoints.GetAll();
        return dataPoints.Select(p => (p.Timestamp, p.Value)).ToList();
    }

    /// <summary>
    /// Gets the color assigned to a signal
    /// </summary>
    public string? GetSignalColor(Guid deviceId, IPlotReference reference)
    {
        if (!_devicePlots.TryGetValue(deviceId, out var plotData))
            return null;

        if (!plotData.Signals.TryGetValue(reference.Name, out var timeSeries))
            return null;

        return timeSeries.Color;
    }

    /// <summary>
    /// Gets current recording state
    /// </summary>
    public RecordingState GetRecordingState(Guid deviceId)
    {
        if (!_devicePlots.TryGetValue(deviceId, out var plotData))
            return RecordingState.Stopped;

        return plotData.State;
    }

    /// <summary>
    /// Starts recording (data collection)
    /// </summary>
    public void StartRecording(Guid deviceId)
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

    /// <summary>
    /// Pauses recording (stops timer, preserves data)
    /// </summary>
    public void PauseRecording(Guid deviceId)
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

    /// <summary>
    /// Stops recording (stops timer, clears all data)
    /// </summary>
    public void StopRecording(Guid deviceId)
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
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / SamplesPerSecond));

        _logger.LogInformation("Started sampling timer for device {DeviceId}", deviceId);
    }

    private void StopSamplingTimer(DevicePlotData plotData)
    {
        plotData.SamplingTimer?.Dispose();
        plotData.SamplingTimer = null;
    }

    private void SampleAllSignals(Guid deviceId)
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
                double value = signal.Reference.GetValue();

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
                    signal.Reference.Name, deviceId);
            }
        }
    }

    private DevicePlotData GetOrCreatePlotData(Guid deviceId)
    {
        if (_devicePlots.TryGetValue(deviceId, out var plotData)) return plotData;
        
        plotData = new DevicePlotData
        {
            DeviceId = deviceId,
            State = RecordingState.Stopped
        };
        _devicePlots[deviceId] = plotData;
        return plotData;
    }
    
    private static List<IPlotReference> CreatePlotReferencesFromObject(object source, string prefix = "Device")
        => PlotReferenceFactory.Create(source);

    public void Dispose()
    {
        foreach (var plotData in _devicePlots.Values)
        {
            plotData.SamplingTimer?.Dispose();
        }
        _devicePlots.Clear();
    }
    
}

public enum RecordingState
{
    Stopped,   // No timer, no data collection
    Recording, // Timer active, collecting data
    Paused     // Timer stopped, data preserved
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
    public IPlotReference Reference { get; set; } = null!;
    public CircularBuffer<PlotDataPoint> DataPoints { get; set; } = null!;
    public string Color { get; set; } = string.Empty;
}

internal class PlotDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}

