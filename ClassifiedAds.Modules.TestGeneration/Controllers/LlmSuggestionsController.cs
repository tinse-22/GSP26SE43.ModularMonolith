using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.TestGeneration.Authorization;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Controllers;

/// <summary>
/// FE-15: LLM Suggestion Review workflow.
/// Manages pending LLM suggestions for a test suite before they become real test cases.
/// </summary>
[Authorize]
[Produces("application/json")]
[Route("api/test-suites/{suiteId:guid}/llm-suggestions")]
[ApiController]
public class LlmSuggestionsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<LlmSuggestionsController> _logger;

    public LlmSuggestionsController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<LlmSuggestionsController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Generate LLM suggestion previews for review.
    /// Calls LLM pipeline and persists suggestions as pending rows (not test cases).
    /// </summary>
    [Authorize(Permissions.GenerateBoundaryNegativeTestCases)]
    [HttpPost("generate")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(GenerateLlmSuggestionPreviewResultModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenerateLlmSuggestionPreviewResultModel>> GeneratePreview(
        Guid suiteId,
        [FromBody] GenerateLlmSuggestionPreviewRequest request)
    {
        var command = new GenerateLlmSuggestionPreviewCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            SpecificationId = request.SpecificationId,
            ForceRefresh = request.ForceRefresh,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Generated LLM suggestion preview. TestSuiteId={TestSuiteId}, TotalSuggestions={Total}, ActorUserId={ActorUserId}",
            suiteId, command.Result?.TotalSuggestions, _currentUser.UserId);

        return Created(
            $"/api/test-suites/{suiteId}/llm-suggestions",
            command.Result);
    }

    /// <summary>
    /// List LLM suggestions for a test suite with optional filters.
    /// </summary>
    [Authorize(Permissions.GetTestCases)]
    [HttpGet]
    [ProducesResponseType(typeof(List<LlmSuggestionModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<LlmSuggestionModel>>> GetAll(
        Guid suiteId,
        [FromQuery] string reviewStatus = null,
        [FromQuery] string testType = null,
        [FromQuery] Guid? endpointId = null)
    {
        var result = await _dispatcher.DispatchAsync(new GetLlmSuggestionsQuery
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            FilterByReviewStatus = reviewStatus,
            FilterByTestType = testType,
            FilterByEndpointId = endpointId,
        });

        return Ok(result);
    }

    /// <summary>
    /// Get full details of a specific LLM suggestion.
    /// </summary>
    [Authorize(Permissions.GetTestCases)]
    [HttpGet("{suggestionId:guid}")]
    [ProducesResponseType(typeof(LlmSuggestionModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LlmSuggestionModel>> GetById(Guid suiteId, Guid suggestionId)
    {
        var result = await _dispatcher.DispatchAsync(new GetLlmSuggestionDetailQuery
        {
            TestSuiteId = suiteId,
            SuggestionId = suggestionId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }

    /// <summary>
    /// Review an LLM suggestion: approve, reject, or modify and approve.
    /// </summary>
    [Authorize(Permissions.UpdateTestCase)]
    [HttpPut("{suggestionId:guid}/review")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(LlmSuggestionModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LlmSuggestionModel>> Review(
        Guid suiteId,
        Guid suggestionId,
        [FromBody] ReviewLlmSuggestionRequest request)
    {
        var command = new ReviewLlmSuggestionCommand
        {
            TestSuiteId = suiteId,
            SuggestionId = suggestionId,
            CurrentUserId = _currentUser.UserId,
            ReviewAction = request.Action,
            RowVersion = request.RowVersion,
            ReviewNotes = request.ReviewNotes,
            ModifiedContent = request.ModifiedContent,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Reviewed LLM suggestion. SuggestionId={SuggestionId}, Action={Action}, TestSuiteId={TestSuiteId}, ActorUserId={ActorUserId}",
            suggestionId, request.Action, suiteId, _currentUser.UserId);

        return Ok(command.Result);
    }

    /// <summary>
    /// Create or update current-user feedback for an LLM suggestion.
    /// </summary>
    [Authorize(Permissions.UpdateTestCase)]
    [HttpPut("{suggestionId:guid}/feedback")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(LlmSuggestionFeedbackModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LlmSuggestionFeedbackModel>> UpsertFeedback(
        Guid suiteId,
        Guid suggestionId,
        [FromBody] UpsertLlmSuggestionFeedbackRequest request)
    {
        var command = new UpsertLlmSuggestionFeedbackCommand
        {
            TestSuiteId = suiteId,
            SuggestionId = suggestionId,
            CurrentUserId = _currentUser.UserId,
            Signal = request.Signal,
            Notes = request.Notes,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Upserted LLM suggestion feedback. SuggestionId={SuggestionId}, TestSuiteId={TestSuiteId}, ActorUserId={ActorUserId}",
            suggestionId,
            suiteId,
            _currentUser.UserId);

        return Ok(command.Result);
    }

    /// <summary>
    /// Bulk review pending LLM suggestions with optional FE-15 style filters.
    /// FE-17 supports bulk approve and bulk reject actions.
    /// </summary>
    [Authorize(Permissions.UpdateTestCase)]
    [HttpPost("bulk-review")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkReviewLlmSuggestionsResultModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BulkReviewLlmSuggestionsResultModel>> BulkReview(
        Guid suiteId,
        [FromBody] BulkReviewLlmSuggestionsRequest request)
    {
        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            Action = request.Action,
            ReviewNotes = request.ReviewNotes,
            FilterBySuggestionType = request.FilterBySuggestionType,
            FilterByTestType = request.FilterByTestType,
            FilterByEndpointId = request.FilterByEndpointId,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Bulk reviewed LLM suggestions. TestSuiteId={TestSuiteId}, Action={Action}, ProcessedCount={ProcessedCount}, ActorUserId={ActorUserId}",
            suiteId,
            command.Result?.Action,
            command.Result?.ProcessedCount,
            _currentUser.UserId);

        return Ok(command.Result);
    }
}
