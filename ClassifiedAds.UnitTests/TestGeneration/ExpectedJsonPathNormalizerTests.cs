using ClassifiedAds.Modules.TestGeneration.Services;
using ClassifiedAds.UnitTests;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class ExpectedJsonPathNormalizerTests
{
    [Fact]
    public void NormalizeForEndpoint_Should_UseActualResponse_NotEndpointName()
    {
        var result = ExpectedJsonPathNormalizer.NormalizeForEndpoint(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["$.data.id"] = "exists",
            },
            endpointPath: "/api/any-resource",
            jsonPathResolver: JsonPathResolutionTestFactory.CreateResolver(),
            actualResponseJson: """{"data":{"_id":"abc123"}}""");

        result.Should().ContainKey("$.data._id");
        result.Should().NotContainKey("$.data.id");
    }

    [Fact]
    public void NormalizeForEndpoint_Should_NotGuess_WhenNoSchemaOrActualResponseExists()
    {
        var result = ExpectedJsonPathNormalizer.NormalizeForEndpoint(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["$.data.id"] = "exists",
            },
            endpointPath: "/api/categories",
            jsonPathResolver: JsonPathResolutionTestFactory.CreateResolver());

        result.Should().ContainKey("$.data.id");
        result.Should().NotContainKey("$.data._id");
    }
}
