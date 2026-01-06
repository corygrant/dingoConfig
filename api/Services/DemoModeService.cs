using Microsoft.Extensions.Configuration;

namespace api.Services;

public class DemoModeService
{
    private readonly IConfiguration _configuration;

    public DemoModeService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsEnabled => _configuration.GetValue<bool>("DemoMode:Enabled");
    public bool AutoStart => _configuration.GetValue<bool>("DemoMode:AutoStart");
    public string SampleLogFile => _configuration.GetValue<string>("DemoMode:SampleLogFile") ?? "sample-logs/demo.csv";
    public bool RestrictModifications => _configuration.GetValue<bool>("DemoMode:RestrictModifications");
    public bool ShowBanner => _configuration.GetValue<bool>("DemoMode:ShowBanner");

    public List<DemoDevice> GetPrePopulatedDevices()
    {
        var devices = new List<DemoDevice>();
        var section = _configuration.GetSection("DemoMode:PrePopulatedDevices");

        foreach (var deviceSection in section.GetChildren())
        {
            devices.Add(new DemoDevice
            {
                Type = deviceSection.GetValue<string>("Type") ?? "",
                Name = deviceSection.GetValue<string>("Name") ?? "",
                BaseId = deviceSection.GetValue<int>("BaseId")
            });
        }

        return devices;
    }
}

public class DemoDevice
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public int BaseId { get; set; }
}
