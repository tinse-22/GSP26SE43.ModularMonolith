using ClassifiedAds.Application;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Controllers;

/// <summary>
/// Callback endpoint for async n8n LLM suggestion refinement results.
/// Authentication is via the x-callback-api-key header instead of JWT.
/// </summary>
[AllowAnonymous]
[Produces("application/json")]
[Route("api/test-generation/llm-suggestions/callback")]
[ApiController]
public class LlmSuggestionRefinementCallbackController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dispatcher _dispatcher;
    private readonly N8nIntegrationOptions _n8nOptions;
    private readonly ILogger<LlmSuggestionRefinementCallbackController> _logger;

    public LlmSuggestionRefinementCallbackController(
        Dispatcher dispatcher,
        IOptions<N8nIntegrationOptions> n8nOptions,
        ILogger<LlmSuggestionRefinementCallbackController> logger)
    {
        _dispatcher = dispatcher;
        _n8nOptions = n8nOptions?.Value ?? new N8nIntegrationOptions();
        _logger = logger;
    }

    [HttpPost("{jobId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceiveLlmSuggestionRefinementResult(
        Guid jobId,
        [FromHeader(Name = "x-callback-api-key")] string callbackApiKey,
        [FromBody] JsonElement request)
    {
        var expectedKey = _n8nOptions.CallbackApiKey;
        if (string.IsNullOrWhiteSpace(expectedKey) || callbackApiKey != expectedKey)
        {
            _logger.LogWarning(
                "Unauthorized LLM suggestion refinement callback attempt. JobId={JobId}",
                jobId);
            return Unauthorized();
        }

        var parsed = TryParseCallbackPayload(request, out var response, out var parseError);
        if (!parsed)
        {
            await _dispatcher.DispatchAsync(new ProcessLlmSuggestionRefinementCallbackCommand
            {
                JobId = jobId,
                Response = new N8nBoundaryNegativeResponse(),
            });

            _logger.LogWarning(
                "Invalid LLM suggestion refinement callback payload. JobId={JobId}, Error={Error}",
                jobId,
                parseError);

            return BadRequest(parseError);
        }

        await _dispatcher.DispatchAsync(new ProcessLlmSuggestionRefinementCallbackCommand
        {
            JobId = jobId,
            Response = response,
        });

        _logger.LogInformation(
            "LLM suggestion refinement callback processed. JobId={JobId}, ScenarioCount={ScenarioCount}",
            jobId,
            response.Scenarios?.Count ?? 0);

        return NoContent();
    }

    public static bool TryParseCallbackPayload(
        JsonElement payload,
        out N8nBoundaryNegativeResponse response,
        out string error)
    {
        response = null;
        error = null;

        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            error = "Request body is required.";
            return false;
        }

        try
        {
            var normalized = UnwrapN8nPayload(payload);
            if (normalized.ValueKind == JsonValueKind.String)
            {
                var raw = normalized.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    error = "Request body is empty.";
                    return false;
                }

                using var document = JsonDocument.Parse(raw);
                return TryParseCallbackPayload(document.RootElement.Clone(), out response, out error);
            }

            if (normalized.ValueKind == JsonValueKind.Array)
            {
                normalized = normalized.EnumerateArray().FirstOrDefault();
            }

            if (normalized.ValueKind != JsonValueKind.Object)
            {
                error = "Request body must be a JSON object.";
                return false;
            }

            if (TryGetProperty(normalized, "scenarios", out _))
            {
                response = normalized.Deserialize<N8nBoundaryNegativeResponse>(JsonOptions);
                return response != null;
            }

            if (TryGetProperty(normalized, "testCases", out var testCasesElement))
            {
                var testCases = testCasesElement.Deserialize<List<N8nGeneratedTestCase>>(JsonOptions);
                response = new N8nBoundaryNegativeResponse
                {
                    Model = TryGetString(normalized, "model"),
                    TokensUsed = TryGetInt(normalized, "tokensUsed"),
                    Scenarios = testCases?
                        .Select(MapTestCaseToScenario)
                        .Where(x => x.EndpointId != Guid.Empty)
                        .ToList() ?? new List<N8nSuggestedScenario>(),
                };

                return true;
            }

            error = "Request body must contain either scenarios or testCases.";
            return false;
        }
        catch (JsonException ex)
        {
            error = $"Request body JSON is invalid: {ex.Message}";
            return false;
        }
    }

    private static JsonElement UnwrapN8nPayload(JsonElement payload)
    {
        var current = payload;
        foreach (var wrapper in new[] { "body", "json", "data", "result", "output" })
        {
            if (current.ValueKind == JsonValueKind.Object && TryGetProperty(current, wrapper, out var wrapped))
            {
                current = wrapped;
            }
        }

        return current;
    }

    private static N8nSuggestedScenario MapTestCaseToScenario(N8nGeneratedTestCase testCase)
    {
        return new N8nSuggestedScenario
        {
            EndpointId = testCase.EndpointId,
            ScenarioName = testCase.Name,
            Description = testCase.Description,
            TestType = testCase.TestType,
            Priority = testCase.Priority,
            Tags = testCase.Tags ?? new List<string>(),
            Request = testCase.Request,
            Expectation = testCase.Expectation,
            Variables = testCase.Variables ?? new List<N8nTestCaseVariable>(),
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string TryGetString(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => null,
        };
    }
}
