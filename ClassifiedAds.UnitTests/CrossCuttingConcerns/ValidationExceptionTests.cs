using ClassifiedAds.CrossCuttingConcerns.Exceptions;

namespace ClassifiedAds.UnitTests.CrossCuttingConcerns;

/// <summary>
/// Unit tests for ValidationException guard methods.
/// Tests the Requires helper method used throughout the codebase.
/// </summary>
public class ValidationExceptionTests
{
    [Fact]
    public void Requires_ShouldNotThrow_WhenConditionIsTrue()
    {
        // Arrange
        const bool condition = true;
        const string errorMessage = "This error should not be thrown";

        // Act
        var action = () => ValidationException.Requires(condition, errorMessage);

        // Assert
        action.Should().NotThrow<ValidationException>(
            "Requires should not throw when condition is true");
    }

    [Fact]
    public void Requires_ShouldThrowValidationException_WhenConditionIsFalse()
    {
        // Arrange
        const bool condition = false;
        const string expectedMessage = "Validation failed: expected condition was not met";

        // Act
        var action = () => ValidationException.Requires(condition, expectedMessage);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage(expectedMessage,
                "Requires should throw ValidationException with exact message when condition is false");
    }

    [Fact]
    public void Constructor_ShouldCreateException_WithProvidedMessage()
    {
        // Arrange
        const string expectedMessage = "Test validation error message";

        // Act
        var exception = new ValidationException(expectedMessage);

        // Assert
        exception.Message.Should().Be(expectedMessage);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldCreateException_WithMessageAndInnerException()
    {
        // Arrange
        const string expectedMessage = "Outer validation error";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new ValidationException(expectedMessage, innerException);

        // Assert
        exception.Message.Should().Be(expectedMessage);
        exception.InnerException.Should().BeSameAs(innerException);
        exception.InnerException!.Message.Should().Be("Inner error");
    }
}
