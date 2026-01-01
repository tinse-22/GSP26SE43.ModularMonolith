# Coding Rules (.NET 10 / C#)

> **Purpose:** Ensure all code is consistent, maintainable, performant, and follows .NET 10/C# best practices. These rules are enforced by analyzers, formatters, and code review.

---

## 1. Naming Conventions

### 1.1 General Naming

| Element | Convention | Example |
|---------|------------|---------|
| Public types, methods, properties | PascalCase | `ProductService`, `GetProductAsync` |
| Private/internal fields | _camelCase (underscore prefix) | `_productRepository` |
| Local variables, parameters | camelCase | `productId`, `cancellationToken` |
| Constants | PascalCase | `MaxRetryCount`, `DefaultTimeout` |
| Interfaces | IPascalCase | `IProductRepository` |
| Generic type parameters | TPascalCase | `TEntity`, `TKey` |

### Rules

- **[COD-001]** All public types and members MUST use PascalCase.
- **[COD-002]** All private fields MUST use _camelCase with underscore prefix.
- **[COD-003]** All local variables and parameters MUST use camelCase.
- **[COD-004]** Interfaces MUST be prefixed with `I`.
- **[COD-005]** Async methods MUST be suffixed with `Async`.

---

## 2. Nullable Reference Types

### Rules

- **[COD-010]** All projects MUST enable nullable reference types: `<Nullable>enable</Nullable>`.
- **[COD-011]** All reference types MUST be explicitly nullable (`?`) or non-nullable.
- **[COD-012]** Use `required` modifier for mandatory properties in DTOs/models.
- **[COD-013]** MUST NOT use `null!` (null-forgiving operator) except in test setup with documented reason.

```csharp
// GOOD
public class ProductModel
{
    public required string Name { get; set; }
    public string? Description { get; set; }  // Nullable OK - optional field
}

// BAD - Do not suppress nullable warnings without justification
public string Name { get; set; } = null!;  // Avoid
```

---

## 3. Async/Await and CancellationToken

### Rules

- **[COD-020]** All I/O-bound operations MUST be async.
- **[COD-021]** All async methods MUST accept `CancellationToken` as the last parameter.
- **[COD-022]** `CancellationToken` MUST be passed to all downstream async calls.
- **[COD-023]** MUST NOT use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` (sync-over-async).
- **[COD-024]** MUST NOT use `async void` except for event handlers.
- **[COD-025]** Use `ConfigureAwait(false)` in library code (not in ASP.NET Core controllers).

```csharp
// GOOD
public async Task<Product> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
{
    return await _repository.FirstOrDefaultAsync(
        _repository.GetQueryableSet().Where(x => x.Id == id),
        cancellationToken);
}

// BAD - Missing CancellationToken, sync-over-async
public Product GetProduct(Guid id)
{
    return _repository.FirstOrDefaultAsync(...).Result;  // VIOLATION
}
```

---

## 4. Exception Handling

### Rules

- **[COD-030]** Exceptions MUST only be thrown for truly exceptional conditions.
- **[COD-031]** For expected failures (validation, not found), use Result pattern or return appropriate HTTP status.
- **[COD-032]** MUST NOT catch generic `Exception` without re-throwing or logging.
- **[COD-033]** Use `NotFoundException` (or similar) for 404 scenarios, handled by global exception middleware.
- **[COD-034]** All exceptions MUST be logged with context before re-throwing.

```csharp
// GOOD - Throw specific exception for expected condition
public async Task<Product> GetProductAsync(GetProductQuery query, CancellationToken ct)
{
    var product = await _repository.FirstOrDefaultAsync(..., ct);
    
    if (product == null && query.ThrowNotFoundIfNull)
        throw new NotFoundException($"Product with Id {query.Id} not found");
    
    return product;
}

// BAD - Catching Exception without context
catch (Exception) { return null; }  // VIOLATION - swallowing exceptions
```

---

## 5. Structured Logging

### Rules

- **[COD-040]** MUST use structured logging (Serilog via `ILogger<T>`).
- **[COD-041]** MUST use message templates with named placeholders, NOT string interpolation.
- **[COD-042]** MUST include correlation/request IDs in all logs (via `Activity.Current?.Id`).
- **[COD-043]** MUST NOT log PII (emails, passwords, personal data) - see `security.md`.
- **[COD-044]** Log levels MUST be appropriate:
  - `Trace/Debug`: Detailed diagnostic info
  - `Information`: Normal operations
  - `Warning`: Unexpected but recoverable
  - `Error`: Failures that need attention
  - `Critical`: Application-wide failures

```csharp
// GOOD - Structured logging with named parameters
_logger.LogInformation("Getting product {ProductId} for user {UserId}", productId, userId);

// BAD - String interpolation loses structured context
_logger.LogInformation($"Getting product {productId}");  // VIOLATION
```

---

## 6. Performance Rules

### Rules

- **[COD-050]** MUST NOT use sync-over-async patterns.
- **[COD-051]** SHOULD use `IAsyncEnumerable<T>` for streaming large result sets.
- **[COD-052]** SHOULD use `Span<T>`, `Memory<T>`, `ReadOnlySpan<T>` for performance-critical buffer operations.
- **[COD-053]** MUST NOT allocate in hot paths unnecessarily (e.g., avoid LINQ in tight loops when array is sufficient).
- **[COD-054]** Database queries MUST use projection (`.Select()`) instead of loading full entities when only partial data is needed.
- **[COD-055]** SHOULD use caching (`IMemoryCache`, `IDistributedCache`) for frequently accessed, rarely changing data.

```csharp
// GOOD - Projection to avoid loading unnecessary data
var productNames = await _repository.ToListAsync(
    _repository.GetQueryableSet()
        .Where(p => p.IsActive)
        .Select(p => new { p.Id, p.Name }));

// BAD - Loading full entities when only names needed
var products = await _repository.ToListAsync(_repository.GetQueryableSet());
var names = products.Select(p => p.Name);  // Inefficient
```

---

## 7. Formatting and Analyzers

### Rules

- **[COD-060]** All code MUST be formatted according to `.editorconfig` in the repository root.
- **[COD-061]** MUST run `dotnet format` before committing (CI will enforce this).
- **[COD-062]** All projects MUST enable Roslyn analyzers (CA rules).
- **[COD-063]** All analyzer warnings MUST be resolved or explicitly suppressed with justification.
- **[COD-064]** File-scoped namespaces MUST be used: `namespace X;` (not block-scoped).

```csharp
// GOOD - File-scoped namespace
namespace ClassifiedAds.Modules.Product.Services;

public class ProductService
{
    // ...
}
```

---

## 8. Dependency Injection

### Rules

- **[COD-070]** MUST use constructor injection for all dependencies.
- **[COD-071]** MUST NOT use Service Locator pattern (`IServiceProvider.GetService<T>()` in business code).
- **[COD-072]** Service lifetimes MUST be correct:
  - `Singleton`: Stateless services, caches
  - `Scoped`: DbContext, request-scoped services
  - `Transient`: Lightweight, stateless factories
- **[COD-073]** MUST NOT inject `Scoped` services into `Singleton` services.
- **[COD-074]** MUST use `IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>` for configuration.
- **[COD-075]** MUST NOT inject `IConfiguration` directly except in startup/composition root.

```csharp
// GOOD - Proper DI with IOptions
public class ProductService
{
    private readonly IProductRepository _repository;
    private readonly IOptions<ProductModuleOptions> _options;
    
    public ProductService(IProductRepository repository, IOptions<ProductModuleOptions> options)
    {
        _repository = repository;
        _options = options;
    }
}

// BAD - Service locator pattern
public class BadService
{
    private readonly IServiceProvider _provider;
    
    public void DoWork()
    {
        var repo = _provider.GetService<IRepository>();  // VIOLATION
    }
}
```

---

## 9. Code Organization

### Rules

- **[COD-080]** One type per file (except nested types and related records).
- **[COD-081]** File name MUST match type name.
- **[COD-082]** `using` statements MUST be inside the namespace (per `.editorconfig`).
- **[COD-083]** Members SHOULD be ordered: Fields → Constructors → Properties → Methods.
- **[COD-084]** Primary constructors MAY be used for simple types (C# 12+).

---

## 10. LINQ and Collections

### Rules

- **[COD-090]** SHOULD prefer LINQ method syntax over query syntax for consistency.
- **[COD-091]** MUST use `.ToListAsync()`, `.FirstOrDefaultAsync()` for EF Core queries.
- **[COD-092]** MUST NOT call `.ToList()` before filtering (materialize only what's needed).
- **[COD-093]** Use collection expressions `[]` for empty collections in C# 12+.

```csharp
// GOOD - Proper async materialization
var products = await _repository.ToListAsync(
    _repository.GetQueryableSet().Where(p => p.IsActive));

// BAD - Materializes all then filters in memory
var products = (await _repository.ToListAsync(_repository.GetQueryableSet()))
    .Where(p => p.IsActive);  // VIOLATION
```

---

## Conflict Resolution

If a coding rule conflicts with a higher-priority rule (Security, Architecture, Testing), follow the higher-priority rule as defined in `00-priority.md`.

---

## Checklist (Complete Before PR)

- [ ] Naming conventions followed (PascalCase, _camelCase, camelCase)
- [ ] Nullable reference types enabled and properly annotated
- [ ] All async methods use `async`/`await` with `CancellationToken`
- [ ] No sync-over-async (`.Result`, `.Wait()`)
- [ ] Structured logging with message templates (no string interpolation)
- [ ] No PII logged
- [ ] Code formatted with `dotnet format`
- [ ] All analyzer warnings resolved or justified
- [ ] Constructor injection used (no service locator)
- [ ] `IOptions<T>` used for configuration
- [ ] File-scoped namespaces used

---

## Good Example: Complete Service Implementation

```csharp
namespace ClassifiedAds.Modules.Product.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ProductService> _logger;
    private readonly IOptions<ProductModuleOptions> _options;

    public ProductService(
        IProductRepository productRepository,
        ILogger<ProductService> logger,
        IOptions<ProductModuleOptions> options)
    {
        _productRepository = productRepository;
        _logger = logger;
        _options = options;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving product {ProductId}", id);

        var product = await _productRepository.FirstOrDefaultAsync(
            _productRepository.GetQueryableSet().Where(p => p.Id == id),
            cancellationToken);

        if (product is null)
        {
            _logger.LogWarning("Product {ProductId} not found", id);
            return null;
        }

        return product.ToDto();
    }
}
```
