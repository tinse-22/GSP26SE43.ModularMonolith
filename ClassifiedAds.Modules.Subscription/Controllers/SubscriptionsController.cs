using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.Subscription.Authorization;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using ClassifiedAds.Modules.Subscription.RateLimiterPolicies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.Collections.Generic;
using System.Security.Claims;
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
        var item = await EnsureSubscriptionOwnershipAsync(id);

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
        var targetUserId = ResolveUserId(userId);

        var item = await _dispatcher.DispatchAsync(new GetCurrentSubscriptionByUserQuery
        {
            UserId = targetUserId,
            ThrowNotFoundIfNull = true,
        });

        return Ok(item);
    }

    [Authorize(Permissions.GetCurrentSubscription)]
    [HttpGet("me/current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionModel>> GetCurrent()
    {
        var item = await _dispatcher.DispatchAsync(new GetCurrentSubscriptionByUserQuery
        {
            UserId = GetCurrentUserId(),
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
        if (model == null)
        {
            return BadRequest(new { Error = "Thong tin dang ky la bat buoc." });
        }

        model.UserId = GetCurrentUserId();

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
        if (model == null)
        {
            return BadRequest(new { Error = "Thong tin dang ky la bat buoc." });
        }

        var existingSubscription = await EnsureSubscriptionOwnershipAsync(id);
        model.UserId = existingSubscription.UserId;
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
        await EnsureSubscriptionOwnershipAsync(id);

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
        await EnsureSubscriptionOwnershipAsync(id);

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
        await EnsureSubscriptionOwnershipAsync(id);

        var items = await _dispatcher.DispatchAsync(new GetPaymentTransactionsQuery
        {
            SubscriptionId = id,
            UserId = IsCurrentUserAdmin() ? null : GetCurrentUserId(),
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
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AddPaymentTransactionModel model = null)
    {
        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = id,
            UserId = IsCurrentUserAdmin() ? Guid.Empty : GetCurrentUserId(),
            Model = model,
        };
        await _dispatcher.DispatchAsync(command);

        return Created($"/api/subscriptions/{id}/payments/{command.SavedTransactionId}", command.SavedTransaction);
    }

    [Authorize(Permissions.GetUsageTracking)]
    [HttpGet("users/{userId}/usage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UsageTrackingModel>>> GetUsage(
        Guid userId,
        [FromQuery] DateOnly? periodStart,
        [FromQuery] DateOnly? periodEnd)
    {
        var targetUserId = ResolveUserId(userId);

        var items = await _dispatcher.DispatchAsync(new GetUsageTrackingsQuery
        {
            UserId = targetUserId,
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
        var targetUserId = ResolveUserId(userId);

        await _dispatcher.DispatchAsync(new UpsertUsageTrackingCommand
        {
            UserId = targetUserId,
            Model = model,
        });

        var items = await _dispatcher.DispatchAsync(new GetUsageTrackingsQuery
        {
            UserId = targetUserId,
            PeriodStart = model.PeriodStart,
            PeriodEnd = model.PeriodEnd,
        });

        return Ok(items);
    }

    private Guid ResolveUserId(Guid requestedUserId)
    {
        var currentUserId = GetCurrentUserId();
        if (!IsCurrentUserAdmin())
        {
            return currentUserId;
        }

        return requestedUserId == Guid.Empty ? currentUserId : requestedUserId;
    }

    private async Task<SubscriptionModel> EnsureSubscriptionOwnershipAsync(Guid subscriptionId)
    {
        var subscription = await _dispatcher.DispatchAsync(new GetSubscriptionQuery
        {
            Id = subscriptionId,
            ThrowNotFoundIfNull = true,
        });

        if (!IsCurrentUserAdmin() && subscription.UserId != GetCurrentUserId())
        {
            throw new NotFoundException($"Khong tim thay dang ky voi ma '{subscriptionId}'.");
        }

        return subscription;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("uid");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new ValidationException("Thong tin nguoi dung la bat buoc.");
        }

        return userId;
    }

    private bool IsCurrentUserAdmin()
    {
        return User.IsInRole("Admin");
    }
}
