using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestExecution.Entities;

/// <summary>
/// Detailed test case execution result persisted to PostgreSQL as cold storage.
/// When Redis cache expires, this data allows Failure Explanation to be generated.
/// </summary>
public class TestCaseResult : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Test run this result belongs to.
    /// </summary>
    public Guid TestRunId { get; set; }

    /// <summary>
    /// ID of the test case executed.
    /// </summary>
    public Guid TestCaseId { get; set; }

    /// <summary>
    /// Endpoint ID (nullable).
    /// </summary>
    public Guid? EndpointId { get; set; }

    /// <summary>
    /// Test case name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Execution order within the run.
    /// </summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// Result status: Passed, Failed, Skipped.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// HTTP response status code.
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Execution duration in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Resolved endpoint URL.
    /// </summary>
    public string ResolvedUrl { get; set; }

    /// <summary>
    /// Request headers as JSON (JSONB).
    /// </summary>
    public string RequestHeaders { get; set; }

    /// <summary>
    /// Response headers as JSON (JSONB).
    /// </summary>
    public string ResponseHeaders { get; set; }

    /// <summary>
    /// Response body preview (truncated).
    /// </summary>
    public string ResponseBodyPreview { get; set; }

    /// <summary>
    /// Validation failures as JSON (JSONB).
    /// </summary>
    public string FailureReasons { get; set; }

    /// <summary>
    /// Extracted variables (masked for sensitive data) as JSON.
    /// </summary>
    public string ExtractedVariables { get; set; }

    /// <summary>
    /// Dependency test case IDs as JSONB array.
    /// </summary>
    public string DependencyIds { get; set; }

    /// <summary>
    /// IDs of tests skipped due to failed dependencies as JSONB array.
    /// </summary>
    public string SkippedBecauseDependencyIds { get; set; }

    /// <summary>
    /// Whether status code matched expected.
    /// </summary>
    public bool StatusCodeMatched { get; set; }

    /// <summary>
    /// Schema validation result (null if not checked).
    /// </summary>
    public bool? SchemaMatched { get; set; }

    /// <summary>
    /// Header validation result (null if not checked).
    /// </summary>
    public bool? HeaderChecksPassed { get; set; }

    /// <summary>
    /// Body contains validation result (null if not checked).
    /// </summary>
    public bool? BodyContainsPassed { get; set; }

    /// <summary>
    /// Body not contains validation result (null if not checked).
    /// </summary>
    public bool? BodyNotContainsPassed { get; set; }

    /// <summary>
    /// JSON path validation result (null if not checked).
    /// </summary>
    public bool? JsonPathChecksPassed { get; set; }

    /// <summary>
    /// Response time validation result (null if not checked).
    /// </summary>
    public bool? ResponseTimePassed { get; set; }
}
