using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class TestCaseExecutionResult
{
    public Guid TestCaseId { get; set; }

    public Guid? EndpointId { get; set; }

    public string Name { get; set; }

    public string TestType { get; set; }

    public int OrderIndex { get; set; }

    public string Status { get; set; }

    public int ExecutionAttempt { get; set; } = 1;

    public int? HttpStatusCode { get; set; }

    public long DurationMs { get; set; }

    public string ResolvedUrl { get; set; }

    public string HttpMethod { get; set; }

    public string BodyType { get; set; }

    public string RequestBody { get; set; }

    public Dictionary<string, string> QueryParams { get; set; } = new();

    public int TimeoutMs { get; set; }

    public string ExpectedStatus { get; set; }

    public Dictionary<string, string> RequestHeaders { get; set; } = new();

    public Dictionary<string, string> ResponseHeaders { get; set; } = new();

    public string ResponseBody { get; set; }

    public List<ValidationFailureModel> FailureReasons { get; set; } = new();

    public List<ValidationWarningModel> Warnings { get; set; } = new();

    public int ChecksPerformed { get; set; }

    public int ChecksSkipped { get; set; }

    public Dictionary<string, string> ExtractedVariables { get; set; } = new();

    public IReadOnlyList<Guid> DependencyIds { get; set; } = Array.Empty<Guid>();

    public List<Guid> SkippedBecauseDependencyIds { get; set; } = new();

    /// <summary>
    /// Human-readable explanation for why this test case was skipped (populated when Status = "Skipped").
    /// </summary>
    public string SkippedCause { get; set; }

    public bool StatusCodeMatched { get; set; }

    public bool? SchemaMatched { get; set; }

    public bool? HeaderChecksPassed { get; set; }

    public bool? BodyContainsPassed { get; set; }

    public bool? BodyNotContainsPassed { get; set; }

    public bool? JsonPathChecksPassed { get; set; }

    public bool? ResponseTimePassed { get; set; }
}
