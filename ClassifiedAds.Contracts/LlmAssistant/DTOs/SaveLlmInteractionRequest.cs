using System;

namespace ClassifiedAds.Contracts.LlmAssistant.DTOs;

/// <summary>
/// Request to save an LLM interaction for audit purposes.
/// </summary>
public class SaveLlmInteractionRequest
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Maps to InteractionType enum: ScenarioSuggestion=0, FailureExplanation=1, DocumentationParsing=2.
    /// </summary>
    public int InteractionType { get; set; }

    public string InputContext { get; set; }

    public string LlmResponse { get; set; }

    public string ModelUsed { get; set; }

    public int TokensUsed { get; set; }

    public int LatencyMs { get; set; }
}

/// <summary>
/// Result of a cache lookup for LLM suggestions.
/// </summary>
public class CachedSuggestionsDto
{
    /// <summary>
    /// Whether a valid (non-expired) cache entry was found.
    /// </summary>
    public bool HasCache { get; set; }

    /// <summary>
    /// Cached suggestions as JSON. Null if HasCache is false.
    /// </summary>
    public string SuggestionsJson { get; set; }
}
