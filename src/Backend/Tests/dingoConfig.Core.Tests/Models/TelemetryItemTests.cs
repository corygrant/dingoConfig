using dingoConfig.Core.Models;
using FluentAssertions;

namespace dingoConfig.Core.Tests.Models;

public class TelemetryItemTests
{
    [Fact]
    public void TelemetryItem_ShouldValidateRequiredProperties()
    {
        // Arrange
        var telemetryItem = new TelemetryItem();

        // Act
        var validationResult = telemetryItem.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Id"));
        validationResult.Errors.Should().Contain(e => e.Contains("Name"));
    }

    [Fact]
    public void TelemetryItem_ShouldValidateSuccessfully_WhenAllRequiredPropertiesProvided()
    {
        // Arrange
        var telemetryItem = new TelemetryItem
        {
            Id = "test_telemetry",
            Name = "Test Telemetry",
            Type = TelemetryType.Float,
            Unit = "V",
            CanId = 0x600,
            ByteOffset = 0,
            ByteLength = 2,
            Scale = 0.01
        };

        // Act
        var validationResult = telemetryItem.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
        validationResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TelemetryItem_ShouldFailValidation_WhenByteOffsetNegative()
    {
        // Arrange
        var telemetryItem = new TelemetryItem
        {
            Id = "test_telemetry",
            Name = "Test Telemetry",
            Type = TelemetryType.Float,
            ByteOffset = -1,
            ByteLength = 2
        };

        // Act
        var validationResult = telemetryItem.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("ByteOffset") && e.Contains("negative"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TelemetryItem_ShouldFailValidation_WhenByteLengthNotPositive(int byteLength)
    {
        // Arrange
        var telemetryItem = new TelemetryItem
        {
            Id = "test_telemetry",
            Name = "Test Telemetry",
            Type = TelemetryType.Float,
            ByteOffset = 0,
            ByteLength = byteLength
        };

        // Act
        var validationResult = telemetryItem.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("ByteLength") && e.Contains("positive"));
    }

    [Theory]
    [InlineData(5, 4)]  // offset 5 + length 4 = 9 > 8
    [InlineData(7, 2)]  // offset 7 + length 2 = 9 > 8
    [InlineData(8, 1)]  // offset 8 + length 1 = 9 > 8
    public void TelemetryItem_ShouldFailValidation_WhenByteOffsetPlusLengthExceedsCANMessageSize(int offset, int length)
    {
        // Arrange
        var telemetryItem = new TelemetryItem
        {
            Id = "test_telemetry",
            Name = "Test Telemetry",
            Type = TelemetryType.Float,
            ByteOffset = offset,
            ByteLength = length
        };

        // Act
        var validationResult = telemetryItem.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("cannot exceed 8"));
    }

    [Theory]
    [InlineData(0, 1)]  // offset 0 + length 1 = 1 <= 8
    [InlineData(3, 4)]  // offset 3 + length 4 = 7 <= 8
    [InlineData(7, 1)]  // offset 7 + length 1 = 8 <= 8
    public void TelemetryItem_ShouldValidateSuccessfully_WhenByteOffsetPlusLengthWithinCANMessageSize(int offset, int length)
    {
        // Arrange
        var telemetryItem = new TelemetryItem
        {
            Id = "test_telemetry",
            Name = "Test Telemetry",
            Type = TelemetryType.Float,
            ByteOffset = offset,
            ByteLength = length,
            Scale = 1.0  // Non-zero scale
        };

        // Act
        var validationResult = telemetryItem.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TelemetryItem_ShouldFailValidation_WhenScaleIsZero()
    {
        // Arrange
        var telemetryItem = new TelemetryItem
        {
            Id = "test_telemetry",
            Name = "Test Telemetry",
            Type = TelemetryType.Float,
            ByteOffset = 0,
            ByteLength = 2,
            Scale = 0.0
        };

        // Act
        var validationResult = telemetryItem.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Scale") && e.Contains("zero"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TelemetryItem_ShouldFailValidation_WhenIdIsNullOrWhitespace(string invalidId)
    {
        // Arrange
        var telemetryItem = new TelemetryItem
        {
            Id = invalidId,
            Name = "Valid Name",
            Type = TelemetryType.Float,
            ByteOffset = 0,
            ByteLength = 2,
            Scale = 1.0
        };

        // Act
        var validationResult = telemetryItem.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Id"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TelemetryItem_ShouldFailValidation_WhenNameIsNullOrWhitespace(string invalidName)
    {
        // Arrange
        var telemetryItem = new TelemetryItem
        {
            Id = "valid_id",
            Name = invalidName,
            Type = TelemetryType.Float,
            ByteOffset = 0,
            ByteLength = 2,
            Scale = 1.0
        };

        // Act
        var validationResult = telemetryItem.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Name"));
    }

    [Theory]
    [InlineData(TelemetryType.Boolean)]
    [InlineData(TelemetryType.Integer)]
    [InlineData(TelemetryType.Float)]
    [InlineData(TelemetryType.String)]
    public void TelemetryItem_ShouldAcceptAllTelemetryTypes(TelemetryType type)
    {
        // Arrange
        var telemetryItem = new TelemetryItem
        {
            Id = "test_telemetry",
            Name = "Test Telemetry",
            Type = type,
            ByteOffset = 0,
            ByteLength = 2,
            Scale = 1.0
        };

        // Act
        var validationResult = telemetryItem.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }
}