namespace ClassifiedAds.Modules.TestExecution.Models;

/// <summary>
/// Controls how strictly the <see cref="Services.RuleBasedValidator"/> evaluates HTTP responses.
/// </summary>
public enum ValidationProfile
{
    /// <summary>
    /// Standard validation. Adaptive/permissive status matching is enabled.
    /// Missing expectations generate a warning but do not fail the test case.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Demo mode. Identical to <see cref="Default"/> but clearly communicates intent
    /// for presentation or sandbox environments where leniency is expected.
    /// </summary>
    DemoAdaptive = 1,

    /// <summary>
    /// SRS-strict validation. All adaptive permissive status matching is disabled.
    /// A missing expectation causes an immediate FAIL.
    /// Use this profile for SRS-linked test suites where compliance must be deterministically verified.
    /// </summary>
    SrsStrict = 2,
}
