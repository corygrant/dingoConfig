using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace domain.Devices.Generic;

public class DbcDevice : IDevice
{
    [JsonIgnore] protected ILogger<DbcDevice> Logger = null!;

    [JsonIgnore] public Guid Guid { get; }
    [JsonIgnore] public string Type => "DbcDevice";
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }
    [JsonIgnore] private DateTime LastRxTime { get; set; }
    
    public event EventHandler? SignalsChanged;

    [JsonIgnore]
    public bool Connected
    {
        get;
        private set
        {
            if (field && !value)
            {
                Clear();
            }

            field = value;
        }
    }


    [JsonIgnore] private string? DbcFilePath { get; set; }
    [JsonPropertyName("minId")] private int MinId { get; set; }
    [JsonPropertyName("maxId")] private int MaxId { get; set; }
    [JsonPropertyName("dbcSignal")] public List<DbcSignal> DbcSignals { get; init; } = [];

    [JsonConstructor]
    public DbcDevice(string name, int baseId)
    {
        Name = name;
        BaseId = baseId;
        Guid =  Guid.NewGuid();
    }
    
    public void SetLogger(ILogger<DbcDevice> logger)
    {
        Logger = logger;
    }
    
    public void UpdateIsConnected()
    {
        TimeSpan timeSpan = DateTime.Now - LastRxTime;
        Connected = timeSpan.TotalMilliseconds < 500;
    }

    private void Clear()
    {
        foreach (var signal in DbcSignals)
        {
            signal.Value = 0.0;
        }
    }

    public void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> queue)
    {
        if (DbcSignals.Count == 0) return;

        foreach (var dbcProp in DbcSignals)
        {
            if (id == dbcProp.Id)
            {
                dbcProp.Value = DbcSignalCodec.ExtractSignal(data, dbcProp);
            }
        }
        
        LastRxTime = DateTime.Now;
    }

    public bool InIdRange(int id)
    {
        return id >= MinId && id <= MaxId;
    }

    public async Task<bool> ParseDbcFile(string dbcFilePath)
    {
        DbcFilePath = dbcFilePath;

        if (string.IsNullOrEmpty(DbcFilePath))
        {
            Logger.LogError("StatusDevice {Name} DbcFilePath empty", Name);
            return await Task.FromResult(false);
        }

        DbcSignals.Clear();

        DbcSignals.AddRange(DbcParser.ParseFile(DbcFilePath, Logger));
        UpdateIdRange();

        Logger.LogInformation("{Name} loaded {Count} signals. ID range: {MinId}-{MaxId}",
            Name, DbcSignals.Count, MinId, MaxId);
        
        SignalsChanged?.Invoke(this, EventArgs.Empty);

        return await Task.FromResult(true);
    }

    public async Task<bool> AddCustomSignal(DbcSignal signal)
    {
        if(signal.Id == 0) return await Task.FromResult(false);
        if(signal.Length == 0) return await Task.FromResult(false);

        DbcSignals.Add(signal);

        UpdateIdRange();

        Logger.LogInformation("{Name} added {SignalName} signal",
            Name, signal.Name);
        
        SignalsChanged?.Invoke(this, EventArgs.Empty);

        return await Task.FromResult(true);
    }

    /// <summary>
    /// Updates MinId and MaxId based on the IDs in DbcProperties
    /// </summary>
    public void UpdateIdRange()
    {
        if (DbcSignals.Count == 0)
        {
            MinId = int.MaxValue;
            MaxId = int.MinValue;
            return;
        }

        MinId = DbcSignals.Min(p => p.Id);
        MaxId = DbcSignals.Max(p => p.Id);
    }

    public IEnumerable<(int MessageId, DbcSignal Signal)> GetStatusSignals()
    {
        // DbcSignals already have Id populated from DBC file
        foreach (var signal in DbcSignals)
        {
            yield return (signal.Id, signal);
        }
    }
}