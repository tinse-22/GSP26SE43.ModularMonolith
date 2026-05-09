using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class TestCaseRunResultModel
{
    public Guid TestCaseId { get; set; }

    public Guid? EndpointId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

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

    public string ResponseBodyPreview { get; set; }

    public List<ValidationFailureModel> FailureReasons { get; set; } = new();

    public List<ValidationWarningModel> Warnings { get; set; } = new();

    public bool HasWarnings => Warnings.Count > 0;

    public int ChecksPerformed { get; set; }

    public int ChecksSkipped { get; set; }

    public Dictionary<string, string> ExtractedVariables { get; set; } = new();

    public List<Guid> DependencyIds { get; set; } = new();

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

    // ── Expectation snapshots (for FE Evidence panel) ────────────────────
    public string ExpectedBodyContains { get; set; }

    public string ExpectedBodyNotContains { get; set; }

    public string ExpectedHeaderChecks { get; set; }

    public string ExpectedJsonPathChecks { get; set; }

    public int? ExpectedMaxResponseTime { get; set; }

    public string ExpectationSource { get; set; }

    public string RequirementCode { get; set; }

    public Guid? PrimaryRequirementId { get; set; }

    /// <summary>
    /// Total number of execution attempts made for this test case across the entire run
    /// (including retries). This equals 1 when no retry was needed.
    /// </summary>
    public int TotalAttempts { get; set; } = 1;
}
