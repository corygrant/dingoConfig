using dingoConfig.Core.Interfaces;
using dingoConfig.Core.Models;
using dingoConfig.Core.Services;
using dingoConfig.Core.Tests.TestData;
using FluentAssertions;
using Moq;

namespace dingoConfig.Core.Tests.Services;

public class CatalogServiceTests
{
    private readonly Mock<IFileStorageService> _mockFileStorageService;
    private readonly CatalogService _catalogService;

    public CatalogServiceTests()
    {
        _mockFileStorageService = new Mock<IFileStorageService>();
        _catalogService = new CatalogService(_mockFileStorageService.Object);
    }

    [Fact]
    public async Task LoadCatalogAsync_ShouldReturnValidCatalog_WhenFileExists()
    {
        // Arrange
        var catalogPath = "/test/catalog.json";
        var expectedDevice = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM
        };

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(catalogPath))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<Device>(catalogPath))
            .ReturnsAsync(expectedDevice);

        // Act
        var result = await _catalogService.LoadCatalogAsync(catalogPath);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("test-device");
        result.Name.Should().Be("Test Device");
        result.Type.Should().Be(DeviceType.DingoPDM);
    }

    [Fact]
    public async Task LoadCatalogAsync_ShouldThrowException_WhenFileNotFound()
    {
        // Arrange
        var catalogPath = "/test/nonexistent.json";

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(catalogPath))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => _catalogService.LoadCatalogAsync(catalogPath));
        
        exception.Message.Should().Contain(catalogPath);
    }

    [Fact]
    public async Task LoadCatalogAsync_ShouldThrowException_WhenJsonInvalid()
    {
        // Arrange
        var catalogPath = "/test/invalid.json";

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(catalogPath))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<Device>(catalogPath))
            .ReturnsAsync((Device?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => _catalogService.LoadCatalogAsync(catalogPath));
        
        exception.Message.Should().Contain("Failed to deserialize");
    }

    [Fact]
    public async Task LoadAllCatalogsAsync_ShouldReturnMultipleCatalogs_WhenFilesExist()
    {
        // Arrange
        var catalogDirectory = "/test/catalogs";
        var catalogFiles = new List<string>
        {
            "/test/catalogs/device1.json",
            "/test/catalogs/device2.json"
        };

        var device1 = new Device { Id = "device1", Name = "Device 1", Type = DeviceType.DingoPDM };
        var device2 = new Device { Id = "device2", Name = "Device 2", Type = DeviceType.CANBoard };

        _mockFileStorageService
            .Setup(x => x.DirectoryExistsAsync(catalogDirectory))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.GetFilesAsync(catalogDirectory, "*.json"))
            .ReturnsAsync(catalogFiles);

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(catalogFiles[0]))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(catalogFiles[1]))
            .ReturnsAsync(true);

        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<Device>(catalogFiles[0]))
            .ReturnsAsync(device1);
        
        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<Device>(catalogFiles[1]))
            .ReturnsAsync(device2);

        // Act
        var result = await _catalogService.LoadAllCatalogsAsync(catalogDirectory);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(d => d.Id == "device1");
        result.Should().Contain(d => d.Id == "device2");
    }

    [Fact]
    public async Task ReloadCatalogsAsync_ShouldUpdateCacheCorrectly()
    {
        // Arrange
        var catalogDirectory = "/test/catalogs";
        var catalogFiles = new List<string> { "/test/catalogs/device1.json" };
        var device = new Device { Id = "device1", Name = "Device 1", Type = DeviceType.DingoPDM };

        _catalogService.SetCatalogDirectory(catalogDirectory);

        _mockFileStorageService
            .Setup(x => x.DirectoryExistsAsync(catalogDirectory))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.GetFilesAsync(catalogDirectory, "*.json"))
            .ReturnsAsync(catalogFiles);

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(catalogFiles[0]))
            .ReturnsAsync(true);

        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<Device>(catalogFiles[0]))
            .ReturnsAsync(device);

        // Act
        await _catalogService.ReloadCatalogsAsync();

        // Assert
        var availableTypes = await _catalogService.GetAvailableDeviceTypesAsync();
        availableTypes.Should().Contain("DingoPDM");
    }

    [Fact]
    public async Task GetDeviceCatalogAsync_ShouldReturnCachedDevice_WhenExists()
    {
        // Arrange
        var deviceType = "DingoPDM";
        var device = new Device { Id = "test", Name = "Test", Type = DeviceType.DingoPDM };
        
        // Setup to load catalog into cache
        var catalogDirectory = "/test/catalogs";
        var catalogFiles = new List<string> { "/test/catalogs/device1.json" };
        
        _catalogService.SetCatalogDirectory(catalogDirectory);

        _mockFileStorageService
            .Setup(x => x.DirectoryExistsAsync(catalogDirectory))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.GetFilesAsync(catalogDirectory, "*.json"))
            .ReturnsAsync(catalogFiles);

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(catalogFiles[0]))
            .ReturnsAsync(true);

        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<Device>(catalogFiles[0]))
            .ReturnsAsync(device);

        await _catalogService.ReloadCatalogsAsync();

        // Act
        var result = await _catalogService.GetDeviceCatalogAsync(deviceType);

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be(DeviceType.DingoPDM);
    }

    [Fact]
    public async Task GetDeviceCatalogAsync_ShouldReturnNull_WhenDeviceTypeNotFound()
    {
        // Arrange
        var deviceType = "NonExistentType";

        // Act
        var result = await _catalogService.GetDeviceCatalogAsync(deviceType);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateCatalogAsync_ShouldReturnTrue_WhenCatalogIsValid()
    {
        // Arrange
        var catalogPath = "/test/valid-catalog.json";
        var validDevice = new Device
        {
            Id = "valid-device",
            Name = "Valid Device",
            Type = DeviceType.DingoPDM
        };

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(catalogPath))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<Device>(catalogPath))
            .ReturnsAsync(validDevice);

        // Act
        var result = await _catalogService.ValidateCatalogAsync(catalogPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateCatalogAsync_ShouldReturnFalse_WhenCatalogIsInvalid()
    {
        // Arrange
        var catalogPath = "/test/invalid-catalog.json";
        var invalidDevice = new Device(); // Missing required properties

        _mockFileStorageService
            .Setup(x => x.FileExistsAsync(catalogPath))
            .ReturnsAsync(true);
        
        _mockFileStorageService
            .Setup(x => x.ReadJsonAsync<Device>(catalogPath))
            .ReturnsAsync(invalidDevice);

        // Act
        var result = await _catalogService.ValidateCatalogAsync(catalogPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetCatalogDirectory_ShouldReturnDefaultDirectory_WhenNotSet()
    {
        // Act
        var result = _catalogService.GetCatalogDirectory();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("catalogs");
    }

    [Fact]
    public void SetCatalogDirectory_ShouldUpdateDirectory()
    {
        // Arrange
        var newDirectory = "/custom/catalogs";

        // Act
        _catalogService.SetCatalogDirectory(newDirectory);

        // Assert
        var result = _catalogService.GetCatalogDirectory();
        result.Should().Be(newDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SetCatalogDirectory_ShouldThrowException_WhenDirectoryIsInvalid(string invalidDirectory)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => _catalogService.SetCatalogDirectory(invalidDirectory));
        
        exception.Message.Should().Contain("Directory path cannot be null or empty");
    }
}