using ClassifiedAds.Contracts.Storage.Enums;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Services;

public class ExcelReportRenderer : IReportRenderer
{
    public ReportFormat Format => ReportFormat.Excel;

    public async Task<RenderedReportFile> RenderAsync(TestRunReportDocumentModel document, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();

        RenderSummarySheet(workbook, document);
        RenderTestCasesSheet(workbook, document);
        RenderAttemptsSheet(workbook, document);
        RenderBugReportSheet(workbook, document);
        RenderCoverageSheet(workbook, document);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new RenderedReportFile
        {
            Content = stream.ToArray(),
            FileName = $"{document.FileBaseName}.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileCategory = FileCategory.Export,
        };
    }

    private void RenderSummarySheet(IXLWorkbook workbook, TestRunReportDocumentModel document)
    {
        var sheet = workbook.Worksheets.Add("Summary");

        sheet.Cell(1, 1).Value = "Test Run Summary Report";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 18;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.DarkBlue;
        sheet.Range(1, 1, 1, 4).Merge();

        var row = 3;
        AddSummaryRow(sheet, ref row, "Project Name", document.ProjectName ?? "(unknown)");
        AddSummaryRow(sheet, ref row, "Suite Name", document.SuiteName);
        AddSummaryRow(sheet, ref row, "Run Number", document.Run?.RunNumber);
        AddSummaryRow(sheet, ref row, "Final Status", document.Run?.Status);
        AddSummaryRow(sheet, ref row, "Environment", document.Run?.ResolvedEnvironmentName);
        AddSummaryRow(sheet, ref row, "Test Date", document.Run?.ExecutedAt.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? document.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));
        AddSummaryRow(sheet, ref row, "Generated At", document.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));

        row++;
        sheet.Cell(row, 1).Value = "Execution Statistics";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
        row++;

        AddSummaryRow(sheet, ref row, "Total Tests", document.Run?.TotalTests);
        AddSummaryRow(sheet, ref row, "Passed", document.Run?.PassedCount);
        AddSummaryRow(sheet, ref row, "Failed", document.Run?.FailedCount);
        AddSummaryRow(sheet, ref row, "Skipped", document.Run?.SkippedCount);

        var totalTests = document.Run?.TotalTests ?? 0;
        var passedCount = document.Run?.PassedCount ?? 0;
        var passRate = totalTests > 0 ? (double)passedCount / totalTests : 0.0;
        var passRateRow = row;
        AddSummaryRow(sheet, ref row, "Pass Rate", passRate);
        sheet.Cell(passRateRow, 2).Style.NumberFormat.Format = "0.00%";
        if (passRate >= 1.0) sheet.Cell(passRateRow, 2).Style.Font.FontColor = XLColor.Green;
        else if (passRate >= 0.7) sheet.Cell(passRateRow, 2).Style.Font.FontColor = XLColor.DarkOrange;
        else sheet.Cell(passRateRow, 2).Style.Font.FontColor = XLColor.Red;
        sheet.Cell(passRateRow, 2).Style.Font.Bold = true;

        AddSummaryRow(sheet, ref row, "Total Duration", $"{document.Run?.DurationMs} ms");

        row++;
        sheet.Cell(row, 1).Value = "API Coverage Metrics";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
        row++;

        AddSummaryRow(sheet, ref row, "Coverage Percent", document.Coverage?.CoveragePercent / 100m);
        sheet.Cell(row - 1, 2).Style.NumberFormat.Format = "0.00%";
        AddSummaryRow(sheet, ref row, "Tested Endpoints", document.Coverage?.TestedEndpoints);
        AddSummaryRow(sheet, ref row, "Total Endpoints", document.Coverage?.TotalEndpoints);

        row++;
        sheet.Cell(row, 1).Value = "Conclusion";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
        sheet.Cell(row, 1).Style.Font.FontSize = 13;
        row++;

        string conclusionText;
        XLColor conclusionColor;
        if (passRate >= 1.0)
        {
            conclusionText = "All test cases passed. System is stable. RECOMMENDED FOR RELEASE.";
            conclusionColor = XLColor.Green;
        }
        else if (passRate >= 0.7)
        {
            conclusionText = $"System is mostly stable ({passRate:P0} pass rate) but has failures. Review failed cases and defects before release.";
            conclusionColor = XLColor.DarkOrange;
        }
        else
        {
            conclusionText = $"Critical failures detected ({passRate:P0} pass rate). System is NOT recommended for release. See Bug Report sheet.";
            conclusionColor = XLColor.Red;
        }

        sheet.Cell(row, 1).Value = conclusionText;
        sheet.Cell(row, 1).Style.Font.FontColor = conclusionColor;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 4).Merge();
        sheet.Range(row, 1, row, 4).Style.Alignment.WrapText = true;

        sheet.Columns(1, 2).AdjustToContents();
        sheet.Column(1).Width = 25;
        sheet.Column(2).Width = 50;
    }

    private void AddSummaryRow(IXLWorksheet sheet, ref int row, string label, object value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
        sheet.Cell(row, 2).Value = XLCellValue.FromObject(value);

        if (label == "Final Status")
        {
            var status = value?.ToString();
            if (status == "Completed") sheet.Cell(row, 2).Style.Font.FontColor = XLColor.Green;
            else if (status == "Failed") sheet.Cell(row, 2).Style.Font.FontColor = XLColor.Red;
        }

        row++;
    }

    private void RenderTestCasesSheet(IXLWorkbook workbook, TestRunReportDocumentModel document)
    {
        var sheet = workbook.Worksheets.Add("Test Cases");

        var headers = new[]
        {
            "Order", "Test Case Name", "Status", "HTTP", "Duration (ms)",
            "Retries", "Resolved URL", "Expected Result", "Actual Result", "Failure Analysis", "Response Preview"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var rowIdx = 2;
        foreach (var tc in document.Cases.OrderBy(x => x.OrderIndex))
        {
            sheet.Cell(rowIdx, 1).Value = tc.OrderIndex;
            sheet.Cell(rowIdx, 2).Value = tc.Name;
            sheet.Cell(rowIdx, 3).Value = tc.Status;
            sheet.Cell(rowIdx, 4).Value = tc.HttpStatusCode;
            sheet.Cell(rowIdx, 5).Value = tc.DurationMs;
            sheet.Cell(rowIdx, 6).Value = tc.TotalAttempts > 1 ? tc.TotalAttempts - 1 : 0;
            sheet.Cell(rowIdx, 7).Value = tc.ResolvedUrl;

            // Expected Result (col 8)
            var expectedParts = new List<string>();
            if (!string.IsNullOrEmpty(tc.Expectation?.ExpectedStatus))
                expectedParts.Add($"Status = {tc.Expectation.ExpectedStatus}");
            if (tc.Expectation?.MaxResponseTime.HasValue == true)
                expectedParts.Add($"Response time ≤ {tc.Expectation.MaxResponseTime}ms");
            if (!string.IsNullOrEmpty(tc.Expectation?.BodyContains) && tc.Expectation.BodyContains != "[]")
                expectedParts.Add($"Body contains: {tc.Expectation.BodyContains}");
            sheet.Cell(rowIdx, 8).Value = expectedParts.Any()
                ? string.Join(", ", expectedParts)
                : "(no expectation defined)";

            // Actual Result (col 9)
            var isPassed = string.Equals(tc.Status, "Passed", StringComparison.OrdinalIgnoreCase);
            var isSkipped = string.Equals(tc.Status, "Skipped", StringComparison.OrdinalIgnoreCase);
            string actualResult;
            if (isSkipped)
                actualResult = "Skipped (dependency failure)";
            else if (tc.HttpStatusCode.HasValue)
                actualResult = $"Status = {tc.HttpStatusCode} → {(isPassed ? "PASS" : "FAIL")}";
            else
                actualResult = tc.Status ?? "Unknown";
            sheet.Cell(rowIdx, 9).Value = actualResult;
            sheet.Cell(rowIdx, 9).Style.Font.FontColor = isPassed ? XLColor.Green : (isSkipped ? XLColor.Orange : XLColor.Red);
            sheet.Cell(rowIdx, 9).Style.Font.Bold = !isPassed;

            // Failure Analysis (col 10)
            var failureInfo = new List<string>();
            if (tc.Status == "Failed")
            {
                failureInfo.AddRange(tc.FailureReasons.Select(f => $"[{f.Code}] {f.Message}"));
            }
            else if (tc.Status == "Skipped")
            {
                if (tc.SkippedBecauseDependencyIds != null && tc.SkippedBecauseDependencyIds.Any())
                {
                    failureInfo.Add($"Skipped due to dependency failure: {string.Join(", ", tc.SkippedBecauseDependencyIds.Select(id => id.ToString().Substring(0, 8)))}");
                }
            }
            sheet.Cell(rowIdx, 10).Value = string.Join("\n", failureInfo);

            // Response Preview (col 11)
            sheet.Cell(rowIdx, 11).Value = tc.ResponseBodyPreview;

            // Styling
            sheet.Cell(rowIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(rowIdx, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(rowIdx, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(rowIdx, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Cell(rowIdx, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if (tc.Status == "Passed") sheet.Cell(rowIdx, 3).Style.Font.FontColor = XLColor.Green;
            else if (tc.Status == "Failed") sheet.Cell(rowIdx, 3).Style.Font.FontColor = XLColor.Red;
            else if (tc.Status == "Skipped") sheet.Cell(rowIdx, 3).Style.Font.FontColor = XLColor.Orange;

            if (tc.TotalAttempts > 1)
            {
                sheet.Cell(rowIdx, 6).Style.Font.Bold = true;
                sheet.Cell(rowIdx, 6).Style.Fill.BackgroundColor = XLColor.LightYellow;
            }

            rowIdx++;
        }

        sheet.Columns(1, 6).AdjustToContents();
        sheet.Column(7).Width = 35;
        sheet.Column(8).Width = 35;
        sheet.Column(8).Style.Alignment.WrapText = true;
        sheet.Column(9).Width = 25;
        sheet.Column(10).Width = 60;
        sheet.Column(10).Style.Alignment.WrapText = true;
        sheet.Column(11).Width = 40;
        sheet.Column(11).Style.Alignment.WrapText = true;

        sheet.SheetView.FreezeRows(1);
    }

    private void RenderAttemptsSheet(IXLWorkbook workbook, TestRunReportDocumentModel document)
    {
        if (document.Attempts == null || !document.Attempts.Any()) return;

        var sheet = workbook.Worksheets.Add("Execution Timeline");

        var headers = new[]
        {
            "Test Case Name", "Attempt", "Status", "Duration (ms)",
            "Retry Reason", "Skipped Cause", "Start Time", "End Time", "Detailed Errors"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.DarkSlateBlue;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var caseMap = document.Cases.ToDictionary(x => x.TestCaseId, x => x.Name);

        var rowIdx = 2;
        foreach (var attempt in document.Attempts.OrderBy(a => a.StartedAt))
        {
            caseMap.TryGetValue(attempt.TestCaseId, out var caseName);

            sheet.Cell(rowIdx, 1).Value = caseName ?? attempt.TestCaseId.ToString();
            sheet.Cell(rowIdx, 2).Value = attempt.AttemptNumber;
            sheet.Cell(rowIdx, 3).Value = attempt.Status;
            sheet.Cell(rowIdx, 4).Value = attempt.DurationMs;
            sheet.Cell(rowIdx, 5).Value = attempt.RetryReason;
            sheet.Cell(rowIdx, 6).Value = attempt.SkippedCause;
            sheet.Cell(rowIdx, 7).Value = attempt.StartedAt.ToString("HH:mm:ss.fff");
            sheet.Cell(rowIdx, 8).Value = attempt.CompletedAt?.ToString("HH:mm:ss.fff") ?? "-";

            var errorDetails = string.Join("\n", attempt.FailureReasons.Select(f =>
                $"[{f.Code}] {f.Message}" +
                (string.IsNullOrEmpty(f.Expected) ? "" : $"\n  Expected: {f.Expected}") +
                (string.IsNullOrEmpty(f.Actual) ? "" : $"\n  Actual: {f.Actual}")));

            sheet.Cell(rowIdx, 9).Value = errorDetails;

            // Styling
            if (attempt.Status == "Passed") sheet.Cell(rowIdx, 3).Style.Font.FontColor = XLColor.Green;
            else if (attempt.Status == "Failed") sheet.Cell(rowIdx, 3).Style.Font.FontColor = XLColor.Red;
            else if (attempt.Status == "Skipped") sheet.Cell(rowIdx, 3).Style.Font.FontColor = XLColor.Orange;

            if (attempt.AttemptNumber > 1)
            {
                sheet.Range(rowIdx, 1, rowIdx, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF9C4");
            }

            rowIdx++;
        }

        sheet.Columns(1, 8).AdjustToContents();
        sheet.Column(9).Width = 80;
        sheet.Column(9).Style.Alignment.WrapText = true;
        sheet.SheetView.FreezeRows(1);
    }

    private void RenderBugReportSheet(IXLWorkbook workbook, TestRunReportDocumentModel document)
    {
        var sheet = workbook.Worksheets.Add("Bug Report");

        sheet.Cell(1, 1).Value = "Bug / Defect Report";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.DarkRed;
        sheet.Range(1, 1, 1, 6).Merge();

        var failedCases = document.Cases
            .Where(x => string.Equals(x.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.OrderIndex)
            .ToList();

        if (!failedCases.Any())
        {
            sheet.Cell(3, 1).Value = "No defects found. All executed test cases passed or were skipped.";
            sheet.Cell(3, 1).Style.Font.Italic = true;
            sheet.Cell(3, 1).Style.Font.FontColor = XLColor.Green;
            sheet.Range(3, 1, 3, 6).Merge();
            sheet.Columns().AdjustToContents();
            return;
        }

        var headers = new[] { "Bug ID", "Test Case Name", "Description", "Steps to Reproduce", "Severity", "Status" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(2, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.DarkRed;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var rowIdx = 3;
        var bugIdx = 1;
        foreach (var tc in failedCases)
        {
            var failureSummary = tc.FailureReasons.Any()
                ? string.Join("; ", tc.FailureReasons.Select(f => f.Message))
                : "Test case failed without specific failure reason";

            var steps = $"1. {tc.Request?.HttpMethod ?? "HTTP"} {tc.ResolvedUrl ?? tc.Request?.Url ?? "(unknown URL)"}\n" +
                        $"2. Expected: Status = {tc.Expectation?.ExpectedStatus ?? "N/A"}\n" +
                        $"3. Actual: Status = {tc.HttpStatusCode?.ToString() ?? "N/A"}";

            string severity;
            if (tc.HttpStatusCode.HasValue && tc.HttpStatusCode >= 500)
                severity = "Critical";
            else if (tc.FailureReasons.Any(f => f.Code?.Contains("STATUS_CODE", StringComparison.OrdinalIgnoreCase) == true
                                              || f.Code?.Contains("SCHEMA", StringComparison.OrdinalIgnoreCase) == true))
                severity = "Major";
            else
                severity = "Minor";

            sheet.Cell(rowIdx, 1).Value = $"BUG-{bugIdx:D3}";
            sheet.Cell(rowIdx, 2).Value = tc.Name;
            sheet.Cell(rowIdx, 3).Value = failureSummary;
            sheet.Cell(rowIdx, 4).Value = steps;
            sheet.Cell(rowIdx, 5).Value = severity;
            sheet.Cell(rowIdx, 6).Value = "Open";

            if (severity == "Critical")
            {
                sheet.Cell(rowIdx, 5).Style.Font.FontColor = XLColor.DarkRed;
                sheet.Cell(rowIdx, 5).Style.Font.Bold = true;
                sheet.Range(rowIdx, 1, rowIdx, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF0F0");
            }
            else if (severity == "Major")
            {
                sheet.Cell(rowIdx, 5).Style.Font.FontColor = XLColor.DarkOrange;
            }

            sheet.Cell(rowIdx, 6).Style.Font.FontColor = XLColor.DarkOrange;

            rowIdx++;
            bugIdx++;
        }

        sheet.Columns(1, 3).AdjustToContents();
        sheet.Column(4).Width = 60;
        sheet.Column(4).Style.Alignment.WrapText = true;
        sheet.Column(5).AdjustToContents();
        sheet.Column(6).AdjustToContents();
        sheet.SheetView.FreezeRows(2);
    }

    private void RenderCoverageSheet(IXLWorkbook workbook, TestRunReportDocumentModel document)
    {
        if (document.Coverage == null) return;

        var sheet = workbook.Worksheets.Add("API Coverage");

        // Coverage By Method
        sheet.Cell(1, 1).Value = "Endpoint Coverage By HTTP Method";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Range(1, 1, 1, 2).Merge();

        sheet.Cell(2, 1).Value = "Method";
        sheet.Cell(2, 2).Value = "Coverage %";
        sheet.Range(2, 1, 2, 2).Style.Font.Bold = true;
        sheet.Range(2, 1, 2, 2).Style.Fill.BackgroundColor = XLColor.LightBlue;

        var rowIdx = 3;
        foreach (var method in document.Coverage.ByMethod.OrderBy(x => x.Key))
        {
            sheet.Cell(rowIdx, 1).Value = method.Key;
            sheet.Cell(rowIdx, 2).Value = (double)method.Value / 100.0;
            sheet.Cell(rowIdx, 2).Style.NumberFormat.Format = "0.00%";
            rowIdx++;
        }

        // Coverage By Tag
        rowIdx += 2;
        sheet.Cell(rowIdx, 1).Value = "Endpoint Coverage By Tag";
        sheet.Cell(rowIdx, 1).Style.Font.Bold = true;
        sheet.Cell(rowIdx, 1).Style.Font.FontSize = 14;
        sheet.Range(rowIdx, 1, rowIdx, 2).Merge();
        rowIdx++;

        sheet.Cell(rowIdx, 1).Value = "Tag";
        sheet.Cell(rowIdx, 2).Value = "Coverage %";
        sheet.Range(rowIdx, 1, rowIdx, 2).Style.Font.Bold = true;
        sheet.Range(rowIdx, 1, rowIdx, 2).Style.Fill.BackgroundColor = XLColor.LightBlue;
        rowIdx++;

        foreach (var tag in document.Coverage.ByTag.OrderBy(x => x.Key))
        {
            sheet.Cell(rowIdx, 1).Value = tag.Key;
            sheet.Cell(rowIdx, 2).Value = (double)tag.Value / 100.0;
            sheet.Cell(rowIdx, 2).Style.NumberFormat.Format = "0.00%";
            rowIdx++;
        }

        // Uncovered Paths
        rowIdx += 2;
        sheet.Cell(rowIdx, 1).Value = "Uncovered API Endpoints";
        sheet.Cell(rowIdx, 1).Style.Font.Bold = true;
        sheet.Cell(rowIdx, 1).Style.Font.FontSize = 14;
        sheet.Range(rowIdx, 1, rowIdx, 1).Merge();
        rowIdx++;

        sheet.Cell(rowIdx, 1).Value = "Endpoint";
        sheet.Cell(rowIdx, 1).Style.Font.Bold = true;
        sheet.Cell(rowIdx, 1).Style.Fill.BackgroundColor = XLColor.LightPink;
        rowIdx++;

        foreach (var path in document.Coverage.UncoveredPaths.OrderBy(x => x))
        {
            sheet.Cell(rowIdx, 1).Value = path;
            rowIdx++;
        }

        sheet.Columns().AdjustToContents();
    }
}

