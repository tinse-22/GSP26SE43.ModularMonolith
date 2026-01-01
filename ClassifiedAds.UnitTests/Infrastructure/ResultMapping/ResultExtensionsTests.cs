using ClassifiedAds.Domain.Infrastructure.ResultPattern;
using ClassifiedAds.Infrastructure.Web.ResultMapping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClassifiedAds.UnitTests.Infrastructure.ResultMapping;

/// <summary>
/// Unit tests for ResultExtensions that map Result types to ActionResult.
/// Tests HTTP status code mapping and ProblemDetails formatting.
/// </summary>
public class ResultExtensionsTests
{
    private readonly DefaultHttpContext _httpContext;

    public ResultExtensionsTests()
    {
        _httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-id"
        };
        _httpContext.Request.Path = "/api/test";
    }

    #region Result (non-generic) Tests

    [Fact]
    public void ToActionResult_OnSuccess_ShouldReturnNoContent()
    {
        // Arrange
        var result = Result.Ok();

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        actionResult.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void ToActionResult_OnNotFoundError_ShouldReturn404()
    {
        // Arrange
        var result = Result.NotFound("Product", 123);

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status404NotFound);
        problemDetails.Title.Should().Be("Not Found");
        problemDetails.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public void ToActionResult_OnValidationError_ShouldReturn400WithValidationProblemDetails()
    {
        // Arrange
        var result = Result.ValidationFailed("Email", "Email is invalid");

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("One or more validation errors occurred.");
        problemDetails.Errors.Should().ContainKey("Email");
    }

    [Fact]
    public void ToActionResult_OnConflictError_ShouldReturn409()
    {
        // Arrange
        var result = Result.Fail(Error.Conflict("Product", "Product already exists"));

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void ToActionResult_OnUnauthorizedError_ShouldReturn401()
    {
        // Arrange
        var result = Result.Fail(Error.Unauthorized());

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void ToActionResult_OnForbiddenError_ShouldReturn403()
    {
        // Arrange
        var result = Result.Fail(Error.Forbidden());

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void ToActionResult_OnInternalError_ShouldReturn500()
    {
        // Arrange
        var result = Result.Fail(Error.Internal());

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    #endregion

    #region Result<T> (generic) Tests

    [Fact]
    public void GenericToActionResult_OnSuccess_ShouldReturnOkWithValue()
    {
        // Arrange
        var expectedValue = new TestDto { Id = 1, Name = "Test" };
        var result = Result<TestDto>.Ok(expectedValue);

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedValue);
    }

    [Fact]
    public void GenericToActionResult_OnFailure_ShouldReturnAppropriateError()
    {
        // Arrange
        var result = Result<TestDto>.NotFound("TestDto", 1);

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void GenericToActionResult_WithCustomStatusCode_ShouldUseCustomCode()
    {
        // Arrange
        var result = Result<TestDto>.Ok(new TestDto { Id = 1, Name = "Test" });

        // Act
        var actionResult = result.ToActionResult(_httpContext, StatusCodes.Status201Created);

        // Assert
        var objectResult = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public void ToCreatedResult_OnSuccess_ShouldReturn201()
    {
        // Arrange
        var expectedValue = new TestDto { Id = 1, Name = "Test" };
        var result = Result<TestDto>.Ok(expectedValue);

        // Act
        var actionResult = result.ToCreatedResult(_httpContext);

        // Assert
        var objectResult = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public void ToCreatedResult_WithLocation_ShouldReturnCreatedResultWithLocation()
    {
        // Arrange
        var expectedValue = new TestDto { Id = 1, Name = "Test" };
        var result = Result<TestDto>.Ok(expectedValue);

        // Act
        var actionResult = result.ToCreatedResult(_httpContext, "/api/test/1");

        // Assert
        var createdResult = actionResult.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be("/api/test/1");
    }

    #endregion

    #region ProblemDetails Format Tests

    [Fact]
    public void ToActionResult_ShouldIncludeTraceIdInProblemDetails()
    {
        // Arrange
        var result = Result.NotFound("Product", 1);

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = (ObjectResult)actionResult;
        var problemDetails = (ProblemDetails)objectResult.Value!;
        problemDetails.Extensions["traceId"].Should().Be("test-trace-id");
    }

    [Fact]
    public void ToActionResult_ShouldIncludeInstancePath()
    {
        // Arrange
        var result = Result.NotFound("Product", 1);

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = (ObjectResult)actionResult;
        var problemDetails = (ProblemDetails)objectResult.Value!;
        problemDetails.Instance.Should().Be("/api/test");
    }

    [Fact]
    public void ToActionResult_ShouldIncludeTypeUri()
    {
        // Arrange
        var result = Result.NotFound("Product", 1);

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = (ObjectResult)actionResult;
        var problemDetails = (ProblemDetails)objectResult.Value!;
        problemDetails.Type.Should().Contain("rfc7231");
    }

    [Fact]
    public void ToActionResult_MultipleValidationErrors_ShouldGroupByField()
    {
        // Arrange
        var result = Result.ValidationFailed(new[]
        {
            ("Name", "Name is required"),
            ("Name", "Name must be at least 3 characters"),
            ("Email", "Email is invalid")
        });

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = (ObjectResult)actionResult;
        var problemDetails = (ValidationProblemDetails)objectResult.Value!;

        problemDetails.Errors.Should().ContainKey("Name");
        problemDetails.Errors["Name"].Should().HaveCount(2);
        problemDetails.Errors.Should().ContainKey("Email");
        problemDetails.Errors["Email"].Should().HaveCount(1);
    }

    #endregion

    #region Content Type Tests

    [Fact]
    public void ToActionResult_OnError_ShouldSetProblemJsonContentType()
    {
        // Arrange
        var result = Result.NotFound("Product", 1);

        // Act
        var actionResult = result.ToActionResult(_httpContext);

        // Assert
        var objectResult = (ObjectResult)actionResult;
        objectResult.ContentTypes.Should().Contain("application/problem+json");
    }

    #endregion

    // Test DTO for generic result tests
    private class TestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
