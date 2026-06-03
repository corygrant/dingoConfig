using System.Text.Json;
using domain.Devices;
using domain.Models;

namespace application.Services;

public class FwDeviceDefManager
{
    private const string Directory = "Definitions/";
    private const string DeviceFilename = "device-definitions.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static readonly FwDeviceDef DefaultFwDevice = new(
        DeviceType: 0,
        DefaultId: "0x0DE",
        TypeName: "dingoPDM",
        Icon: "Bolt",
        SignalsFile: "dingopdm-signals.json",
        NumDigitalInputs: 2,
        NumDigitalOutputs: 0,
        NumAnalogInputs: 0,
        NumOutputs: 8,
        NumCanInputs: 32,
        NumCanOutputs: 32,
        NumVirtualInputs: 16,
        NumFlashers: 4,
        NumCounters: 4,
        NumConditions: 32,
        NumKeypads: 2,
        HasWipers: true,
        HasStarterDisable: true,
        HasBattVoltSense: true,
        HasUsb: true,
        HasExtTempSensor: true,
        CanSleep: true,
        CanBootloader: true,
        MinMajorVersion: 0,
        MinMinorVersion: 5,
        MinBuildVersion: 0);

    private readonly List<FwDeviceDef> _fwDeviceDefs;
    private readonly List<CyclicSigsConfig> _fwDeviceCyclicSigsConfigs = [];

    public FwDeviceDefManager()
    {
        //Load device definitions
        _fwDeviceDefs = LoadDeviceDefinitions();
        
        //Load cyclic messages for each device
        foreach (var device in _fwDeviceDefs)
        {
            var signals = LoadCyclicSigsConfig(device.SignalsFile);
            if(signals != null)
                _fwDeviceCyclicSigsConfigs.Add(signals);
        }
    }

    private static List<FwDeviceDef> LoadDeviceDefinitions()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, Path.Combine(Directory, DeviceFilename));
        if (!File.Exists(filePath))
            return [DefaultFwDevice];

        try
        {
            var json = File.ReadAllText(filePath);
            var defs = JsonSerializer.Deserialize<List<FwDeviceDef>>(json, JsonOptions);
            return defs?.Count > 0 ? defs : [DefaultFwDevice];
        }
        catch
        {
            return [DefaultFwDevice];
        }
    }

    private static CyclicSigsConfig? LoadCyclicSigsConfig(string filename)
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, Path.Combine(Directory, filename));
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<CyclicSigsConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<FwDeviceDef> GetAllDevices() => _fwDeviceDefs;

    public FwDeviceDef? GetByDeviceType(int deviceType) =>
        _fwDeviceDefs.FirstOrDefault(d => d.DeviceType == deviceType);

    public CyclicSigsConfig? GetDeviceCyclicSigsConfig(int deviceType)
    {
        if (deviceType < 0 || deviceType >= _fwDeviceCyclicSigsConfigs.Count) return null;
        
        var sigs = _fwDeviceCyclicSigsConfigs[deviceType];
        return sigs;
    }
}
