using System;
using System.Collections.Generic;

namespace ClassifiedAds.Contracts.TestExecution.DTOs;

/// <summary>
/// Execution evidence for a single test case in the latest (or specified) test run.
/// Used by GetSrsTraceabilityQuery to compute RequirementValidationStatus.
/// </summary>
public sealed class TestCaseExecutionEvidenceDto
{
    /// <summary>Test suite the evidence belongs to.</summary>
    public Guid TestSuiteId { get; set; }

    /// <summary>The test run the evidence was taken from.</summary>
    public Guid TestRunId { get; set; }

    /// <summary>Sequential run number within the suite (1-based).</summary>
    public int? RunNumber { get; set; }

    /// <summary>When the run completed (null if still running).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>ID of the test case.</summary>
    public Guid TestCaseId { get; set; }

    /// <summary>Final execution status: Passed / Failed / Skipped.</summary>
    public string Status { get; set; }

    /// <summary>Actual HTTP status code returned by the API (null for pre-execution failures).</summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>Failure codes from RuleBasedValidator (empty when Passed or Skipped).</summary>
    public IReadOnlyList<string> FailureCodes { get; set; } = Array.Empty<string>();

    /// <summary>Short human-readable summary of the first failure (empty when Passed).</summary>
    public string FailureSummary { get; set; }

    /// <summary>True when at least one adaptive/permissive warning was raised during validation.</summary>
    public bool HasAdaptiveWarning { get; set; }

    /// <summary>Warning codes raised during validation (ADAPTIVE_PERMISSIVE_STATUS_MATCH, etc.).</summary>
    public IReadOnlyList<string> WarningCodes { get; set; } = Array.Empty<string>();
}
