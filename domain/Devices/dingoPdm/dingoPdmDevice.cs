using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using domain.Devices.dingoPdm.Functions;
using domain.Devices.dingoPdm.Enums;
using domain.Enums;
using domain.Interfaces;
using domain.Models;

namespace domain.Devices;

public class dingoPdmDevice : IDevice
{
    protected virtual int _minMajorVersion { get; } = 0;
    protected virtual int _minMinorVersion { get; } = 4;
    protected virtual int _minBuildVersion { get; } = 18;

    protected virtual int _numDigitalInputs { get; } = 2;
    protected virtual int _numOutputs { get; } = 8;
    protected virtual int _numCanInputs { get; } = 32;
    protected virtual int _numVirtualInputs { get; } = 16;
    protected virtual int _numFlashers { get; } = 4;
    protected virtual int _numCounters { get; } = 4;
    protected virtual int _numConditions { get; } = 32;
    
    [JsonPropertyName("guid")] public Guid Id { get; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("baseId")] public int BaseId { get; set; }

    [JsonIgnore] public DateTime LastRxTime { get; set; }
    [JsonIgnore] public DeviceState DeviceState { get; set; }
    [JsonIgnore] public double TotalCurrent { get; set; }
    [JsonIgnore] public double BatteryVoltage { get; set; }
    [JsonIgnore] public double BoardTempC { get; set; }
    [JsonIgnore] public double BoardTempF { get; set; }
    [JsonIgnore] public string Version { get; set; } = "";
    [JsonIgnore] public bool SleepEnabled { get; set; }
    [JsonIgnore] public bool CanFiltersEnabled { get; set; }
    [JsonIgnore] public CanBitRate BitRate { get; set; }
    
    [JsonPropertyName("digitalInputs")] protected List<Input> DigitalInputs { get; set; } = new List<Input>();
    [JsonPropertyName("outputs")] protected List<Output> Outputs { get; set; } = new List<Output>();
    [JsonPropertyName("canInputs")] protected List<CanInput> CanInputs { get; set; } = new List<CanInput>();
    [JsonPropertyName("virtualInputs")] protected List<VirtualInput> VirtualInputs { get; set; } = new List<VirtualInput>();
    [JsonPropertyName("wipers")] protected Wiper Wipers { get; set; } = new Wiper("wiper");
    [JsonPropertyName("flashers")] protected List<Flasher> Flashers { get; set; } = new List<Flasher>();
    [JsonPropertyName("starterDisable")] protected StarterDisable StarterDisable { get; set; } = new StarterDisable("starterDisable");
    [JsonPropertyName("counters")] protected  List<Counter> Counters { get; set; } = new List<Counter>();
    [JsonPropertyName("conditions")] protected List<Condition> Conditions { get; set; } = new List<Condition>();
    
    public bool Connected
    {
        get;
        set
        {
            if (field != value)
            {
                Clear();
                
                field = value;
            }
        }
    }
    
    public dingoPdmDevice(string name, int baseId)
    {
        Name = name;
        BaseId = baseId;

        for (var i = 0; i < _numDigitalInputs; i++)
            DigitalInputs.Add(new Input(i + 1, "digitalInput" + i));
        
        for (var i = 0; i < _numOutputs; i++)
            Outputs.Add(new Output(i + 1, "output" + i));

        for (var i = 0; i < _numCanInputs; i++)
            CanInputs.Add(new CanInput(i + 1, "canInput" + i));

        for (var i = 0; i < _numVirtualInputs; i++)
            VirtualInputs.Add(new VirtualInput(i + 1, "virtualInput" + i));

        for (var i = 0; i < _numFlashers; i++)
            Flashers.Add(new Flasher(i + 1,  "flasher" + i));

        for (var i = 0; i < _numCounters; i++)
            Counters.Add(new Counter(i  + 1, "counter" + i));

        for (var i = 0; i < _numConditions; i++)
            Conditions.Add(new Condition(i + 1, "condition" + i));
    }
    public void UpdateConnected()
    {
        TimeSpan timeSpan = DateTime.Now - LastRxTime;
        Connected = timeSpan.TotalMilliseconds < 500;
    }

    public void Clear()
    {
        foreach(var input in DigitalInputs)
            input.State = false;

        foreach(var output in Outputs)
        {
            output.Current = 0;
            output.State = OutState.Off;
        }

        foreach(var input in VirtualInputs)
            input.Value = false;

        foreach(var canInput in CanInputs)
            canInput.Output = false;
    }

    public bool InIdRange(int id)
    {
        return (id >= BaseId) && (id <= BaseId + 31);
    }
    
    public void Read(int id, byte[] data, ref ConcurrentDictionary<(int BaseId, int Prefix, int Index), DeviceResponse> queue)
    {
        throw new NotImplementedException();
    }

    public List<DeviceResponse> GetUploadMsgs()
    {
        throw new NotImplementedException();
    }

    public List<DeviceResponse> GetDownloadMsgs()
    {
        throw new NotImplementedException();
    }

    public List<DeviceResponse> GetUpdateMsgs(int newId)
    {
        throw new NotImplementedException();
    }

    public DeviceResponse GetBurnMsg()
    {
        throw new NotImplementedException();
    }

    public DeviceResponse GetSleepMsg()
    {
        throw new NotImplementedException();
    }

    public DeviceResponse GetVersionMsg()
    {
        throw new NotImplementedException();
    }
}