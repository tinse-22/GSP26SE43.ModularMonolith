using ClassifiedAds.Domain.Infrastructure.ResultPattern;

namespace ClassifiedAds.UnitTests.Domain.ResultPattern;

/// <summary>
/// Unit tests for the Error record type.
/// Tests error creation, factory methods, and properties.
/// </summary>
public class ErrorTests
{
    [Fact]
    public void Create_ShouldCreateErrorWithCodeAndMessage()
    {
        // Arrange
        const string code = "Test.Error";
        const string message = "Test error message";

        // Act
        var error = Error.Create(code, message);

        // Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
        error.Metadata.Should().BeNull();
    }

    [Fact]
    public void Create_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        const string code = "Test.Error";
        const string message = "Test error message";
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var error = Error.Create(code, message, metadata);

        // Assert
        error.Metadata.Should().NotBeNull();
        error.Metadata!["key"].Should().Be("value");
    }

    [Fact]
    public void Validation_ShouldCreateValidationErrorWithFieldMetadata()
    {
        // Arrange
        const string field = "Email";
        const string message = "Email is required";

        // Act
        var error = Error.Validation(field, message);

        // Assert
        error.Code.Should().Be("Validation.Email");
        error.Message.Should().Be(message);
        error.Metadata.Should().NotBeNull();
        error.Metadata!["field"].Should().Be(field);
    }

    [Fact]
    public void NotFound_ShouldCreateNotFoundErrorWithEntityAndId()
    {
        // Arrange
        const string entity = "Product";
        var id = Guid.NewGuid();

        // Act
        var error = Error.NotFound(entity, id);

        // Assert
        error.Code.Should().Be("Product.NotFound");
        error.Message.Should().Contain(entity);
        error.Message.Should().Contain(id.ToString());
        error.Metadata!["entity"].Should().Be(entity);
        error.Metadata!["id"].Should().Be(id);
    }

    [Fact]
    public void Conflict_ShouldCreateConflictError()
    {
        // Arrange
        const string entity = "Product";
        const string message = "Product with same name already exists";

        // Act
        var error = Error.Conflict(entity, message);

        // Assert
        error.Code.Should().Be("Product.Conflict");
        error.Message.Should().Be(message);
        error.Metadata!["entity"].Should().Be(entity);
    }

    [Fact]
    public void Unauthorized_ShouldCreateUnauthorizedError()
    {
        // Act
        var error = Error.Unauthorized();

        // Assert
        error.Code.Should().Be("Auth.Unauthorized");
        error.Message.Should().Be("Authentication is required.");
    }

    [Fact]
    public void Unauthorized_WithCustomMessage_ShouldUseCustomMessage()
    {
        // Arrange
        const string customMessage = "Token expired";

        // Act
        var error = Error.Unauthorized(customMessage);

        // Assert
        error.Code.Should().Be("Auth.Unauthorized");
        error.Message.Should().Be(customMessage);
    }

    [Fact]
    public void Forbidden_ShouldCreateForbiddenError()
    {
        // Act
        var error = Error.Forbidden();

        // Assert
        error.Code.Should().Be("Auth.Forbidden");
        error.Message.Should().Be("You do not have permission to perform this action.");
    }

    [Fact]
    public void Internal_ShouldCreateInternalServerError()
    {
        // Act
        var error = Error.Internal();

        // Assert
        error.Code.Should().Be("Server.InternalError");
        error.Message.Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var error = Error.Create("Test.Code", "Test message");

        // Act
        var result = error.ToString();

        // Assert
        result.Should().Be("[Test.Code] Test message");
    }

    [Fact]
    public void Create_WithNullCode_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => Error.Create(null!, "message");

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullMessage_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => Error.Create("code", null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }
}
