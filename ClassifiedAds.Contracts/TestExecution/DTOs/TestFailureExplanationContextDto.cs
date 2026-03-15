using ClassifiedAds.Contracts.TestGeneration.DTOs;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Contracts.TestExecution.DTOs;

public class TestFailureExplanationContextDto
{
    public Guid TestSuiteId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? ApiSpecId { get; set; }

    public Guid CreatedById { get; set; }

    public Guid TestRunId { get; set; }

    public int RunNumber { get; set; }

    public Guid TriggeredById { get; set; }

    public string ResolvedEnvironmentName { get; set; }

    public DateTimeOffset ExecutedAt { get; set; }

    public FailureExplanationDefinitionDto Definition { get; set; }

    public FailureExplanationActualResultDto ActualResult { get; set; }
}

public class FailureExplanationDefinitionDto
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

public class FailureExplanationActualResultDto
{
    public string Status { get; set; }

    public int? HttpStatusCode { get; set; }

    public long DurationMs { get; set; }

    public string ResolvedUrl { get; set; }

    public Dictionary<string, string> RequestHeaders { get; set; } = new();

    public Dictionary<string, string> ResponseHeaders { get; set; } = new();

    public string ResponseBodyPreview { get; set; }

    public IReadOnlyList<FailureExplanationFailureReasonDto> FailureReasons { get; set; } = Array.Empty<FailureExplanationFailureReasonDto>();

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

public class FailureExplanationFailureReasonDto
{
    public string Code { get; set; }

    public string Message { get; set; }

    public string Target { get; set; }

    public string Expected { get; set; }

    public string Actual { get; set; }
}
