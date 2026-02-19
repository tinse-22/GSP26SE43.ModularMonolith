using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.CrossCuttingConcerns.Logging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Infrastructure.Web.ExceptionHandlers;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly GlobalExceptionHandlerOptions _options;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IOptions<GlobalExceptionHandlerOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var response = httpContext.Response;

        if (exception is NotFoundException)
        {
            var problemDetails = new ProblemDetails
            {
                Detail = exception.Message,
                Instance = null,
                Status = (int)HttpStatusCode.NotFound,
                Title = "Not Found",
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4"
            };

            problemDetails.Extensions.Add("message", exception.Message);
            problemDetails.Extensions.Add("traceId", Activity.Current.GetTraceId());

            response.ContentType = "application/problem+json";
            response.StatusCode = problemDetails.Status.Value;

            var result = JsonSerializer.Serialize(problemDetails);
            await response.WriteAsync(result, cancellationToken: cancellationToken);

            return true;
        }
        else if (exception is ValidationException)
        {
            var problemDetails = new ProblemDetails
            {
                Detail = exception.Message,
                Instance = null,
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Bad Request",
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1"
            };

            problemDetails.Extensions.Add("message", exception.Message);
            problemDetails.Extensions.Add("traceId", Activity.Current.GetTraceId());

            response.ContentType = "application/problem+json";
            response.StatusCode = problemDetails.Status.Value;

            var result = JsonSerializer.Serialize(problemDetails);
            await response.WriteAsync(result, cancellationToken: cancellationToken);

            return true;
        }
        else if (exception is ConflictException conflictException)
        {
            var problemDetails = new ProblemDetails
            {
                Detail = exception.Message,
                Instance = null,
                Status = (int)HttpStatusCode.Conflict,
                Title = "Conflict",
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8"
            };

            problemDetails.Extensions.Add("message", exception.Message);
            problemDetails.Extensions.Add("traceId", Activity.Current.GetTraceId());
            if (!string.IsNullOrWhiteSpace(conflictException.ReasonCode))
            {
                problemDetails.Extensions.Add("reasonCode", conflictException.ReasonCode);
            }

            response.ContentType = "application/problem+json";
            response.StatusCode = problemDetails.Status.Value;

            var result = JsonSerializer.Serialize(problemDetails);
            await response.WriteAsync(result, cancellationToken: cancellationToken);

            return true;
        }
        else if (exception is DbUpdateConcurrencyException)
        {
            var problemDetails = new ProblemDetails
            {
                Detail = "Du lieu da thay doi boi thao tac khac. Vui long tai lai va thu lai.",
                Instance = null,
                Status = (int)HttpStatusCode.Conflict,
                Title = "Conflict",
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8"
            };

            problemDetails.Extensions.Add("message", problemDetails.Detail);
            problemDetails.Extensions.Add("traceId", Activity.Current.GetTraceId());
            problemDetails.Extensions.Add("reasonCode", "CONCURRENCY_CONFLICT");

            response.ContentType = "application/problem+json";
            response.StatusCode = problemDetails.Status.Value;

            var result = JsonSerializer.Serialize(problemDetails);
            await response.WriteAsync(result, cancellationToken: cancellationToken);

            return true;
        }
        else
        {
            _logger.LogError(exception, "[{Ticks}-{ThreadId}]", DateTime.UtcNow.Ticks, Environment.CurrentManagedThreadId);

            if (_options.DetailLevel == GlobalExceptionDetailLevel.Throw)
            {
                return false;
            }

            var problemDetails = new ProblemDetails
            {
                Detail = _options.GetErrorMessage(exception),
                Instance = null,
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Internal Server Error",
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1"
            };

            problemDetails.Extensions.Add("message", _options.GetErrorMessage(exception));
            problemDetails.Extensions.Add("traceId", Activity.Current.GetTraceId());

            response.ContentType = "application/problem+json";
            response.StatusCode = problemDetails.Status.Value;

            var result = JsonSerializer.Serialize(problemDetails);
            await response.WriteAsync(result, cancellationToken: cancellationToken);

            return true;
        }
    }
}
