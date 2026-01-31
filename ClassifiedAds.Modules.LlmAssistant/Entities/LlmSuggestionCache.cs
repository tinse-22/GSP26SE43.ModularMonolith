using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.LlmAssistant.Entities;

/// <summary>
/// Cached LLM suggestions for API endpoints.
/// </summary>
public class LlmSuggestionCache : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// API endpoint this cache is for.
    /// </summary>
    public Guid EndpointId { get; set; }

    /// <summary>
    /// Type of suggestion: BoundaryCase, NegativeCase.
    /// </summary>
    public SuggestionType SuggestionType { get; set; }

    /// <summary>
    /// Hash key for cache lookup.
    /// </summary>
    public string CacheKey { get; set; }

    /// <summary>
    /// Cached suggestions as JSON.
    /// </summary>
    public string Suggestions { get; set; }

    /// <summary>
    /// When this cache entry expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}

public enum SuggestionType
{
    BoundaryCase = 0,
    NegativeCase = 1,
    HappyPath = 2,
    SecurityCase = 3
}
