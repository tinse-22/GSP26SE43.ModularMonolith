using ClassifiedAds.Application;
using ClassifiedAds.Application.Common.DTOs;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.TestExecution.Authorization;
using ClassifiedAds.Modules.TestExecution.Commands;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Models.Requests;
using ClassifiedAds.Modules.TestExecution.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Controllers;

[Authorize]
[Produces("application/json")]
[Route("api/test-suites/{suiteId:guid}/test-runs")]
[ApiController]
public class TestRunsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<TestRunsController> _logger;

    public TestRunsController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<TestRunsController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
    }

    [Authorize(Permissions.StartTestRun)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TestRunResultModel>> StartTestRun(
        Guid suiteId,
        [FromBody] StartTestRunRequest request)
    {
        var command = new StartTestRunCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            EnvironmentId = request?.EnvironmentId,
            StrictValidation = request?.StrictValidation ?? false,
            SelectedTestCaseIds = request?.SelectedTestCaseIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList(),
        };

        await _dispatcher.DispatchAsync(command);

        return StatusCode(StatusCodes.Status201Created, command.Result);
    }

    [Authorize(Permissions.GetTestRuns)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<Paged<TestRunModel>>> GetTestRuns(
        Guid suiteId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string status = null)
    {
        TestRunStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TestRunStatus>(status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var result = await _dispatcher.DispatchAsync(new GetTestRunsQuery
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Status = statusFilter,
        });

        return Ok(result);
    }

    [Authorize(Permissions.GetTestRuns)]
    [HttpGet("{runId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestRunModel>> GetTestRun(Guid suiteId, Guid runId)
    {
        var result = await _dispatcher.DispatchAsync(new GetTestRunQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.GetTestRuns)]
    [HttpGet("{runId:guid}/results")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TestRunResultModel>> GetTestRunResults(Guid suiteId, Guid runId)
    {
        var result = await _dispatcher.DispatchAsync(new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }
}
