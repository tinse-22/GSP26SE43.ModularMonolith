#nullable enable
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Domain.Infrastructure.ResultPattern;

/// <summary>
/// Represents an error with a code and message.
/// Error codes follow convention: {Domain}.{ErrorType} (e.g., "Product.NotFound", "Validation.Required")
/// </summary>
public sealed record Error
{
    /// <summary>
    /// Error code following pattern: {Domain}.{ErrorType}
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Optional metadata/details for the error.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; }

    private Error(string code, string message, IReadOnlyDictionary<string, object>? metadata = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Metadata = metadata;
    }

    /// <summary>
    /// Creates a new error instance.
    /// </summary>
    public static Error Create(string code, string message, IReadOnlyDictionary<string, object>? metadata = null)
        => new Error(code, message, metadata);

    /// <summary>
    /// Creates a validation error for a specific field.
    /// </summary>
    public static Error Validation(string field, string message)
        => new Error($"Validation.{field}", message, new Dictionary<string, object> { ["field"] = field });

    /// <summary>
    /// Creates a not found error.
    /// </summary>
    public static Error NotFound(string entity, object id)
        => new Error($"{entity}.NotFound", $"{entity} with identifier '{id}' was not found.",
            new Dictionary<string, object> { ["entity"] = entity, ["id"] = id });

    /// <summary>
    /// Creates a conflict error.
    /// </summary>
    public static Error Conflict(string entity, string message)
        => new Error($"{entity}.Conflict", message,
            new Dictionary<string, object> { ["entity"] = entity });

    /// <summary>
    /// Creates an unauthorized error.
    /// </summary>
    public static Error Unauthorized(string message = "Authentication is required.")
        => new Error("Auth.Unauthorized", message);

    /// <summary>
    /// Creates a forbidden error.
    /// </summary>
    public static Error Forbidden(string message = "You do not have permission to perform this action.")
        => new Error("Auth.Forbidden", message);

    /// <summary>
    /// Creates an internal server error.
    /// </summary>
    public static Error Internal(string message = "An unexpected error occurred.")
        => new Error("Server.InternalError", message);

    /// <summary>
    /// Predefined error codes for common scenarios.
    /// </summary>
    public static class Codes
    {
        public const string ValidationFailed = "Validation.Failed";
        public const string NotFound = "NotFound";
        public const string Conflict = "Conflict";
        public const string Unauthorized = "Auth.Unauthorized";
        public const string Forbidden = "Auth.Forbidden";
        public const string InternalError = "Server.InternalError";
    }

    public override string ToString() => $"[{Code}] {Message}";
}
