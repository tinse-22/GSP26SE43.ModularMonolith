using ClassifiedAds.Contracts.LlmAssistant.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.LlmAssistant.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public class LlmAssistantGatewayService : ILlmAssistantGatewayService
{
    private readonly IRepository<LlmInteraction, Guid> _interactionRepository;
    private readonly IRepository<LlmSuggestionCache, Guid> _cacheRepository;

    public LlmAssistantGatewayService(
        IRepository<LlmInteraction, Guid> interactionRepository,
        IRepository<LlmSuggestionCache, Guid> cacheRepository)
    {
        _interactionRepository = interactionRepository;
        _cacheRepository = cacheRepository;
    }

    public async Task SaveInteractionAsync(SaveLlmInteractionRequest request, CancellationToken cancellationToken = default)
    {
        var interaction = new LlmInteraction
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            InteractionType = (InteractionType)request.InteractionType,
            InputContext = request.InputContext,
            LlmResponse = request.LlmResponse,
            ModelUsed = request.ModelUsed,
            TokensUsed = request.TokensUsed,
            LatencyMs = request.LatencyMs,
            CreatedDateTime = DateTimeOffset.UtcNow,
        };

        await _interactionRepository.AddAsync(interaction, cancellationToken);
        await _interactionRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<CachedSuggestionsDto> GetCachedSuggestionsAsync(
        Guid endpointId,
        int suggestionType,
        string cacheKey,
        CancellationToken cancellationToken = default)
    {
        var cachedEntry = await _cacheRepository.FirstOrDefaultAsync(
            _cacheRepository.GetQueryableSet()
                .Where(x => x.EndpointId == endpointId
                    && x.SuggestionType == (SuggestionType)suggestionType
                    && x.CacheKey == cacheKey
                    && x.ExpiresAt > DateTimeOffset.UtcNow));

        if (cachedEntry == null)
        {
            return new CachedSuggestionsDto { HasCache = false };
        }

        return new CachedSuggestionsDto
        {
            HasCache = true,
            SuggestionsJson = cachedEntry.Suggestions,
        };
    }

    public async Task CacheSuggestionsAsync(
        Guid endpointId,
        int suggestionType,
        string cacheKey,
        string suggestionsJson,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var existing = await _cacheRepository.FirstOrDefaultAsync(
            _cacheRepository.GetQueryableSet()
                .Where(x => x.EndpointId == endpointId
                    && x.SuggestionType == (SuggestionType)suggestionType
                    && x.CacheKey == cacheKey));

        if (existing != null)
        {
            existing.Suggestions = suggestionsJson;
            existing.ExpiresAt = DateTimeOffset.UtcNow.Add(ttl);
            existing.UpdatedDateTime = DateTimeOffset.UtcNow;
            await _cacheRepository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            var cacheEntry = new LlmSuggestionCache
            {
                Id = Guid.NewGuid(),
                EndpointId = endpointId,
                SuggestionType = (SuggestionType)suggestionType,
                CacheKey = cacheKey,
                Suggestions = suggestionsJson,
                ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
                CreatedDateTime = DateTimeOffset.UtcNow,
            };

            await _cacheRepository.AddAsync(cacheEntry, cancellationToken);
        }

        await _cacheRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
