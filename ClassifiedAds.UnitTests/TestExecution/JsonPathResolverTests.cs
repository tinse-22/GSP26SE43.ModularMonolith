using ClassifiedAds.Contracts.TestExecution.JsonPathResolution;
using ClassifiedAds.UnitTests;

namespace ClassifiedAds.UnitTests.TestExecution;

public class JsonPathResolverTests
{
    [Theory]
    [InlineData("$.data.id", """{"data":{"_id":"cat-123"}}""", "$.data._id", "id_alias")]
    [InlineData("$.data.data.id", """{"data":{"data":{"_id":"cat-123"}}}""", "$.data.data._id", "id_alias")]
    [InlineData("$.data.data.id", """{"data":{"_id":"cat-123"}}""", "$.data._id", "wrapper_collapsed")]
    [InlineData("$.id", """{"data":{"_id":"cat-123"}}""", "$.data._id", "wrapper_expanded")]
    [InlineData("$.data[0].id", """{"data":[{"_id":"cat-123"}]}""", "$.data[0]._id", "array_item_resolved")]
    [InlineData("$.data.items[0].id", """{"data":{"items":[{"_id":"cat-123"}]}}""", "$.data.items[0]._id", "array_item_resolved")]
    public void Resolve_Should_MapExpectedPath_ToActualPath(
        string expectedPath,
        string actualJson,
        string resolvedPath,
        string reason)
    {
        var result = JsonPathResolutionTestFactory.CreateResolver().Resolve(new JsonPathResolutionRequest
        {
            OriginalPath = expectedPath,
            ActualResponseJson = actualJson,
        });

        result.IsResolved.Should().BeTrue();
        result.ResolvedPath.Should().Be(resolvedPath);
        result.ResolutionStrategy.Should().Contain(reason);
        result.Source.Should().Be("actual_response");
    }

    [Fact]
    public void Resolve_Should_ReturnDiagnostic_WhenPathCannotBeMappedSafely()
    {
        var result = JsonPathResolutionTestFactory.CreateResolver().Resolve(new JsonPathResolutionRequest
        {
            OriginalPath = "$.data.id",
            ActualResponseJson = """{"data":{"name":"Sample"},"meta":{"traceId":"abc"}}""",
        });

        result.IsResolved.Should().BeFalse();
        result.ResolvedPath.Should().BeNull();
        result.Diagnostics.Should().Contain(x => x.Contains("$.data.id", StringComparison.Ordinal));
        result.Diagnostics.Should().Contain(x => x.Contains("field_not_found", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolve_Should_NotApplyAliasesOrWrappers_WhenOptionsAreEmpty()
    {
        var resolver = new JsonPathResolver(new JsonPathResolutionOptions());

        var result = resolver.Resolve(new JsonPathResolutionRequest
        {
            OriginalPath = "$.data.id",
            ActualResponseJson = """{"data":{"_id":"cat-123"}}""",
        });

        result.IsResolved.Should().BeFalse();
        result.ResolvedPath.Should().BeNull();
    }
}
