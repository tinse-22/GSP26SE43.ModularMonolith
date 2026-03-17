using ClassifiedAds.Modules.TestReporting.Services;
using RazorLight;

namespace ClassifiedAds.UnitTests.TestReporting;

public class HtmlReportRendererTests
{
    [Fact]
    public async Task RenderAsync_ShouldIncludeExpectedSectionsAndExcludeRawSecrets()
    {
        // Arrange
        var engine = new RazorLightEngineBuilder()
            .UseFileSystemProject(Environment.CurrentDirectory)
            .UseMemoryCachingProvider()
            .Build();
        var renderer = new HtmlReportRenderer(engine);
        var document = ReportTestData.CreateDocument();

        // Act
        var result = await renderer.RenderAsync(document);
        var html = System.Text.Encoding.UTF8.GetString(result.Content);

        // Assert
        result.FileName.Should().EndWith(".html");
        result.ContentType.Should().Be("text/html; charset=utf-8");
        html.Should().Contain("Run Summary");
        html.Should().Contain("Coverage Summary");
        html.Should().Contain("Failure Distribution");
        html.Should().Contain("Recent Run History");
        html.Should().Contain("Detailed Results");
        html.Should().Contain("***MASKED***");
        html.Should().NotContain("raw-order-token");
        html.Should().NotContain("raw-client-secret");
    }
}
