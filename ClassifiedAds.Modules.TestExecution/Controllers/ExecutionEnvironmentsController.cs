using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.TestExecution.Authorization;
using ClassifiedAds.Modules.TestExecution.Commands;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Models.Requests;
using ClassifiedAds.Modules.TestExecution.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Controllers;

[Authorize]
[Produces("application/json")]
[Route("api/projects/{projectId:guid}/execution-environments")]
[ApiController]
public class ExecutionEnvironmentsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ExecutionEnvironmentsController> _logger;

    public ExecutionEnvironmentsController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<ExecutionEnvironmentsController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
    }

    [Authorize(Permissions.GetExecutionEnvironments)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExecutionEnvironmentModel>>> GetAll(Guid projectId)
    {
        var result = await _dispatcher.DispatchAsync(new GetExecutionEnvironmentsQuery
        {
            ProjectId = projectId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.GetExecutionEnvironments)]
    [HttpGet("{environmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExecutionEnvironmentModel>> GetById(Guid projectId, Guid environmentId)
    {
        var result = await _dispatcher.DispatchAsync(new GetExecutionEnvironmentQuery
        {
            ProjectId = projectId,
            EnvironmentId = environmentId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.AddExecutionEnvironment)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExecutionEnvironmentModel>> Create(
        Guid projectId,
        [FromBody] CreateExecutionEnvironmentRequest request)
    {
        var command = new AddUpdateExecutionEnvironmentCommand
        {
            ProjectId = projectId,
            CurrentUserId = _currentUser.UserId,
            Name = request.Name,
            BaseUrl = request.BaseUrl,
            Variables = request.Variables,
            Headers = request.Headers,
            AuthConfig = request.AuthConfig,
            IsDefault = request.IsDefault,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Created execution environment. EnvironmentId={EnvironmentId}, ProjectId={ProjectId}",
            command.Result?.Id, projectId);

        return Created(
            $"/api/projects/{projectId}/execution-environments/{command.Result?.Id}",
            command.Result);
    }

    [Authorize(Permissions.UpdateExecutionEnvironment)]
    [HttpPut("{environmentId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExecutionEnvironmentModel>> Update(
        Guid projectId,
        Guid environmentId,
        [FromBody] UpdateExecutionEnvironmentRequest request)
    {
        var command = new AddUpdateExecutionEnvironmentCommand
        {
            EnvironmentId = environmentId,
            ProjectId = projectId,
            CurrentUserId = _currentUser.UserId,
            Name = request.Name,
            BaseUrl = request.BaseUrl,
            Variables = request.Variables,
            Headers = request.Headers,
            AuthConfig = request.AuthConfig,
            IsDefault = request.IsDefault,
            RowVersion = request.RowVersion,
        };

        await _dispatcher.DispatchAsync(command);

        return Ok(command.Result);
    }

    [Authorize(Permissions.DeleteExecutionEnvironment)]
    [HttpDelete("{environmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(
        Guid projectId,
        Guid environmentId,
        [FromQuery] string rowVersion)
    {
        await _dispatcher.DispatchAsync(new DeleteExecutionEnvironmentCommand
        {
            EnvironmentId = environmentId,
            ProjectId = projectId,
            CurrentUserId = _currentUser.UserId,
            RowVersion = rowVersion,
        });

        return NoContent();
    }
}
