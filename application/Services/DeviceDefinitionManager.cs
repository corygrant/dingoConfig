using System.Text.Json;
using domain.Devices.dingoPdm;

namespace application.Services;

public class DeviceDefinitionManager
{
    private const string PdmFilename = "pdm-definitions.json";

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

    public DeviceDefinitionManager()
    {
        _pdmDefinitions = LoadPdmDefinitions();
    }

    private static IReadOnlyList<PdmDeviceDefinition> LoadPdmDefinitions()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, PdmFilename);
        if (!File.Exists(filePath))
            return [DefaultPdm];

        try
        {
            var json = File.ReadAllText(filePath);
            var defs = JsonSerializer.Deserialize<List<PdmDeviceDefinition>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return defs?.Count > 0 ? defs : [DefaultPdm];
        }
        catch
        {
            return [DefaultPdm];
        }
    }

    public IReadOnlyList<PdmDeviceDefinition> GetAllPdms() => _pdmDefinitions;

    public PdmDeviceDefinition? GetByPdmType(int pdmType) =>
        _pdmDefinitions.FirstOrDefault(d => d.PdmType == pdmType);
}
