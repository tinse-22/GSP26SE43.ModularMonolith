using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.TestExecution.Services;

/// <summary>
/// Deterministic policy for deciding whether a dependency test case result counts as satisfied,
/// allowing downstream dependent cases to proceed.
///
/// A dependency is considered satisfied in two situations:
/// <list type="number">
///   <item>The case Passed outright.</item>
///   <item>The case returned a 2xx HTTP status code AND its only failures are
///         expectation-mismatch codes (<c>STATUS_CODE_MISMATCH</c> or
///         <c>RESPONSE_SCHEMA_MISMATCH</c>).
///         This handles real-world APIs that return valid data with a slightly different
///         status or schema than the test expectation specifies, but only when the case
///         did not extract flow variables. Producer cases must pass before downstream
///         consumers can use their output.</item>
/// </list>
///
/// Any other failure — transport errors, unresolved variables, pre-validation, 4xx/5xx
/// responses — blocks dependent cases.
/// </summary>
internal static class DependencySatisfactionPolicy
{
    private static readonly HashSet<string> ExpectationMismatchCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "STATUS_CODE_MISMATCH",
            "RESPONSE_SCHEMA_MISMATCH",
        };

    /// <summary>
    /// Returns <c>true</c> when <paramref name="dependencyResult"/> is considered satisfied.
    /// </summary>
    public static bool IsSatisfied(TestCaseExecutionResult dependencyResult)
    {
        if (dependencyResult == null)
        {
            return false;
        }

        if (string.Equals(dependencyResult.Status, "Passed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return dependencyResult.HttpStatusCode is >= 200 and < 300
            && !HasMeaningfulExtractedVariables(dependencyResult.ExtractedVariables)
            && IsOnlyExpectationMismatch(dependencyResult.FailureReasons);
    }

    private static bool HasMeaningfulExtractedVariables(IReadOnlyDictionary<string, string> extractedVariables)
    {
        if (extractedVariables == null || extractedVariables.Count == 0)
        {
            return false;
        }

        return extractedVariables.Any(x =>
            !string.IsNullOrWhiteSpace(x.Key)
            && !x.Key.StartsWith("__resolved.", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(x.Value));
    }

    private static bool IsOnlyExpectationMismatch(IReadOnlyCollection<ValidationFailureModel> failures)
    {
        if (failures == null || failures.Count == 0)
        {
            return true;
        }

        return failures
            .Select(x => x?.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .All(x => ExpectationMismatchCodes.Contains(x.Trim()));
    }
}
