using ClassifiedAds.Contracts.Storage.Enums;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Services;

public class ExcelReportRenderer : IReportRenderer
{
    private static readonly XLColor PrimaryBlue = XLColor.FromHtml("#1F4E78");
    private static readonly XLColor LightBlue = XLColor.FromHtml("#D9EAF7");
    private static readonly XLColor HeaderGray = XLColor.FromHtml("#D9E1F2");
    private static readonly XLColor PaleYellow = XLColor.FromHtml("#FFF2CC");

    public ReportFormat Format => ReportFormat.Excel;

    public async Task<RenderedReportFile> RenderAsync(TestRunReportDocumentModel document, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();
        var groups = BuildGroups(document);

        RenderCoverSheet(workbook, document);
        RenderTestCasesIndexSheet(workbook, document, groups);
        RenderStatisticsSheet(workbook, document, groups);

        foreach (var group in groups)
        {
            RenderGroupSheet(workbook, document, group);
        }

        RenderAttemptsSheet(workbook, document);
        RenderCoverageSheet(workbook, document);
        RenderBugReportSheet(workbook, document);

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

    private static IReadOnlyList<ReportGroup> BuildGroups(TestRunReportDocumentModel document)
    {
        var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cover",
            "Test Cases",
            "Test Statistics",
            "Execution Timeline",
            "API Coverage",
            "Bug Report",
        };

        var cases = document.Cases ?? Array.Empty<TestRunReportCaseDocumentModel>();
        var groups = cases
            .GroupBy(testCase => ResolveGroupName(testCase.TestType))
            .OrderBy(group => group.Min(testCase => testCase.OrderIndex))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var displayName = group.Key;
                return new ReportGroup
                {
                    DisplayName = displayName,
                    SheetName = CreateUniqueSheetName(displayName, usedSheetNames),
                    Cases = group.OrderBy(testCase => testCase.OrderIndex).ToArray(),
                };
            })
            .ToArray();

        return groups.Length > 0
            ? groups
            : new[]
            {
                new ReportGroup
                {
                    DisplayName = "Runtime",
                    SheetName = CreateUniqueSheetName("Runtime", usedSheetNames),
                    Cases = Array.Empty<TestRunReportCaseDocumentModel>(),
                },
            };
    }

    private static void RenderCoverSheet(IXLWorkbook workbook, TestRunReportDocumentModel document)
    {
        var sheet = workbook.Worksheets.Add("Cover");
        ApplyBaseSheetStyle(sheet);

        sheet.Cell(2, 2).Value = "TEST REPORT DOCUMENT";
        sheet.Range(2, 2, 2, 6).Merge();
        var titleRange = sheet.Range(2, 2, 2, 6);
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 18;
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Fill.BackgroundColor = PrimaryBlue;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        AddCoverRow(sheet, 4, "Project Name", document.ProjectName ?? "(unknown)", "Creator", "ClassifiedAds runtime export");
        AddCoverRow(sheet, 5, "Project Code", document.ProjectId.ToString(), "Issue Date", document.GeneratedAt.DateTime);
        AddCoverRow(sheet, 6, "Document Code", $"TEST-RUN-{document.Run?.RunNumber ?? 0}", "Version", "2.1-AniMusicUI-Runtime");
        AddCoverRow(sheet, 8, "Suite Name", document.SuiteName ?? "(unknown)", "Report Type", document.ReportType.ToString());
        AddCoverRow(sheet, 9, "Run Status", document.Run?.Status ?? "(unknown)", "Environment", document.Run?.ResolvedEnvironmentName ?? "(unknown)");
        AddCoverRow(sheet, 10, "Executed At", FormatDateTime(document.Run?.ExecutedAt), "Generated At", FormatDateTime(document.GeneratedAt));

        sheet.Cell(12, 1).Value = "Record of change";
        sheet.Range(12, 1, 12, 6).Merge();
        ApplySectionHeader(sheet.Range(12, 1, 12, 6));

        var headers = new[] { "Effective Date", "Version", "Change Item", "*A,D,M", "Change description", "Reference" };
        WriteHeaderRow(sheet, 13, 1, headers, PrimaryBlue);

        sheet.Cell(14, 1).Value = document.GeneratedAt.DateTime;
        sheet.Cell(14, 1).Style.NumberFormat.Format = "yyyy-mm-dd";
        sheet.Cell(14, 2).Value = "2.1";
        sheet.Cell(14, 3).Value = "Use AniMusic workbook UI";
        sheet.Cell(14, 4).Value = "M";
        sheet.Cell(14, 5).Value = "Runtime Excel export rebuilt from the current test run report context.";
        sheet.Cell(14, 6).Value = "TestRunReportDocumentModel";
        sheet.Range(14, 1, 14, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(14, 1, 14, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        sheet.Column(1).Width = 24;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 34;
        sheet.Column(4).Width = 10;
        sheet.Column(5).Width = 34;
        sheet.Column(6).Width = 34;
        sheet.RangeUsed()?.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.RangeUsed()?.Style.Alignment.SetWrapText();
    }

    private static void RenderTestCasesIndexSheet(IXLWorkbook workbook, TestRunReportDocumentModel document, IReadOnlyList<ReportGroup> groups)
    {
        var sheet = workbook.Worksheets.Add("Test Cases");
        ApplyBaseSheetStyle(sheet);

        sheet.Cell(1, 4).Value = "TEST CASE LIST";
        sheet.Range(1, 4, 1, 6).Merge();
        ApplyTitle(sheet.Range(1, 4, 1, 6));

        AddInfoPair(sheet, 3, "Project Name", document.ProjectName ?? "(unknown)");
        AddInfoPair(sheet, 4, "Project Code", document.ProjectId.ToString());
        AddInfoPair(sheet, 5, "Test Environment Setup Description", BuildEnvironmentDescription(document));

        var headers = new[] { "No", "Function Name", "Sheet Name", "Description", "Pre-Condition" };
        WriteHeaderRow(sheet, 8, 2, headers, PrimaryBlue);

        var row = 9;
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            sheet.Cell(row, 2).Value = i + 1;
            sheet.Cell(row, 3).Value = group.DisplayName;
            sheet.Cell(row, 4).Value = group.SheetName;
            sheet.Cell(row, 5).Value = BuildGroupDescription(group);
            sheet.Cell(row, 6).Value = "Generated from runtime test run data; Round 1 cells reflect execution status.";
            sheet.Range(row, 2, row, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Range(row, 2, row, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            row++;
        }

        sheet.Column(1).Width = 3;
        sheet.Column(2).Width = 10;
        sheet.Column(3).Width = 26;
        sheet.Column(4).Width = 30;
        sheet.Column(5).Width = 56;
        sheet.Column(6).Width = 42;
        sheet.RangeUsed()?.Style.Alignment.SetWrapText();
        sheet.SheetView.FreezeRows(8);
    }

    private static void RenderStatisticsSheet(IXLWorkbook workbook, TestRunReportDocumentModel document, IReadOnlyList<ReportGroup> groups)
    {
        var sheet = workbook.Worksheets.Add("Test Statistics");
        ApplyBaseSheetStyle(sheet);

        sheet.Cell(1, 2).Value = "TEST STATISTICS";
        sheet.Range(1, 2, 1, 8).Merge();
        ApplyTitle(sheet.Range(1, 2, 1, 8));

        AddStatsInfoPair(sheet, 3, "Project Name", document.ProjectName ?? "(unknown)", "Creator", "ClassifiedAds runtime export");
        AddStatsInfoPair(sheet, 4, "Project Code", document.ProjectId.ToString(), "Reviewer/Approver", "TBD");
        AddStatsInfoPair(sheet, 5, "Document Code", $"TEST-RUN-{document.Run?.RunNumber ?? 0}", "Issue Date", document.GeneratedAt.DateTime);
        AddStatsInfoPair(sheet, 6, "Notes", "Status formulas aggregate Round 1 cells from generated runtime feature sheets.", null, null);

        var headers = new[] { "No", "Module code", "Passed", "Failed", "Pending", "N/A", "Number of test cases", "Notes" };
        WriteHeaderRow(sheet, 10, 2, headers, PrimaryBlue);

        var row = 11;
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            sheet.Cell(row, 2).Value = i + 1;
            sheet.Cell(row, 3).FormulaA1 = $"'{EscapeFormulaSheetName(group.SheetName)}'!B2";
            sheet.Cell(row, 4).FormulaA1 = $"'{EscapeFormulaSheetName(group.SheetName)}'!B6";
            sheet.Cell(row, 5).FormulaA1 = $"'{EscapeFormulaSheetName(group.SheetName)}'!C6";
            sheet.Cell(row, 6).FormulaA1 = $"'{EscapeFormulaSheetName(group.SheetName)}'!D6";
            sheet.Cell(row, 7).FormulaA1 = $"'{EscapeFormulaSheetName(group.SheetName)}'!E6";
            sheet.Cell(row, 8).FormulaA1 = $"'{EscapeFormulaSheetName(group.SheetName)}'!B4";
            sheet.Cell(row, 9).Value = BuildGroupDescription(group);
            sheet.Range(row, 2, row, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Range(row, 2, row, 9).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            row++;
        }

        sheet.Cell(row, 3).Value = "TOTAL";
        sheet.Cell(row, 4).FormulaA1 = $"SUM(D11:D{row - 1})";
        sheet.Cell(row, 5).FormulaA1 = $"SUM(E11:E{row - 1})";
        sheet.Cell(row, 6).FormulaA1 = $"SUM(F11:F{row - 1})";
        sheet.Cell(row, 7).FormulaA1 = $"SUM(G11:G{row - 1})";
        sheet.Cell(row, 8).FormulaA1 = $"SUM(H11:H{row - 1})";
        sheet.Range(row, 3, row, 8).Style.Font.Bold = true;
        sheet.Range(row, 3, row, 8).Style.Fill.BackgroundColor = PaleYellow;
        sheet.Range(row, 3, row, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 3, row, 8).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        sheet.Column(1).Width = 4;
        sheet.Column(2).Width = 10;
        sheet.Column(3).Width = 28;
        sheet.Columns(4, 8).Width = 14;
        sheet.Column(9).Width = 44;
        sheet.RangeUsed()?.Style.Alignment.SetWrapText();
        sheet.SheetView.FreezeRows(10);
    }

    private static void RenderGroupSheet(IXLWorkbook workbook, TestRunReportDocumentModel document, ReportGroup group)
    {
        var sheet = workbook.Worksheets.Add(group.SheetName);
        ApplyBaseSheetStyle(sheet);

        sheet.Cell(2, 1).Value = "Feature";
        sheet.Cell(2, 2).Value = group.DisplayName;
        sheet.Range(2, 2, 2, 5).Merge();
        sheet.Cell(3, 1).Value = "Test requirement";
        sheet.Cell(3, 2).Value = BuildGroupDescription(group);
        sheet.Range(3, 2, 3, 5).Merge();
        sheet.Cell(4, 1).Value = "Number of TCs";
        sheet.Cell(4, 2).Value = group.Cases.Count;
        sheet.Range(2, 1, 4, 1).Style.Font.Bold = true;
        sheet.Range(2, 1, 4, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(2, 1, 4, 5).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        WriteHeaderRow(sheet, 5, 1, new[] { "Testing Round", "Passed", "Failed", "Pending", "N/A" }, PrimaryBlue);
        sheet.Cell(6, 1).Value = "Round 1";
        sheet.Cell(6, 2).FormulaA1 = "COUNTIF($F$10:$F$5000,B$5)";
        sheet.Cell(6, 3).FormulaA1 = "COUNTIF($F$10:$F$5000,C$5)";
        sheet.Cell(6, 4).FormulaA1 = "COUNTIF($F$10:$F$5000,D$5)";
        sheet.Cell(6, 5).FormulaA1 = "COUNTIF($F$10:$F$5000,E$5)";
        sheet.Range(6, 1, 6, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(6, 1, 6, 5).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var headers = new[]
        {
            "Test Case ID", "Test Case Description", "Test Case Procedure", "Expected Results", "Pre-conditions",
            "Round 1", "Test date", "Tester", "Round 2", "Test date", "Tester", "Round 3", "Test date", "Tester",
            "Actual Result", "Failure Analysis", "Response Preview", "Duration (ms)"
        };
        WriteHeaderRow(sheet, 10, 1, headers, PrimaryBlue);

        var row = 11;
        sheet.Cell(row, 1).Value = "Function A: Runtime test cases";
        sheet.Range(row, 1, row, headers.Length).Merge();
        ApplySectionHeader(sheet.Range(row, 1, row, headers.Length));
        row++;

        foreach (var testCase in group.Cases)
        {
            var mappedStatus = MapStatus(testCase.Status);
            sheet.Cell(row, 1).Value = BuildRuntimeTestCaseId(document, testCase);
            sheet.Cell(row, 2).Value = testCase.Name ?? "(unnamed test case)";
            sheet.Cell(row, 3).Value = BuildProcedure(testCase);
            sheet.Cell(row, 4).Value = BuildExpectedResult(testCase);
            sheet.Cell(row, 5).Value = BuildPreconditions(testCase);
            sheet.Cell(row, 6).Value = mappedStatus;
            sheet.Cell(row, 7).Value = document.Run?.ExecutedAt.DateTime ?? document.GeneratedAt.DateTime;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "yyyy-mm-dd";
            sheet.Cell(row, 8).Value = "QA Team";
            sheet.Cell(row, 15).Value = BuildActualResult(testCase);
            sheet.Cell(row, 16).Value = BuildFailureAnalysis(testCase);
            sheet.Cell(row, 17).Value = testCase.ResponseBodyPreview;
            sheet.Cell(row, 18).Value = testCase.DurationMs;

            ApplyMappedStatusStyle(sheet.Cell(row, 6), mappedStatus);
            sheet.Range(row, 1, row, headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Range(row, 1, row, headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            row++;
        }

        sheet.Column(1).Width = 18;
        sheet.Column(2).Width = 42;
        sheet.Column(3).Width = 44;
        sheet.Column(4).Width = 42;
        sheet.Column(5).Width = 34;
        sheet.Column(6).Width = 12;
        sheet.Column(7).Width = 14;
        sheet.Column(8).Width = 16;
        sheet.Columns(9, 14).Width = 12;
        sheet.Column(15).Width = 32;
        sheet.Column(16).Width = 48;
        sheet.Column(17).Width = 42;
        sheet.Column(18).Width = 14;
        sheet.RangeUsed()?.Style.Alignment.SetWrapText();
        sheet.RangeUsed()?.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.SheetView.FreezeRows(10);
    }

    private static void RenderAttemptsSheet(IXLWorkbook workbook, TestRunReportDocumentModel document)
    {
        var sheet = workbook.Worksheets.Add("Execution Timeline");
        ApplyBaseSheetStyle(sheet);

        sheet.Cell(1, 1).Value = "EXECUTION TIMELINE";
        sheet.Range(1, 1, 1, 9).Merge();
        ApplyTitle(sheet.Range(1, 1, 1, 9));

        var headers = new[]
        {
            "Test Case Name", "Attempt", "Status", "Duration (ms)",
            "Retry Reason", "Skipped Cause", "Start Time", "End Time", "Detailed Errors"
        };
        WriteHeaderRow(sheet, 3, 1, headers, PrimaryBlue);

        var caseMap = (document.Cases ?? Array.Empty<TestRunReportCaseDocumentModel>())
            .GroupBy(testCase => testCase.TestCaseId)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var row = 4;
        var attempts = document.Attempts ?? Array.Empty<TestRunExecutionAttemptDto>();
        foreach (var attempt in attempts.OrderBy(attempt => attempt.StartedAt))
        {
            caseMap.TryGetValue(attempt.TestCaseId, out var caseName);
            sheet.Cell(row, 1).Value = caseName ?? attempt.TestCaseId.ToString();
            sheet.Cell(row, 2).Value = attempt.AttemptNumber;
            sheet.Cell(row, 3).Value = MapStatus(attempt.Status);
            sheet.Cell(row, 4).Value = attempt.DurationMs;
            sheet.Cell(row, 5).Value = attempt.RetryReason;
            sheet.Cell(row, 6).Value = attempt.SkippedCause;
            sheet.Cell(row, 7).Value = attempt.StartedAt.DateTime;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "hh:mm:ss.000";
            sheet.Cell(row, 8).Value = attempt.CompletedAt?.DateTime;
            sheet.Cell(row, 8).Style.NumberFormat.Format = "hh:mm:ss.000";
            sheet.Cell(row, 9).Value = string.Join("\n", attempt.FailureReasons.Select(FormatFailureReason));
            ApplyMappedStatusStyle(sheet.Cell(row, 3), sheet.Cell(row, 3).GetString());
            sheet.Range(row, 1, row, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Range(row, 1, row, 9).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            row++;
        }

        if (row == 4)
        {
            sheet.Cell(row, 1).Value = "No execution attempts were available in the runtime report context.";
            sheet.Range(row, 1, row, 9).Merge();
        }

        sheet.Columns(1, 8).AdjustToContents();
        sheet.Column(9).Width = 70;
        sheet.RangeUsed()?.Style.Alignment.SetWrapText();
        sheet.SheetView.FreezeRows(3);
    }

    private static void RenderCoverageSheet(IXLWorkbook workbook, TestRunReportDocumentModel document)
    {
        var sheet = workbook.Worksheets.Add("API Coverage");
        ApplyBaseSheetStyle(sheet);

        sheet.Cell(1, 1).Value = "API COVERAGE";
        sheet.Range(1, 1, 1, 4).Merge();
        ApplyTitle(sheet.Range(1, 1, 1, 4));

        var coverage = document.Coverage;
        if (coverage == null)
        {
            sheet.Cell(3, 1).Value = "No API coverage data was available in the runtime report context.";
            sheet.Range(3, 1, 3, 4).Merge();
            sheet.Columns().AdjustToContents();
            return;
        }

        WriteHeaderRow(sheet, 3, 1, new[] { "Metric", "Value" }, PrimaryBlue);
        sheet.Cell(4, 1).Value = "Coverage Percent";
        sheet.Cell(4, 2).Value = (double)coverage.CoveragePercent / 100.0;
        sheet.Cell(4, 2).Style.NumberFormat.Format = "0.00%";
        sheet.Cell(5, 1).Value = "Tested Endpoints";
        sheet.Cell(5, 2).Value = coverage.TestedEndpoints;
        sheet.Cell(6, 1).Value = "Total Endpoints";
        sheet.Cell(6, 2).Value = coverage.TotalEndpoints;
        sheet.Range(4, 1, 6, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(4, 1, 6, 2).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        sheet.Cell(8, 1).Value = "Endpoint Coverage By HTTP Method";
        sheet.Range(8, 1, 8, 2).Merge();
        ApplySectionHeader(sheet.Range(8, 1, 8, 2));
        WriteHeaderRow(sheet, 9, 1, new[] { "Method", "Coverage %" }, PrimaryBlue);
        var row = 10;
        foreach (var method in coverage.ByMethod.OrderBy(x => x.Key))
        {
            sheet.Cell(row, 1).Value = method.Key;
            sheet.Cell(row, 2).Value = (double)method.Value / 100.0;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "0.00%";
            row++;
        }

        row += 2;
        sheet.Cell(row, 1).Value = "Endpoint Coverage By Tag";
        sheet.Range(row, 1, row, 2).Merge();
        ApplySectionHeader(sheet.Range(row, 1, row, 2));
        row++;
        WriteHeaderRow(sheet, row, 1, new[] { "Tag", "Coverage %" }, PrimaryBlue);
        row++;
        foreach (var tag in coverage.ByTag.OrderBy(x => x.Key))
        {
            sheet.Cell(row, 1).Value = tag.Key;
            sheet.Cell(row, 2).Value = (double)tag.Value / 100.0;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "0.00%";
            row++;
        }

        row += 2;
        sheet.Cell(row, 1).Value = "Uncovered API Endpoints";
        sheet.Range(row, 1, row, 4).Merge();
        ApplySectionHeader(sheet.Range(row, 1, row, 4));
        row++;
        WriteHeaderRow(sheet, row, 1, new[] { "Endpoint" }, PrimaryBlue);
        row++;
        foreach (var path in coverage.UncoveredPaths.OrderBy(x => x))
        {
            sheet.Cell(row, 1).Value = path;
            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.Column(1).Width = Math.Max(sheet.Column(1).Width, 32);
    }

    private static void RenderBugReportSheet(IXLWorkbook workbook, TestRunReportDocumentModel document)
    {
        var sheet = workbook.Worksheets.Add("Bug Report");
        ApplyBaseSheetStyle(sheet);

        sheet.Cell(1, 1).Value = "BUG / DEFECT REPORT";
        sheet.Range(1, 1, 1, 7).Merge();
        ApplyTitle(sheet.Range(1, 1, 1, 7));

        var failedCases = (document.Cases ?? Array.Empty<TestRunReportCaseDocumentModel>())
            .Where(testCase => string.Equals(testCase.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            .OrderBy(testCase => testCase.OrderIndex)
            .ToArray();

        WriteHeaderRow(sheet, 3, 1, new[] { "Bug ID", "Test Case Name", "Description", "Steps to Reproduce", "Severity", "Status", "Failure Code" }, PrimaryBlue);

        if (failedCases.Length == 0)
        {
            sheet.Cell(4, 1).Value = "No defects found. All executed test cases passed, were skipped, or are pending.";
            sheet.Range(4, 1, 4, 7).Merge();
            sheet.Range(4, 1, 4, 7).Style.Font.Italic = true;
            sheet.Range(4, 1, 4, 7).Style.Font.FontColor = XLColor.Green;
            sheet.Columns().AdjustToContents();
            return;
        }

        var row = 4;
        for (var i = 0; i < failedCases.Length; i++)
        {
            var testCase = failedCases[i];
            var failureSummary = BuildFailureAnalysis(testCase);
            var primaryFailure = testCase.FailureReasons.FirstOrDefault();

            sheet.Cell(row, 1).Value = $"BUG-{i + 1:D3}";
            sheet.Cell(row, 2).Value = testCase.Name ?? "(unnamed test case)";
            sheet.Cell(row, 3).Value = string.IsNullOrWhiteSpace(failureSummary)
                ? "Test case failed without a specific failure reason."
                : failureSummary;
            sheet.Cell(row, 4).Value = BuildBugSteps(testCase);
            sheet.Cell(row, 5).Value = ResolveSeverity(testCase);
            sheet.Cell(row, 6).Value = "Open";
            sheet.Cell(row, 7).Value = primaryFailure?.Code;

            if (sheet.Cell(row, 5).GetString() == "Critical")
            {
                sheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#FCE4D6");
            }

            sheet.Range(row, 1, row, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Range(row, 1, row, 7).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            row++;
        }

        sheet.Columns(1, 2).AdjustToContents();
        sheet.Column(3).Width = 48;
        sheet.Column(4).Width = 56;
        sheet.Columns(5, 7).AdjustToContents();
        sheet.RangeUsed()?.Style.Alignment.SetWrapText();
        sheet.SheetView.FreezeRows(3);
    }

    private static void AddCoverRow(IXLWorksheet sheet, int row, string leftLabel, object leftValue, string rightLabel, object rightValue)
    {
        sheet.Cell(row, 1).Value = leftLabel;
        sheet.Cell(row, 2).Value = XLCellValue.FromObject(leftValue);
        sheet.Range(row, 2, row, 4).Merge();
        sheet.Cell(row, 5).Value = rightLabel;
        sheet.Cell(row, 6).Value = XLCellValue.FromObject(rightValue);
        sheet.Range(row, 1, row, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 1, row, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 1, row, 1).Style.Font.Bold = true;
        sheet.Range(row, 5, row, 5).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 1).Style.Fill.BackgroundColor = HeaderGray;
        sheet.Range(row, 5, row, 5).Style.Fill.BackgroundColor = HeaderGray;
    }

    private static void AddInfoPair(IXLWorksheet sheet, int row, string label, object value)
    {
        sheet.Cell(row, 2).Value = label;
        sheet.Range(row, 2, row, 3).Merge();
        sheet.Cell(row, 4).Value = XLCellValue.FromObject(value);
        sheet.Range(row, 4, row, 6).Merge();
        sheet.Range(row, 2, row, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 2, row, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 2, row, 3).Style.Font.Bold = true;
        sheet.Range(row, 2, row, 3).Style.Fill.BackgroundColor = HeaderGray;
    }

    private static void AddStatsInfoPair(IXLWorksheet sheet, int row, string leftLabel, object leftValue, string rightLabel, object rightValue)
    {
        sheet.Cell(row, 2).Value = leftLabel;
        sheet.Cell(row, 3).Value = XLCellValue.FromObject(leftValue);
        sheet.Range(row, 3, row, 4).Merge();
        sheet.Range(row, 2, row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(row, 2, row, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Cell(row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 2).Style.Fill.BackgroundColor = HeaderGray;

        if (!string.IsNullOrWhiteSpace(rightLabel))
        {
            sheet.Cell(row, 5).Value = rightLabel;
            sheet.Cell(row, 7).Value = XLCellValue.FromObject(rightValue);
            sheet.Range(row, 5, row, 6).Merge();
            sheet.Range(row, 7, row, 8).Merge();
            sheet.Range(row, 5, row, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Range(row, 5, row, 8).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            sheet.Range(row, 5, row, 6).Style.Font.Bold = true;
            sheet.Range(row, 5, row, 6).Style.Fill.BackgroundColor = HeaderGray;
        }
    }

    private static void WriteHeaderRow(IXLWorksheet sheet, int row, int startColumn, IReadOnlyList<string> headers, XLColor backgroundColor)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(row, startColumn + i);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = backgroundColor;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
    }

    private static void ApplyBaseSheetStyle(IXLWorksheet sheet)
    {
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = 10;
        sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void ApplyTitle(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 16;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = PrimaryBlue;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }

    private static void ApplySectionHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = LightBlue;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void ApplyMappedStatusStyle(IXLCell cell, string mappedStatus)
    {
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Font.Bold = true;

        switch (mappedStatus)
        {
            case "Passed":
                cell.Style.Font.FontColor = XLColor.Green;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2F0D9");
                break;
            case "Failed":
                cell.Style.Font.FontColor = XLColor.DarkRed;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FCE4D6");
                break;
            case "N/A":
                cell.Style.Font.FontColor = XLColor.Gray;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7E6E6");
                break;
            default:
                cell.Style.Font.FontColor = XLColor.DarkOrange;
                cell.Style.Fill.BackgroundColor = PaleYellow;
                break;
        }
    }

    private static string MapStatus(string status)
    {
        if (string.Equals(status, "Passed", StringComparison.OrdinalIgnoreCase))
        {
            return "Passed";
        }

        if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return "Failed";
        }

        if (string.Equals(status, "Skipped", StringComparison.OrdinalIgnoreCase))
        {
            return "N/A";
        }

        return "Pending";
    }

    private static string ResolveGroupName(string testType)
    {
        return string.IsNullOrWhiteSpace(testType)
            ? "Runtime"
            : testType.Trim();
    }

    private static string CreateUniqueSheetName(string preferredName, ISet<string> usedSheetNames)
    {
        var baseName = SanitizeSheetName(preferredName);
        var sheetName = baseName;
        var suffix = 1;
        while (usedSheetNames.Contains(sheetName))
        {
            var suffixText = $" {suffix}";
            var maxBaseLength = 31 - suffixText.Length;
            sheetName = $"{baseName[..Math.Min(baseName.Length, maxBaseLength)]}{suffixText}";
            suffix++;
        }

        usedSheetNames.Add(sheetName);
        return sheetName;
    }

    private static string SanitizeSheetName(string value)
    {
        var invalidChars = new HashSet<char> { '[', ']', ':', '*', '?', '/', '\\' };
        var sanitized = new string((value ?? "Runtime")
            .Select(ch => invalidChars.Contains(ch) || char.IsControl(ch) ? '-' : ch)
            .ToArray())
            .Trim(' ', '\'');

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Runtime";
        }

        return sanitized.Length > 31 ? sanitized[..31] : sanitized;
    }

    private static string EscapeFormulaSheetName(string sheetName)
    {
        return sheetName.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string BuildEnvironmentDescription(TestRunReportDocumentModel document)
    {
        var parts = new[]
        {
            $"Suite: {document.SuiteName ?? "(unknown)"}",
            $"Environment: {document.Run?.ResolvedEnvironmentName ?? "(unknown)"}",
            $"Run number: {document.Run?.RunNumber.ToString() ?? "(unknown)"}",
        };

        return string.Join("\n", parts.Select((part, index) => $"{index + 1}. {part}"));
    }

    private static string BuildGroupDescription(ReportGroup group)
    {
        return $"{group.DisplayName} runtime test cases generated from the current test run ({group.Cases.Count} case(s)).";
    }

    private static string BuildRuntimeTestCaseId(TestRunReportDocumentModel document, TestRunReportCaseDocumentModel testCase)
    {
        var runNumber = document.Run?.RunNumber ?? 0;
        return $"RUN-{runNumber:D4}-{testCase.OrderIndex:D3}";
    }

    private static string BuildProcedure(TestRunReportCaseDocumentModel testCase)
    {
        var method = testCase.Request?.HttpMethod ?? "HTTP";
        var url = testCase.ResolvedUrl ?? testCase.Request?.Url ?? "(unknown URL)";
        return $"1. Send {method} request to {url}\n2. Capture response status, headers, body preview, and duration\n3. Compare runtime result with configured expectation";
    }

    private static string BuildExpectedResult(TestRunReportCaseDocumentModel testCase)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(testCase.Expectation?.ExpectedStatus))
        {
            parts.Add($"Status = {testCase.Expectation.ExpectedStatus}");
        }

        if (testCase.Expectation?.MaxResponseTime.HasValue == true)
        {
            parts.Add($"Response time <= {testCase.Expectation.MaxResponseTime}ms");
        }

        if (!string.IsNullOrWhiteSpace(testCase.Expectation?.BodyContains) && testCase.Expectation.BodyContains != "[]")
        {
            parts.Add($"Body contains: {testCase.Expectation.BodyContains}");
        }

        return parts.Count > 0
            ? string.Join("\n", parts)
            : "Runtime execution should complete according to the generated test case definition.";
    }

    private static string BuildPreconditions(TestRunReportCaseDocumentModel testCase)
    {
        var parts = new List<string>();
        if (testCase.DependencyIds.Count > 0)
        {
            parts.Add($"Dependencies completed: {string.Join(", ", testCase.DependencyIds.Select(ShortGuid))}");
        }

        if (!string.IsNullOrWhiteSpace(testCase.Request?.Headers))
        {
            parts.Add("Configured request headers are available.");
        }

        return parts.Count > 0
            ? string.Join("\n", parts)
            : "Runtime environment and test data are available.";
    }

    private static string BuildActualResult(TestRunReportCaseDocumentModel testCase)
    {
        var mappedStatus = MapStatus(testCase.Status);
        if (testCase.HttpStatusCode.HasValue)
        {
            return $"Status = {testCase.HttpStatusCode} -> {mappedStatus}";
        }

        return mappedStatus;
    }

    private static string BuildFailureAnalysis(TestRunReportCaseDocumentModel testCase)
    {
        if (testCase.FailureReasons.Count > 0)
        {
            return string.Join("\n", testCase.FailureReasons.Select(FormatFailureReason));
        }

        if (string.Equals(testCase.Status, "Skipped", StringComparison.OrdinalIgnoreCase) && testCase.SkippedBecauseDependencyIds.Count > 0)
        {
            return $"Skipped because dependency test case(s) did not pass: {string.Join(", ", testCase.SkippedBecauseDependencyIds.Select(ShortGuid))}";
        }

        return string.Empty;
    }

    private static string BuildBugSteps(TestRunReportCaseDocumentModel testCase)
    {
        return $"1. {testCase.Request?.HttpMethod ?? "HTTP"} {testCase.ResolvedUrl ?? testCase.Request?.Url ?? "(unknown URL)"}\n" +
               $"2. Expected: {BuildExpectedResult(testCase)}\n" +
               $"3. Actual: {BuildActualResult(testCase)}";
    }

    private static string ResolveSeverity(TestRunReportCaseDocumentModel testCase)
    {
        if (testCase.HttpStatusCode.HasValue && testCase.HttpStatusCode >= 500)
        {
            return "Critical";
        }

        if (testCase.FailureReasons.Any(failure =>
                failure.Code?.Contains("STATUS_CODE", StringComparison.OrdinalIgnoreCase) == true ||
                failure.Code?.Contains("SCHEMA", StringComparison.OrdinalIgnoreCase) == true))
        {
            return "Major";
        }

        return "Minor";
    }

    private static string FormatFailureReason(ReportValidationFailureDto failure)
    {
        var detail = $"[{failure.Code}] {failure.Message}";
        if (!string.IsNullOrWhiteSpace(failure.Expected))
        {
            detail += $"\nExpected: {failure.Expected}";
        }

        if (!string.IsNullOrWhiteSpace(failure.Actual))
        {
            detail += $"\nActual: {failure.Actual}";
        }

        return detail;
    }

    private static string ShortGuid(Guid value)
    {
        return value.ToString("N")[..8];
    }

    private static string FormatDateTime(DateTimeOffset? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "(unknown)";
    }

    private sealed class ReportGroup
    {
        public string DisplayName { get; init; }

        public string SheetName { get; init; }

        public IReadOnlyList<TestRunReportCaseDocumentModel> Cases { get; init; } = Array.Empty<TestRunReportCaseDocumentModel>();
    }
}
