namespace ClassifiedAds.Modules.TestExecution.Models;

/// <summary>
/// Represents a non-fatal warning during validation.
/// Warnings indicate potential issues but don't cause test failure.
/// </summary>
public class ValidationWarningModel
{
    /// <summary>
    /// Warning code for programmatic handling.
    /// Examples: NO_EXPECTATION_DEFINED, ALL_CHECKS_SKIPPED
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Human-readable warning message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Optional target field/property that caused the warning.
    /// </summary>
    public string Target { get; set; }
}
