using ClassifiedAds.Contracts.Storage.Enums;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Services;

public class CsvReportRenderer : IReportRenderer
{
    public ReportFormat Format => ReportFormat.CSV;

    public Task<RenderedReportFile> RenderAsync(TestRunReportDocumentModel document, CancellationToken ct = default)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        WriteHeader(csv);

        foreach (var row in BuildRows(document))
        {
            WriteRow(csv, row);
        }

        return Task.FromResult(new RenderedReportFile
        {
            Content = Encoding.UTF8.GetBytes(writer.ToString()),
            FileName = $"{document.FileBaseName}.csv",
            ContentType = "text/csv; charset=utf-8",
            FileCategory = FileCategory.Export,
        });
    }

    private static IReadOnlyList<CsvReportRow> BuildRows(TestRunReportDocumentModel document)
    {
        var rows = new List<CsvReportRow>
        {
            CreateSummaryRow("suiteName", document.SuiteName),
            CreateSummaryRow("runNumber", FormatNumber(document.Run?.RunNumber)),
            CreateSummaryRow("runStatus", document.Run?.Status),
            CreateSummaryRow("environment", document.Run?.ResolvedEnvironmentName),
            CreateSummaryRow("totalTests", FormatNumber(document.Run?.TotalTests)),
            CreateSummaryRow("passedCount", FormatNumber(document.Run?.PassedCount)),
            CreateSummaryRow("failedCount", FormatNumber(document.Run?.FailedCount)),
            CreateSummaryRow("skippedCount", FormatNumber(document.Run?.SkippedCount)),
            CreateSummaryRow("durationMs", FormatNumber(document.Run?.DurationMs)),
            CreateSummaryRow("coveragePercent", FormatNumber(document.Coverage?.CoveragePercent)),
            CreateSummaryRow("testedEndpoints", FormatNumber(document.Coverage?.TestedEndpoints)),
            CreateSummaryRow("totalEndpoints", FormatNumber(document.Coverage?.TotalEndpoints)),
        };

        if (document.ReportType == ReportType.Coverage)
        {
            rows.AddRange((document.Coverage?.ByMethod ?? new Dictionary<string, decimal>())
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => new CsvReportRow
                {
                    Section = "coverage_by_method",
                    Key = x.Key,
                    Value = x.Value.ToString(CultureInfo.InvariantCulture),
                }));
            rows.AddRange((document.Coverage?.ByTag ?? new Dictionary<string, decimal>())
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => new CsvReportRow
                {
                    Section = "coverage_by_tag",
                    Key = x.Key,
                    Value = x.Value.ToString(CultureInfo.InvariantCulture),
                }));
            rows.AddRange((document.Coverage?.UncoveredPaths ?? new List<string>())
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(x => new CsvReportRow
                {
                    Section = "uncovered_paths",
                    Key = x,
                }));
        }
        else
        {
            rows.AddRange((document.FailureDistribution ?? new Dictionary<string, int>())
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => new CsvReportRow
                {
                    Section = "failure_distribution",
                    Key = x.Key,
                    Value = x.Value.ToString(CultureInfo.InvariantCulture),
                }));
            rows.AddRange((document.RecentRuns ?? Array.Empty<TestRunHistoryItemDto>())
                .Select(x => new CsvReportRow
                {
                    Section = "recent_runs",
                    Key = x.RunNumber.ToString(CultureInfo.InvariantCulture),
                    Value = x.Status,
                    Name = x.TestRunId.ToString(),
                    Status = x.Status,
                    DurationMs = x.DurationMs,
                    Details = $"passed={x.PassedCount};failed={x.FailedCount};skipped={x.SkippedCount}",
                }));
        }

        if (document.ReportType == ReportType.Detailed)
        {
            rows.AddRange((document.Cases ?? Array.Empty<TestRunReportCaseDocumentModel>())
                .OrderBy(x => x.OrderIndex)
                .ThenBy(x => x.TestCaseId)
                .Select(x => new CsvReportRow
                {
                    Section = "cases",
                    Key = x.TestCaseId.ToString(),
                    Value = x.ResolvedUrl ?? x.Request?.Url,
                    TestCaseId = x.TestCaseId,
                    EndpointId = x.EndpointId,
                    OrderIndex = x.OrderIndex,
                    Name = x.Name,
                    Status = x.Status,
                    HttpStatusCode = x.HttpStatusCode,
                    DurationMs = x.DurationMs,
                    Details = BuildCaseDetails(x),
                    TotalAttempts = x.TotalAttempts,
                }));
        }

        return rows;
    }

    private static CsvReportRow CreateSummaryRow(string key, string value)
    {
        return new CsvReportRow
        {
            Section = "summary",
            Key = key,
            Value = value,
        };
    }

    private static string FormatNumber<T>(T? value)
        where T : struct, IFormattable
    {
        return value.HasValue
            ? value.Value.ToString(null, CultureInfo.InvariantCulture)
            : null;
    }

    private static string BuildCaseDetails(TestRunReportCaseDocumentModel testCase)
    {
        var parts = new List<string>
        {
            $"requestHeaders={FormatDictionary(testCase.RequestHeaders)}",
            $"responseHeaders={FormatDictionary(testCase.ResponseHeaders)}",
            $"extractedVariables={FormatDictionary(testCase.ExtractedVariables)}",
            $"failureReasons={FormatFailures(testCase.FailureReasons)}",
            $"bodyPreview={testCase.ResponseBodyPreview}",
            $"totalAttempts={testCase.TotalAttempts}",
        };

        return string.Join(" | ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string FormatDictionary(IReadOnlyDictionary<string, string> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("; ", values
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Key}={x.Value}"));
    }

    private static string FormatFailures(IReadOnlyList<ReportValidationFailureDto> failureReasons)
    {
        if (failureReasons == null || failureReasons.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("; ", failureReasons
            .OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Target, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Code}:{x.Message}"));
    }

    private static void WriteHeader(CsvWriter csv)
    {
        csv.WriteField("Section");
        csv.WriteField("Key");
        csv.WriteField("Value");
        csv.WriteField("TestCaseId");
        csv.WriteField("EndpointId");
        csv.WriteField("OrderIndex");
        csv.WriteField("Name");
        csv.WriteField("Status");
        csv.WriteField("HttpStatusCode");
        csv.WriteField("DurationMs");
        csv.WriteField("TotalAttempts");
        csv.WriteField("Details");
        csv.NextRecord();
    }

    private static void WriteRow(CsvWriter csv, CsvReportRow row)
    {
        csv.WriteField(row.Section);
        csv.WriteField(row.Key);
        csv.WriteField(row.Value);
        csv.WriteField(row.TestCaseId);
        csv.WriteField(row.EndpointId);
        csv.WriteField(row.OrderIndex);
        csv.WriteField(row.Name);
        csv.WriteField(row.Status);
        csv.WriteField(row.HttpStatusCode);
        csv.WriteField(row.DurationMs);
        csv.WriteField(row.TotalAttempts);
        csv.WriteField(row.Details);
        csv.NextRecord();
    }

    private sealed class CsvReportRow
    {
        public string Section { get; set; }

        public string Key { get; set; }

        public string Value { get; set; }

        public Guid? TestCaseId { get; set; }

        public Guid? EndpointId { get; set; }

        public int? OrderIndex { get; set; }

        public string Name { get; set; }

        public string Status { get; set; }

        public int? HttpStatusCode { get; set; }

        public long? DurationMs { get; set; }

        public int? TotalAttempts { get; set; }

        public string Details { get; set; }
    }
}
