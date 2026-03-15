using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.LlmAssistant.Commands;
using ClassifiedAds.Modules.LlmAssistant.Models;
using ClassifiedAds.Modules.LlmAssistant.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.LlmAssistant.Controllers;

[Authorize]
[Produces("application/json")]
[Route("api/test-suites/{suiteId:guid}/test-runs/{runId:guid}/failures")]
[ApiController]
public class FailureExplanationsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;

    public FailureExplanationsController(
        Dispatcher dispatcher,
        ICurrentUser currentUser)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
    }

    [Authorize("Permission:GetTestRuns")]
    [HttpGet("{testCaseId:guid}/explanation")]
    [ProducesResponseType(typeof(FailureExplanationModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FailureExplanationModel>> GetFailureExplanation(
        Guid suiteId,
        Guid runId,
        Guid testCaseId)
    {
        var result = await _dispatcher.DispatchAsync(new GetFailureExplanationQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            TestCaseId = testCaseId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize("Permission:GetTestRuns")]
    [HttpPost("{testCaseId:guid}/explanation")]
    [ProducesResponseType(typeof(FailureExplanationModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FailureExplanationModel>> ExplainFailure(
        Guid suiteId,
        Guid runId,
        Guid testCaseId)
    {
        var command = new ExplainTestFailureCommand
        {
            TestSuiteId = suiteId,
            RunId = runId,
            TestCaseId = testCaseId,
            CurrentUserId = _currentUser.UserId,
        };

        await _dispatcher.DispatchAsync(command);
        return Ok(command.Result);
    }
}
