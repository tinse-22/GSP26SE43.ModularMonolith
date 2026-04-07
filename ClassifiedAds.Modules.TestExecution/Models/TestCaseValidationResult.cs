using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class TestCaseValidationResult
{
    public bool IsPassed { get; set; }

    public bool StatusCodeMatched { get; set; }

    public bool? SchemaMatched { get; set; }

    public bool? HeaderChecksPassed { get; set; }

    public bool? BodyContainsPassed { get; set; }

    public bool? BodyNotContainsPassed { get; set; }

    public bool? JsonPathChecksPassed { get; set; }

    public bool? ResponseTimePassed { get; set; }

    public List<ValidationFailureModel> Failures { get; set; } = new();

    /// <summary>
    /// Non-fatal warnings that don't cause test failure but indicate potential issues.
    /// </summary>
    public List<ValidationWarningModel> Warnings { get; set; } = new();

    /// <summary>
    /// True if there are any warnings.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// Number of validation checks that were actually performed.
    /// </summary>
    public int ChecksPerformed { get; set; }

    /// <summary>
    /// Number of validation checks that were skipped (e.g., empty expectation fields).
    /// </summary>
    public int ChecksSkipped { get; set; }
}
