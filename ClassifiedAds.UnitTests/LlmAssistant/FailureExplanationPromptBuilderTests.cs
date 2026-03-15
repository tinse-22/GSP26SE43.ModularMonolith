using ClassifiedAds.Modules.LlmAssistant.Services;

namespace ClassifiedAds.UnitTests.LlmAssistant;

public class FailureExplanationPromptBuilderTests
{
    private readonly FailureExplanationSanitizer _sanitizer = new();

    [Fact]
    public void Build_ShouldContainDeterministicSections()
    {
        // Arrange
        var context = _sanitizer.Sanitize(FailureExplanationTestData.CreateContext());
        var endpointMetadata = FailureExplanationTestData.CreateEndpointMetadata(context.Definition.EndpointId);
        var builder = new FailureExplanationPromptBuilder(FailureExplanationTestData.CreateOptions());

        // Act
        var prompt = builder.Build(context, endpointMetadata);

        // Assert
        prompt.Provider.Should().Be("N8n");
        prompt.Model.Should().Be("gpt-4.1-mini");
        prompt.Prompt.Should().Contain("Failure reasons deterministic:");
        prompt.Prompt.Should().Contain("Original test definition:");
        prompt.Prompt.Should().Contain("Actual response and execution result:");
        prompt.Prompt.Should().Contain("Endpoint metadata:");
        prompt.Prompt.Should().Contain(context.TestSuiteId.ToString());
        prompt.Prompt.Should().Contain(context.Definition.TestCaseId.ToString());
        prompt.Prompt.Should().Contain("Khong duoc quyet dinh pass/fail.");
    }

    [Fact]
    public void Build_ShouldNotContainRawSecrets()
    {
        // Arrange
        const string secret = "raw-secret-value";
        var context = _sanitizer.Sanitize(FailureExplanationTestData.CreateContext(secret));
        var builder = new FailureExplanationPromptBuilder(FailureExplanationTestData.CreateOptions());

        // Act
        var prompt = builder.Build(context, FailureExplanationTestData.CreateEndpointMetadata(context.Definition.EndpointId));

        // Assert
        prompt.Prompt.Should().NotContain(secret);
        prompt.SanitizedContextJson.Should().NotContain(secret);
        prompt.Prompt.Should().Contain("***MASKED***");
        prompt.SanitizedContextJson.Should().Contain("***MASKED***");
    }
}
