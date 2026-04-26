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
/// FE-18B: Callback endpoint for n8n Phase 1 SRS analysis results.
/// Called by n8n after LLM extracts requirements from the SRS document.
/// Authentication is via the x-callback-api-key header (shared secret) instead of JWT.
/// </summary>
[AllowAnonymous]
[Produces("application/json")]
[Route("api/srs-analysis-callback")]
[ApiController]
public class SrsAnalysisCallbackController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly N8nIntegrationOptions _n8nOptions;
    private readonly ILogger<SrsAnalysisCallbackController> _logger;

    public SrsAnalysisCallbackController(
        Dispatcher dispatcher,
        IOptions<N8nIntegrationOptions> n8nOptions,
        ILogger<SrsAnalysisCallbackController> logger)
    {
        _dispatcher = dispatcher;
        _n8nOptions = n8nOptions?.Value ?? new N8nIntegrationOptions();
        _logger = logger;
    }

    /// <summary>
    /// Receives LLM-extracted requirements from n8n Phase 1 analysis.
    /// Creates SrsRequirement records and SrsRequirementClarification records.
    /// </summary>
    [HttpPost("{jobId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceiveSrsAnalysisResult(
        Guid jobId,
        [FromHeader(Name = "x-callback-api-key")] string callbackApiKey,
        [FromBody] SrsAnalysisCallbackRequest request)
    {
        var expectedKey = _n8nOptions.CallbackApiKey;
        if (string.IsNullOrWhiteSpace(expectedKey) || callbackApiKey != expectedKey)
        {
            _logger.LogWarning(
                "Unauthorized SRS analysis callback attempt. JobId={JobId}",
                jobId);
            return Unauthorized();
        }

        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        await _dispatcher.DispatchAsync(new ProcessSrsAnalysisCallbackCommand
        {
            JobId = jobId,
            Requirements = request.Requirements ?? new System.Collections.Generic.List<N8nSrsRequirementResult>(),
            ClarificationQuestions = request.ClarificationQuestions ?? new System.Collections.Generic.List<N8nSrsClarificationQuestion>(),
            ErrorMessage = request.ErrorMessage,
        });

        _logger.LogInformation(
            "SRS analysis callback processed. JobId={JobId}, RequirementsCount={Count}, ClarificationsCount={Clarifications}",
            jobId,
            request.Requirements?.Count ?? 0,
            request.ClarificationQuestions?.Count ?? 0);

        return NoContent();
    }
}
