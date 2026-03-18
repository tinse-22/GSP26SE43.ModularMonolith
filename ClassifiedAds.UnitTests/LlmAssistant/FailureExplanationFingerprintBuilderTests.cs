using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Modules.LlmAssistant.Services;

namespace ClassifiedAds.UnitTests.LlmAssistant;

public class FailureExplanationFingerprintBuilderTests
{
    private readonly FailureExplanationFingerprintBuilder _builder = new();

    [Fact]
    public void Build_SameDeterministicInput_ShouldReturnSameKey()
    {
        // Arrange
        var first = FailureExplanationTestData.CreateContext();
        var second = FailureExplanationTestData.CreateContext();

        // Act
        var firstKey = _builder.Build(first);
        var secondKey = _builder.Build(second);

        // Assert
        firstKey.Should().Be(secondKey);
    }

    [Fact]
    public void Build_DifferentFailurePayload_ShouldReturnDifferentKey()
    {
        // Arrange
        var first = FailureExplanationTestData.CreateContext();
        var second = FailureExplanationTestData.CreateContext();
        second.ActualResult.ResponseBodyPreview = "{\"error\":\"different-payload\"}";
        second.ActualResult.FailureReasons = new[]
        {
            new FailureExplanationFailureReasonDto
            {
                Code = "BODY_CONTAINS_FAILED",
                Message = "Response body khong co truong mong doi.",
                Target = "body",
                Expected = "id",
                Actual = "error",
            },
        };
        second.ActualResult.BodyContainsPassed = false;

        // Act
        var firstKey = _builder.Build(first);
        var secondKey = _builder.Build(second);

        // Assert
        firstKey.Should().NotBe(secondKey);
    }
}
