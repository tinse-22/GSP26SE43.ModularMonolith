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
        [FromBody] N8nBoundaryNegativeResponse request)
    {
        var expectedKey = _n8nOptions.CallbackApiKey;
        if (string.IsNullOrWhiteSpace(expectedKey) || callbackApiKey != expectedKey)
        {
            _logger.LogWarning(
                "Unauthorized LLM suggestion refinement callback attempt. JobId={JobId}",
                jobId);
            return Unauthorized();
        }

        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        await _dispatcher.DispatchAsync(new ProcessLlmSuggestionRefinementCallbackCommand
        {
            JobId = jobId,
            Response = request,
        });

        _logger.LogInformation(
            "LLM suggestion refinement callback processed. JobId={JobId}, ScenarioCount={ScenarioCount}",
            jobId,
            request.Scenarios?.Count ?? 0);

        return NoContent();
    }
}
