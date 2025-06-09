using dingoConfig.Core.Models;
using FluentAssertions;

namespace dingoConfig.Core.Tests.Models;

public class CommunicationSettingsTests
{
    [Fact]
    public void CommunicationSettings_ShouldValidateSuccessfully_WithDefaultValues()
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = CommunicationProtocol.CAN,
            BaudRate = 500000,
            BaseId = 0x600,
            MaxNodes = 16
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
        validationResult.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CommunicationSettings_ShouldFailValidation_WhenBaudRateNotPositive(int baudRate)
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = CommunicationProtocol.CAN,
            BaudRate = baudRate
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("BaudRate") && e.Contains("positive"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void CommunicationSettings_ShouldFailValidation_WhenMaxNodesNotPositive(int maxNodes)
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = CommunicationProtocol.CAN,
            MaxNodes = maxNodes
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("MaxNodes") && e.Contains("positive"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void CommunicationSettings_ShouldFailValidation_WhenTimeoutNotPositive(int timeout)
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = CommunicationProtocol.CAN,
            TimeoutMs = timeout
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("TimeoutMs") && e.Contains("positive"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void CommunicationSettings_ShouldFailValidation_WhenRetryCountNegative(int retryCount)
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = CommunicationProtocol.CAN,
            RetryCount = retryCount
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("RetryCount") && e.Contains("negative"));
    }

    [Theory]
    [InlineData(125000)]
    [InlineData(250000)]
    [InlineData(500000)]
    [InlineData(1000000)]
    public void CommunicationSettings_ShouldValidateSuccessfully_WithValidCANBaudRates(int baudRate)
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = CommunicationProtocol.CAN,
            BaudRate = baudRate,
            BaseId = 0x600
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(100000)]
    [InlineData(300000)]
    [InlineData(750000)]
    [InlineData(2000000)]
    public void CommunicationSettings_ShouldFailValidation_WithInvalidCANBaudRates(int baudRate)
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = CommunicationProtocol.CAN,
            BaudRate = baudRate
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("BaudRate") && e.Contains("125000, 250000, 500000, 1000000"));
    }

    [Theory]
    [InlineData(0x000u)]     // Minimum standard CAN ID
    [InlineData(0x123u)]     // Mid-range standard CAN ID
    [InlineData(0x7FFu)]     // Maximum standard CAN ID
    public void CommunicationSettings_ShouldValidateSuccessfully_WithValidStandardCANIds(uint baseId)
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = CommunicationProtocol.CAN,
            BaudRate = 500000,
            BaseId = baseId
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0x800u)]         // First extended CAN ID
    [InlineData(0x1234567u)]     // Mid-range extended CAN ID
    [InlineData(0x1FFFFFFFu)]    // Maximum extended CAN ID
    public void CommunicationSettings_ShouldValidateSuccessfully_WithValidExtendedCANIds(uint baseId)
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = CommunicationProtocol.CAN,
            BaudRate = 500000,
            BaseId = baseId
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0x20000000u)]    // Above maximum extended CAN ID
    [InlineData(0xFFFFFFFFu)]    // Maximum uint value
    public void CommunicationSettings_ShouldFailValidation_WithInvalidCANIds(uint baseId)
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = CommunicationProtocol.CAN,
            BaudRate = 500000,
            BaseId = baseId
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("BaseId") && e.Contains("range"));
    }

    [Theory]
    [InlineData(CommunicationProtocol.Serial, 115200)]
    [InlineData(CommunicationProtocol.TCP, 8080)]
    [InlineData(CommunicationProtocol.UDP, 8081)]
    public void CommunicationSettings_ShouldValidateSuccessfully_WithNonCANProtocols(CommunicationProtocol protocol, int baudRate)
    {
        // Arrange
        var settings = new CommunicationSettings
        {
            Protocol = protocol,
            BaudRate = baudRate,
            BaseId = 0x123  // Any value should be fine for non-CAN protocols
        };

        // Act
        var validationResult = settings.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CommunicationSettings_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var settings = new CommunicationSettings();

        // Assert
        settings.BaudRate.Should().Be(500000);
        settings.MaxNodes.Should().Be(16);
        settings.TimeoutMs.Should().Be(1000);
        settings.RetryCount.Should().Be(3);
        settings.Protocol.Should().Be(CommunicationProtocol.Unknown);
    }
}