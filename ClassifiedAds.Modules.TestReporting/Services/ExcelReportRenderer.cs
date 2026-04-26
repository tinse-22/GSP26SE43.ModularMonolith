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
        AddSummaryRow(sheet, ref row, "Suite Name", document.SuiteName);
        AddSummaryRow(sheet, ref row, "Run Number", document.Run?.RunNumber);
        AddSummaryRow(sheet, ref row, "Final Status", document.Run?.Status);
        AddSummaryRow(sheet, ref row, "Environment", document.Run?.ResolvedEnvironmentName);
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
            "Retries", "Resolved URL", "Failure Analysis", "Response Preview"
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

            // Failure Analysis
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
            sheet.Cell(rowIdx, 8).Value = string.Join("\n", failureInfo);
            sheet.Cell(rowIdx, 9).Value = tc.ResponseBodyPreview;

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
        sheet.Column(7).Width = 40;
        sheet.Column(8).Width = 60;
        sheet.Column(8).Style.Alignment.WrapText = true;
        sheet.Column(9).Width = 40;
        sheet.Column(9).Style.Alignment.WrapText = true;

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

