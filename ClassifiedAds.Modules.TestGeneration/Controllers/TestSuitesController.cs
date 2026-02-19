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

[Authorize]
[Produces("application/json")]
[Route("api/projects/{projectId:guid}/test-suites")]
[ApiController]
public class TestSuitesController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<TestSuitesController> _logger;

    public TestSuitesController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<TestSuitesController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
    }

    [Authorize(Permissions.GetTestSuites)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TestSuiteScopeModel>>> GetAll(Guid projectId)
    {
        var result = await _dispatcher.DispatchAsync(new GetTestSuiteScopesQuery
        {
            ProjectId = projectId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.GetTestSuites)]
    [HttpGet("{suiteId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestSuiteScopeModel>> GetById(Guid projectId, Guid suiteId)
    {
        var result = await _dispatcher.DispatchAsync(new GetTestSuiteScopeQuery
        {
            ProjectId = projectId,
            SuiteId = suiteId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.AddTestSuite)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TestSuiteScopeModel>> Create(
        Guid projectId,
        [FromBody] CreateTestSuiteScopeRequest request)
    {
        var command = new AddUpdateTestSuiteScopeCommand
        {
            ProjectId = projectId,
            CurrentUserId = _currentUser.UserId,
            Name = request.Name,
            Description = request.Description,
            ApiSpecId = request.ApiSpecId,
            GenerationType = request.GenerationType,
            SelectedEndpointIds = request.SelectedEndpointIds,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Created test suite scope. SuiteId={SuiteId}, ProjectId={ProjectId}, ActorUserId={ActorUserId}",
            command.Result?.Id, projectId, _currentUser.UserId);

        return Created(
            $"/api/projects/{projectId}/test-suites/{command.Result?.Id}",
            command.Result);
    }

    [Authorize(Permissions.UpdateTestSuite)]
    [HttpPut("{suiteId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TestSuiteScopeModel>> Update(
        Guid projectId,
        Guid suiteId,
        [FromBody] UpdateTestSuiteScopeRequest request)
    {
        var command = new AddUpdateTestSuiteScopeCommand
        {
            SuiteId = suiteId,
            ProjectId = projectId,
            CurrentUserId = _currentUser.UserId,
            Name = request.Name,
            Description = request.Description,
            ApiSpecId = request.ApiSpecId,
            GenerationType = request.GenerationType,
            SelectedEndpointIds = request.SelectedEndpointIds,
            RowVersion = request.RowVersion,
        };

        await _dispatcher.DispatchAsync(command);

        return Ok(command.Result);
    }

    [Authorize(Permissions.DeleteTestSuite)]
    [HttpDelete("{suiteId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Archive(
        Guid projectId,
        Guid suiteId,
        [FromQuery] string rowVersion)
    {
        await _dispatcher.DispatchAsync(new ArchiveTestSuiteScopeCommand
        {
            SuiteId = suiteId,
            ProjectId = projectId,
            CurrentUserId = _currentUser.UserId,
            RowVersion = rowVersion,
        });

        return NoContent();
    }
}
