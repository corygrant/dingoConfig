using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using domain.Common;
using domain.Interfaces;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace domain.Devices.Generic;

public class StatusDevice : IDevice 
{
    [JsonIgnore] protected readonly ILogger<StatusDevice> Logger;
    
    [JsonIgnore] public Guid Guid { get; }
    [JsonIgnore] public string Type => "StatusDevice";
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }
    [JsonIgnore] private DateTime LastRxTime { get; set; }

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

    [JsonIgnore] public bool Configurable { get; }
    [JsonIgnore] private string? DbcFilePath { get; set; }
    
    [JsonPropertyName("minId")] public int MinId { get; set; }
    [JsonPropertyName("maxId")] public int MaxId { get; set; }
    
    [JsonPropertyName("dbcProp")][Plotable(displayName:"DbcProp")] public List<DbcProperty> DbcProperties { get; } = [];

    
    /// <summary>
    /// Must set DbcFilePath before calling constructor
    /// </summary>
    public StatusDevice(ILogger<StatusDevice> logger, string name, int baseId)
    {
        Logger = logger;
        Guid = Guid.NewGuid();
        Name = name;
        BaseId = baseId;

        Configurable = false;

        Logger.LogDebug("StatusDevice {Name} created", Name);
    }
    
    public void UpdateIsConnected()
    {
        TimeSpan timeSpan = DateTime.Now - LastRxTime;
        Connected = timeSpan.TotalMilliseconds < 500;
    }

    private void Clear()
    {
        DbcProperties.Clear(); 
    }

    public void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceCanFrame> queue)
    {
        if (DbcProperties.Count == 0) return;

        foreach (var dbcProp in DbcProperties)
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

        DbcProperties.Clear();
        
        DbcProperties.AddRange(DbcParser.ParseFile(DbcFilePath, Logger));
        UpdateIdRange();

        Logger.LogInformation("StatusDevice {Name} loaded {Count} signals. ID range: {MinId}-{MaxId}",
            Name, DbcProperties.Count, MinId, MaxId);
        
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Updates MinId and MaxId based on the IDs in DbcProperties
    /// </summary>
    private void UpdateIdRange()
    {
        if (DbcProperties.Count == 0)
        {
            MinId = int.MaxValue;
            MaxId = int.MinValue;
            return;
        }

        MinId = DbcProperties.Min(p => p.Id);
        MaxId = DbcProperties.Max(p => p.Id);
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