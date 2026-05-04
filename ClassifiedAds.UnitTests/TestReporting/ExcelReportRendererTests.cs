using ClassifiedAds.Modules.TestReporting.Services;
using ClosedXML.Excel;
using System.IO;

namespace ClassifiedAds.UnitTests.TestReporting;

public class ExcelReportRendererTests
{
    [Fact]
    public async Task RenderAsync_ShouldProduceStandardXlsxWorkbook()
    {
        // Arrange
        var document = ReportTestData.CreateDocument();
        var renderer = new ExcelReportRenderer();

        // Act
        var result = await renderer.RenderAsync(document);

        // Assert
        result.FileName.Should().EndWith(".xlsx");
        result.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.Content.Should().NotBeEmpty();

        using var stream = new MemoryStream(result.Content);
        using var workbook = new XLWorkbook(stream);

        workbook.Worksheets.Select(x => x.Name).Should().Equal(
            "Summary",
            "Test Cases",
            "Execution Timeline",
            "Bug Report",
            "API Coverage");

        var summary = workbook.Worksheet("Summary");
        summary.Cell(3, 1).GetString().Should().Be("Project Name");
        summary.Cell(3, 2).GetString().Should().Be(document.ProjectName);
        summary.Cell(4, 1).GetString().Should().Be("Suite Name");
        summary.Cell(4, 2).GetString().Should().Be(document.SuiteName);

        var cases = workbook.Worksheet("Test Cases");
        cases.Cell(1, 1).GetString().Should().Be("Order");
        cases.Cell(1, 8).GetString().Should().Be("Expected Result");
        cases.Cell(1, 9).GetString().Should().Be("Actual Result");
        cases.Cell(1, 11).GetString().Should().Be("Response Preview");
        cases.Cell(3, 8).GetString().Should().Contain("200"); // Expected Status for Get user
        cases.Cell(3, 9).GetString().Should().Contain("500"); // Actual status for Get user (failed)
        cases.Cell(3, 10).GetString().Should().Contain("STATUS_CODE_MISMATCH");

        var bugReport = workbook.Worksheet("Bug Report");
        bugReport.Cell(2, 1).GetString().Should().Be("Bug ID");
        bugReport.Cell(2, 2).GetString().Should().Be("Test Case Name");
        bugReport.Cell(3, 1).GetString().Should().Be("BUG-001");
        bugReport.Cell(3, 2).GetString().Should().Be("Get user");
        bugReport.Cell(3, 5).GetString().Should().Be("Critical");

        var timeline = workbook.Worksheet("Execution Timeline");
        timeline.Cell(1, 5).GetString().Should().Be("Retry Reason");
        timeline.Cell(3, 1).GetString().Should().Be("Get user");
        timeline.Cell(3, 9).GetString().Should().Contain("STATUS_CODE_MISMATCH");

        var coverage = workbook.Worksheet("API Coverage");
        coverage.CellsUsed()
            .Select(x => x.GetString())
            .Should()
            .Contain("GET /api/payments");
    }
}
