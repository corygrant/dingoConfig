using dingoConfig.Core.Models;
using dingoConfig.Core.Tests.TestData;
using FluentAssertions;
using Newtonsoft.Json;

namespace dingoConfig.Core.Tests.Models;

public class DeviceTests
{
    [Fact]
    public void Device_ShouldValidateRequiredProperties()
    {
        // Arrange & Act
        var device = new Device();
        var validationResult = device.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Id"));
        validationResult.Errors.Should().Contain(e => e.Contains("Name"));
        validationResult.Errors.Should().Contain(e => e.Contains("Type"));
    }

    [Fact]
    public void Device_ShouldValidateSuccessfully_WhenAllRequiredPropertiesProvided()
    {
        // Arrange
        var device = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM,
            Parameters = new List<Parameter>(),
            Communication = new CommunicationSettings()
        };

        // Act
        var validationResult = device.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
        validationResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Device_ShouldSerializeToJsonCorrectly()
    {
        // Arrange
        var device = new Device
        {
            Id = TestConstants.ValidDeviceId,
            Name = TestConstants.ValidDeviceName,
            Type = DeviceType.DingoPDM,
            Parameters = new List<Parameter>
            {
                new Parameter { Id = "test_param", Name = "Test Parameter", Type = ParameterType.Boolean, DefaultValue = true }
            },
            Communication = new CommunicationSettings
            {
                Protocol = CommunicationProtocol.CAN,
                BaudRate = TestConstants.ValidBaudRate,
                BaseId = TestConstants.ValidCanBaseId
            }
        };

        // Act
        var json = JsonConvert.SerializeObject(device, Formatting.Indented);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain(TestConstants.ValidDeviceId);
        json.Should().Contain(TestConstants.ValidDeviceName);
        json.Should().Contain("DingoPDM");
        json.Should().Contain("test_param");
    }

    [Fact]
    public void Device_ShouldDeserializeFromJsonCorrectly()
    {
        // Arrange
        var json = @"{
            ""Id"": ""test-device"",
            ""Name"": ""Test Device"",
            ""Type"": ""DingoPDM"",
            ""Parameters"": [
                {
                    ""Id"": ""test_param"",
                    ""Name"": ""Test Parameter"",
                    ""Type"": ""Boolean"",
                    ""DefaultValue"": true
                }
            ],
            ""Communication"": {
                ""Protocol"": ""CAN"",
                ""BaudRate"": 500000,
                ""BaseId"": 1536
            }
        }";

        // Act
        var device = JsonConvert.DeserializeObject<Device>(json);

        // Assert
        device.Should().NotBeNull();
        device!.Id.Should().Be("test-device");
        device.Name.Should().Be("Test Device");
        device.Type.Should().Be(DeviceType.DingoPDM);
        device.Parameters.Should().HaveCount(1);
        device.Parameters.First().Id.Should().Be("test_param");
        device.Communication.Should().NotBeNull();
        device.Communication!.Protocol.Should().Be(CommunicationProtocol.CAN);
    }

    [Fact]
    public void Device_ShouldHaveEmptyCollections_WhenInitialized()
    {
        // Arrange & Act
        var device = new Device();

        // Assert
        device.Parameters.Should().NotBeNull().And.BeEmpty();
        device.Telemetry.Should().NotBeNull().And.BeEmpty();
        device.Commands.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Device_ShouldValidateParameterIds_AreUnique()
    {
        // Arrange
        var device = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM,
            Parameters = new List<Parameter>
            {
                new Parameter { Id = "duplicate_id", Name = "Parameter 1", Type = ParameterType.Boolean },
                new Parameter { Id = "duplicate_id", Name = "Parameter 2", Type = ParameterType.Integer }
            }
        };

        // Act
        var validationResult = device.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("duplicate") && e.Contains("Parameter"));
    }

    [Fact]
    public void Device_ShouldValidateTelemetryIds_AreUnique()
    {
        // Arrange
        var device = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM,
            Telemetry = new List<TelemetryItem>
            {
                new TelemetryItem { Id = "duplicate_id", Name = "Telemetry 1", Type = TelemetryType.Float },
                new TelemetryItem { Id = "duplicate_id", Name = "Telemetry 2", Type = TelemetryType.Integer }
            }
        };

        // Act
        var validationResult = device.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("duplicate") && e.Contains("Telemetry"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Device_ShouldFailValidation_WhenIdIsNullOrWhitespace(string invalidId)
    {
        // Arrange
        var device = new Device
        {
            Id = invalidId,
            Name = "Valid Name",
            Type = DeviceType.DingoPDM
        };

        // Act
        var validationResult = device.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Id"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Device_ShouldFailValidation_WhenNameIsNullOrWhitespace(string invalidName)
    {
        // Arrange
        var device = new Device
        {
            Id = "valid-id",
            Name = invalidName,
            Type = DeviceType.DingoPDM
        };

        // Act
        var validationResult = device.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Name"));
    }
}