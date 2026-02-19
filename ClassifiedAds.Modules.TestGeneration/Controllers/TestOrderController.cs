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
    private readonly ILogger<TestOrderController> _logger;

    public TestOrderController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<TestOrderController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
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
}
