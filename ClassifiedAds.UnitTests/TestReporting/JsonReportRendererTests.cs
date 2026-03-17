using ClassifiedAds.Modules.TestReporting.Services;
using System.Text;
using System.Text.Json;

namespace ClassifiedAds.UnitTests.TestReporting;

public class JsonReportRendererTests
{
    [Fact]
    public async Task RenderAsync_ShouldSerializeStructuredOutput()
    {
        // Arrange
        var renderer = new JsonReportRenderer();
        var document = ReportTestData.CreateDocument();

        // Act
        var result = await renderer.RenderAsync(document);
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(result.Content));

        // Assert
        result.FileName.Should().EndWith(".json");
        result.ContentType.Should().Be("application/json; charset=utf-8");
        result.FileCategory.ToString().Should().Be("Export");
        json.RootElement.GetProperty("suiteName").GetString().Should().Be("Checkout Regression");
        json.RootElement.GetProperty("reportType").GetString().Should().Be("Detailed");
        json.RootElement.GetProperty("run").GetProperty("runNumber").GetInt32().Should().Be(5);
        json.RootElement.GetProperty("run").GetProperty("status").GetString().Should().Be("Failed");
        json.RootElement.GetProperty("coverage").GetProperty("totalEndpoints").GetInt32().Should().Be(3);
        json.RootElement.GetProperty("cases").GetArrayLength().Should().Be(2);
    }
}
