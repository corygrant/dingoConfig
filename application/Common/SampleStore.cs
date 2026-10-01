namespace application.Common;

/// <summary>
/// Fixed-capacity ring buffers for many series that are sampled together and therefore share one time axis
/// (the aligned layout uPlot draws directly). Readers fetch only the samples added since their last read.
/// Thread-safe.
/// </summary>
public sealed class SampleStore(int capacity)
{
    private readonly object _lock = new();
    private readonly double[] _times = new double[capacity];
    private readonly Dictionary<string, double[]> _series = new();
    private long _count; // samples written since the last Clear()

    public int Capacity { get; } = capacity;

    /// <summary>Incremented on Clear() so readers know to discard what they have</summary>
    public int Generation { get; private set; }

    public long Count
    {
        get { lock (_lock) return _count; }
    }

    /// <summary>Adds a series; samples taken before it existed read as gaps</summary>
    public void AddSeries(string key)
    {
        lock (_lock)
        {
            if (_series.ContainsKey(key)) return;
            var values = new double[Capacity];
            Array.Fill(values, double.NaN);
            _series[key] = values;
        }
    }

    public void RemoveSeries(string key)
    {
        lock (_lock) _series.Remove(key);
    }

    public bool HasSeries(string key)
    {
        lock (_lock) return _series.ContainsKey(key);
    }

    /// <summary>Appends one sample to every series. NaN is a gap.</summary>
    public void Append(double time, Func<string, double> valueOf)
    {
        lock (_lock)
        {
            var slot = (int)(_count % Capacity);
            _times[slot] = time;
            foreach (var (key, values) in _series)
                values[slot] = valueOf(key);
            _count++;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _count = 0;
            Generation++;
            foreach (var values in _series.Values)
                Array.Fill(values, double.NaN);
        }
    }

    /// <summary>
    /// Reads the samples after <paramref name="since"/>. If the reader's generation is stale or it has fallen
    /// further behind than the buffer holds, everything held is returned with Reset set.
    /// </summary>
    public SampleChunk Read(IReadOnlyList<string> keys, int generation, long since)
    {
        lock (_lock)
        {
            var oldest = Math.Max(0, _count - Capacity);
            var reset = generation != Generation || since < oldest || since > _count;
            var from = reset ? oldest : since;
            var length = (int)(_count - from);

            var times = new double[length];
            var values = new double?[keys.Count][];

            for (var k = 0; k < keys.Count; k++)
                values[k] = new double?[length];

            for (var i = 0; i < length; i++)
            {
                var slot = (int)((from + i) % Capacity);
                times[i] = _times[slot];

                for (var k = 0; k < keys.Count; k++)
                {
                    if (!_series.TryGetValue(keys[k], out var series)) continue;
                    var v = series[slot];
                    // JSON has no NaN; null is a gap for uPlot
                    values[k][i] = double.IsFinite(v) ? v : null;
                }
            }

            return new SampleChunk(Generation, _count, reset, times, values);
        }
    }
}

/// <param name="Generation">Store generation the data belongs to</param>
/// <param name="Seq">Pass back as 'since' on the next read</param>
/// <param name="Reset">Discard previously received data before applying this chunk</param>
/// <param name="Times">Unix time in seconds</param>
/// <param name="Values">One array per requested key, aligned with Times</param>
public sealed record SampleChunk(int Generation, long Seq, bool Reset, double[] Times, double?[][] Values);
