using ClassifiedAds.Application;
using ClassifiedAds.Contracts.AuditLog.DTOs;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Authorization;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Controllers;

[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class PlansController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<PlansController> _logger;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;

    public PlansController(
        Dispatcher dispatcher,
        ILogger<PlansController> logger,
        IRepository<SubscriptionPlan, Guid> planRepository)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _planRepository = planRepository;
    }

    [Authorize(Permissions.GetPlans)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlanModel>>> Get(
        [FromQuery] bool? isActive,
        [FromQuery] string search)
    {
        _logger.LogInformation("Getting all plans. IsActive={IsActive}, Search={Search}", isActive, search);

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
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlanModel>> Post([FromBody] CreateUpdatePlanModel model)
    {
        ValidateModel(model);

        // Check name uniqueness
        await ValidateNameUniqueness(model.Name);

        var plan = model.ToEntity();
        var limits = model.ToLimitEntities(plan.Id);

        await _dispatcher.DispatchAsync(new AddUpdatePlanCommand
        {
            Plan = plan,
            Limits = limits,
        });

        var result = await _dispatcher.DispatchAsync(new GetPlanQuery
        {
            Id = plan.Id,
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
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlanModel>> Put(Guid id, [FromBody] CreateUpdatePlanModel model)
    {
        ValidateModel(model);

        // Get existing plan
        var existingPlan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(p => p.Id == id));

        if (existingPlan == null)
        {
            throw new NotFoundException($"Plan {id} not found.");
        }

        // Check name uniqueness (excluding current plan)
        await ValidateNameUniqueness(model.Name, id);

        // Update plan properties
        existingPlan.Name = model.Name?.Trim();
        existingPlan.DisplayName = model.DisplayName?.Trim();
        existingPlan.Description = model.Description?.Trim();
        existingPlan.PriceMonthly = model.PriceMonthly;
        existingPlan.PriceYearly = model.PriceYearly;
        existingPlan.Currency = model.Currency?.Trim().ToUpperInvariant() ?? "USD";
        existingPlan.IsActive = model.IsActive;
        existingPlan.SortOrder = model.SortOrder;

        var limits = model.ToLimitEntities(existingPlan.Id);

        await _dispatcher.DispatchAsync(new AddUpdatePlanCommand
        {
            Plan = existingPlan,
            Limits = limits,
        });

        var result = await _dispatcher.DispatchAsync(new GetPlanQuery
        {
            Id = existingPlan.Id,
            ThrowNotFoundIfNull = true,
        });

        return Ok(result);
    }

    [Authorize(Permissions.DeletePlan)]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid id)
    {
        var existingPlan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(p => p.Id == id));

        if (existingPlan == null)
        {
            throw new NotFoundException($"Plan {id} not found.");
        }

        await _dispatcher.DispatchAsync(new DeletePlanCommand { Plan = existingPlan });

        return Ok();
    }

    [Authorize(Permissions.GetPlanAuditLogs)]
    [HttpGet("{id}/auditlogs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AuditLogEntryDTO>>> GetAuditLogs(Guid id)
    {
        var logs = await _dispatcher.DispatchAsync(new GetAuditEntriesQuery { ObjectId = id.ToString() });

        List<dynamic> entries = [];
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

    private static void ValidateModel(CreateUpdatePlanModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new ValidationException("Name is required.");
        }

        if (model.Name.Trim().Length > 50)
        {
            throw new ValidationException("Name must not exceed 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            throw new ValidationException("DisplayName is required.");
        }

        if (model.DisplayName.Trim().Length > 100)
        {
            throw new ValidationException("DisplayName must not exceed 100 characters.");
        }

        if (model.Description?.Length > 500)
        {
            throw new ValidationException("Description must not exceed 500 characters.");
        }

        if (model.PriceMonthly.HasValue && model.PriceMonthly.Value < 0)
        {
            throw new ValidationException("PriceMonthly must be >= 0.");
        }

        if (model.PriceYearly.HasValue && model.PriceYearly.Value < 0)
        {
            throw new ValidationException("PriceYearly must be >= 0.");
        }

        if (model.SortOrder < 0)
        {
            throw new ValidationException("SortOrder must be >= 0.");
        }

        if (!string.IsNullOrWhiteSpace(model.Currency) && model.Currency.Trim().Length != 3)
        {
            throw new ValidationException("Currency must be a 3-letter ISO code (e.g., USD, VND).");
        }
    }

    private async Task ValidateNameUniqueness(string name, Guid? excludeId = null)
    {
        var normalizedName = name.Trim().ToLower();

        var query = _planRepository.GetQueryableSet()
            .Where(p => p.Name.ToLower() == normalizedName);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        var existing = await _planRepository.FirstOrDefaultAsync(query);

        if (existing != null)
        {
            throw new ValidationException($"A plan with name '{name.Trim()}' already exists.");
        }
    }
}
