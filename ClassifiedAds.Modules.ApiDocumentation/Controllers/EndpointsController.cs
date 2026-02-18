using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.ApiDocumentation.Authorization;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
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
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Controllers;

[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Authorize]
[Produces("application/json")]
[Route("api/projects/{projectId}/specifications/{specId}/endpoints")]
[ApiController]
public class EndpointsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<EndpointsController> _logger;

    public EndpointsController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<EndpointsController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
    }

    [Authorize(Permissions.GetEndpoints)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EndpointModel>>> Get(Guid projectId, Guid specId)
    {
        _logger.LogInformation("Fetching endpoints for spec {SpecId} in project {ProjectId}.", specId, projectId);

        var result = await _dispatcher.DispatchAsync(new GetEndpointsQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            OwnerId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.GetEndpoints)]
    [HttpGet("{endpointId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EndpointDetailModel>> GetById(Guid projectId, Guid specId, Guid endpointId)
    {
        var result = await _dispatcher.DispatchAsync(new GetEndpointQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            OwnerId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.AddEndpoint)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EndpointDetailModel>> Post(
        Guid projectId,
        Guid specId,
        [FromBody] CreateUpdateEndpointModel model)
    {
        _logger.LogInformation("Creating endpoint for spec {SpecId} in project {ProjectId}.", specId, projectId);

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = projectId,
            SpecId = specId,
            CurrentUserId = _currentUser.UserId,
            Model = model,
        };

        await _dispatcher.DispatchAsync(command);

        var result = await _dispatcher.DispatchAsync(new GetEndpointQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = command.SavedEndpointId,
            OwnerId = _currentUser.UserId,
        });

        return Created($"/api/projects/{projectId}/specifications/{specId}/endpoints/{result.Id}", result);
    }

    [Authorize(Permissions.UpdateEndpoint)]
    [HttpPut("{endpointId}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EndpointDetailModel>> Put(
        Guid projectId,
        Guid specId,
        Guid endpointId,
        [FromBody] CreateUpdateEndpointModel model)
    {
        _logger.LogInformation("Updating endpoint {EndpointId} for spec {SpecId}.", endpointId, specId);

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            CurrentUserId = _currentUser.UserId,
            Model = model,
        };

        await _dispatcher.DispatchAsync(command);

        var result = await _dispatcher.DispatchAsync(new GetEndpointQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = command.SavedEndpointId,
            OwnerId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize(Permissions.DeleteEndpoint)]
    [HttpDelete("{endpointId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid projectId, Guid specId, Guid endpointId)
    {
        _logger.LogInformation("Deleting endpoint {EndpointId} from spec {SpecId}.", endpointId, specId);

        await _dispatcher.DispatchAsync(new DeleteEndpointCommand
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            CurrentUserId = _currentUser.UserId,
        });

        return NoContent();
    }

    [Authorize(Permissions.GetEndpoints)]
    [HttpGet("{endpointId}/resolved-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResolvedUrlResult>> GetResolvedUrl(
        Guid projectId, Guid specId, Guid endpointId)
    {
        // Read query string as simple key=value pairs: ?userId=42&orderId=abc
        var paramValues = Request.Query
            .ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var result = await _dispatcher.DispatchAsync(new GetResolvedUrlQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            OwnerId = _currentUser.UserId,
            ParameterValues = paramValues,
        });

        return Ok(result);
    }

    [Authorize(Permissions.GetEndpoints)]
    [HttpGet("{endpointId}/path-param-mutations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PathParamMutationsResult>> GetPathParamMutations(
        Guid projectId, Guid specId, Guid endpointId)
    {
        var result = await _dispatcher.DispatchAsync(new GetPathParamMutationsQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            OwnerId = _currentUser.UserId,
        });

        return Ok(result);
    }
}
