using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.CrossCuttingConcerns.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClassifiedAds.Infrastructure.Web.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly GlobalExceptionHandlerMiddlewareOptions _options;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        GlobalExceptionHandlerMiddlewareOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var response = context.Response;
            response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Detail = GetErrorMessage(ex)
            };

            problemDetails.Extensions.Add("message", GetErrorMessage(ex));
            problemDetails.Extensions.Add("traceId", Activity.Current.GetTraceId());

            switch (ex)
            {
                case NotFoundException:
                    problemDetails.Status = (int)HttpStatusCode.NotFound;
                    problemDetails.Title = "Not Found";
                    problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4";
                    break;
                case ValidationException:
                    problemDetails.Status = (int)HttpStatusCode.BadRequest;
                    problemDetails.Title = "Bad Request";
                    problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1";
                    break;
                case ConflictException conflictException:
                    problemDetails.Status = (int)HttpStatusCode.Conflict;
                    problemDetails.Title = "Conflict";
                    problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8";
                    if (!string.IsNullOrWhiteSpace(conflictException.ReasonCode))
                    {
                        problemDetails.Extensions.Add("reasonCode", conflictException.ReasonCode);
                    }

                    break;
                case DbUpdateConcurrencyException:
                    problemDetails.Detail = "Du lieu da thay doi boi thao tac khac. Vui long tai lai va thu lai.";
                    problemDetails.Extensions["message"] = problemDetails.Detail;
                    problemDetails.Extensions["reasonCode"] = "CONCURRENCY_CONFLICT";
                    problemDetails.Status = (int)HttpStatusCode.Conflict;
                    problemDetails.Title = "Conflict";
                    problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8";
                    break;
                default:
                    _logger.LogError(ex, "[{Ticks}-{ThreadId}]", DateTime.UtcNow.Ticks, Environment.CurrentManagedThreadId);
                    problemDetails.Status = (int)HttpStatusCode.InternalServerError;
                    problemDetails.Title = "Internal Server Error";
                    problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1";
                    break;
            }

            response.StatusCode = problemDetails.Status.Value;

            var result = JsonSerializer.Serialize(problemDetails);
            await response.WriteAsync(result);
        }
    }

    private string GetErrorMessage(Exception ex)
    {
        if (ex is ValidationException || ex is ConflictException)
        {
            return ex.Message;
        }

        if (ex is DbUpdateConcurrencyException)
        {
            return "Du lieu da thay doi boi thao tac khac. Vui long tai lai va thu lai.";
        }

        return _options.DetailLevel switch
        {
            GlobalExceptionDetailLevel.None => "An internal exception has occurred.",
            GlobalExceptionDetailLevel.Message => ex.Message,
            GlobalExceptionDetailLevel.StackTrace => ex.StackTrace,
            GlobalExceptionDetailLevel.ToString => ex.ToString(),
            _ => "An internal exception has occurred.",
        };
    }
}

public class GlobalExceptionHandlerMiddlewareOptions
{
    public GlobalExceptionDetailLevel DetailLevel { get; set; }
}

public enum GlobalExceptionDetailLevel
{
    None,
    Message,
    StackTrace,
    ToString,
    Throw,
}
