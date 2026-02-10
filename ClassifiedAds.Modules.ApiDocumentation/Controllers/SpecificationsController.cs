using ClassifiedAds.Application;
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
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Controllers;

[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Authorize]
[Produces("application/json")]
[Route("api/projects/{projectId}/specifications")]
[ApiController]
public class SpecificationsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<SpecificationsController> _logger;

    public SpecificationsController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<SpecificationsController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
    }

    [Authorize(Permissions.GetSpecifications)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SpecificationModel>>> Get(
        Guid projectId,
        [FromQuery] ParseStatus? parseStatus,
        [FromQuery] SourceType? sourceType)
    {
        _logger.LogInformation("Fetching specifications for project {ProjectId}.", projectId);

        var result = await _dispatcher.DispatchAsync(new GetSpecificationsQuery
        {
            ProjectId = projectId,
            OwnerId = _currentUser.UserId,
            ParseStatus = parseStatus,
            SourceType = sourceType,
        });

        return Ok(result);
    }

    [Authorize(Permissions.GetSpecifications)]
    [HttpGet("{specId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpecificationDetailModel>> GetById(Guid projectId, Guid specId)
    {
        var result = await _dispatcher.DispatchAsync(new GetSpecificationQuery
        {
            SpecId = specId,
            ProjectId = projectId,
            OwnerId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.AddSpecification)]
    [HttpGet("upload-methods")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<List<UploadMethodOptionModel>> GetUploadMethods(Guid projectId)
    {
        return Ok(new List<UploadMethodOptionModel>
        {
            new UploadMethodOptionModel
            {
                Method = SpecificationUploadMethod.StorageGatewayContract.ToString(),
                UploadApi = $"/api/projects/{projectId}/specifications/upload",
            },
        });
    }

    [Authorize(Permissions.AddSpecification)]
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpecificationModel>> Upload(
        Guid projectId,
        [FromForm] UploadSpecificationModel model)
    {
        _logger.LogInformation("Uploading specification for project {ProjectId}.", projectId);

        var command = new UploadApiSpecificationCommand
        {
            ProjectId = projectId,
            CurrentUserId = _currentUser.UserId,
            UploadMethod = model.UploadMethod,
            File = model.File,
            Name = model.Name,
            SourceType = model.SourceType,
            Version = model.Version,
            AutoActivate = model.AutoActivate,
        };

        await _dispatcher.DispatchAsync(command);

        var result = await _dispatcher.DispatchAsync(new GetSpecificationQuery
        {
            SpecId = command.SavedSpecId,
            ProjectId = projectId,
            OwnerId = _currentUser.UserId,
        });

        return Created($"/api/projects/{projectId}/specifications/{result.Id}", result);
    }

    [Authorize(Permissions.ActivateSpecification)]
    [HttpPut("{specId}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpecificationModel>> Activate(Guid projectId, Guid specId)
    {
        await _dispatcher.DispatchAsync(new ActivateSpecificationCommand
        {
            ProjectId = projectId,
            SpecId = specId,
            CurrentUserId = _currentUser.UserId,
            Activate = true,
        });

        var result = await _dispatcher.DispatchAsync(new GetSpecificationQuery
        {
            SpecId = specId,
            ProjectId = projectId,
            OwnerId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.ActivateSpecification)]
    [HttpPut("{specId}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpecificationModel>> Deactivate(Guid projectId, Guid specId)
    {
        await _dispatcher.DispatchAsync(new ActivateSpecificationCommand
        {
            ProjectId = projectId,
            SpecId = specId,
            CurrentUserId = _currentUser.UserId,
            Activate = false,
        });

        var result = await _dispatcher.DispatchAsync(new GetSpecificationQuery
        {
            SpecId = specId,
            ProjectId = projectId,
            OwnerId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.DeleteSpecification)]
    [HttpDelete("{specId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid projectId, Guid specId)
    {
        await _dispatcher.DispatchAsync(new DeleteSpecificationCommand
        {
            ProjectId = projectId,
            SpecId = specId,
            CurrentUserId = _currentUser.UserId,
        });

        return NoContent();
    }
}
