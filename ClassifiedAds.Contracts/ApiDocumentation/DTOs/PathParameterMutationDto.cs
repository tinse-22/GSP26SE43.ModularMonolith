using System.Collections.Generic;

namespace ClassifiedAds.Contracts.ApiDocumentation.DTOs;

/// <summary>
/// DTO for a single path parameter mutation variant.
/// Used for boundary/negative test generation (FE-06).
/// </summary>
public class PathParameterMutationDto
{
    /// <summary>Mutation type identifier: empty, wrongType, boundary_zero, nonExistent, etc.</summary>
    public string MutationType { get; set; } = string.Empty;

    /// <summary>Human-readable label: '{paramName} - {mutation description}'.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The mutated value to substitute into the URL.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Expected HTTP status code when using this mutated value (400, 404, etc.).</summary>
    public int ExpectedStatusCode { get; set; }

    /// <summary>
    /// List of all acceptable HTTP status codes for this mutation.
    /// If null or empty, falls back to <see cref="ExpectedStatusCode"/>.
    /// </summary>
    public List<int> ExpectedStatusCodes { get; set; }

    /// <summary>Description of what this mutation tests.</summary>
    public string Description { get; set; } = string.Empty;

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
