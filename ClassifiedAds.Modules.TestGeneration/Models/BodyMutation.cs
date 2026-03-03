using ClassifiedAds.Modules.TestGeneration.Entities;

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

    /// <summary>Description of what this mutation tests.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether this is a Boundary or Negative test case.</summary>
    public TestType SuggestedTestType { get; set; } = TestType.Negative;
}
