using ClassifiedAds.CrossCuttingConcerns.Exceptions;

namespace ClassifiedAds.UnitTests.CrossCuttingConcerns;

/// <summary>
/// Unit tests for NotFoundException.
/// Tests exception creation and message handling.
/// </summary>
public class NotFoundExceptionTests
{
    [Fact]
    public void Constructor_ShouldCreateException_WithDefaultMessage()
    {
        // Arrange & Act
        var exception = new NotFoundException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeNullOrEmpty(
            "Default constructor should create exception with default message");
    }

    [Fact]
    public void Constructor_ShouldCreateException_WithCustomMessage()
    {
        // Arrange
        const string expectedMessage = "Product with Id 12345 not found";

        // Act
        var exception = new NotFoundException(expectedMessage);

        // Assert
        exception.Message.Should().Be(expectedMessage,
            "Exception should contain the exact message provided in constructor");
    }

    [Theory]
    [InlineData("User not found")]
    [InlineData("Resource with ID abc-123 does not exist")]
    [InlineData("")]
    public void Constructor_ShouldAcceptVariousMessages(string message)
    {
        // Arrange & Act
        var exception = new NotFoundException(message);

        // Assert
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void Exception_ShouldBeThrowable_AndCatchableAsException()
    {
        // Arrange
        const string errorMessage = "Entity not found";

        // Act
        Action act = () => throw new NotFoundException(errorMessage);

        // Assert
        act.Should().Throw<Exception>()
            .Which.Should().BeOfType<NotFoundException>();
    }
}
