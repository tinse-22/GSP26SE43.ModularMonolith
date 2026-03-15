using ClassifiedAds.Modules.LlmAssistant.Services;
using System.Text.Json;

namespace ClassifiedAds.UnitTests.LlmAssistant;

public class FailureExplanationSanitizerTests
{
    private readonly FailureExplanationSanitizer _sanitizer = new();

    [Fact]
    public void Sanitize_ShouldMaskAuthorizationCookieTokenPasswordAndApiKeyValues()
    {
        // Arrange
        const string secret = "raw-secret-value";
        var context = FailureExplanationTestData.CreateContext(secret);

        // Act
        var sanitized = _sanitizer.Sanitize(context);
        var serialized = JsonSerializer.Serialize(sanitized);

        // Assert
        serialized.Should().NotContain(secret);
        serialized.Should().Contain("***MASKED***");

        sanitized.Definition.Request.Url.Should().Contain("***MASKED***");
        sanitized.Definition.Request.Headers.Should().Contain("***MASKED***");
        sanitized.Definition.Request.Body.Should().Contain("***MASKED***");
        sanitized.Definition.Expectation.HeaderChecks.Should().Contain("***MASKED***");
        sanitized.ActualResult.ResolvedUrl.Should().Contain("***MASKED***");
        sanitized.ActualResult.RequestHeaders["Authorization"].Should().Be("***MASKED***");
        sanitized.ActualResult.RequestHeaders["Cookie"].Should().Be("***MASKED***");
        sanitized.ActualResult.ResponseHeaders["Set-Cookie"].Should().Be("***MASKED***");
        sanitized.ActualResult.ExtractedVariables["authToken"].Should().Be("***MASKED***");
        sanitized.ActualResult.ExtractedVariables["password"].Should().Be("***MASKED***");
        sanitized.ActualResult.ExtractedVariables["apiKey"].Should().Be("***MASKED***");
        sanitized.ActualResult.ResponseBodyPreview.Should().Contain("***MASKED***");
    }
}
