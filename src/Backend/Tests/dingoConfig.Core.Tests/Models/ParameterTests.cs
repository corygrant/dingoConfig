using dingoConfig.Core.Models;
using FluentAssertions;

namespace dingoConfig.Core.Tests.Models;

public class ParameterTests
{
    [Fact]
    public void Parameter_ShouldValidateRequiredProperties()
    {
        // Arrange
        var parameter = new Parameter();

        // Act
        var validationResult = parameter.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Id"));
        validationResult.Errors.Should().Contain(e => e.Contains("Name"));
    }

    [Fact]
    public void Parameter_ShouldValidateSuccessfully_WhenAllRequiredPropertiesProvided()
    {
        // Arrange
        var parameter = new Parameter
        {
            Id = "test_param",
            Name = "Test Parameter",
            Type = ParameterType.Boolean,
            DefaultValue = true
        };

        // Act
        var validationResult = parameter.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
        validationResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parameter_ShouldFailValidation_WhenEnumTypeHasNoOptions()
    {
        // Arrange
        var parameter = new Parameter
        {
            Id = "enum_param",
            Name = "Enum Parameter",
            Type = ParameterType.Enum,
            Options = new List<string>()
        };

        // Act
        var validationResult = parameter.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Enum") && e.Contains("options"));
    }

    [Fact]
    public void Parameter_ShouldFailValidation_WhenMinGreaterThanMax()
    {
        // Arrange
        var parameter = new Parameter
        {
            Id = "range_param",
            Name = "Range Parameter",
            Type = ParameterType.Float,
            Min = 10.0,
            Max = 5.0
        };

        // Act
        var validationResult = parameter.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Min") && e.Contains("Max"));
    }

    [Theory]
    [InlineData(ParameterType.Boolean, true)]
    [InlineData(ParameterType.Boolean, false)]
    [InlineData(ParameterType.Integer, 42)]
    [InlineData(ParameterType.Integer, 0)]
    [InlineData(ParameterType.Float, 3.14)]
    [InlineData(ParameterType.Float, 0.0)]
    [InlineData(ParameterType.String, "test")]
    [InlineData(ParameterType.String, "")]
    public void Parameter_ShouldValidateDefaultValue_WhenTypeMatches(ParameterType type, object defaultValue)
    {
        // Arrange
        var parameter = new Parameter
        {
            Id = "test_param",
            Name = "Test Parameter",
            Type = type,
            DefaultValue = defaultValue
        };

        // Act
        var validationResult = parameter.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(ParameterType.Boolean, "not_a_boolean")]
    [InlineData(ParameterType.Integer, "not_an_integer")]
    [InlineData(ParameterType.Float, "not_a_float")]
    [InlineData(ParameterType.String, 123)]
    public void Parameter_ShouldFailValidation_WhenDefaultValueTypeMismatch(ParameterType type, object defaultValue)
    {
        // Arrange
        var parameter = new Parameter
        {
            Id = "test_param",
            Name = "Test Parameter",
            Type = type,
            DefaultValue = defaultValue
        };

        // Act
        var validationResult = parameter.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Default value"));
    }

    [Fact]
    public void Parameter_ShouldValidateEnumDefaultValue_WhenInOptions()
    {
        // Arrange
        var parameter = new Parameter
        {
            Id = "enum_param",
            Name = "Enum Parameter",
            Type = ParameterType.Enum,
            Options = new List<string> { "option1", "option2", "option3" },
            DefaultValue = "option2"
        };

        // Act
        var validationResult = parameter.Validate();

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parameter_ShouldFailValidation_WhenEnumDefaultValueNotInOptions()
    {
        // Arrange
        var parameter = new Parameter
        {
            Id = "enum_param",
            Name = "Enum Parameter",
            Type = ParameterType.Enum,
            Options = new List<string> { "option1", "option2", "option3" },
            DefaultValue = "invalid_option"
        };

        // Act
        var validationResult = parameter.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("options"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parameter_ShouldFailValidation_WhenIdIsNullOrWhitespace(string invalidId)
    {
        // Arrange
        var parameter = new Parameter
        {
            Id = invalidId,
            Name = "Valid Name",
            Type = ParameterType.Boolean
        };

        // Act
        var validationResult = parameter.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Id"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parameter_ShouldFailValidation_WhenNameIsNullOrWhitespace(string invalidName)
    {
        // Arrange
        var parameter = new Parameter
        {
            Id = "valid_id",
            Name = invalidName,
            Type = ParameterType.Boolean
        };

        // Act
        var validationResult = parameter.Validate();

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.Contains("Name"));
    }
}