using dingoConfig.Core.Interfaces;
using dingoConfig.Core.Models;
using dingoConfig.Core.Services;
using dingoConfig.Core.Tests.TestData;
using FluentAssertions;
using Moq;

namespace dingoConfig.Core.Tests.Services;

public class ConfigurationServiceTests
{
    private readonly Mock<IFileStorageService> _mockFileStorageService;
    private readonly ConfigurationService _configurationService;

    public ConfigurationServiceTests()
    {
        _mockFileStorageService = new Mock<IFileStorageService>();
        _configurationService = new ConfigurationService(_mockFileStorageService.Object);
    }

    [Fact]
    public async Task LoadConfigurationAsync_ShouldReturnConfiguration_WhenFileExists()
    {
        // Arrange
        var configPath = "/test/config.json";
        var expectedConfig = DeviceConfigurationBuilder.CreateDefault();

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(configPath))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<DeviceConfiguration>(configPath))
            .ReturnsAsync(expectedConfig);

        // Act
        var result = await _configurationService.LoadConfigurationAsync(configPath);

        // Assert
        result.Should().NotBeNull();
        result!.DeviceId.Should().Be(expectedConfig.DeviceId);
        result.DeviceType.Should().Be(expectedConfig.DeviceType);
    }

    [Fact]
    public async Task LoadConfigurationAsync_ShouldReturnNull_WhenFileNotFound()
    {
        // Arrange
        var configPath = "/test/nonexistent.json";

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(configPath))
            .ReturnsAsync(false);

        // Act
        var result = await _configurationService.LoadConfigurationAsync(configPath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveConfigurationAsync_ShouldReturnTrue_WhenValidConfiguration()
    {
        // Arrange
        var configPath = "/test/config.json";
        var configuration = DeviceConfigurationBuilder.CreateDefault();

        _mockFileStorageService
            .Setup(x => x.CreateDirectoryAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.WriteJsonAsync(configuration, configPath))
            .ReturnsAsync(true);

        // Act
        var result = await _configurationService.SaveConfigurationAsync(configuration, configPath);

        // Assert
        result.Should().BeTrue();
        _mockFileStorageService.Verify(x => x.WriteJsonAsync(configuration, configPath), Times.Once);
    }

    [Fact]
    public async Task SaveConfigurationAsync_ShouldUpdateLastModified_WhenSaving()
    {
        // Arrange
        var configPath = "/test/config.json";
        var configuration = DeviceConfigurationBuilder.CreateDefault();
        var originalLastModified = configuration.LastModified;

        _mockFileStorageService
            .Setup(x => x.CreateDirectoryAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.WriteJsonAsync(It.IsAny<DeviceConfiguration>(), configPath))
            .ReturnsAsync(true);

        // Act
        await _configurationService.SaveConfigurationAsync(configuration, configPath);

        // Assert
        configuration.LastModified.Should().BeAfter(originalLastModified);
    }

    [Fact]
    public async Task LoadAllConfigurationsAsync_ShouldReturnMultipleConfigurations_WhenFilesExist()
    {
        // Arrange
        var configDirectory = "/test/configs";
        var configFiles = new List<string>
        {
            "/test/configs/config1.json",
            "/test/configs/config2.json"
        };

        var config1 = new DeviceConfigurationBuilder()
            .WithDeviceId("device1")
            .WithDefaultDingoPDMSettings()
            .Build();
        
        var config2 = new DeviceConfigurationBuilder()
            .WithDeviceId("device2")
            .WithDefaultDingoPDMSettings()
            .Build();

        _mockFileStorageService
            .Setup(x => x.DirectoryExistsAsync(configDirectory))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.GetFilesAsync(configDirectory, "*.json"))
            .ReturnsAsync(configFiles);

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(configFiles[0]))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(configFiles[1]))
            .ReturnsAsync(true);

        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<DeviceConfiguration>(configFiles[0]))
            .ReturnsAsync(config1);
        
        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<DeviceConfiguration>(configFiles[1]))
            .ReturnsAsync(config2);

        // Act
        var result = await _configurationService.LoadAllConfigurationsAsync(configDirectory);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.DeviceId == "device1");
        result.Should().Contain(c => c.DeviceId == "device2");
    }

    [Fact]
    public async Task DeleteConfigurationAsync_ShouldReturnTrue_WhenConfigurationExists()
    {
        // Arrange
        var configPath = "/test/config.json";

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(configPath))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.DeleteFileAsync(configPath))
            .ReturnsAsync(true);

        // Act
        var result = await _configurationService.DeleteConfigurationAsync(configPath);

        // Assert
        result.Should().BeTrue();
        _mockFileStorageService.Verify(x => x.DeleteFileAsync(configPath), Times.Once);
    }

    [Fact]
    public async Task DeleteConfigurationAsync_ShouldReturnFalse_WhenConfigurationDoesNotExist()
    {
        // Arrange
        var configPath = "/test/nonexistent.json";

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(configPath))
            .ReturnsAsync(false);

        // Act
        var result = await _configurationService.DeleteConfigurationAsync(configPath);

        // Assert
        result.Should().BeFalse();
        _mockFileStorageService.Verify(x => x.DeleteFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetConfigurationsByTypeAsync_ShouldReturnFilteredConfigurations()
    {
        // Arrange
        var deviceType = "DingoPDM";
        var configDirectory = "/test/configs";
        var configFiles = new List<string>
        {
            "/test/configs/config1.json",
            "/test/configs/config2.json"
        };

        var config1 = new DeviceConfigurationBuilder()
            .WithDeviceType("DingoPDM")
            .WithDeviceId("device1")
            .WithDefaultDingoPDMSettings()
            .Build();
        
        var config2 = new DeviceConfigurationBuilder()
            .WithDeviceType("CANBoard")
            .WithDeviceId("device2")
            .WithDefaultDingoPDMSettings()
            .Build();

        _configurationService.SetConfigurationDirectory(configDirectory);

        _mockFileStorageService
            .Setup(x => x.DirectoryExistsAsync(configDirectory))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.GetFilesAsync(configDirectory, "*.json"))
            .ReturnsAsync(configFiles);

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(configFiles[0]))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(configFiles[1]))
            .ReturnsAsync(true);

        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<DeviceConfiguration>(configFiles[0]))
            .ReturnsAsync(config1);
        
        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<DeviceConfiguration>(configFiles[1]))
            .ReturnsAsync(config2);

        // Act
        var result = await _configurationService.GetConfigurationsByTypeAsync(deviceType);

        // Assert
        result.Should().HaveCount(1);
        result.First().DeviceType.Should().Be("DingoPDM");
        result.First().DeviceId.Should().Be("device1");
    }

    [Fact]
    public async Task ValidateConfigurationAsync_ShouldReturnTrue_WhenConfigurationIsValid()
    {
        // Arrange
        var configuration = DeviceConfigurationBuilder.CreateDefault();
        var deviceCatalog = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM,
            Parameters = new List<Parameter>
            {
                new Parameter { Id = TestConstants.ParameterNames.CanNodeId, Type = ParameterType.Integer, Min = 1, Max = 16 }
            }
        };

        // Act
        var result = await _configurationService.ValidateConfigurationAsync(configuration, deviceCatalog);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateConfigurationAsync_ShouldReturnFalse_WhenParameterValueOutOfRange()
    {
        // Arrange
        var configuration = new DeviceConfigurationBuilder()
            .WithSetting(TestConstants.ParameterNames.CanNodeId, 50) // Outside valid range
            .WithDefaultDingoPDMSettings()
            .Build();
        
        var deviceCatalog = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM,
            Parameters = new List<Parameter>
            {
                new Parameter { Id = TestConstants.ParameterNames.CanNodeId, Type = ParameterType.Integer, Min = 1, Max = 16 }
            }
        };

        // Act
        var result = await _configurationService.ValidateConfigurationAsync(configuration, deviceCatalog);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetConfigurationDirectory_ShouldReturnDefaultDirectory_WhenNotSet()
    {
        // Act
        var result = _configurationService.GetConfigurationDirectory();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("configs");
    }

    [Fact]
    public void SetConfigurationDirectory_ShouldUpdateDirectory()
    {
        // Arrange
        var newDirectory = "/custom/configs";

        // Act
        _configurationService.SetConfigurationDirectory(newDirectory);

        // Assert
        var result = _configurationService.GetConfigurationDirectory();
        result.Should().Be(newDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SetConfigurationDirectory_ShouldThrowException_WhenDirectoryIsInvalid(string invalidDirectory)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => _configurationService.SetConfigurationDirectory(invalidDirectory));
        
        exception.Message.Should().Contain("Directory path cannot be null or empty");
    }
}