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
    /// Queue LLM suggestion generation for review.
    /// Suggestions are persisted only after n8n posts the async callback result.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.GenerateBoundaryNegativeTestCases)]
    [HttpPost("generate")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(GenerateTestsAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenerateTestsAcceptedResponse>> GeneratePreview(
        Guid suiteId,
        [FromBody] GenerateLlmSuggestionPreviewRequest request)
    {
        var command = new GenerateLlmSuggestionPreviewCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            SpecificationId = request.SpecificationId,
            ForceRefresh = request.ForceRefresh,
            AlgorithmProfile = request.AlgorithmProfile ?? new GenerationAlgorithmProfile(),
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Queued LLM suggestion generation. TestSuiteId={TestSuiteId}, JobId={JobId}, ActorUserId={ActorUserId}",
            suiteId, command.JobId, _currentUser.UserId);

        return Accepted(new GenerateTestsAcceptedResponse
        {
            JobId = command.JobId,
            TestSuiteId = suiteId,
            Mode = "callback",
            Message = "Đã tạo job và đưa yêu cầu trigger n8n vào hàng đợi. Suggestions sẽ xuất hiện sau khi callback hoàn tất.",
        });
    }

    /// <summary>
    /// List LLM suggestions for a test suite with optional filters.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.GetTestCases)]
    [HttpGet]
    [ProducesResponseType(typeof(List<LlmSuggestionModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<LlmSuggestionModel>>> GetAll(
        Guid suiteId,
        [FromQuery] string reviewStatus = null,
        [FromQuery] string testType = null,
        [FromQuery] Guid? endpointId = null,
        [FromQuery] bool includeDeleted = false)
    {
        var result = await _dispatcher.DispatchAsync(new GetLlmSuggestionsQuery
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            FilterByReviewStatus = reviewStatus,
            FilterByTestType = testType,
            FilterByEndpointId = endpointId,
            IncludeDeleted = includeDeleted,
        });

        return Ok(result);
    }

    /// <summary>
    /// Get full details of a specific LLM suggestion.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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

    /// <summary>
    /// Bulk soft-delete LLM suggestions.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.UpdateTestCase)]
    [HttpPost("bulk-delete")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkOperationResultModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BulkOperationResultModel>> BulkDelete(
        Guid suiteId,
        [FromBody] BulkDeleteLlmSuggestionsRequest request)
    {
        var command = new BulkDeleteLlmSuggestionsCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            SuggestionIds = request.SuggestionIds,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Bulk soft-deleted LLM suggestions. TestSuiteId={TestSuiteId}, ProcessedCount={ProcessedCount}, ActorUserId={ActorUserId}",
            suiteId, command.Result?.ProcessedCount, _currentUser.UserId);

        return Ok(command.Result);
    }

    /// <summary>
    /// Bulk restore soft-deleted LLM suggestions.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.UpdateTestCase)]
    [HttpPost("bulk-restore")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkOperationResultModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BulkOperationResultModel>> BulkRestore(
        Guid suiteId,
        [FromBody] BulkRestoreLlmSuggestionsRequest request)
    {
        var command = new BulkRestoreLlmSuggestionsCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            SuggestionIds = request.SuggestionIds,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Bulk restored LLM suggestions. TestSuiteId={TestSuiteId}, ProcessedCount={ProcessedCount}, ActorUserId={ActorUserId}",
            suiteId, command.Result?.ProcessedCount, _currentUser.UserId);

        return Ok(command.Result);
    }
}
