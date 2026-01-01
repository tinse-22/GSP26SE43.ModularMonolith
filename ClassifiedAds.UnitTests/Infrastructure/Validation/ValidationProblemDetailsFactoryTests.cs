using ClassifiedAds.Infrastructure.Web.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ClassifiedAds.UnitTests.Infrastructure.Validation;

/// <summary>
/// Unit tests for ValidationProblemDetailsFactory.
/// Tests standardized validation error response creation.
/// </summary>
public class ValidationProblemDetailsFactoryTests
{
    private readonly DefaultHttpContext _httpContext;

    public ValidationProblemDetailsFactoryTests()
    {
        _httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-id"
        };
        _httpContext.Request.Path = "/api/test";
    }

    [Fact]
    public void CreateValidationProblemDetails_FromModelState_ShouldReturnValidationProblemDetails()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Name is required");
        modelState.AddModelError("Email", "Email is invalid");

        // Act
        var problemDetails = ValidationProblemDetailsFactory.CreateValidationProblemDetails(_httpContext, modelState);

        // Assert
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("One or more validation errors occurred.");
        problemDetails.Errors.Should().ContainKey("Name");
        problemDetails.Errors.Should().ContainKey("Email");
    }

    [Fact]
    public void CreateValidationProblemDetails_ShouldIncludeTraceId()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Field", "Error");

        // Act
        var problemDetails = ValidationProblemDetailsFactory.CreateValidationProblemDetails(_httpContext, modelState);

        // Assert
        problemDetails.Extensions.Should().ContainKey("traceId");
        problemDetails.Extensions["traceId"].Should().Be("test-trace-id");
    }

    [Fact]
    public void CreateValidationProblemDetails_ShouldIncludeInstancePath()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Field", "Error");

        // Act
        var problemDetails = ValidationProblemDetailsFactory.CreateValidationProblemDetails(_httpContext, modelState);

        // Assert
        problemDetails.Instance.Should().Be("/api/test");
    }

    [Fact]
    public void CreateValidationProblemDetails_ShouldIncludeTypeUri()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Field", "Error");

        // Act
        var problemDetails = ValidationProblemDetailsFactory.CreateValidationProblemDetails(_httpContext, modelState);

        // Assert
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.1");
    }

    [Fact]
    public void CreateValidationProblemDetails_FromDictionary_ShouldReturnValidationProblemDetails()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["Name"] = new[] { "Name is required", "Name must be at least 3 characters" },
            ["Email"] = new[] { "Email is invalid" }
        };

        // Act
        var problemDetails = ValidationProblemDetailsFactory.CreateValidationProblemDetails(_httpContext, errors);

        // Assert
        problemDetails.Errors.Should().ContainKey("Name");
        problemDetails.Errors["Name"].Should().HaveCount(2);
        problemDetails.Errors.Should().ContainKey("Email");
        problemDetails.Errors["Email"].Should().HaveCount(1);
    }

    [Fact]
    public void CreateFactory_ShouldReturnBadRequestObjectResult()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Name is required");

        var actionContext = new ActionContext(
            _httpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(),
            modelState);

        var factory = ValidationProblemDetailsFactory.CreateFactory();

        // Act
        var result = factory(actionContext);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.ContentTypes.Should().Contain("application/problem+json");
        badRequestResult.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public void CreateValidationProblemDetails_WithMultipleErrorsOnSameField_ShouldIncludeAllErrors()
    {
        // Arrange
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Password", "Password is required");
        modelState.AddModelError("Password", "Password must be at least 8 characters");
        modelState.AddModelError("Password", "Password must contain a special character");

        // Act
        var problemDetails = ValidationProblemDetailsFactory.CreateValidationProblemDetails(_httpContext, modelState);

        // Assert
        problemDetails.Errors["Password"].Should().HaveCount(3);
        problemDetails.Errors["Password"].Should().Contain("Password is required");
        problemDetails.Errors["Password"].Should().Contain("Password must be at least 8 characters");
        problemDetails.Errors["Password"].Should().Contain("Password must contain a special character");
    }

    [Fact]
    public void CreateValidationProblemDetails_WithEmptyModelState_ShouldReturnEmptyErrors()
    {
        // Arrange
        var modelState = new ModelStateDictionary();

        // Act
        var problemDetails = ValidationProblemDetailsFactory.CreateValidationProblemDetails(_httpContext, modelState);

        // Assert
        problemDetails.Errors.Should().BeEmpty();
    }
}
