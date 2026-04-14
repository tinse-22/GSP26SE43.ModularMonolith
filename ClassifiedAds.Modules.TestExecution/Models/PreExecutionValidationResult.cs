using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.TestExecution.Models;

/// <summary>
/// Result of pre-execution validation — collects ALL issues in a single pass.
/// </summary>
public class PreExecutionValidationResult
{
    public List<ValidationFailureModel> Errors { get; set; } = new();

    public List<ValidationWarningModel> Warnings { get; set; } = new();

    public bool HasErrors => Errors.Count > 0;

    /// <summary>
    /// Merges errors into FailureReasons for a failed TestCaseExecutionResult.
    /// </summary>
    public List<ValidationFailureModel> ToFailureReasons()
    {
        return Errors.ToList();
    }
}
