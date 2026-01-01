#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ClassifiedAds.Domain.Infrastructure.ResultPattern;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClassifiedAds.Infrastructure.Web.ResultMapping;

/// <summary>
/// Extension methods to map Result and Result&lt;T&gt; to ASP.NET Core ActionResult.
/// Provides consistent HTTP response formatting based on error types.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Maps a Result to an ActionResult.
    /// Success returns 204 No Content, failure returns appropriate error response.
    /// </summary>
    public static IActionResult ToActionResult(this Result result, HttpContext httpContext)
    {
        if (result.IsSuccess)
        {
            return new NoContentResult();
        }

        return CreateErrorResponse(result, httpContext);
    }

    /// <summary>
    /// Maps a Result&lt;T&gt; to an ActionResult&lt;T&gt;.
    /// Success returns 200 OK with value, failure returns appropriate error response.
    /// </summary>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result, HttpContext httpContext)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        return CreateErrorResponse(result, httpContext);
    }

    /// <summary>
    /// Maps a Result&lt;T&gt; to an ActionResult&lt;T&gt; with a custom success status code.
    /// </summary>
    public static ActionResult<T> ToActionResult<T>(
        this Result<T> result,
        HttpContext httpContext,
        int successStatusCode)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(result.Value) { StatusCode = successStatusCode };
        }

        return CreateErrorResponse(result, httpContext);
    }

    /// <summary>
    /// Maps a Result to an ActionResult with a custom success response.
    /// Useful when you want to return a specific object on success.
    /// </summary>
    public static IActionResult ToActionResult<TResponse>(
        this Result result,
        HttpContext httpContext,
        TResponse successResponse)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(successResponse);
        }

        return CreateErrorResponse(result, httpContext);
    }

    /// <summary>
    /// Maps a Result to an ActionResult with Created (201) status on success.
    /// </summary>
    public static ActionResult<T> ToCreatedResult<T>(
        this Result<T> result,
        HttpContext httpContext,
        string? location = null)
    {
        if (result.IsSuccess)
        {
            if (!string.IsNullOrEmpty(location))
            {
                return new CreatedResult(location, result.Value);
            }

            return new ObjectResult(result.Value) { StatusCode = StatusCodes.Status201Created };
        }

        return CreateErrorResponse(result, httpContext);
    }

    private static ObjectResult CreateErrorResponse(Result result, HttpContext httpContext)
    {
        var firstError = result.FirstError;
        if (firstError == null)
        {
            return CreateProblemDetailsResponse(
                StatusCodes.Status500InternalServerError,
                "Unknown Error",
                "An unknown error occurred.",
                httpContext);
        }

        // Determine status code based on error code patterns
        var statusCode = GetStatusCodeFromError(firstError);

        // For validation errors, return ValidationProblemDetails
        if (statusCode == StatusCodes.Status400BadRequest &&
            result.Errors.Any(e => e.Code.StartsWith("Validation.", StringComparison.OrdinalIgnoreCase)))
        {
            return CreateValidationProblemDetailsResponse(result.Errors, httpContext);
        }

        // For other errors, return ProblemDetails
        return CreateProblemDetailsResponse(statusCode, GetTitleFromStatusCode(statusCode), firstError.Message, httpContext);
    }

    private static int GetStatusCodeFromError(Error error)
    {
        // Check by error code patterns
        if (error.Code.EndsWith(".NotFound", StringComparison.OrdinalIgnoreCase) ||
            error.Code == Error.Codes.NotFound)
        {
            return StatusCodes.Status404NotFound;
        }

        if (error.Code.StartsWith("Validation.", StringComparison.OrdinalIgnoreCase) ||
            error.Code == Error.Codes.ValidationFailed)
        {
            return StatusCodes.Status400BadRequest;
        }

        if (error.Code.EndsWith(".Conflict", StringComparison.OrdinalIgnoreCase) ||
            error.Code == Error.Codes.Conflict)
        {
            return StatusCodes.Status409Conflict;
        }

        if (error.Code == Error.Codes.Unauthorized ||
            error.Code.StartsWith("Auth.Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCodes.Status401Unauthorized;
        }

        if (error.Code == Error.Codes.Forbidden ||
            error.Code.StartsWith("Auth.Forbidden", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCodes.Status403Forbidden;
        }

        // Default to 500 for unknown errors
        return StatusCodes.Status500InternalServerError;
    }

    private static string GetTitleFromStatusCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status500InternalServerError => "Internal Server Error",
            _ => "Error"
        };
    }

    private static string GetTypeFromStatusCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
            StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };
    }

    private static ObjectResult CreateProblemDetailsResponse(
        int statusCode,
        string title,
        string detail,
        HttpContext httpContext)
    {
        var problemDetails = new ProblemDetails
        {
            Type = GetTypeFromStatusCode(statusCode),
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        // Add traceId for correlation
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static ObjectResult CreateValidationProblemDetailsResponse(
        IReadOnlyList<Error> errors,
        HttpContext httpContext)
    {
        // Group errors by field
        var errorDictionary = errors
            .Where(e => e.Code.StartsWith("Validation.", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.Metadata?.TryGetValue("field", out var field) == true ? field?.ToString() ?? "" : ExtractFieldFromCode(e.Code))
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.Message).ToArray());

        var problemDetails = new ValidationProblemDetails(errorDictionary)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path
        };

        // Add traceId for correlation
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static string ExtractFieldFromCode(string code)
    {
        // Extract field name from "Validation.FieldName" pattern
        if (code.StartsWith("Validation.", StringComparison.OrdinalIgnoreCase))
        {
            return code.Substring("Validation.".Length);
        }

        return "";
    }
}
