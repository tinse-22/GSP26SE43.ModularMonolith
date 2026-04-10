using ClassifiedAds.Modules.TestGeneration.Models;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

/// <summary>
/// Request body for generating LLM suggestion previews (FE-15).
/// </summary>
public class GenerateLlmSuggestionPreviewRequest
{
    /// <summary>
    /// API specification ID to fetch endpoint metadata from.
    /// </summary>
    public Guid SpecificationId { get; set; }

    /// <summary>
    /// If true, supersedes existing pending suggestions and generates fresh ones.
    /// </summary>
    public bool ForceRefresh { get; set; }

    /// <summary>
    /// Optional algorithm profile for suggestion generation.
    /// If omitted, all algorithms are enabled.
    /// </summary>
    public GenerationAlgorithmProfile AlgorithmProfile { get; set; } = new ();
}
