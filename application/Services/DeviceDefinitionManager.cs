using System.Text.Json;
using domain.Devices.dingoPdm;
using domain.Models;

namespace application.Services;

public class DeviceDefinitionManager
{
    private const string PdmFilename = "pdm-definitions.json";
    private const string PdmCyclicSigsFilename = "pdm-cyclic-sigs.json";
    private const string CanboardCyclicSigsFilename = "canboard-cyclic-sigs.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static readonly PdmDeviceDefinition DefaultPdm = new(
        PdmType: 0,
        TypeName: "dingoPDM",
        Icon: "Bolt",
        NumDigitalInputs: 2,
        NumOutputs: 8,
        NumCanInputs: 32,
        NumCanOutputs: 32,
        NumVirtualInputs: 16,
        NumFlashers: 4,
        NumCounters: 4,
        NumConditions: 32,
        NumKeypads: 2,
        MinMajorVersion: 0,
        MinMinorVersion: 5,
        MinBuildVersion: 0);

    private readonly IReadOnlyList<PdmDeviceDefinition> _pdmDefinitions;
    private readonly CyclicSigsConfig? _pdmCyclicSigsConfig;
    private readonly CyclicSigsConfig? _canboardCyclicSigsConfig;

    public DeviceDefinitionManager()
    {
        _pdmDefinitions = LoadPdmDefinitions();
        _pdmCyclicSigsConfig = LoadCyclicSigsConfig(PdmCyclicSigsFilename);
        _canboardCyclicSigsConfig = LoadCyclicSigsConfig(CanboardCyclicSigsFilename);
    }

    private static IReadOnlyList<PdmDeviceDefinition> LoadPdmDefinitions()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, PdmFilename);
        if (!File.Exists(filePath))
            return [DefaultPdm];

        try
        {
            var json = File.ReadAllText(filePath);
            var defs = JsonSerializer.Deserialize<List<PdmDeviceDefinition>>(json, JsonOptions);
            return defs?.Count > 0 ? defs : [DefaultPdm];
        }
        catch
        {
            return [DefaultPdm];
        }
    }

    private static CyclicSigsConfig? LoadCyclicSigsConfig(string filename)
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, filename);
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

    public IReadOnlyList<PdmDeviceDefinition> GetAllPdms() => _pdmDefinitions;

    public PdmDeviceDefinition? GetByPdmType(int pdmType) =>
        _pdmDefinitions.FirstOrDefault(d => d.PdmType == pdmType);

    public CyclicSigsConfig? GetPdmCyclicSigsConfig() => _pdmCyclicSigsConfig;
    public CyclicSigsConfig? GetCanboardCyclicSigsConfig() => _canboardCyclicSigsConfig;
}
