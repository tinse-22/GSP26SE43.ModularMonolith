using System;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

/// <summary>
/// Request body for triggering boundary/negative test case generation.
/// Requires an approved API order to exist for the target test suite.
/// </summary>
public class GenerateBoundaryNegativeTestCasesRequest
{
    /// <summary>
    /// API specification ID to fetch endpoint metadata from.
    /// Must match the spec used in the approved proposal.
    /// </summary>
    public Guid SpecificationId { get; set; }

    /// <summary>
    /// If true, re-generates and replaces existing boundary/negative test cases for this suite.
    /// If false (default), generation is blocked when boundary/negative cases already exist.
    /// </summary>
    public bool ForceRegenerate { get; set; }

    /// <summary>
    /// Include rule-based path parameter mutations (empty, wrongType, boundary, injection).
    /// Default: true.
    /// </summary>
    public bool IncludePathMutations { get; set; } = true;

    /// <summary>
    /// Include rule-based body mutations (missingRequired, typeMismatch, overflow, malformedJson).
    /// Default: true.
    /// </summary>
    public bool IncludeBodyMutations { get; set; } = true;

    /// <summary>
    /// Include LLM-suggested boundary/negative scenarios via n8n webhook.
    /// Default: true.
    /// </summary>
    public bool IncludeLlmSuggestions { get; set; } = true;
}
