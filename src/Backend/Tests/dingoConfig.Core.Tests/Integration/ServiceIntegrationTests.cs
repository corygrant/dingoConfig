using dingoConfig.Core.Interfaces;
using dingoConfig.Core.Models;
using dingoConfig.Core.Services;
using dingoConfig.Core.Tests.TestData;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace dingoConfig.Core.Tests.Integration;

public class ServiceIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<FileStorageService>> _mockFileLogger;
    private readonly FileStorageService _fileStorageService;
    private readonly CatalogService _catalogService;
    private readonly ConfigurationService _configurationService;
    private readonly string _testDirectory;

    public ServiceIntegrationTests()
    {
        _mockFileLogger = new Mock<ILogger<FileStorageService>>();
        _fileStorageService = new FileStorageService(_mockFileLogger.Object);
        _catalogService = new CatalogService(_fileStorageService);
        _configurationService = new ConfigurationService(_fileStorageService);
        
        _testDirectory = Path.Combine(Path.GetTempPath(), "dingoConfig_integration_tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        
        // Set up test directories
        _catalogService.SetCatalogDirectory(Path.Combine(_testDirectory, "catalogs"));
        _configurationService.SetConfigurationDirectory(Path.Combine(_testDirectory, "configurations"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task EndToEndWorkflow_ShouldCreateLoadValidateAndDeleteConfiguration()
    {
        // Arrange - Create a device catalog
        var catalogDirectory = _catalogService.GetCatalogDirectory();
        await _fileStorageService.CreateDirectoryAsync(catalogDirectory);

        var device = new Device
        {
            Id = "dingo-pdm-001",
            Name = "Dingo PDM Standard",
            Type = DeviceType.DingoPDM,
            Description = "Standard PDM configuration",
            Parameters = new List<Parameter>
            {
                new Parameter
                {
                    Id = TestConstants.ParameterNames.CanNodeId,
                    Name = "CAN Node ID",
                    Type = ParameterType.Integer,
                    Min = 1,
                    Max = 16,
                    DefaultValue = 1,
                    IsRequired = true
                },
                new Parameter
                {
                    Id = TestConstants.ParameterNames.OutputChannels,
                    Name = "Output Channels",
                    Type = ParameterType.Integer,
                    Min = 1,
                    Max = 8,
                    DefaultValue = 8,
                    IsRequired = true
                }
            }
        };

        var catalogPath = Path.Combine(catalogDirectory, "dingo-pdm.json");
        await _fileStorageService.WriteJsonAsync(device, catalogPath);

        // Act - Load catalog and create configuration
        var loadedDevice = await _catalogService.LoadCatalogAsync(catalogPath);
        loadedDevice.Should().NotBeNull();

        var configuration = new DeviceConfigurationBuilder()
            .WithDeviceId("test-device-001")
            .WithDeviceType("DingoPDM")
            .WithSetting(TestConstants.ParameterNames.CanNodeId, 5)
            .WithSetting(TestConstants.ParameterNames.OutputChannels, 6)
            .Build();

        var configDirectory = _configurationService.GetConfigurationDirectory();
        await _fileStorageService.CreateDirectoryAsync(configDirectory);
        
        var configPath = Path.Combine(configDirectory, "test-device-001.json");

        // Save configuration
        var saveResult = await _configurationService.SaveConfigurationAsync(configuration, configPath);
        saveResult.Should().BeTrue();

        // Load configuration back
        var loadedConfig = await _configurationService.LoadConfigurationAsync(configPath);
        loadedConfig.Should().NotBeNull();
        loadedConfig!.DeviceId.Should().Be("test-device-001");
        loadedConfig.Settings[TestConstants.ParameterNames.CanNodeId].Should().Be(5);

        // Validate configuration against catalog
        var validationResult = await _configurationService.ValidateConfigurationAsync(loadedConfig, loadedDevice!);
        validationResult.Should().BeTrue();

        // Delete configuration
        var deleteResult = await _configurationService.DeleteConfigurationAsync(configPath);
        deleteResult.Should().BeTrue();

        // Verify deletion
        var fileExists = await _fileStorageService.FileExistsAsync(configPath);
        fileExists.Should().BeFalse();
    }

    [Fact]
    public async Task CatalogService_ShouldHandleMultipleDeviceTypes()
    {
        // Arrange
        var catalogDirectory = _catalogService.GetCatalogDirectory();
        await _fileStorageService.CreateDirectoryAsync(catalogDirectory);

        var devices = new List<Device>
        {
            new Device
            {
                Id = "dingo-pdm",
                Name = "Dingo PDM",
                Type = DeviceType.DingoPDM,
                Parameters = new List<Parameter>
                {
                    new Parameter { Id = "param1", Type = ParameterType.Integer, Min = 1, Max = 10 }
                }
            },
            new Device
            {
                Id = "dingo-pdm-max",
                Name = "Dingo PDM Max",
                Type = DeviceType.DingoPDMMax,
                Parameters = new List<Parameter>
                {
                    new Parameter { Id = "param2", Type = ParameterType.Float, Min = 0.1, Max = 99.9 }
                }
            },
            new Device
            {
                Id = "can-board",
                Name = "CAN Board",
                Type = DeviceType.CANBoard,
                Parameters = new List<Parameter>
                {
                    new Parameter { Id = "param3", Type = ParameterType.Boolean }
                }
            }
        };

        // Save all device catalogs
        foreach (var device in devices)
        {
            var catalogPath = Path.Combine(catalogDirectory, $"{device.Id}.json");
            await _fileStorageService.WriteJsonAsync(device, catalogPath);
        }

        // Act
        await _catalogService.ReloadCatalogsAsync();
        var availableTypes = await _catalogService.GetAvailableDeviceTypesAsync();

        // Assert
        availableTypes.Should().HaveCount(3);
        availableTypes.Should().Contain("DingoPDM");
        availableTypes.Should().Contain("DingoPDMMax");
        availableTypes.Should().Contain("CANBoard");

        // Test individual catalog retrieval
        var dingoPDM = await _catalogService.GetDeviceCatalogAsync("DingoPDM");
        dingoPDM.Should().NotBeNull();
        dingoPDM!.Id.Should().Be("dingo-pdm");

        var canBoard = await _catalogService.GetDeviceCatalogAsync("CANBoard");
        canBoard.Should().NotBeNull();
        canBoard!.Id.Should().Be("can-board");
    }

    [Fact]
    public async Task ConfigurationService_ShouldHandleMultipleConfigurationsOfSameType()
    {
        // Arrange
        var configDirectory = _configurationService.GetConfigurationDirectory();
        await _fileStorageService.CreateDirectoryAsync(configDirectory);

        var configurations = new List<DeviceConfiguration>
        {
            new DeviceConfigurationBuilder()
                .WithDeviceId("device-001")
                .WithDeviceType("DingoPDM")
                .WithSetting("param1", 1)
                .Build(),
            new DeviceConfigurationBuilder()
                .WithDeviceId("device-002")
                .WithDeviceType("DingoPDM")
                .WithSetting("param1", 2)
                .Build(),
            new DeviceConfigurationBuilder()
                .WithDeviceId("device-003")
                .WithDeviceType("CANBoard")
                .WithSetting("param2", true)
                .Build()
        };

        // Save configurations
        foreach (var config in configurations)
        {
            var configPath = Path.Combine(configDirectory, $"{config.DeviceId}.json");
            await _configurationService.SaveConfigurationAsync(config, configPath);
        }

        // Act - Load all configurations
        var allConfigs = await _configurationService.LoadAllConfigurationsAsync(configDirectory);

        // Assert
        allConfigs.Should().HaveCount(3);
        allConfigs.Should().Contain(c => c.DeviceId == "device-001");
        allConfigs.Should().Contain(c => c.DeviceId == "device-002");
        allConfigs.Should().Contain(c => c.DeviceId == "device-003");

        // Test filtering by type
        var dingoPDMConfigs = await _configurationService.GetConfigurationsByTypeAsync("DingoPDM");
        dingoPDMConfigs.Should().HaveCount(2);
        dingoPDMConfigs.Should().AllSatisfy(c => c.DeviceType.Should().Be("DingoPDM"));

        var canBoardConfigs = await _configurationService.GetConfigurationsByTypeAsync("CANBoard");
        canBoardConfigs.Should().HaveCount(1);
        canBoardConfigs.First().DeviceId.Should().Be("device-003");
    }

    [Fact]
    public async Task ValidationWorkflow_ShouldRejectInvalidConfigurations()
    {
        // Arrange - Create device catalog with strict validation
        var catalogDirectory = _catalogService.GetCatalogDirectory();
        await _fileStorageService.CreateDirectoryAsync(catalogDirectory);

        var device = new Device
        {
            Id = "strict-device",
            Name = "Strict Validation Device",
            Type = DeviceType.DingoPDM,
            Parameters = new List<Parameter>
            {
                new Parameter
                {
                    Id = "voltage",
                    Type = ParameterType.Float,
                    Min = 12.0,
                    Max = 14.0,
                    IsRequired = true
                },
                new Parameter
                {
                    Id = "current",
                    Type = ParameterType.Integer,
                    Min = 1,
                    Max = 10,
                    IsRequired = true
                },
                new Parameter
                {
                    Id = "mode",
                    Type = ParameterType.Enum,
                    Options = new List<string> { "Auto", "Manual", "Test" },
                    IsRequired = true
                }
            }
        };

        var catalogPath = Path.Combine(catalogDirectory, "strict-device.json");
        await _fileStorageService.WriteJsonAsync(device, catalogPath);
        var loadedDevice = await _catalogService.LoadCatalogAsync(catalogPath);

        // Test valid configuration
        var validConfig = new DeviceConfigurationBuilder()
            .WithDeviceType("DingoPDM")
            .WithSetting("voltage", 13.2)
            .WithSetting("current", 5)
            .WithSetting("mode", "Auto")
            .Build();

        var validResult = await _configurationService.ValidateConfigurationAsync(validConfig, loadedDevice!);
        validResult.Should().BeTrue();

        // Test invalid configurations
        var invalidVoltageConfig = new DeviceConfigurationBuilder()
            .WithDeviceType("DingoPDM")
            .WithSetting("voltage", 15.0) // Out of range
            .WithSetting("current", 5)
            .WithSetting("mode", "Auto")
            .Build();

        var invalidVoltageResult = await _configurationService.ValidateConfigurationAsync(invalidVoltageConfig, loadedDevice!);
        invalidVoltageResult.Should().BeFalse();

        var invalidEnumConfig = new DeviceConfigurationBuilder()
            .WithDeviceType("DingoPDM")
            .WithSetting("voltage", 13.2)
            .WithSetting("current", 5)
            .WithSetting("mode", "InvalidMode") // Invalid enum value
            .Build();

        var invalidEnumResult = await _configurationService.ValidateConfigurationAsync(invalidEnumConfig, loadedDevice!);
        invalidEnumResult.Should().BeFalse();

        var missingRequiredConfig = new DeviceConfigurationBuilder()
            .WithDeviceType("DingoPDM")
            .WithSetting("voltage", 13.2)
            // Missing required 'current' parameter
            .WithSetting("mode", "Auto")
            .Build();

        var missingRequiredResult = await _configurationService.ValidateConfigurationAsync(missingRequiredConfig, loadedDevice!);
        missingRequiredResult.Should().BeFalse();
    }

    [Fact]
    public async Task FileOperations_ShouldHandleErrorsGracefully()
    {
        // Test handling of invalid file paths
        var invalidConfig = await _configurationService.LoadConfigurationAsync("");
        invalidConfig.Should().BeNull();

        var invalidCatalog = await _catalogService.GetDeviceCatalogAsync("");
        invalidCatalog.Should().BeNull();

        // Test handling of non-existent directories
        var emptyConfigs = await _configurationService.LoadAllConfigurationsAsync("/nonexistent/directory");
        emptyConfigs.Should().BeEmpty();

        var emptyCatalogs = await _catalogService.LoadAllCatalogsAsync("/nonexistent/directory");
        emptyCatalogs.Should().BeEmpty();

        // Test directory creation and validation
        var testDir = Path.Combine(_testDirectory, "new-test-dir");
        var createResult = await _fileStorageService.CreateDirectoryAsync(testDir);
        createResult.Should().BeTrue();

        var existsResult = await _fileStorageService.DirectoryExistsAsync(testDir);
        existsResult.Should().BeTrue();
    }

    [Fact]
    public async Task CacheOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var catalogDirectory = _catalogService.GetCatalogDirectory();
        await _fileStorageService.CreateDirectoryAsync(catalogDirectory);

        var device = new Device
        {
            Id = "cache-test",
            Name = "Cache Test Device",
            Type = DeviceType.DingoPDM
        };

        var catalogPath = Path.Combine(catalogDirectory, "cache-test.json");
        await _fileStorageService.WriteJsonAsync(device, catalogPath);

        // Act - First load should cache the device
        var firstLoad = await _catalogService.GetDeviceCatalogAsync("DingoPDM");
        firstLoad.Should().NotBeNull();

        // Second load should use cache (verified by consistent results)
        var secondLoad = await _catalogService.GetDeviceCatalogAsync("DingoPDM");
        secondLoad.Should().NotBeNull();
        secondLoad!.Id.Should().Be(firstLoad!.Id);

        // Test cache clearing when directory changes
        var newCatalogDirectory = Path.Combine(_testDirectory, "new-catalogs");
        _catalogService.SetCatalogDirectory(newCatalogDirectory);

        var afterDirectoryChange = await _catalogService.GetDeviceCatalogAsync("DingoPDM");
        afterDirectoryChange.Should().BeNull(); // Cache should be cleared
    }
}