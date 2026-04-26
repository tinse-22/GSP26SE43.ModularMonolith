using ClassifiedAds.Modules.TestReporting.ConfigurationOptions;
using ClassifiedAds.Modules.TestReporting.Services;
using Microsoft.Extensions.Options;

namespace ClassifiedAds.UnitTests.TestReporting;

public class ReportDataSanitizerTests
{
    [Fact]
    public void Sanitize_ShouldMaskSecretBearingHeadersVariablesAndBodyPreview()
    {
        // Arrange
        var options = Options.Create(new TestReportingModuleOptions
        {
            ReportGeneration = new ReportGenerationOptions
            {
                MaxResponseBodyPreviewChars = 45,
            },
        });
        var sanitizer = new ReportDataSanitizer(options);
        var context = ReportTestData.CreateContext();

        // Act
        var result = sanitizer.Sanitize(context);

        // Assert
        result.ProjectName.Should().Be(ReportTestData.ProjectName);
        result.Attempts.Should().HaveCount(2);
        result.Definitions[0].Request.Headers.Should().Contain("***MASKED***");
        result.Definitions[0].Request.Headers.Should().NotContain("raw-order-token");
        result.Definitions[0].Request.QueryParams.Should().Contain("***MASKED***");
        result.Definitions[0].Request.QueryParams.Should().NotContain("raw-order-key");
        result.Definitions[0].Request.Body.Should().Contain("***MASKED***");
        result.Results[0].RequestHeaders["Authorization"].Should().Be("***MASKED***");
        result.Results[0].ResponseHeaders["Set-Cookie"].Should().Be("***MASKED***");
        result.Results[0].ExtractedVariables["access_token"].Should().Be("***MASKED***");
        result.Results[1].RequestHeaders["Cookie"].Should().Be("***MASKED***");
        result.Results[1].ResponseHeaders["Authorization"].Should().Be("***MASKED***");
        result.Results[1].ExtractedVariables["client_secret"].Should().Be("***MASKED***");
        result.Results[1].ResponseBodyPreview.Should().NotContain("raw-failure-password");
        result.Results[1].ResponseBodyPreview.Should().NotContain("raw-response-token");
        result.Results[1].ResponseBodyPreview.Length.Should().BeLessThanOrEqualTo(45);
        result.Attempts[1].RetryReason.Should().Contain("***MASKED***");
        result.Attempts[1].RetryReason.Should().NotContain("raw-retry-token");
        result.Attempts[1].SkippedCause.Should().Contain("***MASKED***");
        result.Attempts[1].SkippedCause.Should().NotContain("secret-cookie");
    }
}
