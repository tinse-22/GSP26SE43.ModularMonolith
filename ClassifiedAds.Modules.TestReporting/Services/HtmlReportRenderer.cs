using ClassifiedAds.Contracts.Storage.Enums;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using RazorLight;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Services;

public class HtmlReportRenderer : IReportRenderer
{
    private const string Template = """
@using System.Linq
@using ClassifiedAds.Modules.TestReporting.Entities
@model ClassifiedAds.Modules.TestReporting.Models.TestRunReportDocumentModel
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>@Model.ReportType Test Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 24px; color: #1f2933; }
        h1, h2, h3 { color: #102a43; }
        table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }
        th, td { border: 1px solid #d9e2ec; padding: 8px; text-align: left; vertical-align: top; }
        th { background: #f0f4f8; }
        .card { border: 1px solid #d9e2ec; border-radius: 6px; padding: 16px; margin-bottom: 16px; }
        .meta { color: #52606d; margin-bottom: 8px; }
        .pill { display: inline-block; padding: 2px 8px; border-radius: 999px; background: #d9e2ec; margin-right: 8px; }
        pre { background: #f8fafc; padding: 12px; border-radius: 6px; overflow-x: auto; white-space: pre-wrap; }
        ul { margin-top: 8px; }
    </style>
</head>
<body>
    <h1>@Model.ReportType Test Report</h1>
    <p class="meta">Generated @Model.GeneratedAt.UtcDateTime.ToString("u") for suite @Model.SuiteName</p>

    <section>
        <h2>Run Summary</h2>
        <table>
            <tbody>
                <tr><th>Run Number</th><td>@Model.Run.RunNumber</td><th>Status</th><td>@Model.Run.Status</td></tr>
                <tr><th>Environment</th><td>@Model.Run.ResolvedEnvironmentName</td><th>Duration (ms)</th><td>@Model.Run.DurationMs</td></tr>
                <tr><th>Total Tests</th><td>@Model.Run.TotalTests</td><th>Passed / Failed / Skipped</th><td>@Model.Run.PassedCount / @Model.Run.FailedCount / @Model.Run.SkippedCount</td></tr>
                <tr><th>Started At</th><td>@Model.Run.StartedAt</td><th>Completed At</th><td>@Model.Run.CompletedAt</td></tr>
            </tbody>
        </table>
    </section>

    <section>
        <h2>Coverage Summary</h2>
        <table>
            <tbody>
                <tr><th>Total Endpoints</th><td>@Model.Coverage.TotalEndpoints</td><th>Tested Endpoints</th><td>@Model.Coverage.TestedEndpoints</td></tr>
                <tr><th>Coverage Percent</th><td>@Model.Coverage.CoveragePercent%</td><th>Calculated At</th><td>@Model.Coverage.CalculatedAt.UtcDateTime.ToString("u")</td></tr>
            </tbody>
        </table>
    </section>

    @if (Model.ReportType != ReportType.Coverage)
    {
        <section>
            <h2>Failure Distribution</h2>
            @if (Model.FailureDistribution == null || Model.FailureDistribution.Count == 0)
            {
                <p>No failure reasons recorded for this run.</p>
            }
            else
            {
                <table>
                    <thead>
                        <tr><th>Failure Code</th><th>Count</th></tr>
                    </thead>
                    <tbody>
                    @foreach (var item in Model.FailureDistribution)
                    {
                        <tr><td>@item.Key</td><td>@item.Value</td></tr>
                    }
                    </tbody>
                </table>
            }
        </section>

        <section>
            <h2>Recent Run History</h2>
            @if (Model.RecentRuns == null || Model.RecentRuns.Count == 0)
            {
                <p>No recent runs available.</p>
            }
            else
            {
                <table>
                    <thead>
                        <tr>
                            <th>Run Number</th>
                            <th>Status</th>
                            <th>Duration (ms)</th>
                            <th>Passed</th>
                            <th>Failed</th>
                            <th>Skipped</th>
                            <th>Completed At</th>
                        </tr>
                    </thead>
                    <tbody>
                    @foreach (var item in Model.RecentRuns)
                    {
                        <tr>
                            <td>@item.RunNumber</td>
                            <td>@item.Status</td>
                            <td>@item.DurationMs</td>
                            <td>@item.PassedCount</td>
                            <td>@item.FailedCount</td>
                            <td>@item.SkippedCount</td>
                            <td>@item.CompletedAt</td>
                        </tr>
                    }
                    </tbody>
                </table>
            }
        </section>
    }

    <section>
        <h2>Coverage By Method</h2>
        <table>
            <thead>
                <tr><th>Method</th><th>Coverage Percent</th></tr>
            </thead>
            <tbody>
            @foreach (var item in Model.Coverage.ByMethod)
            {
                <tr><td>@item.Key</td><td>@item.Value%</td></tr>
            }
            </tbody>
        </table>
    </section>

    <section>
        <h2>Coverage By Tag</h2>
        <table>
            <thead>
                <tr><th>Tag</th><th>Coverage Percent</th></tr>
            </thead>
            <tbody>
            @foreach (var item in Model.Coverage.ByTag)
            {
                <tr><td>@item.Key</td><td>@item.Value%</td></tr>
            }
            </tbody>
        </table>
    </section>

    <section>
        <h2>Uncovered Paths</h2>
        @if (Model.Coverage.UncoveredPaths == null || Model.Coverage.UncoveredPaths.Count == 0)
        {
            <p>All scoped endpoints were executed.</p>
        }
        else
        {
            <ul>
            @foreach (var path in Model.Coverage.UncoveredPaths)
            {
                <li>@path</li>
            }
            </ul>
        }
    </section>

    @if (Model.ReportType == ReportType.Detailed)
    {
        <section>
            <h2>Detailed Results</h2>
            @foreach (var testCase in Model.Cases.OrderBy(x => x.OrderIndex).ThenBy(x => x.TestCaseId))
            {
                <div class="card">
                    <h3>@testCase.OrderIndex. @testCase.Name</h3>
                    <p class="meta">
                        <span class="pill">Status: @(string.IsNullOrWhiteSpace(testCase.Status) ? "NotExecuted" : testCase.Status)</span>
                        <span class="pill">Endpoint: @(testCase.EndpointId?.ToString() ?? "n/a")</span>
                        <span class="pill">HTTP: @(testCase.HttpStatusCode?.ToString() ?? "n/a")</span>
                        <span class="pill">Duration: @testCase.DurationMs ms</span>
                    </p>
                    @if (!string.IsNullOrWhiteSpace(testCase.Description))
                    {
                        <p>@testCase.Description</p>
                    }
                    @if (testCase.Request != null)
                    {
                        <h4>Request</h4>
                        <p>Method: @testCase.Request.HttpMethod</p>
                        <p>Url: @testCase.Request.Url</p>
                        <pre>@testCase.Request.Headers</pre>
                        <pre>@testCase.Request.Body</pre>
                    }
                    <h4>Runtime Headers</h4>
                    <pre>@string.Join("; ", testCase.RequestHeaders.Select(x => $"{x.Key}={x.Value}"))</pre>
                    <pre>@string.Join("; ", testCase.ResponseHeaders.Select(x => $"{x.Key}={x.Value}"))</pre>
                    <h4>Extracted Variables</h4>
                    <pre>@string.Join("; ", testCase.ExtractedVariables.Select(x => $"{x.Key}={x.Value}"))</pre>
                    <h4>Response Preview</h4>
                    <pre>@testCase.ResponseBodyPreview</pre>
                    <h4>Failure Details</h4>
                    @if (testCase.FailureReasons == null || testCase.FailureReasons.Count == 0)
                    {
                        <p>No failure reasons recorded.</p>
                    }
                    else
                    {
                        <ul>
                        @foreach (var failure in testCase.FailureReasons)
                        {
                            <li>@failure.Code: @failure.Message</li>
                        }
                        </ul>
                    }
                </div>
            }
        </section>
    }
</body>
</html>
""";

    private readonly IRazorLightEngine _razorLightEngine;

    public HtmlReportRenderer(IRazorLightEngine razorLightEngine)
    {
        _razorLightEngine = razorLightEngine;
    }

    public ReportFormat Format => ReportFormat.HTML;

    public async Task<RenderedReportFile> RenderAsync(TestRunReportDocumentModel document, CancellationToken ct = default)
    {
        var html = await _razorLightEngine.CompileRenderStringAsync(document.FileBaseName, Template, document);

        return new RenderedReportFile
        {
            Content = Encoding.UTF8.GetBytes(html),
            FileName = $"{document.FileBaseName}.html",
            ContentType = "text/html; charset=utf-8",
            FileCategory = FileCategory.Report,
        };
    }
}
