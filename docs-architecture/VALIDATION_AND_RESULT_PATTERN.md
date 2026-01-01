# FluentValidation & Result<T> Pattern — Architecture Guide

> **Status**: Base Infrastructure Only  
> **Modules**: Not yet migrated; adoption will be incremental

This document describes the standardized validation and error-handling infrastructure established for the Modular Monolith. The goal is to provide consistent, predictable API responses across all modules while enabling clean, testable application logic.

---

## Table of Contents

1. [Overview](#overview)
2. [FluentValidation Integration](#fluentvalidation-integration)
3. [Standardized Validation Errors](#standardized-validation-errors)
4. [Result<T> Pattern](#resultt-pattern)
5. [HTTP Status Code Mapping](#http-status-code-mapping)
6. [Sample Responses](#sample-responses)
7. [Migration Guide for Modules](#migration-guide-for-modules)

---

## Overview

### Design Principles

1. **No Exceptions for Expected Failures** — Use `Result<T>` to represent operations that can fail in expected ways
2. **Consistent Error Format** — All validation errors follow RFC 7807 Problem Details
3. **Type-Safe Errors** — The `Error` type carries structured metadata (code, message, field info)
4. **Testability** — All components are unit-testable without HTTP context

### Package Versions

| Package | Version | Project |
|---------|---------|---------|
| FluentValidation | 11.11.0 | ClassifiedAds.Application |
| FluentValidation.AspNetCore | 11.3.0 | ClassifiedAds.WebAPI |

---

## FluentValidation Integration

### Automatic Validator Registration

FluentValidation validators are automatically registered during application startup via assembly scanning:

```csharp
// Program.cs
builder.Services.AddValidatorsFromAssemblies(
[
    typeof(ApplicationServicesExtensions).Assembly,  // Application layer
    typeof(Program).Assembly,                        // WebAPI layer
]);

builder.Services.AddFluentValidationAutoValidation();
```

### Creating a Validator (Example)

```csharp
using FluentValidation;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters.");
        
        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.");
    }
}
```

Validators are automatically invoked by the ASP.NET MVC pipeline before the action executes.

---

## Standardized Validation Errors

### Configuration

Invalid model states are handled via a custom factory that produces RFC 7807-compliant responses:

```csharp
// Program.cs
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = 
        ValidationProblemDetailsFactory.CreateFactory();
});
```

### Response Structure

All validation errors return **HTTP 400 Bad Request** with a `ValidationProblemDetails` body:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/products",
  "traceId": "00-abc123...",
  "errors": {
    "Name": ["Product name is required."],
    "Price": ["Price must be greater than zero."]
  }
}
```

### Key Characteristics

| Field | Description |
|-------|-------------|
| `type` | RFC 7231 reference for 400 errors |
| `title` | Human-readable summary |
| `status` | HTTP status code |
| `instance` | Request path that caused the error |
| `traceId` | Correlation ID for distributed tracing |
| `errors` | Dictionary of field → error messages |

---

## Result<T> Pattern

### Location

- **Types**: `ClassifiedAds.Domain.Infrastructure.ResultPattern`
- **HTTP Extensions**: `ClassifiedAds.Infrastructure.Web.ResultMapping`

### Core Types

#### Error Record

```csharp
public sealed record Error(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object>? Metadata = null)
{
    // Factory methods
    public static Error Validation(string field, string message);
    public static Error NotFound(string entity, object id);
    public static Error Conflict(string message);
    public static Error Unauthorized(string? message = null);
    public static Error Forbidden(string? message = null);
    public static Error Internal(string message);
}
```

#### Result (non-generic)

```csharp
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public IReadOnlyList<Error> Errors { get; }
    
    public static Result Ok();
    public static Result Fail(Error error);
    public static Result Fail(IEnumerable<Error> errors);
}
```

#### Result<T> (generic)

```csharp
public class Result<T> : Result
{
    public T Value { get; }  // Throws if IsFailure
    
    public static new Result<T> Ok(T value);
    public static new Result<T> Fail(Error error);
    
    // Transformations
    public Result<TNew> Map<TNew>(Func<T, TNew> transform);
    public T GetValueOrDefault(T defaultValue);
    
    // Callbacks
    public Result<T> OnSuccess(Action<T> action);
    public Result<T> OnFailure(Action<IReadOnlyList<Error>> action);
    
    // Implicit conversions
    public static implicit operator Result<T>(T value);
    public static implicit operator Result<T>(Error error);
}
```

### Usage Examples

#### Returning Success

```csharp
public Result<Product> GetProduct(Guid id)
{
    var product = _repository.Find(id);
    if (product is null)
        return Error.NotFound("Product", id);
    
    return product;  // Implicit conversion to Result<Product>
}
```

#### Returning Validation Errors

```csharp
public Result<Order> CreateOrder(CreateOrderCommand command)
{
    var errors = new List<Error>();
    
    if (command.Items.Count == 0)
        errors.Add(Error.Validation("Items", "At least one item is required."));
    
    if (command.CustomerId == Guid.Empty)
        errors.Add(Error.Validation("CustomerId", "Customer ID is required."));
    
    if (errors.Count > 0)
        return Result<Order>.Fail(errors);
    
    var order = new Order(command);
    return Result<Order>.Ok(order);
}
```

#### Chaining with Map

```csharp
public Result<ProductDto> GetProductDto(Guid id)
{
    return GetProduct(id)
        .Map(product => new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price
        });
}
```

---

## HTTP Status Code Mapping

The `ResultExtensions` class maps `Result<T>` to appropriate ASP.NET Core responses.

### Mapping Table

| Error Code Prefix | HTTP Status | Response Type |
|-------------------|-------------|---------------|
| `VALIDATION.*` | 400 Bad Request | ValidationProblemDetails |
| `NOT_FOUND.*` | 404 Not Found | ProblemDetails |
| `CONFLICT.*` | 409 Conflict | ProblemDetails |
| `UNAUTHORIZED.*` | 401 Unauthorized | ProblemDetails |
| `FORBIDDEN.*` | 403 Forbidden | ProblemDetails |
| `INTERNAL.*` | 500 Internal Server Error | ProblemDetails |
| *(other)* | 400 Bad Request | ProblemDetails |

### Controller Usage

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        Result<ProductDto> result = _productService.GetProduct(id);
        return result.ToActionResult();
    }
    
    [HttpPost]
    public IActionResult Create([FromBody] CreateProductCommand command)
    {
        Result<ProductDto> result = _productService.CreateProduct(command);
        return result.ToCreatedResult(
            routeName: nameof(Get),
            routeValues: dto => new { id = dto.Id });
    }
}
```

---

## Sample Responses

### Validation Error (400)

**Request:**
```http
POST /api/products
Content-Type: application/json

{
  "name": "",
  "price": -10
}
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-1234567890abcdef-abcdef123456-01",
  "errors": {
    "Name": ["Product name is required."],
    "Price": ["Price must be greater than zero."]
  }
}
```

### Not Found Error (404)

**Request:**
```http
GET /api/products/00000000-0000-0000-0000-000000000000
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Product with id '00000000-0000-0000-0000-000000000000' was not found.",
  "traceId": "00-1234567890abcdef-abcdef123456-01"
}
```

### Conflict Error (409)

**Request:**
```http
POST /api/products
Content-Type: application/json

{
  "name": "Existing Product",
  "code": "PROD-001"
}
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "A product with code 'PROD-001' already exists.",
  "traceId": "00-1234567890abcdef-abcdef123456-01"
}
```

### Created Response (201)

**Request:**
```http
POST /api/products
Content-Type: application/json

{
  "name": "New Product",
  "price": 99.99
}
```

**Response:**
```http
HTTP/1.1 201 Created
Location: /api/products/a1b2c3d4-e5f6-7890-abcd-ef1234567890

{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "name": "New Product",
  "price": 99.99
}
```

---

## Migration Guide for Modules

> **Note**: Modules are not yet migrated; adoption will be incremental.

### Phase 1: Add Validators

1. Create validators in your module's `Commands/` or `Queries/` folder
2. Inherit from `AbstractValidator<TCommand>`
3. Validators are auto-registered via assembly scanning

### Phase 2: Update Service Layer

1. Replace `throw new ValidationException(...)` with `return Result.Fail(...)`
2. Replace `throw new NotFoundException(...)` with `return Error.NotFound(...)`
3. Return `Result<T>` from service methods

### Phase 3: Update Controllers

1. Replace manual `BadRequest()` / `NotFound()` calls with `.ToActionResult()`
2. Use `.ToCreatedResult()` for POST endpoints

### Example Migration

**Before:**
```csharp
public ProductDto GetProduct(Guid id)
{
    var product = _repository.Find(id);
    if (product is null)
        throw new NotFoundException($"Product {id} not found");
    
    return _mapper.Map<ProductDto>(product);
}

[HttpGet("{id}")]
public IActionResult Get(Guid id)
{
    try
    {
        var dto = _service.GetProduct(id);
        return Ok(dto);
    }
    catch (NotFoundException ex)
    {
        return NotFound(ex.Message);
    }
}
```

**After:**
```csharp
public Result<ProductDto> GetProduct(Guid id)
{
    var product = _repository.Find(id);
    if (product is null)
        return Error.NotFound("Product", id);
    
    return _mapper.Map<ProductDto>(product);
}

[HttpGet("{id}")]
public IActionResult Get(Guid id)
{
    return _service.GetProduct(id).ToActionResult();
}
```

---

## Related Documentation

- [RFC 7807 - Problem Details for HTTP APIs](https://tools.ietf.org/html/rfc7807)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [PROJECT_GUIDE.md](../PROJECT_GUIDE.md) — Main project guide

---

*Last Updated: June 2025*
