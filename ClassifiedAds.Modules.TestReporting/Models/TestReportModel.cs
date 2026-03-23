using ClassifiedAds.Modules.TestReporting.Entities;
using System;

namespace ClassifiedAds.Modules.TestReporting.Models;

public class TestReportModel
{
    public Guid Id { get; set; }

    public Guid TestSuiteId { get; set; }

    public Guid TestRunId { get; set; }

    public string ReportType { get; set; }

    public string Format { get; set; }

    public string DownloadUrl { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public CoverageMetricModel Coverage { get; set; }

    public static TestReportModel FromEntity(TestReport entity, Guid testSuiteId, CoverageMetricModel coverage = null)
    {
        if (entity == null)
        {
            return null;
        }

        return new TestReportModel
        {
            Id = entity.Id,
            TestSuiteId = testSuiteId,
            TestRunId = entity.TestRunId,
            ReportType = entity.ReportType.ToString(),
            Format = entity.Format.ToString(),
            DownloadUrl = BuildDownloadUrl(testSuiteId, entity.TestRunId, entity.Id),
            GeneratedAt = entity.GeneratedAt,
            ExpiresAt = entity.ExpiresAt,
            Coverage = coverage,
        };
    }

    public static string BuildDownloadUrl(Guid testSuiteId, Guid testRunId, Guid reportId)
    {
        return $"/api/test-suites/{testSuiteId}/test-runs/{testRunId}/reports/{reportId}/download";
    }
}
