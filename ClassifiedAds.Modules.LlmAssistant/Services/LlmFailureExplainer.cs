using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.Services;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.LlmAssistant.ConfigurationOptions;
using ClassifiedAds.Modules.LlmAssistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public class LlmFailureExplainer : ILlmFailureExplainer
{
    private const int FailureExplanationSuggestionType = 4;
    private const int FailureExplanationInteractionType = 1;

    private static readonly JsonSerializerOptions JsonOptions = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IFailureExplanationSanitizer _sanitizer;
    private readonly IFailureExplanationFingerprintBuilder _fingerprintBuilder;
    private readonly IFailureExplanationPromptBuilder _promptBuilder;
    private readonly ILlmFailureExplanationClient _client;
    private readonly ILlmAssistantGatewayService _gatewayService;
    private readonly FailureExplanationOptions _options;
    private readonly ILogger<LlmFailureExplainer> _logger;

    public LlmFailureExplainer(
        IFailureExplanationSanitizer sanitizer,
        IFailureExplanationFingerprintBuilder fingerprintBuilder,
        IFailureExplanationPromptBuilder promptBuilder,
        ILlmFailureExplanationClient client,
        ILlmAssistantGatewayService gatewayService,
        IOptions<LlmAssistantModuleOptions> options,
        ILogger<LlmFailureExplainer> logger)
    {
        _sanitizer = sanitizer;
        _fingerprintBuilder = fingerprintBuilder;
        _promptBuilder = promptBuilder;
        _client = client;
        _gatewayService = gatewayService;
        _options = options?.Value?.FailureExplanation ?? new FailureExplanationOptions();
        _logger = logger;
    }

    public async Task<FailureExplanationModel> GetCachedAsync(
        TestFailureExplanationContextDto context,
        CancellationToken ct = default)
    {
        var sanitizedContext = _sanitizer.Sanitize(context);
        var fingerprint = _fingerprintBuilder.Build(sanitizedContext);
        var endpointId = ResolveCacheEndpointId(sanitizedContext);

        var cached = await _gatewayService.GetCachedSuggestionsAsync(
            endpointId,
            FailureExplanationSuggestionType,
            fingerprint,
            ct);

        if (!cached.HasCache || string.IsNullOrWhiteSpace(cached.SuggestionsJson))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<FailureExplanationCachePayload>(cached.SuggestionsJson, JsonOptions);
            if (payload == null)
            {
                return null;
            }

            return MapToModel(sanitizedContext, payload, "cache", 0);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid failure explanation cache payload. EndpointId={EndpointId}, TestCaseId={TestCaseId}",
                endpointId,
                context?.Definition?.TestCaseId);

            return null;
        }
    }

    public async Task<FailureExplanationModel> ExplainAsync(
        TestFailureExplanationContextDto context,
        ApiEndpointMetadataDto endpointMetadata,
        CancellationToken ct = default)
    {
        var sanitizedContext = _sanitizer.Sanitize(context);
        var fingerprint = _fingerprintBuilder.Build(sanitizedContext);
        var endpointId = ResolveCacheEndpointId(sanitizedContext);

        var cachedModel = await GetCachedAsync(sanitizedContext, ct);
        if (cachedModel != null)
        {
            return cachedModel;
        }

        var prompt = _promptBuilder.Build(sanitizedContext, endpointMetadata);
        var stopwatch = Stopwatch.StartNew();
        var providerResponse = await _client.ExplainAsync(prompt, ct);
        stopwatch.Stop();

        if (providerResponse == null || string.IsNullOrWhiteSpace(providerResponse.SummaryVi))
        {
            throw new ValidationException("FAILURE_EXPLANATION_PROVIDER_INVALID_JSON: Thieu summaryVi.");
        }

        var generatedAt = DateTimeOffset.UtcNow;
        var cachePayload = new FailureExplanationCachePayload
        {
            SummaryVi = providerResponse.SummaryVi,
            PossibleCauses = providerResponse.PossibleCauses?.ToArray() ?? Array.Empty<string>(),
            SuggestedNextActions = providerResponse.SuggestedNextActions?.ToArray() ?? Array.Empty<string>(),
            Confidence = providerResponse.Confidence,
            Provider = prompt.Provider,
            Model = string.IsNullOrWhiteSpace(providerResponse.Model) ? prompt.Model : providerResponse.Model,
            TokensUsed = providerResponse.TokensUsed,
            GeneratedAt = generatedAt,
            FailureCodes = ExtractFailureCodes(sanitizedContext),
        };

        await TrySaveAuditAsync(prompt, providerResponse, context?.CreatedById ?? Guid.Empty, (int)stopwatch.ElapsedMilliseconds, ct);
        await TrySaveCacheAsync(endpointId, fingerprint, cachePayload, ct);

        return MapToModel(sanitizedContext, cachePayload, "live", (int)stopwatch.ElapsedMilliseconds);
    }

    private async Task TrySaveAuditAsync(
        FailureExplanationPrompt prompt,
        FailureExplanationProviderResponse providerResponse,
        Guid userId,
        int latencyMs,
        CancellationToken ct)
    {
        try
        {
            await _gatewayService.SaveInteractionAsync(new SaveLlmInteractionRequest
            {
                UserId = userId,
                InteractionType = FailureExplanationInteractionType,
                InputContext = JsonSerializer.Serialize(new
                {
                    prompt.Provider,
                    prompt.Model,
                    prompt.Prompt,
                    prompt.SanitizedContextJson,
                }, JsonOptions),
                LlmResponse = JsonSerializer.Serialize(providerResponse, JsonOptions),
                ModelUsed = string.IsNullOrWhiteSpace(providerResponse.Model) ? prompt.Model : providerResponse.Model,
                TokensUsed = providerResponse.TokensUsed,
                LatencyMs = latencyMs,
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to save FE-09 audit log. TestCaseId={TestCaseId}",
                prompt?.SanitizedContext?.Definition?.TestCaseId);
        }
    }

    private async Task TrySaveCacheAsync(
        Guid endpointId,
        string fingerprint,
        FailureExplanationCachePayload cachePayload,
        CancellationToken ct)
    {
        try
        {
            await _gatewayService.CacheSuggestionsAsync(
                endpointId,
                FailureExplanationSuggestionType,
                fingerprint,
                JsonSerializer.Serialize(cachePayload, JsonOptions),
                TimeSpan.FromHours(_options.CacheTtlHours > 0 ? _options.CacheTtlHours : 24),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to cache FE-09 explanation. EndpointId={EndpointId}",
                endpointId);
        }
    }

    private static FailureExplanationModel MapToModel(
        TestFailureExplanationContextDto context,
        FailureExplanationCachePayload payload,
        string source,
        int latencyMs)
    {
        return new FailureExplanationModel
        {
            TestSuiteId = context.TestSuiteId,
            TestRunId = context.TestRunId,
            TestCaseId = context.Definition?.TestCaseId ?? Guid.Empty,
            EndpointId = context.Definition?.EndpointId,
            SummaryVi = payload.SummaryVi,
            PossibleCauses = payload.PossibleCauses ?? Array.Empty<string>(),
            SuggestedNextActions = payload.SuggestedNextActions ?? Array.Empty<string>(),
            Confidence = payload.Confidence,
            Source = source,
            Provider = payload.Provider,
            Model = payload.Model,
            TokensUsed = payload.TokensUsed,
            LatencyMs = latencyMs,
            GeneratedAt = payload.GeneratedAt,
            FailureCodes = payload.FailureCodes ?? Array.Empty<string>(),
        };
    }

    private static Guid ResolveCacheEndpointId(TestFailureExplanationContextDto context)
    {
        return context?.Definition?.EndpointId ?? Guid.Empty;
    }

    private static IReadOnlyList<string> ExtractFailureCodes(TestFailureExplanationContextDto context)
    {
        return context?.ActualResult?.FailureReasons?
            .Select(x => x.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
    }

    private class FailureExplanationCachePayload
    {
        public string SummaryVi { get; set; }

        public IReadOnlyList<string> PossibleCauses { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> SuggestedNextActions { get; set; } = Array.Empty<string>();

        public string Confidence { get; set; }

        public string Provider { get; set; }

        public string Model { get; set; }

        public int TokensUsed { get; set; }

        public DateTimeOffset GeneratedAt { get; set; }

        public IReadOnlyList<string> FailureCodes { get; set; } = Array.Empty<string>();
    }
}
