using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace domain.Devices.Generic;

public class DbcDevice(string name, int baseId) : IDevice
{
    [JsonIgnore] protected ILogger<DbcDevice> Logger = null!;

    [JsonIgnore] public Guid Guid { get; } = Guid.NewGuid();
    [JsonIgnore] public string Type => "DbcDevice";
    [JsonPropertyName("name")] public string Name { get; set; } = name;
    [JsonPropertyName("baseId")] public int BaseId { get; set; } = baseId;
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
    [JsonIgnore] public bool Configurable => false;

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
    private void UpdateIdRange()
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

    #region Unused IDevice methods
    
    public List<DeviceCanFrame> GetReadMsgs()
    {
        //No read configuration messages on Status Devices
        return [];
    }

    public List<DeviceCanFrame> GetWriteMsgs()
    {
        //No write configuration message on Status Devices
        return [];
    }

    public List<DeviceCanFrame> GetModifyMsgs(int newId)
    {
        //No modify configuration message on Status Devices
        return [];
    }

    public DeviceCanFrame GetBurnMsg()
    {
        //No burn configuration message on Status Devices
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetSleepMsg()
    {
        //No sleep message on Status Devices
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetVersionMsg()
    {
        //No version message on Status Devices
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetWakeupMsg()
    {
        //No wakeup message on Status Devices
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }

    public DeviceCanFrame GetBootloaderMsg()
    {
        //No bootloader message on Status Devices
        return new DeviceCanFrame
        {
            Frame = new CanFrame
            (
                0,
                0,
                new byte[8]
            )
        };
    }
    
    #endregion
}