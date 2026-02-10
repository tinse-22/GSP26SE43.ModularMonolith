using ClassifiedAds.Application;
using ClassifiedAds.Contracts.AuditLog.DTOs;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.ApiDocumentation.Authorization;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using ClassifiedAds.Modules.ApiDocumentation.Queries;
using ClassifiedAds.Modules.ApiDocumentation.RateLimiterPolicies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Controllers;

[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class ProjectsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<ProjectsController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
    }

    [Authorize(Permissions.GetProjects)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<ProjectModel>>> Get(
        [FromQuery] ProjectStatus? status,
        [FromQuery] string search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var safeSearch = search?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        _logger.LogInformation("Fetching projects. Status={Status}, Search={Search}, Page={Page}", status, safeSearch, page);

        var result = await _dispatcher.DispatchAsync(new GetProjectsQuery
        {
            OwnerId = _currentUser.UserId,
            Status = status,
            Search = search,
            Page = page,
            PageSize = pageSize,
        });

        return Ok(result);
    }

    [Authorize(Permissions.GetProjects)]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailModel>> Get(Guid id)
    {
        var project = await _dispatcher.DispatchAsync(new GetProjectQuery
        {
            ProjectId = id,
            OwnerId = _currentUser.UserId,
        });

        return Ok(project);
    }

    [Authorize(Permissions.AddProject)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectModel>> Post([FromBody] CreateUpdateProjectModel model)
    {
        var command = new AddUpdateProjectCommand
        {
            Model = model,
            CurrentUserId = _currentUser.UserId,
        };
        await _dispatcher.DispatchAsync(command);

        var result = await _dispatcher.DispatchAsync(new GetProjectQuery
        {
            ProjectId = command.SavedProjectId,
            OwnerId = _currentUser.UserId,
        });

        return Created($"/api/projects/{result.Id}", result);
    }

    [Authorize(Permissions.UpdateProject)]
    [HttpPut("{id}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectModel>> Put(Guid id, [FromBody] CreateUpdateProjectModel model)
    {
        var command = new AddUpdateProjectCommand
        {
            ProjectId = id,
            Model = model,
            CurrentUserId = _currentUser.UserId,
        };
        await _dispatcher.DispatchAsync(command);

        var result = await _dispatcher.DispatchAsync(new GetProjectQuery
        {
            ProjectId = command.SavedProjectId,
            OwnerId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.ArchiveProject)]
    [HttpPut("{id}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectModel>> Archive(Guid id)
    {
        await _dispatcher.DispatchAsync(new ArchiveProjectCommand
        {
            ProjectId = id,
            CurrentUserId = _currentUser.UserId,
            Archive = true,
        });

        var result = await _dispatcher.DispatchAsync(new GetProjectQuery
        {
            ProjectId = id,
            OwnerId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.ArchiveProject)]
    [HttpPut("{id}/unarchive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectModel>> Unarchive(Guid id)
    {
        await _dispatcher.DispatchAsync(new ArchiveProjectCommand
        {
            ProjectId = id,
            CurrentUserId = _currentUser.UserId,
            Archive = false,
        });

        var result = await _dispatcher.DispatchAsync(new GetProjectQuery
        {
            ProjectId = id,
            OwnerId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.DeleteProject)]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id)
    {
        await _dispatcher.DispatchAsync(new DeleteProjectCommand
        {
            ProjectId = id,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok();
    }

    [Authorize(Permissions.GetProjects)]
    [HttpGet("{id}/auditlogs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AuditLogEntryDTO>>> GetAuditLogs(Guid id)
    {
        var logs = await _dispatcher.DispatchAsync(new GetAuditEntriesQuery { ObjectId = id.ToString() });

        List<dynamic> entries = new List<dynamic>();
        ProjectModel previous = null;
        foreach (var log in logs.OrderBy(x => x.CreatedDateTime))
        {
            var data = JsonSerializer.Deserialize<ProjectModel>(log.Log);
            var highLight = new
            {
                Name = previous != null && data.Name != previous.Name,
                Description = previous != null && data.Description != previous.Description,
                BaseUrl = previous != null && data.BaseUrl != previous.BaseUrl,
                Status = previous != null && data.Status != previous.Status,
            };

            var entry = new
            {
                log.Id,
                log.UserName,
                Action = log.Action.Replace("_PROJECT", string.Empty),
                log.CreatedDateTime,
                data,
                highLight,
            };
            entries.Add(entry);

            previous = data;
        }

        return Ok(entries.OrderByDescending(x => x.CreatedDateTime));
    }
}
