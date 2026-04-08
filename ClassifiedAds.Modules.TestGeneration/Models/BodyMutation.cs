using ClassifiedAds.Modules.TestGeneration.Entities;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Represents a single body mutation variant for boundary/negative testing.
/// </summary>
public class BodyMutation
{
    /// <summary>Mutation type: missingRequired, typeMismatch, overflow, emptyBody, malformedJson, invalidEnum.</summary>
    public string MutationType { get; set; } = string.Empty;

    /// <summary>Human-readable label describing the mutation.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The JSON body with mutation applied. Null for emptyBody mutations.</summary>
    public string MutatedBody { get; set; }

    /// <summary>Which field was mutated. Null for whole-body mutations (emptyBody, malformedJson).</summary>
    public string TargetFieldName { get; set; }

    /// <summary>Expected HTTP status code (400, 422, etc.).</summary>
    public int ExpectedStatusCode { get; set; } = 400;

    /// <summary>
    /// List of all acceptable status codes for this mutation.
    /// If null or empty, falls back to <see cref="ExpectedStatusCode"/>.
    /// </summary>
    public List<int> ExpectedStatusCodes { get; set; }

    /// <summary>Description of what this mutation tests.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether this is a Boundary or Negative test case.</summary>
    public TestType SuggestedTestType { get; set; } = TestType.Negative;

    /// <summary>
    /// Gets the effective list of expected status codes, preferring the full list if available.
    /// </summary>
    public List<int> GetEffectiveExpectedStatusCodes()
    {
        if (ExpectedStatusCodes != null && ExpectedStatusCodes.Count > 0)
        {
            return ExpectedStatusCodes;
        }

        return new List<int> { ExpectedStatusCode };
    }
}
