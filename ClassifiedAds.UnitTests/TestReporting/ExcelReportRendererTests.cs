using ClassifiedAds.Modules.TestReporting.Services;
using ClosedXML.Excel;
using System;
using System.IO;
using System.Linq;

namespace ClassifiedAds.UnitTests.TestReporting;

public class ExcelReportRendererTests
{
    [Fact]
    public async Task RenderAsync_ShouldProduceAniMusicRuntimeWorkbook()
    {
        // Arrange
        var document = ReportTestData.CreateDocument();
        document.Cases = document.Cases.Concat(new[]
        {
            new ClassifiedAds.Modules.TestReporting.Models.TestRunReportCaseDocumentModel
            {
                TestCaseId = Guid.Parse("abababab-1111-1111-1111-abababababab"),
                EndpointId = ReportTestData.EndpointIdPayments,
                Name = "Skip payment reconciliation",
                Description = "Skipped when a dependency does not pass",
                TestType = "Boundary",
                OrderIndex = 3,
                Request = new ClassifiedAds.Contracts.TestGeneration.DTOs.ExecutionTestCaseRequestDto
                {
                    HttpMethod = "GET",
                    Url = "/api/payments/reconcile",
                },
                Status = "Skipped",
                ResolvedUrl = "/api/payments/reconcile",
                SkippedBecauseDependencyIds = new[] { ReportTestData.TestCaseIdUsers },
            },
            new ClassifiedAds.Modules.TestReporting.Models.TestRunReportCaseDocumentModel
            {
                TestCaseId = Guid.Parse("bcbcbcbc-1111-1111-1111-bcbcbcbcbcbc"),
                EndpointId = ReportTestData.EndpointIdPayments,
                Name = "Pending runtime check",
                Description = "Unknown runtime state maps to pending",
                TestType = null,
                OrderIndex = 4,
                Request = new ClassifiedAds.Contracts.TestGeneration.DTOs.ExecutionTestCaseRequestDto
                {
                    HttpMethod = "GET",
                    Url = "/api/payments/pending",
                },
                Status = "Running",
                ResolvedUrl = "/api/payments/pending",
            },
        }).ToArray();
        var renderer = new ExcelReportRenderer();

        // Act
        var result = await renderer.RenderAsync(document);

        // Assert
        result.FileName.Should().EndWith(".xlsx");
        result.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.Content.Should().NotBeEmpty();

        using var stream = new MemoryStream(result.Content);
        using var workbook = new XLWorkbook(stream);

        workbook.Worksheets.Select(x => x.Name).Should().StartWith(new[]
        {
            "Cover",
            "Test Cases",
            "Test Statistics",
            "HappyPath",
            "Boundary",
            "Runtime",
            "Execution Timeline",
            "API Coverage",
            "Bug Report",
        });

        var cover = workbook.Worksheet("Cover");
        cover.Cell(2, 2).GetString().Should().Be("TEST REPORT DOCUMENT");
        cover.Cell(4, 1).GetString().Should().Be("Project Name");
        cover.Cell(4, 2).GetString().Should().Be(document.ProjectName);
        cover.Cell(8, 1).GetString().Should().Be("Suite Name");
        cover.Cell(8, 2).GetString().Should().Be(document.SuiteName);

        var cases = workbook.Worksheet("Test Cases");
        cases.Cell(1, 4).GetString().Should().Be("TEST CASE LIST");
        cases.Cell(8, 2).GetString().Should().Be("No");
        cases.Cell(8, 4).GetString().Should().Be("Sheet Name");
        cases.Cell(9, 3).GetString().Should().Be("HappyPath");
        cases.Cell(10, 4).GetString().Should().Be("Boundary");

        var statistics = workbook.Worksheet("Test Statistics");
        statistics.Cell(1, 2).GetString().Should().Be("TEST STATISTICS");
        statistics.Cell(10, 3).GetString().Should().Be("Module code");
        statistics.Cell(11, 3).FormulaA1.Should().Contain("HappyPath!B2");
        statistics.Cell(11, 4).FormulaA1.Should().Contain("HappyPath!B6");

        var happyPath = workbook.Worksheet("HappyPath");
        happyPath.Cell(2, 1).GetString().Should().Be("Feature");
        happyPath.Cell(2, 2).GetString().Should().Be("HappyPath");
        happyPath.Cell(10, 1).GetString().Should().Be("Test Case ID");
        happyPath.Cell(12, 6).GetString().Should().Be("Passed");
        happyPath.Cell(13, 6).GetString().Should().Be("Failed");
        happyPath.Cell(13, 4).GetString().Should().Contain("200");
        happyPath.Cell(13, 15).GetString().Should().Contain("500");
        happyPath.Cell(13, 16).GetString().Should().Contain("STATUS_CODE_MISMATCH");

        var boundary = workbook.Worksheet("Boundary");
        boundary.Cell(12, 6).GetString().Should().Be("N/A");

        var runtime = workbook.Worksheet("Runtime");
        runtime.Cell(12, 6).GetString().Should().Be("Pending");

        var timeline = workbook.Worksheet("Execution Timeline");
        timeline.Cell(3, 5).GetString().Should().Be("Retry Reason");
        timeline.Cell(5, 1).GetString().Should().Be("Get user");
        timeline.Cell(5, 9).GetString().Should().Contain("STATUS_CODE_MISMATCH");

        var coverage = workbook.Worksheet("API Coverage");
        coverage.CellsUsed()
            .Select(x => x.GetString())
            .Should()
            .Contain("GET /api/payments");

        var bugReport = workbook.Worksheet("Bug Report");
        bugReport.Cell(3, 1).GetString().Should().Be("Bug ID");
        bugReport.Cell(4, 1).GetString().Should().Be("BUG-001");
        bugReport.Cell(4, 2).GetString().Should().Be("Get user");
        bugReport.Cell(4, 5).GetString().Should().Be("Critical");
        bugReport.Cell(4, 7).GetString().Should().Be("STATUS_CODE_MISMATCH");
    }
}
