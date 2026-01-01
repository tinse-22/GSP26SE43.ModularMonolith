#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Domain.Infrastructure.ResultPattern;

/// <summary>
/// Represents the result of an operation that does not return a value.
/// Used for operations that can either succeed or fail with one or more errors.
/// </summary>
public class Result
{
    private readonly List<Error> _errors = new List<Error>();

    /// <summary>
    /// Gets whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the collection of errors if the operation failed.
    /// </summary>
    public IReadOnlyList<Error> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Gets the first error if any, otherwise null.
    /// </summary>
    public Error? FirstError => _errors.FirstOrDefault();

    protected Result(bool isSuccess, IEnumerable<Error>? errors = null)
    {
        IsSuccess = isSuccess;
        if (errors != null)
        {
            _errors.AddRange(errors);
        }

        // Invariant: Success results must not have errors
        if (isSuccess && _errors.Count > 0)
        {
            throw new InvalidOperationException("A successful result cannot contain errors.");
        }

        // Invariant: Failure results must have at least one error
        if (!isSuccess && _errors.Count == 0)
        {
            throw new InvalidOperationException("A failed result must contain at least one error.");
        }
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Ok() => new Result(true);

    /// <summary>
    /// Creates a failed result with a single error.
    /// </summary>
    public static Result Fail(Error error) => new Result(false, new[] { error });

    /// <summary>
    /// Creates a failed result with multiple errors.
    /// </summary>
    public static Result Fail(IEnumerable<Error> errors) => new Result(false, errors);

    /// <summary>
    /// Creates a failed result from error code and message.
    /// </summary>
    public static Result Fail(string errorCode, string message)
        => Fail(Error.Create(errorCode, message));

    /// <summary>
    /// Creates a not found failure result.
    /// </summary>
    public static Result NotFound(string entity, object id)
        => Fail(Error.NotFound(entity, id));

    /// <summary>
    /// Creates a validation failure result.
    /// </summary>
    public static Result ValidationFailed(string field, string message)
        => Fail(Error.Validation(field, message));

    /// <summary>
    /// Creates a validation failure result with multiple field errors.
    /// </summary>
    public static Result ValidationFailed(IEnumerable<(string Field, string Message)> fieldErrors)
        => Fail(fieldErrors.Select(e => Error.Validation(e.Field, e.Message)));

    /// <summary>
    /// Checks if the result contains a specific error code.
    /// </summary>
    public bool HasErrorCode(string code)
        => _errors.Any(e => e.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Checks if any error code starts with the given prefix.
    /// </summary>
    public bool HasErrorCodePrefix(string prefix)
        => _errors.Any(e => e.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets all error messages concatenated.
    /// </summary>
    public string GetErrorMessages(string separator = "; ")
        => string.Join(separator, _errors.Select(e => e.Message));

    /// <summary>
    /// Implicit conversion from Error to Result.
    /// </summary>
    public static implicit operator Result(Error error) => Fail(error);
}

/// <summary>
/// Represents the result of an operation that returns a value of type T.
/// Used for operations that can either succeed with a value or fail with one or more errors.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>
    /// Gets the value if the operation was successful.
    /// Throws InvalidOperationException if accessed on a failed result.
    /// </summary>
    public T Value
    {
        get
        {
            if (IsFailure)
            {
                throw new InvalidOperationException(
                    $"Cannot access Value on a failed result. Errors: {GetErrorMessages()}");
            }

            return _value!;
        }
    }

    private Result(bool isSuccess, T? value, IEnumerable<Error>? errors = null)
        : base(isSuccess, errors)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static Result<T> Ok(T value) => new Result<T>(true, value);

    /// <summary>
    /// Creates a failed result with a single error.
    /// </summary>
    public static new Result<T> Fail(Error error) => new Result<T>(false, default, new[] { error });

    /// <summary>
    /// Creates a failed result with multiple errors.
    /// </summary>
    public static new Result<T> Fail(IEnumerable<Error> errors) => new Result<T>(false, default, errors);

    /// <summary>
    /// Creates a failed result from error code and message.
    /// </summary>
    public static new Result<T> Fail(string errorCode, string message)
        => Fail(Error.Create(errorCode, message));

    /// <summary>
    /// Creates a not found failure result.
    /// </summary>
    public static new Result<T> NotFound(string entity, object id)
        => Fail(Error.NotFound(entity, id));

    /// <summary>
    /// Creates a validation failure result.
    /// </summary>
    public static new Result<T> ValidationFailed(string field, string message)
        => Fail(Error.Validation(field, message));

    /// <summary>
    /// Creates a result from a nullable value, failing with NotFound if null.
    /// </summary>
    public static Result<T> FromNullable(T? value, string entity, object id)
        => value is null ? NotFound(entity, id) : Ok(value);

    /// <summary>
    /// Maps the value to a new type if successful.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        => IsSuccess ? Result<TNew>.Ok(mapper(Value)) : Result<TNew>.Fail(Errors);

    /// <summary>
    /// Executes an action if successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
        {
            action(Value);
        }

        return this;
    }

    /// <summary>
    /// Executes an action if failed.
    /// </summary>
    public Result<T> OnFailure(Action<IReadOnlyList<Error>> action)
    {
        if (IsFailure)
        {
            action(Errors);
        }

        return this;
    }

    /// <summary>
    /// Gets the value or a default if failed.
    /// </summary>
    public T? GetValueOrDefault(T? defaultValue = default)
        => IsSuccess ? Value : defaultValue;

    /// <summary>
    /// Implicit conversion from T to Result&lt;T&gt;.
    /// </summary>
    public static implicit operator Result<T>(T value) => Ok(value);

    /// <summary>
    /// Implicit conversion from Error to Result&lt;T&gt;.
    /// </summary>
    public static implicit operator Result<T>(Error error) => Fail(error);
}
