using application.Common;
using application.Models;
using domain.Devices.Generic;
using domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace application.Services;

/// <summary>
/// Application-wide plotting. Every [Plotable] value of every device is sampled continuously into a short
/// live history (dashboard sparklines). Signals picked for the application plot are additionally recorded
/// into a longer history while recording is active.
/// </summary>
public sealed class PlotService : IDisposable
{
    public const int SampleRateHz = 20;
    public const int LiveSeconds = 30;
    public const int RecordSeconds = 600;
    public const int MaxTraces = 8;

    /// <summary>Categorical series colors, assigned in this order (validated for CVD separation on dark surfaces)</summary>
    public static readonly string[] TraceColors =
        ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767"];

    private readonly DeviceManager _deviceManager;
    private readonly ILogger<PlotService> _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, PlotSignal> _signals = new();
    private readonly Dictionary<Guid, EventHandler> _signalsChangedHandlers = new();
    private readonly List<PlotTrace> _traces = [];
    private readonly Timer _sampleTimer;
    private PlotSignal[] _signalSnapshot = [];
    private int _sampling;

    // Orders "Outputs[2]" before "Outputs[10]"
    private static readonly Comparer<string> NaturalComparer = Comparer<string>.Create((a, b) =>
        string.Compare(PadNumbers(a), PadNumbers(b), StringComparison.OrdinalIgnoreCase));

    private static string PadNumbers(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s, @"\d+", m => m.Value.PadLeft(6, '0'));

    public SampleStore Live { get; } = new(SampleRateHz * LiveSeconds);
    public SampleStore Recording { get; } = new(SampleRateHz * RecordSeconds);

    public RecordingState State { get; private set; } = RecordingState.Stopped;

    /// <summary>Incremented when the available signals change</summary>
    public int SignalsVersion { get; private set; }

    /// <summary>Incremented when the traces or recording state change</summary>
    public int TracesVersion { get; private set; }

    public PlotService(DeviceManager deviceManager, ILogger<PlotService> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;

        _deviceManager.DeviceAdded += (_, e) => AddDevice(e.Device);
        _deviceManager.DeviceRemoved += (_, e) => RemoveDevice(e.Device);

        foreach (var device in _deviceManager.GetAllDevices())
            AddDevice(device);

        _sampleTimer = new Timer(_ => Sample(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000.0 / SampleRateHz));
    }

    public IReadOnlyList<PlotSignal> GetSignals() => _signalSnapshot;

    public PlotSignal? GetSignal(string key)
    {
        lock (_lock) return _signals.GetValueOrDefault(key);
    }

    /// <summary>Finds the signal for a [Plotable] property of a device or function instance</summary>
    public PlotSignal? FindSignal(object source, string propertyName) =>
        _signalSnapshot.FirstOrDefault(s =>
            ReferenceEquals(s.Reference.SourceObject, source) && s.Reference.Prop.Name == propertyName);

    public IReadOnlyList<PlotTrace> GetTraces()
    {
        lock (_lock) return _traces.ToList();
    }

    public bool IsTraced(string key)
    {
        lock (_lock) return _traces.Any(t => t.Key == key);
    }

    /// <summary>Adds a signal to the application plot. Returns false if it can't be added.</summary>
    public bool AddTrace(string key)
    {
        lock (_lock)
        {
            if (!_signals.ContainsKey(key) || _traces.Any(t => t.Key == key) || _traces.Count >= MaxTraces)
                return false;

            // A color stays with its signal; new signals take the first free slot
            var color = TraceColors.First(c => _traces.All(t => t.Color != c));
            _traces.Add(new PlotTrace(key, color));
            Recording.AddSeries(key);
            TracesVersion++;
            return true;
        }
    }

    public void RemoveTrace(string key)
    {
        lock (_lock)
        {
            if (_traces.RemoveAll(t => t.Key == key) == 0) return;
            Recording.RemoveSeries(key);
            TracesVersion++;
        }
    }

    public void StartRecording()
    {
        lock (_lock)
        {
            State = RecordingState.Recording;
            TracesVersion++;
        }
    }

    /// <summary>Stops sampling, keeps the recorded data</summary>
    public void PauseRecording()
    {
        lock (_lock)
        {
            if (State != RecordingState.Recording) return;
            State = RecordingState.Paused;
            TracesVersion++;
        }
    }

    /// <summary>Stops sampling and clears the recorded data</summary>
    public void StopRecording()
    {
        lock (_lock)
        {
            State = RecordingState.Stopped;
            Recording.Clear();
            TracesVersion++;
        }
    }

    private void AddDevice(IDevice device)
    {
        SetDeviceSignals(device);

        // DBC devices change their signal list at runtime
        if (device is DbcDevice dbcDevice && !_signalsChangedHandlers.ContainsKey(device.Guid))
        {
            EventHandler handler = (_, _) => SetDeviceSignals(dbcDevice);
            _signalsChangedHandlers[device.Guid] = handler;
            dbcDevice.SignalsChanged += handler;
        }
    }

    private void RemoveDevice(IDevice device)
    {
        if (device is DbcDevice dbcDevice && _signalsChangedHandlers.Remove(device.Guid, out var handler))
            dbcDevice.SignalsChanged -= handler;

        lock (_lock)
        {
            foreach (var key in _signals.Values.Where(s => s.Device == device).Select(s => s.Key).ToList())
                RemoveSignal(key);

            UpdateSnapshot();
        }
    }

    /// <summary>
    /// (Re)discovers a device's signals. Signals whose key survives keep their history and traces.
    /// </summary>
    private void SetDeviceSignals(IDevice device)
    {
        var references = PlotReferenceFactory.Create(device);

        lock (_lock)
        {
            var keys = new HashSet<string>();
            foreach (var reference in references)
            {
                var signal = new PlotSignal(device, reference);
                if (!keys.Add(signal.Key))
                {
                    _logger.LogWarning("Duplicate plot signal {Signal} on {Device}", reference.Name, device.Name);
                    continue;
                }

                _signals[signal.Key] = signal;
                Live.AddSeries(signal.Key);
            }

            foreach (var key in _signals.Values.Where(s => s.Device == device && !keys.Contains(s.Key))
                         .Select(s => s.Key).ToList())
                RemoveSignal(key);

            UpdateSnapshot();
        }
    }

    private void RemoveSignal(string key)
    {
        _signals.Remove(key);
        Live.RemoveSeries(key);
        if (_traces.RemoveAll(t => t.Key == key) > 0)
        {
            Recording.RemoveSeries(key);
            TracesVersion++;
        }
    }

    private void UpdateSnapshot()
    {
        // Device-level values first, then by path with indexes in numeric order
        _signalSnapshot = _signals.Values
            .OrderBy(s => s.Device.Name)
            .ThenBy(s => s.Device.Guid)
            .ThenBy(s => s.Reference.Group != "Device")
            .ThenBy(s => s.Reference.Name, NaturalComparer)
            .ToArray();
        SignalsVersion++;
    }

    private void Sample()
    {
        // Skip a tick rather than overlap if sampling ever runs long
        if (Interlocked.Exchange(ref _sampling, 1) == 1) return;

        try
        {
            var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            var values = new Dictionary<string, double>(_signalSnapshot.Length);

            foreach (var signal in _signalSnapshot)
            {
                // Disconnected devices plot as gaps rather than stale values
                values[signal.Key] = signal.Device.Connected ? signal.Reference.GetValue() : double.NaN;
            }

            Live.Append(time, key => values.GetValueOrDefault(key, double.NaN));

            if (State == RecordingState.Recording)
                Recording.Append(time, key => values.GetValueOrDefault(key, double.NaN));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sampling plot signals");
        }
        finally
        {
            Volatile.Write(ref _sampling, 0);
        }
    }

    public void Dispose()
    {
        _sampleTimer.Dispose();
    }
}

public sealed class PlotSignal(IDevice device, IPlotReference reference)
{
    public string Key { get; } = $"{device.Guid}/{reference.Name}";
    public IDevice Device { get; } = device;
    public IPlotReference Reference { get; } = reference;
    public string Label => Reference.Label;
    public string FullLabel => $"{Device.Name} {Reference.Label}";
    public string Unit => Reference.Unit;
    public bool IsDiscrete => Reference.IsDiscrete;
}

public sealed record PlotTrace(string Key, string Color);
