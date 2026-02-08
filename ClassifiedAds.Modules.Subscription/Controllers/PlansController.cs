using ClassifiedAds.Application;
using ClassifiedAds.Contracts.AuditLog.DTOs;
using ClassifiedAds.Modules.Subscription.Authorization;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using ClassifiedAds.Modules.Subscription.RateLimiterPolicies;
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

namespace ClassifiedAds.Modules.Subscription.Controllers;

[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class PlansController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<PlansController> _logger;

    public PlansController(
        Dispatcher dispatcher,
        ILogger<PlansController> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [Authorize(Permissions.GetPlans)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlanModel>>> Get(
        [FromQuery] bool? isActive,
        [FromQuery] string search)
    {
        var safeSearch = search?.Replace("\r", string.Empty).Replace("\n", string.Empty);
        _logger.LogInformation("Fetching subscription plans. IsActive={IsActive}, Search={Search}", isActive, safeSearch);

        var plans = await _dispatcher.DispatchAsync(new GetPlansQuery
        {
            IsActive = isActive,
            Search = search,
        });

        return Ok(plans);
    }

    [Authorize(Permissions.GetPlans)]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanModel>> Get(Guid id)
    {
        var plan = await _dispatcher.DispatchAsync(new GetPlanQuery
        {
            Id = id,
            ThrowNotFoundIfNull = true,
        });

        return Ok(plan);
    }

    [Authorize(Permissions.AddPlan)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanModel>> Post([FromBody] CreateUpdatePlanModel model)
    {
        var command = new AddUpdatePlanCommand
        {
            Model = model,
        };
        await _dispatcher.DispatchAsync(command);

        var result = await _dispatcher.DispatchAsync(new GetPlanQuery
        {
            Id = command.SavedPlanId,
            ThrowNotFoundIfNull = true,
        });

        return Created($"/api/plans/{result.Id}", result);
    }

    [Authorize(Permissions.UpdatePlan)]
    [HttpPut("{id}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanModel>> Put(Guid id, [FromBody] CreateUpdatePlanModel model)
    {
        var command = new AddUpdatePlanCommand
        {
            PlanId = id,
            Model = model,
        };
        await _dispatcher.DispatchAsync(command);

        var result = await _dispatcher.DispatchAsync(new GetPlanQuery
        {
            Id = command.SavedPlanId,
            ThrowNotFoundIfNull = true,
        });

        return Ok(result);
    }

    [Authorize(Permissions.DeletePlan)]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id)
    {
        await _dispatcher.DispatchAsync(new DeletePlanCommand { PlanId = id });

        return Ok();
    }

    [Authorize(Permissions.GetPlanAuditLogs)]
    [HttpGet("{id}/auditlogs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AuditLogEntryDTO>>> GetAuditLogs(Guid id)
    {
        var logs = await _dispatcher.DispatchAsync(new GetAuditEntriesQuery { ObjectId = id.ToString() });

        List<dynamic> entries = new List<dynamic>();
        PlanModel previous = null;
        foreach (var log in logs.OrderBy(x => x.CreatedDateTime))
        {
            var data = JsonSerializer.Deserialize<PlanModel>(log.Log);
            var highLight = new
            {
                Name = previous != null && data.Name != previous.Name,
                DisplayName = previous != null && data.DisplayName != previous.DisplayName,
                Description = previous != null && data.Description != previous.Description,
                PriceMonthly = previous != null && data.PriceMonthly != previous.PriceMonthly,
                PriceYearly = previous != null && data.PriceYearly != previous.PriceYearly,
                IsActive = previous != null && data.IsActive != previous.IsActive,
            };

            var entry = new
            {
                log.Id,
                log.UserName,
                Action = log.Action.Replace("_PLAN", string.Empty),
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
