using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.TestGeneration.Authorization;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Controllers;

[Authorize]
[Produces("application/json")]
[Route("api/test-suites/{suiteId:guid}")]
[ApiController]
public class TestOrderController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly N8nIntegrationOptions _n8nOptions;
    private readonly ILogger<TestOrderController> _logger;

    public TestOrderController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        IOptions<N8nIntegrationOptions> n8nOptions,
        ILogger<TestOrderController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _n8nOptions = n8nOptions.Value;
        _logger = logger;
    }

    [Authorize(Permissions.ProposeTestOrder)]
    [HttpPost("order-proposals")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiTestOrderProposalModel>> Propose(
        Guid suiteId,
        [FromBody] ProposeApiTestOrderRequest request)
    {
        var command = new ProposeApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            SpecificationId = request.SpecificationId,
            SelectedEndpointIds = request.SelectedEndpointIds,
            Source = request.Source,
            LlmModel = request.LlmModel,
            ReasoningNote = request.ReasoningNote,
        };

        await _dispatcher.DispatchAsync(command);
        _logger.LogInformation(
            "Created test order proposal. TestSuiteId={TestSuiteId}, ProposalId={ProposalId}, ActorUserId={ActorUserId}",
            suiteId,
            command.Result?.ProposalId,
            _currentUser.UserId);

        return Created($"/api/test-suites/{suiteId}/order-proposals/{command.Result?.ProposalId}", command.Result);
    }

    [Authorize(Permissions.GetTestOrderProposal)]
    [HttpGet("order-proposals/latest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiTestOrderProposalModel>> GetLatest(Guid suiteId)
    {
        var proposal = await _dispatcher.DispatchAsync(new GetLatestApiTestOrderProposalQuery
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(proposal);
    }

    [Authorize(Permissions.ReorderTestOrder)]
    [HttpPut("order-proposals/{proposalId:guid}/reorder")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiTestOrderProposalModel>> Reorder(
        Guid suiteId,
        Guid proposalId,
        [FromBody] ReorderApiTestOrderRequest request)
    {
        var command = new ReorderApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = _currentUser.UserId,
            RowVersion = request.RowVersion,
            OrderedEndpointIds = request.OrderedEndpointIds,
            ReviewNotes = request.ReviewNotes,
        };

        await _dispatcher.DispatchAsync(command);
        return Ok(command.Result);
    }

    [Authorize(Permissions.ApproveTestOrder)]
    [HttpPost("order-proposals/{proposalId:guid}/approve")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiTestOrderProposalModel>> Approve(
        Guid suiteId,
        Guid proposalId,
        [FromBody] ApproveApiTestOrderRequest request)
    {
        var command = new ApproveApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = _currentUser.UserId,
            RowVersion = request.RowVersion,
            ReviewNotes = request.ReviewNotes,
        };

        await _dispatcher.DispatchAsync(command);
        return Ok(command.Result);
    }

    [Authorize(Permissions.ApproveTestOrder)]
    [HttpPost("order-proposals/{proposalId:guid}/reject")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiTestOrderProposalModel>> Reject(
        Guid suiteId,
        Guid proposalId,
        [FromBody] RejectApiTestOrderRequest request)
    {
        var command = new RejectApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = _currentUser.UserId,
            RowVersion = request.RowVersion,
            ReviewNotes = request.ReviewNotes,
        };

        await _dispatcher.DispatchAsync(command);
        return Ok(command.Result);
    }

    [Authorize(Permissions.GetTestOrderProposal)]
    [HttpGet("order-gate-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiTestOrderGateStatusModel>> GetGateStatus(Guid suiteId)
    {
        var status = await _dispatcher.DispatchAsync(new GetApiTestOrderGateStatusQuery
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(status);
    }

    [Authorize(Permissions.GetTestSuites)]
    [HttpGet("test-cases")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TestCaseModel>>> GetTestCases(Guid suiteId)
    {
        var result = await _dispatcher.DispatchAsync(new GetTestCasesByTestSuiteQuery
        {
            TestSuiteId = suiteId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.GenerateTestCases)]
    [HttpPost("generate-tests")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GenerateTests(Guid suiteId)
    {
        await _dispatcher.DispatchAsync(new GenerateTestCasesCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
        });

        _logger.LogInformation(
            "Triggered test generation. TestSuiteId={TestSuiteId}, ActorUserId={ActorUserId}",
            suiteId, _currentUser.UserId);

        return Accepted();
    }

    /// <summary>
    /// Callback endpoint called by n8n after AI test-case generation.
    /// Authentication is via the x-callback-api-key header (shared secret) instead of JWT.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("test-cases/from-ai")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReceiveAiGeneratedTestCases(
        Guid suiteId,
        [FromHeader(Name = "x-callback-api-key")] string callbackApiKey,
        [FromBody] N8nTestCasesCallbackRequest request)
    {
        var expectedKey = _n8nOptions.CallbackApiKey;
        if (string.IsNullOrWhiteSpace(expectedKey) || callbackApiKey != expectedKey)
        {
            _logger.LogWarning(
                "Unauthorized n8n callback attempt for TestSuiteId={TestSuiteId}", suiteId);
            return Unauthorized();
        }

        if (request?.TestCases is null || request.TestCases.Count == 0)
        {
            return BadRequest("testCases array is required and must not be empty.");
        }

        await _dispatcher.DispatchAsync(new SaveAiGeneratedTestCasesCommand
        {
            TestSuiteId = suiteId,
            TestCases = request.TestCases,
        });

        _logger.LogInformation(
            "Received {Count} AI-generated test cases from n8n for TestSuiteId={TestSuiteId}",
            request.TestCases.Count, suiteId);

        return NoContent();
    }
}

public class N8nTestCasesCallbackRequest
{
    public List<AiGeneratedTestCaseDto> TestCases { get; set; } = new();
}
