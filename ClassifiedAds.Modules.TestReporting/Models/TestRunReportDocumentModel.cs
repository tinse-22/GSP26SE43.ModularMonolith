using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestReporting.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestReporting.Models;

public class TestRunReportDocumentModel
{
    public Guid TestSuiteId { get; set; }

    public Guid ProjectId { get; set; }

    public string ProjectName { get; set; }

    public Guid? ApiSpecId { get; set; }

    public string SuiteName { get; set; }

    public ReportType ReportType { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    public string FileBaseName { get; set; }

    public TestRunReportRunDto Run { get; set; }

    public CoverageMetricModel Coverage { get; set; }

    public IReadOnlyDictionary<string, int> FailureDistribution { get; set; } = new Dictionary<string, int>();

    public IReadOnlyList<TestRunHistoryItemDto> RecentRuns { get; set; } = Array.Empty<TestRunHistoryItemDto>();

    public IReadOnlyList<TestRunReportCaseDocumentModel> Cases { get; set; } = Array.Empty<TestRunReportCaseDocumentModel>();

    public IReadOnlyList<TestRunExecutionAttemptDto> Attempts { get; set; } = Array.Empty<TestRunExecutionAttemptDto>();
}

public class TestRunReportCaseDocumentModel
{
    public Guid TestCaseId { get; set; }

    public Guid? EndpointId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string TestType { get; set; }

    public int OrderIndex { get; set; }

    public IReadOnlyList<Guid> DependencyIds { get; set; } = Array.Empty<Guid>();

    public ExecutionTestCaseRequestDto Request { get; set; }

    public ExecutionTestCaseExpectationDto Expectation { get; set; }

    public string Status { get; set; }

    public int? HttpStatusCode { get; set; }

    public long DurationMs { get; set; }

    public string ResolvedUrl { get; set; }

    public IReadOnlyDictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();

    public string ResponseBodyPreview { get; set; }

    public IReadOnlyList<ReportValidationFailureDto> FailureReasons { get; set; } = Array.Empty<ReportValidationFailureDto>();

    public IReadOnlyDictionary<string, string> ExtractedVariables { get; set; } = new Dictionary<string, string>();

    public IReadOnlyList<Guid> SkippedBecauseDependencyIds { get; set; } = Array.Empty<Guid>();

    public bool StatusCodeMatched { get; set; }

    public bool? SchemaMatched { get; set; }

    public bool? HeaderChecksPassed { get; set; }

    public bool? BodyContainsPassed { get; set; }

    public bool? BodyNotContainsPassed { get; set; }

    public bool? JsonPathChecksPassed { get; set; }

    public bool? ResponseTimePassed { get; set; }

    public int TotalAttempts { get; set; }
}
