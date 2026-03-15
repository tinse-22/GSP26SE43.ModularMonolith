using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.LlmAssistant.ConfigurationOptions;
using ClassifiedAds.Modules.LlmAssistant.Models;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public class N8nFailureExplanationClient : ILlmFailureExplanationClient
{
    public const string HttpClientName = "LlmAssistant.FailureExplanation";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FailureExplanationOptions _options;

    public N8nFailureExplanationClient(
        IHttpClientFactory httpClientFactory,
        IOptions<LlmAssistantModuleOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options?.Value?.FailureExplanation ?? new FailureExplanationOptions();
    }

    public async Task<FailureExplanationProviderResponse> ExplainAsync(
        FailureExplanationPrompt prompt,
        CancellationToken ct = default)
    {
        if (prompt == null)
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        if (client.BaseAddress == null && string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new ValidationException("FAILURE_EXPLANATION_PROVIDER_HTTP_ERROR: BaseUrl chua duoc cau hinh.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.WebhookPath)
        {
            Content = JsonContent.Create(new
            {
                provider = prompt.Provider,
                model = prompt.Model,
                prompt = prompt.Prompt,
                context = prompt.SanitizedContextJson,
            }),
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", _options.ApiKey);
        }

        using var response = await client.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new ConflictException(
                "FAILURE_EXPLANATION_PROVIDER_HTTP_ERROR",
                $"Failure explanation provider tra ve HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        try
        {
            return ParseResponse(responseText);
        }
        catch (JsonException ex)
        {
            throw new ValidationException("FAILURE_EXPLANATION_PROVIDER_INVALID_JSON: Provider response khong phai JSON hop le.", ex);
        }
    }

    private static FailureExplanationProviderResponse ParseResponse(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        if (TryMapResponse(document.RootElement, out var result))
        {
            return result;
        }

        foreach (var wrapperName in new[] { "data", "result", "response", "output" })
        {
            if (!document.RootElement.TryGetProperty(wrapperName, out var nested))
            {
                continue;
            }

            if (TryMapResponse(nested, out result))
            {
                return result;
            }

            if (nested.ValueKind == JsonValueKind.String)
            {
                using var nestedDocument = JsonDocument.Parse(nested.GetString());
                if (TryMapResponse(nestedDocument.RootElement, out result))
                {
                    return result;
                }
            }
        }

        throw new ValidationException("FAILURE_EXPLANATION_PROVIDER_INVALID_JSON: Thieu cac truong JSON bat buoc.");
    }

    private static bool TryMapResponse(JsonElement element, out FailureExplanationProviderResponse response)
    {
        response = null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetRequiredString(element, "summaryVi", out var summaryVi)
            || !TryGetStringArray(element, "possibleCauses", out var possibleCauses)
            || !TryGetStringArray(element, "suggestedNextActions", out var suggestedNextActions)
            || !TryGetRequiredString(element, "confidence", out var confidence)
            || !TryGetRequiredString(element, "model", out var model)
            || !TryGetRequiredInt(element, "tokensUsed", out var tokensUsed))
        {
            return false;
        }

        response = new FailureExplanationProviderResponse
        {
            SummaryVi = summaryVi,
            PossibleCauses = possibleCauses,
            SuggestedNextActions = suggestedNextActions,
            Confidence = confidence,
            Model = model,
            TokensUsed = tokensUsed,
        };

        return true;
    }

    private static bool TryGetRequiredString(JsonElement element, string propertyName, out string value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetRequiredInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static bool TryGetStringArray(JsonElement element, string propertyName, out string[] values)
    {
        values = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        values = property.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return true;
    }
}
