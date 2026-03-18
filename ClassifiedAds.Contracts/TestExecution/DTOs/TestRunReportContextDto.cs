using ClassifiedAds.Contracts.TestGeneration.DTOs;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Contracts.TestExecution.DTOs;

public class TestRunReportContextDto
{
    public Guid TestSuiteId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? ApiSpecId { get; set; }

    public Guid CreatedById { get; set; }

    public string SuiteName { get; set; }

    public TestRunReportRunDto Run { get; set; }

    public IReadOnlyList<TestRunHistoryItemDto> RecentRuns { get; set; } = Array.Empty<TestRunHistoryItemDto>();

    public IReadOnlyList<Guid> OrderedEndpointIds { get; set; } = Array.Empty<Guid>();

    public IReadOnlyList<ReportTestCaseDefinitionDto> Definitions { get; set; } = Array.Empty<ReportTestCaseDefinitionDto>();

    public IReadOnlyList<ReportTestCaseResultDto> Results { get; set; } = Array.Empty<ReportTestCaseResultDto>();
}

public class TestRunReportRunDto
{
    public Guid TestRunId { get; set; }

    public int RunNumber { get; set; }

    public Guid EnvironmentId { get; set; }

    public Guid TriggeredById { get; set; }

    public string Status { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long DurationMs { get; set; }

    public int TotalTests { get; set; }

    public int PassedCount { get; set; }

    public int FailedCount { get; set; }

    public int SkippedCount { get; set; }

    public string ResolvedEnvironmentName { get; set; }

    public DateTimeOffset ExecutedAt { get; set; }
}

public class TestRunHistoryItemDto
{
    public Guid TestRunId { get; set; }

    public int RunNumber { get; set; }

    public string Status { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long DurationMs { get; set; }

    public int PassedCount { get; set; }

    public int FailedCount { get; set; }

    public int SkippedCount { get; set; }
}

public class ReportTestCaseDefinitionDto
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
}

public class ReportTestCaseResultDto
{
    public Guid TestCaseId { get; set; }

    public Guid? EndpointId { get; set; }

    public string Name { get; set; }

    public int OrderIndex { get; set; }

    public string Status { get; set; }

    public int? HttpStatusCode { get; set; }

    public long DurationMs { get; set; }

    public string ResolvedUrl { get; set; }

    public Dictionary<string, string> RequestHeaders { get; set; } = new();

    public Dictionary<string, string> ResponseHeaders { get; set; } = new();

    public string ResponseBodyPreview { get; set; }

    public IReadOnlyList<ReportValidationFailureDto> FailureReasons { get; set; } = Array.Empty<ReportValidationFailureDto>();

    public Dictionary<string, string> ExtractedVariables { get; set; } = new();

    public IReadOnlyList<Guid> DependencyIds { get; set; } = Array.Empty<Guid>();

    public IReadOnlyList<Guid> SkippedBecauseDependencyIds { get; set; } = Array.Empty<Guid>();

    public bool StatusCodeMatched { get; set; }

    public bool? SchemaMatched { get; set; }

    public bool? HeaderChecksPassed { get; set; }

    public bool? BodyContainsPassed { get; set; }

    public bool? BodyNotContainsPassed { get; set; }

    public bool? JsonPathChecksPassed { get; set; }

    public bool? ResponseTimePassed { get; set; }
}

public class ReportValidationFailureDto
{
    public string Code { get; set; }

    public string Message { get; set; }

    public string Target { get; set; }

    public string Expected { get; set; }

    public string Actual { get; set; }
}
