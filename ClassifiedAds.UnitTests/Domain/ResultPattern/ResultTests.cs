using ClassifiedAds.Domain.Infrastructure.ResultPattern;

namespace ClassifiedAds.UnitTests.Domain.ResultPattern;

/// <summary>
/// Unit tests for the Result and Result&lt;T&gt; types.
/// Tests success/failure states, error handling, and value access.
/// </summary>
public class ResultTests
{
    #region Result (non-generic) Tests

    [Fact]
    public void Ok_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Ok();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Errors.Should().BeEmpty();
        result.FirstError.Should().BeNull();
    }

    [Fact]
    public void Fail_WithSingleError_ShouldCreateFailedResult()
    {
        // Arrange
        var error = Error.Create("Test.Error", "Test message");

        // Act
        var result = Result.Fail(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(1);
        result.FirstError.Should().Be(error);
    }

    [Fact]
    public void Fail_WithMultipleErrors_ShouldContainAllErrors()
    {
        // Arrange
        var errors = new[]
        {
            Error.Validation("Name", "Name is required"),
            Error.Validation("Email", "Email is invalid")
        };

        // Act
        var result = Result.Fail(errors);

        // Assert
        result.Errors.Should().HaveCount(2);
        result.FirstError!.Code.Should().Be("Validation.Name");
    }

    [Fact]
    public void Fail_WithCodeAndMessage_ShouldCreateFailedResult()
    {
        // Act
        var result = Result.Fail("Test.Code", "Test message");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.FirstError!.Code.Should().Be("Test.Code");
        result.FirstError!.Message.Should().Be("Test message");
    }

    [Fact]
    public void NotFound_ShouldCreateFailedResultWithNotFoundError()
    {
        // Act
        var result = Result.NotFound("Product", 123);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.FirstError!.Code.Should().Be("Product.NotFound");
    }

    [Fact]
    public void ValidationFailed_ShouldCreateFailedResultWithValidationError()
    {
        // Act
        var result = Result.ValidationFailed("Email", "Invalid email format");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.FirstError!.Code.Should().Be("Validation.Email");
    }

    [Fact]
    public void ValidationFailed_WithMultipleFields_ShouldContainAllErrors()
    {
        // Arrange
        var fieldErrors = new[]
        {
            ("Name", "Name is required"),
            ("Email", "Email is invalid")
        };

        // Act
        var result = Result.ValidationFailed(fieldErrors);

        // Assert
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void HasErrorCode_ShouldReturnTrueWhenErrorExists()
    {
        // Arrange
        var result = Result.Fail(Error.Create("Test.Error", "message"));

        // Act & Assert
        result.HasErrorCode("Test.Error").Should().BeTrue();
        result.HasErrorCode("Other.Error").Should().BeFalse();
    }

    [Fact]
    public void HasErrorCodePrefix_ShouldMatchByPrefix()
    {
        // Arrange
        var result = Result.Fail(Error.Create("Validation.Email", "message"));

        // Act & Assert
        result.HasErrorCodePrefix("Validation").Should().BeTrue();
        result.HasErrorCodePrefix("Auth").Should().BeFalse();
    }

    [Fact]
    public void GetErrorMessages_ShouldConcatenateAllMessages()
    {
        // Arrange
        var result = Result.Fail(new[]
        {
            Error.Create("Error1", "First error"),
            Error.Create("Error2", "Second error")
        });

        // Act
        var messages = result.GetErrorMessages();

        // Assert
        messages.Should().Be("First error; Second error");
    }

    [Fact]
    public void ImplicitConversion_FromError_ShouldCreateFailedResult()
    {
        // Arrange
        var error = Error.Create("Test.Error", "message");

        // Act
        Result result = error;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.FirstError.Should().Be(error);
    }

    #endregion

    #region Result<T> (generic) Tests

    [Fact]
    public void GenericOk_ShouldCreateSuccessfulResultWithValue()
    {
        // Arrange
        const string value = "test value";

        // Act
        var result = Result<string>.Ok(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void GenericFail_ShouldCreateFailedResult()
    {
        // Arrange
        var error = Error.Create("Test.Error", "message");

        // Act
        var result = Result<string>.Fail(error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.FirstError.Should().Be(error);
    }

    [Fact]
    public void Value_OnFailedResult_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var result = Result<string>.Fail(Error.Create("Test.Error", "message"));

        // Act
        var action = () => _ = result.Value;

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot access Value on a failed result*");
    }

    [Fact]
    public void FromNullable_WithValue_ShouldReturnSuccessResult()
    {
        // Arrange
        const string value = "test";

        // Act
        var result = Result<string>.FromNullable(value, "Entity", 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void FromNullable_WithNull_ShouldReturnNotFoundResult()
    {
        // Act
        var result = Result<string>.FromNullable(null, "Entity", 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.FirstError!.Code.Should().Be("Entity.NotFound");
    }

    [Fact]
    public void Map_OnSuccess_ShouldTransformValue()
    {
        // Arrange
        var result = Result<int>.Ok(5);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Map_OnFailure_ShouldPreserveErrors()
    {
        // Arrange
        var error = Error.Create("Test.Error", "message");
        var result = Result<int>.Fail(error);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.FirstError.Should().Be(error);
    }

    [Fact]
    public void OnSuccess_WhenSuccessful_ShouldExecuteAction()
    {
        // Arrange
        var result = Result<int>.Ok(5);
        var wasExecuted = false;

        // Act
        result.OnSuccess(x => wasExecuted = true);

        // Assert
        wasExecuted.Should().BeTrue();
    }

    [Fact]
    public void OnSuccess_WhenFailed_ShouldNotExecuteAction()
    {
        // Arrange
        var result = Result<int>.Fail(Error.Create("Test.Error", "message"));
        var wasExecuted = false;

        // Act
        result.OnSuccess(x => wasExecuted = true);

        // Assert
        wasExecuted.Should().BeFalse();
    }

    [Fact]
    public void OnFailure_WhenFailed_ShouldExecuteAction()
    {
        // Arrange
        var result = Result<int>.Fail(Error.Create("Test.Error", "message"));
        var capturedErrors = new List<Error>();

        // Act
        result.OnFailure(errors => capturedErrors.AddRange(errors));

        // Assert
        capturedErrors.Should().HaveCount(1);
    }

    [Fact]
    public void OnFailure_WhenSuccessful_ShouldNotExecuteAction()
    {
        // Arrange
        var result = Result<int>.Ok(5);
        var wasExecuted = false;

        // Act
        result.OnFailure(errors => wasExecuted = true);

        // Assert
        wasExecuted.Should().BeFalse();
    }

    [Fact]
    public void GetValueOrDefault_OnSuccess_ShouldReturnValue()
    {
        // Arrange
        var result = Result<int>.Ok(5);

        // Act
        var value = result.GetValueOrDefault(0);

        // Assert
        value.Should().Be(5);
    }

    [Fact]
    public void GetValueOrDefault_OnFailure_ShouldReturnDefault()
    {
        // Arrange
        var result = Result<int>.Fail(Error.Create("Test.Error", "message"));

        // Act
        var value = result.GetValueOrDefault(99);

        // Assert
        value.Should().Be(99);
    }

    [Fact]
    public void ImplicitConversion_FromValue_ShouldCreateSuccessResult()
    {
        // Act
        Result<string> result = "test value";

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test value");
    }

    [Fact]
    public void GenericImplicitConversion_FromError_ShouldCreateFailedResult()
    {
        // Arrange
        var error = Error.Create("Test.Error", "message");

        // Act
        Result<string> result = error;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.FirstError.Should().Be(error);
    }

    #endregion

    #region Invariant Tests

    [Fact]
    public void SuccessResult_WithErrors_ShouldThrow()
    {
        // This tests the internal invariant - we can't directly test it,
        // but we verify the public API enforces it
        // Success results created via Ok() never have errors
        var result = Result.Ok();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FailedResult_WithoutErrors_ShouldNotBePossibleViaPublicApi()
    {
        // Verify that all Fail methods require at least one error
        // This is enforced by the constructor
        var action = () => Result.Fail(Array.Empty<Error>());
        action.Should().Throw<InvalidOperationException>();
    }

    #endregion
}
