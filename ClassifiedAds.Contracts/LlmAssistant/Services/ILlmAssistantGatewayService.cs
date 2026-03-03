using ClassifiedAds.Contracts.LlmAssistant.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.LlmAssistant.Services;

/// <summary>
/// Cross-module gateway for LLM interaction audit logging and suggestion caching.
/// Used by LlmScenarioSuggester (FE-06) to persist and cache LLM interactions.
/// </summary>
public interface ILlmAssistantGatewayService
{
    /// <summary>
    /// Save an LLM interaction record for audit purposes.
    /// </summary>
    Task SaveInteractionAsync(SaveLlmInteractionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached LLM suggestions for an endpoint.
    /// </summary>
    /// <param name="endpointId">API endpoint ID.</param>
    /// <param name="suggestionType">Maps to SuggestionType enum: BoundaryCase=0, NegativeCase=1, etc.</param>
    /// <param name="cacheKey">Hash key for cache lookup (e.g. hash of endpoint schema).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cache result with HasCache flag and cached JSON.</returns>
    Task<CachedSuggestionsDto> GetCachedSuggestionsAsync(
        Guid endpointId,
        int suggestionType,
        string cacheKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache LLM suggestions for an endpoint.
    /// </summary>
    /// <param name="endpointId">API endpoint ID.</param>
    /// <param name="suggestionType">Maps to SuggestionType enum.</param>
    /// <param name="cacheKey">Hash key for cache lookup.</param>
    /// <param name="suggestionsJson">Suggestions serialized as JSON.</param>
    /// <param name="ttl">Time-to-live for the cache entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CacheSuggestionsAsync(
        Guid endpointId,
        int suggestionType,
        string cacheKey,
        string suggestionsJson,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
