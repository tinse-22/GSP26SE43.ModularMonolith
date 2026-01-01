using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ClassifiedAds.Infrastructure.Web.Validation;

/// <summary>
/// Factory for creating standardized validation error responses.
/// Ensures consistent error format across the API when model validation fails.
/// </summary>
public static class ValidationProblemDetailsFactory
{
    /// <summary>
    /// Creates a standardized InvalidModelStateResponseFactory for use with ApiBehaviorOptions.
    /// Returns ValidationProblemDetails with traceId and consistent error formatting.
    /// </summary>
    /// <returns>A factory function that creates IActionResult from ModelStateDictionary.</returns>
    public static Func<ActionContext, IActionResult> CreateFactory()
    {
        return context =>
        {
            var problemDetails = CreateValidationProblemDetails(context.HttpContext, context.ModelState);
            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    }

    /// <summary>
    /// Creates ValidationProblemDetails from ModelStateDictionary.
    /// </summary>
    public static ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelState)
    {
        var problemDetails = new ValidationProblemDetails(modelState)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path
        };

        // Add traceId for correlation
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        return problemDetails;
    }

    /// <summary>
    /// Creates ValidationProblemDetails from a dictionary of field errors.
    /// Useful for FluentValidation errors or custom validation logic.
    /// </summary>
    public static ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        IDictionary<string, string[]> errors)
    {
        var problemDetails = new ValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path
        };

        // Add traceId for correlation
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        return problemDetails;
    }
}
