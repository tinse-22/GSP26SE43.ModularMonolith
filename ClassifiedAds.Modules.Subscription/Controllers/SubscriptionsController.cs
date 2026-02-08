using ClassifiedAds.Application;
using ClassifiedAds.Modules.Subscription.Authorization;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using ClassifiedAds.Modules.Subscription.RateLimiterPolicies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Controllers;

[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class SubscriptionsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;

    public SubscriptionsController(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [Authorize(Permissions.GetSubscription)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionModel>> Get(Guid id)
    {
        var item = await _dispatcher.DispatchAsync(new GetSubscriptionQuery
        {
            Id = id,
            ThrowNotFoundIfNull = true,
        });

        return Ok(item);
    }

    [Authorize(Permissions.GetPlans)]
    [HttpGet("plans")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlanModel>>> GetPlans(
        [FromQuery] bool? isActive = true,
        [FromQuery] string search = null)
    {
        var items = await _dispatcher.DispatchAsync(new GetPlansQuery
        {
            IsActive = isActive,
            Search = search,
        });

        return Ok(items);
    }

    [Authorize(Permissions.GetCurrentSubscription)]
    [HttpGet("users/{userId}/current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionModel>> GetCurrentByUser(Guid userId)
    {
        var item = await _dispatcher.DispatchAsync(new GetCurrentSubscriptionByUserQuery
        {
            UserId = userId,
            ThrowNotFoundIfNull = true,
        });

        return Ok(item);
    }

    [Authorize(Permissions.AddSubscription)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubscriptionModel>> Post([FromBody] CreateUpdateSubscriptionModel model)
    {
        var command = new AddUpdateSubscriptionCommand
        {
            Model = model,
        };
        await _dispatcher.DispatchAsync(command);

        var result = await _dispatcher.DispatchAsync(new GetSubscriptionQuery
        {
            Id = command.SavedSubscriptionId,
            ThrowNotFoundIfNull = true,
        });

        return Created($"/api/subscriptions/{result.Id}", result);
    }

    [Authorize(Permissions.UpdateSubscription)]
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionModel>> Put(Guid id, [FromBody] CreateUpdateSubscriptionModel model)
    {
        var command = new AddUpdateSubscriptionCommand
        {
            SubscriptionId = id,
            Model = model,
        };
        await _dispatcher.DispatchAsync(command);

        var result = await _dispatcher.DispatchAsync(new GetSubscriptionQuery
        {
            Id = command.SavedSubscriptionId,
            ThrowNotFoundIfNull = true,
        });

        return Ok(result);
    }

    [Authorize(Permissions.CancelSubscription)]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionModel>> Cancel(Guid id, [FromBody] CancelSubscriptionModel model)
    {
        await _dispatcher.DispatchAsync(new CancelSubscriptionCommand
        {
            SubscriptionId = id,
            Model = model,
        });

        var result = await _dispatcher.DispatchAsync(new GetSubscriptionQuery
        {
            Id = id,
            ThrowNotFoundIfNull = true,
        });

        return Ok(result);
    }

    [Authorize(Permissions.GetSubscriptionHistory)]
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SubscriptionHistoryModel>>> GetHistory(Guid id)
    {
        var items = await _dispatcher.DispatchAsync(new GetSubscriptionHistoriesQuery
        {
            SubscriptionId = id,
        });

        return Ok(items);
    }

    [Authorize(Permissions.GetPaymentTransactions)]
    [HttpGet("{id:guid}/payments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PaymentTransactionModel>>> GetPayments(
        Guid id,
        [FromQuery] PaymentStatus? status)
    {
        var items = await _dispatcher.DispatchAsync(new GetPaymentTransactionsQuery
        {
            SubscriptionId = id,
            Status = status,
        });

        return Ok(items);
    }

    [Authorize(Permissions.AddPaymentTransaction)]
    [HttpPost("{id:guid}/payments")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentTransactionModel>> AddPayment(
        Guid id,
        [FromBody] AddPaymentTransactionModel model)
    {
        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = id,
            Model = model,
        };
        await _dispatcher.DispatchAsync(command);

        var items = await _dispatcher.DispatchAsync(new GetPaymentTransactionsQuery
        {
            SubscriptionId = id,
        });

        var created = items.Find(x => x.Id == command.SavedTransactionId);
        return Created($"/api/subscriptions/{id}/payments/{command.SavedTransactionId}", created);
    }

    [Authorize(Permissions.GetUsageTracking)]
    [HttpGet("users/{userId}/usage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UsageTrackingModel>>> GetUsage(
        Guid userId,
        [FromQuery] DateOnly? periodStart,
        [FromQuery] DateOnly? periodEnd)
    {
        var items = await _dispatcher.DispatchAsync(new GetUsageTrackingsQuery
        {
            UserId = userId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
        });

        return Ok(items);
    }

    [Authorize(Permissions.UpdateUsageTracking)]
    [HttpPut("users/{userId}/usage")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<UsageTrackingModel>>> UpsertUsage(
        Guid userId,
        [FromBody] UpsertUsageTrackingModel model)
    {
        await _dispatcher.DispatchAsync(new UpsertUsageTrackingCommand
        {
            UserId = userId,
            Model = model,
        });

        var items = await _dispatcher.DispatchAsync(new GetUsageTrackingsQuery
        {
            UserId = userId,
            PeriodStart = model.PeriodStart,
            PeriodEnd = model.PeriodEnd,
        });

        return Ok(items);
    }
}
