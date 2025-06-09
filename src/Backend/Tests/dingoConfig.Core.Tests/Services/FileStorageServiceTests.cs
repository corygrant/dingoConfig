using dingoConfig.Core.Interfaces;
using dingoConfig.Core.Models;
using dingoConfig.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace dingoConfig.Core.Tests.Services;

public class FileStorageServiceTests : IDisposable
{
    private readonly Mock<ILogger<FileStorageService>> _mockLogger;
    private readonly FileStorageService _fileStorageService;
    private readonly string _testDirectory;

    public FileStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<FileStorageService>>();
        _fileStorageService = new FileStorageService(_mockLogger.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), "dingoConfig_tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task ReadJsonAsync_ShouldReturnDeserializedObject_WhenFileExists()
    {
        // Arrange
        var testDevice = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM
        };
        
        var filePath = Path.Combine(_testDirectory, "test-device.json");
        var json = JsonSerializer.Serialize(testDevice);
        await File.WriteAllTextAsync(filePath, json);

        // Act
        var result = await _fileStorageService.ReadJsonAsync<Device>(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("test-device");
        result.Name.Should().Be("Test Device");
        result.Type.Should().Be(DeviceType.DingoPDM);
    }

    [Fact]
    public async Task ReadJsonAsync_ShouldReturnNull_WhenFileNotFound()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.json");

        // Act
        var result = await _fileStorageService.ReadJsonAsync<Device>(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadJsonAsync_ShouldReturnNull_WhenJsonInvalid()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "invalid.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json }");

        // Act
        var result = await _fileStorageService.ReadJsonAsync<Device>(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task WriteJsonAsync_ShouldCreateFileWithCorrectContent_WhenValidObject()
    {
        // Arrange
        var testDevice = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM
        };
        
        var filePath = Path.Combine(_testDirectory, "output.json");

        // Act
        var result = await _fileStorageService.WriteJsonAsync(testDevice, filePath);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        
        var fileContent = await File.ReadAllTextAsync(filePath);
        var deserializedDevice = JsonSerializer.Deserialize<Device>(fileContent);
        deserializedDevice.Should().NotBeNull();
        deserializedDevice!.Id.Should().Be("test-device");
    }

    [Fact]
    public async Task WriteJsonAsync_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        var testDevice = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM
        };
        
        var subDirectory = Path.Combine(_testDirectory, "subdir");
        var filePath = Path.Combine(subDirectory, "output.json");

        // Act
        var result = await _fileStorageService.WriteJsonAsync(testDevice, filePath);

        // Assert
        result.Should().BeTrue();
        Directory.Exists(subDirectory).Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task WriteJsonAsync_ShouldReturnFalse_WhenInvalidPath()
    {
        // Arrange
        var testDevice = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM
        };
        
        var invalidPath = string.Join("", Enumerable.Repeat("x", 300)); // Path too long

        // Act
        var result = await _fileStorageService.WriteJsonAsync(testDevice, invalidPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WriteJsonAsync_ShouldUseAtomicOperation_WhenWritingFile()
    {
        // Arrange
        var testDevice = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM
        };
        
        var filePath = Path.Combine(_testDirectory, "atomic-test.json");

        // Act
        var result = await _fileStorageService.WriteJsonAsync(testDevice, filePath);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        
        // Verify no temporary files remain
        var tempFiles = Directory.GetFiles(_testDirectory, "*.tmp");
        tempFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldReturnTrue_WhenFileExists()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "to-delete.txt");
        await File.WriteAllTextAsync(filePath, "test content");

        // Act
        var result = await _fileStorageService.DeleteFileAsync(filePath);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldReturnFalse_WhenFileNotFound()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var result = await _fileStorageService.DeleteFileAsync(filePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task FileExistsAsync_ShouldReturnTrue_WhenFileExists()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "exists.txt");
        await File.WriteAllTextAsync(filePath, "test content");

        // Act
        var result = await _fileStorageService.FileExistsAsync(filePath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task FileExistsAsync_ShouldReturnFalse_WhenFileNotFound()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "notfound.txt");

        // Act
        var result = await _fileStorageService.FileExistsAsync(filePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetFilesAsync_ShouldReturnMatchingFiles_WhenFilesExist()
    {
        // Arrange
        var jsonFile1 = Path.Combine(_testDirectory, "file1.json");
        var jsonFile2 = Path.Combine(_testDirectory, "file2.json");
        var txtFile = Path.Combine(_testDirectory, "file3.txt");
        
        await File.WriteAllTextAsync(jsonFile1, "{}");
        await File.WriteAllTextAsync(jsonFile2, "{}");
        await File.WriteAllTextAsync(txtFile, "text");

        // Act
        var result = await _fileStorageService.GetFilesAsync(_testDirectory, "*.json");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(jsonFile1);
        result.Should().Contain(jsonFile2);
        result.Should().NotContain(txtFile);
    }

    [Fact]
    public async Task GetFilesAsync_ShouldReturnEmpty_WhenDirectoryNotFound()
    {
        // Arrange
        var nonExistentDirectory = Path.Combine(_testDirectory, "nonexistent");

        // Act
        var result = await _fileStorageService.GetFilesAsync(nonExistentDirectory, "*.json");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDirectoryAsync_ShouldReturnTrue_WhenDirectoryCreated()
    {
        // Arrange
        var newDirectory = Path.Combine(_testDirectory, "new-directory");

        // Act
        var result = await _fileStorageService.CreateDirectoryAsync(newDirectory);

        // Assert
        result.Should().BeTrue();
        Directory.Exists(newDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task CreateDirectoryAsync_ShouldReturnTrue_WhenDirectoryAlreadyExists()
    {
        // Arrange
        var existingDirectory = Path.Combine(_testDirectory, "existing");
        Directory.CreateDirectory(existingDirectory);

        // Act
        var result = await _fileStorageService.CreateDirectoryAsync(existingDirectory);

        // Assert
        result.Should().BeTrue();
        Directory.Exists(existingDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task DirectoryExistsAsync_ShouldReturnTrue_WhenDirectoryExists()
    {
        // Arrange
        var existingDirectory = Path.Combine(_testDirectory, "existing");
        Directory.CreateDirectory(existingDirectory);

        // Act
        var result = await _fileStorageService.DirectoryExistsAsync(existingDirectory);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DirectoryExistsAsync_ShouldReturnFalse_WhenDirectoryNotFound()
    {
        // Arrange
        var nonExistentDirectory = Path.Combine(_testDirectory, "nonexistent");

        // Act
        var result = await _fileStorageService.DirectoryExistsAsync(nonExistentDirectory);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReadTextAsync_ShouldReturnContent_WhenFileExists()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "text-file.txt");
        var content = "This is test content\nWith multiple lines";
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = await _fileStorageService.ReadTextAsync(filePath);

        // Assert
        result.Should().Be(content);
    }

    [Fact]
    public async Task ReadTextAsync_ShouldThrowException_WhenFileNotFound()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _fileStorageService.ReadTextAsync(filePath));
    }

    [Fact]
    public async Task WriteTextAsync_ShouldCreateFileWithContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "output.txt");
        var content = "This is test content\nWith multiple lines";

        // Act
        var result = await _fileStorageService.WriteTextAsync(content, filePath);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        
        var fileContent = await File.ReadAllTextAsync(filePath);
        fileContent.Should().Be(content);
    }

    [Fact]
    public async Task WriteTextAsync_ShouldReturnFalse_WhenInvalidPath()
    {
        // Arrange
        var invalidPath = string.Join("", Enumerable.Repeat("x", 300)); // Path too long
        var content = "test content";

        // Act
        var result = await _fileStorageService.WriteTextAsync(content, invalidPath);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ReadJsonAsync_ShouldReturnNull_WhenPathIsInvalid(string invalidPath)
    {
        // Act
        var result = await _fileStorageService.ReadJsonAsync<Device>(invalidPath);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task WriteJsonAsync_ShouldReturnFalse_WhenPathIsInvalid(string invalidPath)
    {
        // Arrange
        var testDevice = new Device
        {
            Id = "test-device",
            Name = "Test Device",
            Type = DeviceType.DingoPDM
        };

        // Act
        var result = await _fileStorageService.WriteJsonAsync(testDevice, invalidPath);

        // Assert
        result.Should().BeFalse();
    }
}