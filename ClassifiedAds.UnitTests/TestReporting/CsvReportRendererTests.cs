using ClassifiedAds.Modules.TestReporting.Services;
using System.Text;

namespace ClassifiedAds.UnitTests.TestReporting;

public class CsvReportRendererTests
{
    [Fact]
    public async Task RenderAsync_ShouldProduceStableFlattenedRows()
    {
        // Arrange
        var renderer = new CsvReportRenderer();
        var document = ReportTestData.CreateDocument();

        // Act
        var result = await renderer.RenderAsync(document);
        var content = Encoding.UTF8.GetString(result.Content);

        // Assert
        result.FileName.Should().EndWith(".csv");
        result.ContentType.Should().Be("text/csv; charset=utf-8");
        content.Should().Contain("Section,Key,Value,TestCaseId,EndpointId,OrderIndex,Name,Status,HttpStatusCode,DurationMs,Details");
        content.IndexOf("summary,suiteName,Checkout Regression", StringComparison.Ordinal)
            .Should().BeLessThan(content.IndexOf("failure_distribution,STATUS_CODE_MISMATCH,1", StringComparison.Ordinal));
        content.IndexOf("failure_distribution,STATUS_CODE_MISMATCH,1", StringComparison.Ordinal)
            .Should().BeLessThan(content.IndexOf("recent_runs,4,Completed", StringComparison.Ordinal));
        content.IndexOf("recent_runs,4,Completed", StringComparison.Ordinal)
            .Should().BeLessThan(content.IndexOf("cases,88888888-8888-8888-8888-888888888888,/api/orders", StringComparison.Ordinal));
        content.Should().Contain("***MASKED***");
    }
}
