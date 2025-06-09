using dingoConfig.Core.Models;

namespace dingoConfig.Core.Tests.TestData;

public class DeviceConfigurationBuilder
{
    private string _deviceId = TestConstants.ValidDeviceId;
    private string _deviceType = TestConstants.ValidDeviceType;
    private string _name = TestConstants.ValidDeviceName;
    private DateTime _lastModified = DateTime.UtcNow;
    private Dictionary<string, object> _settings = new();
    private string _description = "Test device configuration";
    private string _location = "Test Location";
    private List<string> _tags = new();

    public DeviceConfigurationBuilder WithDeviceId(string deviceId)
    {
        _deviceId = deviceId;
        return this;
    }

    public DeviceConfigurationBuilder WithDeviceType(string deviceType)
    {
        _deviceType = deviceType;
        return this;
    }

    public DeviceConfigurationBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public DeviceConfigurationBuilder WithLastModified(DateTime lastModified)
    {
        _lastModified = lastModified;
        return this;
    }

    public DeviceConfigurationBuilder WithSetting(string key, object value)
    {
        _settings[key] = value;
        return this;
    }

    public DeviceConfigurationBuilder WithSettings(Dictionary<string, object> settings)
    {
        _settings = settings;
        return this;
    }

    public DeviceConfigurationBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public DeviceConfigurationBuilder WithLocation(string location)
    {
        _location = location;
        return this;
    }

    public DeviceConfigurationBuilder WithTags(params string[] tags)
    {
        _tags = tags.ToList();
        return this;
    }

    public DeviceConfigurationBuilder WithDefaultDingoPDMSettings()
    {
        return WithSetting(TestConstants.ParameterNames.CanNodeId, TestConstants.ValidCanNodeId)
               .WithSetting(TestConstants.ParameterNames.Output1Enabled, true)
               .WithSetting(TestConstants.ParameterNames.Output1CurrentLimit, 15.0)
               .WithSetting(TestConstants.ParameterNames.DiagnosticInterval, 1000);
    }

    public DeviceConfiguration Build()
    {
        return new DeviceConfiguration
        {
            DeviceId = _deviceId,
            DeviceType = _deviceType,
            Name = _name,
            LastModified = _lastModified,
            Settings = _settings,
            Description = _description,
            Location = _location,
            Tags = _tags
        };
    }

    public static DeviceConfiguration CreateDefault()
    {
        return new DeviceConfigurationBuilder()
            .WithDefaultDingoPDMSettings()
            .Build();
    }

    public static DeviceConfiguration CreateMinimal()
    {
        return new DeviceConfigurationBuilder()
            .WithSetting(TestConstants.ParameterNames.CanNodeId, 1)
            .Build();
    }
}