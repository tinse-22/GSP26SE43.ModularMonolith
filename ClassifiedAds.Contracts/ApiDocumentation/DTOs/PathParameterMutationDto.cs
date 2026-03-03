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

    /// <summary>Description of what this mutation tests.</summary>
    public string Description { get; set; } = string.Empty;
}
