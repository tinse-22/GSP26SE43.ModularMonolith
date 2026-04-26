using ClassifiedAds.Application;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Controllers;

/// <summary>
/// FE-18C: Callback endpoint for n8n Phase 1.5 SRS requirement refinement results.
/// Called by n8n after LLM refines requirements based on user clarification answers.
/// Authentication is via the x-callback-api-key header (shared secret) instead of JWT.
/// </summary>
[AllowAnonymous]
[Produces("application/json")]
[Route("api/srs-refinement-callback")]
[ApiController]
public class SrsRefinementCallbackController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly N8nIntegrationOptions _n8nOptions;
    private readonly ILogger<SrsRefinementCallbackController> _logger;

    public SrsRefinementCallbackController(
        Dispatcher dispatcher,
        IOptions<N8nIntegrationOptions> n8nOptions,
        ILogger<SrsRefinementCallbackController> logger)
    {
        _dispatcher = dispatcher;
        _n8nOptions = n8nOptions?.Value ?? new N8nIntegrationOptions();
        _logger = logger;
    }

    /// <summary>
    /// Receives refined constraints from n8n Phase 1.5.
    /// Updates RefinedConstraints, RefinedConfidenceScore, RefinementRound, and sets IsReviewed = true.
    /// </summary>
    [HttpPost("{jobId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceiveSrsRefinementResult(
        Guid jobId,
        [FromHeader(Name = "x-callback-api-key")] string callbackApiKey,
        [FromBody] SrsRefinementCallbackRequest request)
    {
        var expectedKey = _n8nOptions.CallbackApiKey;
        if (string.IsNullOrWhiteSpace(expectedKey) || callbackApiKey != expectedKey)
        {
            _logger.LogWarning(
                "Unauthorized SRS refinement callback attempt. JobId={JobId}",
                jobId);
            return Unauthorized();
        }

        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        await _dispatcher.DispatchAsync(new ProcessSrsRefinementCallbackCommand
        {
            JobId = jobId,
            RefinedRequirements = request.RefinedRequirements ?? new System.Collections.Generic.List<N8nSrsRefinedRequirement>(),
        });

        _logger.LogInformation(
            "SRS refinement callback processed. JobId={JobId}, RefinedCount={Count}",
            jobId, request.RefinedRequirements?.Count ?? 0);

        return NoContent();
    }
}
