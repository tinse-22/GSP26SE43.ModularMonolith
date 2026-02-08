using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.Subscription.Authorization;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.ConfigurationOptions;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using ClassifiedAds.Modules.Subscription.RateLimiterPolicies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Controllers;

[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<PaymentsController> _logger;
    private readonly PayOsOptions _payOsOptions;

    public PaymentsController(
        Dispatcher dispatcher,
        ILogger<PaymentsController> logger,
        IOptions<PayOsOptions> payOsOptions)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _payOsOptions = payOsOptions?.Value ?? new PayOsOptions();
    }

    [Authorize(Permissions.CreateSubscriptionPayment)]
    [HttpPost("subscribe/{planId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionPurchaseResultModel>> Subscribe(
        Guid planId,
        [FromBody] CreateSubscriptionPaymentModel model,
        CancellationToken ct)
    {
        var command = new CreateSubscriptionPaymentCommand
        {
            UserId = GetCurrentUserId(),
            PlanId = planId,
            Model = model,
        };

        await _dispatcher.DispatchAsync(command, ct);
        return Ok(command.Result);
    }

    [Authorize(Permissions.GetPlans)]
    [HttpGet("plans")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PlanModel>>> GetPlans(
        [FromQuery] bool? isActive = true,
        [FromQuery] string search = null,
        CancellationToken ct = default)
    {
        var items = await _dispatcher.DispatchAsync(new GetPlansQuery
        {
            IsActive = isActive,
            Search = search,
        }, ct);

        return Ok(items);
    }

    [Authorize(Permissions.GetPaymentIntent)]
    [HttpGet("{intentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentIntentModel>> Get(Guid intentId, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new GetPaymentIntentQuery
        {
            IntentId = intentId,
            UserId = GetCurrentUserId(),
            ThrowNotFoundIfNull = true,
        }, ct);

        return Ok(result);
    }

    [Authorize(Permissions.CreatePayOsCheckout)]
    [HttpPost("payos/create")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayOsCheckoutResponseModel>> CreatePayOsCheckout(
        [FromBody] PayOsCheckoutRequestModel request,
        CancellationToken ct)
    {
        var command = new CreatePayOsCheckoutCommand
        {
            UserId = GetCurrentUserId(),
            IntentId = request.IntentId,
            ReturnUrl = request.ReturnUrl,
        };

        await _dispatcher.DispatchAsync(command, ct);
        return Ok(command.Result);
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("payos/webhook")]
    public async Task<ActionResult> PayOsWebhook(CancellationToken ct)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        }

        _logger.LogInformation("PayOS webhook received. Body={Body}", rawBody);

        if (string.IsNullOrWhiteSpace(rawBody) || rawBody.Trim() == "{}" || rawBody.Trim() == "[]")
        {
            return Ok(new { status = "ok", message = "Test webhook received successfully" });
        }

        PayOsWebhookPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<PayOsWebhookPayload>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize PayOS webhook.");
            return Ok(new { status = "error", message = "Invalid JSON format" });
        }

        if (payload?.Data == null)
        {
            return Ok(new { status = "ok", message = "Test webhook received" });
        }

        Request.Headers.TryGetValue("x-signature", out var signatureHeader);

        try
        {
            var command = new HandlePayOsWebhookCommand
            {
                Payload = payload,
                RawBody = rawBody,
                SignatureHeader = signatureHeader.ToString(),
            };

            await _dispatcher.DispatchAsync(command, ct);

            var statusLabel = command.Outcome == PayOsWebhookOutcome.Ignored ? "ignored" : "ok";
            return Ok(new { status = statusLabel });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PayOS webhook processing failed.");
            return Ok(new { status = "error", error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("payos/return")]
    public async Task<ActionResult> PayOsReturn(CancellationToken ct)
    {
        var status = Request.Query["status"].ToString();
        var orderCode = Request.Query["orderCode"].ToString();

        if (IsSuccessStatus(status) && long.TryParse(orderCode, out var orderCodeValue))
        {
            var paymentIntent = await _dispatcher.DispatchAsync(new GetPaymentIntentByOrderCodeQuery
            {
                OrderCode = orderCodeValue,
            }, ct);

            if (paymentIntent != null)
            {
                return Redirect(BuildResultUrl("success", paymentIntent.Id));
            }
        }

        return Redirect(BuildResultUrl("failed", null));
    }

    [Authorize(Permissions.GetPaymentIntent)]
    [HttpPost("debug/check-payment/{intentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CheckPaymentStatus(Guid intentId, CancellationToken ct)
    {
        var intent = await _dispatcher.DispatchAsync(new GetPaymentIntentQuery
        {
            IntentId = intentId,
            UserId = GetCurrentUserId(),
            ThrowNotFoundIfNull = true,
        }, ct);

        return Ok(new
        {
            intentId = intent.Id,
            status = intent.Status,
            orderCode = intent.OrderCode,
            amount = intent.Amount,
            purpose = intent.Purpose,
            createdAt = intent.CreatedDateTime,
        });
    }

    [Authorize(Permissions.SyncPayment)]
    [HttpPost("debug/sync-payment/{intentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SyncPaymentFromPayOs(Guid intentId, CancellationToken ct)
    {
        var command = new SyncPaymentFromPayOsCommand
        {
            UserId = GetCurrentUserId(),
            IntentId = intentId,
        };

        await _dispatcher.DispatchAsync(command, ct);

        return Ok(new
        {
            status = command.Status,
            payOsStatus = command.PayOsStatus,
        });
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("uid");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new ValidationException("Thông tin người dùng là bắt buộc.");
        }

        return userId;
    }

    private static bool IsSuccessStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Equals("PAID", StringComparison.OrdinalIgnoreCase)
            || status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
            || status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildResultUrl(string status, Guid? intentId)
    {
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = status,
        };

        if (intentId.HasValue)
        {
            query["intentId"] = intentId.Value.ToString();
        }

        var basePath = "/payment/result";
        var frontendBase = _payOsOptions.FrontendBaseUrl?.Trim();

        if (!string.IsNullOrWhiteSpace(frontendBase)
            && Uri.TryCreate(frontendBase, UriKind.Absolute, out var baseUri))
        {
            var targetUri = new Uri(baseUri, basePath);
            return QueryHelpers.AddQueryString(targetUri.ToString(), query);
        }

        return QueryHelpers.AddQueryString(basePath, query);
    }
}
