# Architecture Rules (Modular Monolith)

> **Purpose:** Enforce strict modular monolith architecture, layering boundaries, CQRS patterns, and persistence rules. Ensures maintainability, testability, and clear separation of concerns as defined in `docs-architecture/`.

---

## 1. Module Architecture

### 1.1 Module Boundaries

- **[ARCH-001]** Each module MUST be a self-contained vertical slice with its own:
  - `DbContext` (database per module pattern)
  - Entities, Commands, Queries, Controllers
  - `ServiceCollectionExtensions.cs` for DI registration

- **[ARCH-002]** Modules MUST register via extension methods following the pattern:
  ```csharp
  services.AddProductModule(opt => configuration.GetSection("Modules:Product").Bind(opt));
  ```

- **[ARCH-003]** Module folder structure MUST follow this convention:
  ```
  ClassifiedAds.Modules.{ModuleName}/
  ├── Authorization/           # Permissions, authorization handlers
  ├── Commands/                # CQRS commands and handlers
  ├── ConfigurationOptions/    # Module settings ({ModuleName}ModuleOptions.cs)
  ├── Constants/               # EventTypeConstants, etc.
  ├── Controllers/             # API endpoints
  ├── DbConfigurations/        # EF Core entity configurations
  ├── Entities/                # Domain entities (Product.cs, OutboxMessage.cs)
  ├── EventHandlers/           # Domain event handlers
  ├── HostedServices/          # Background workers
  ├── Models/                  # DTOs for API request/response
  ├── Persistence/             # DbContext, Repository implementations
  ├── Queries/                 # CQRS queries and handlers
  └── ServiceCollectionExtensions.cs
  ```

- **[ARCH-004]** MUST NOT reference other module's internal types directly. Use `ClassifiedAds.Contracts` for shared interfaces.

### 1.2 Inter-Module Communication

- **[ARCH-005]** Synchronous cross-module communication MUST use interfaces defined in `ClassifiedAds.Contracts` (e.g., `ICurrentUser`, `IUserService`).
- **[ARCH-006]** Asynchronous cross-module communication MUST use domain events + outbox pattern.
- **[ARCH-007]** MUST NOT call another module's repository or DbContext directly.

---

## 2. Layered Architecture

### 2.1 Layer Dependencies

```
┌─────────────────────────────────────────────────────────────┐
│                      Presentation                           │
│      (Controllers, Minimal APIs) - ClassifiedAds.WebAPI     │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                      Application                            │
│   (Commands, Queries, Handlers) - ClassifiedAds.Application │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                        Domain                               │
│    (Entities, Events, Interfaces) - ClassifiedAds.Domain    │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                     Infrastructure                          │
│  (Persistence, Messaging) - ClassifiedAds.Infrastructure    │
└─────────────────────────────────────────────────────────────┘
```

### Rules

- **[ARCH-010]** Domain layer MUST NOT depend on any other layer.
- **[ARCH-011]** Application layer MUST depend only on Domain.
- **[ARCH-012]** Infrastructure layer MUST implement interfaces defined in Domain.
- **[ARCH-013]** Presentation layer MUST NOT contain business logic.

---

## 3. Controllers (Presentation Layer)

### Rules

- **[ARCH-020]** Controllers MUST be thin — delegate all work to `Dispatcher`.
- **[ARCH-021]** Controllers MUST NOT contain business logic, validation logic, or direct database access.
- **[ARCH-022]** Controllers MUST use `[Authorize]` attribute with specific permission policies.
- **[ARCH-023]** Controllers MUST return appropriate HTTP status codes via `ActionResult<T>`.
- **[ARCH-024]** Controllers MUST use `[ProducesResponseType]` attributes for Swagger documentation.
- **[ARCH-025]** Controllers MUST apply rate limiting via `[EnableRateLimiting]`.

```csharp
// GOOD - Thin controller delegating to Dispatcher
[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(Dispatcher dispatcher, ILogger<ProductsController> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [Authorize(Permissions.GetProduct)]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductModel>> Get(Guid id)
    {
        _logger.LogInformation("Getting product {ProductId}", id);
        var product = await _dispatcher.DispatchAsync(new GetProductQuery { Id = id, ThrowNotFoundIfNull = true });
        return Ok(product.ToModel());
    }
}

// BAD - Business logic in controller
[HttpPost]
public async Task<IActionResult> Create([FromBody] ProductModel model)
{
    // VIOLATION: Business logic should be in handler
    if (await _dbContext.Products.AnyAsync(p => p.Code == model.Code))
        return BadRequest("Code already exists");
    
    var product = new Product { ... };
    _dbContext.Products.Add(product);
    await _dbContext.SaveChangesAsync();
    return Ok();
}
```

---

## 4. CQRS Pattern

### 4.1 Commands (Write Operations)

- **[ARCH-030]** Commands MUST implement `ICommand` marker interface.
- **[ARCH-031]** Command handlers MUST implement `ICommandHandler<TCommand>`.
- **[ARCH-032]** Commands MUST NOT return domain data. Use out-parameter pattern for generated IDs if needed.
- **[ARCH-033]** Command naming: `{Verb}{Entity}Command` (e.g., `AddUpdateProductCommand`, `DeleteProductCommand`).
- **[ARCH-034]** Command handlers MUST be in the `Commands/` folder of the module.

```csharp
// Command definition
public class AddUpdateProductCommand : ICommand
{
    public Product Product { get; set; }
}

// Handler
public class AddUpdateProductCommandHandler : ICommandHandler<AddUpdateProductCommand>
{
    private readonly ICrudService<Product> _productService;

    public AddUpdateProductCommandHandler(ICrudService<Product> productService)
    {
        _productService = productService;
    }

    public async Task HandleAsync(AddUpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        await _productService.AddOrUpdateAsync(command.Product, cancellationToken);
    }
}
```

### 4.2 Queries (Read Operations)

- **[ARCH-040]** Queries MUST implement `IQuery<TResult>`.
- **[ARCH-041]** Query handlers MUST implement `IQueryHandler<TQuery, TResult>`.
- **[ARCH-042]** Queries MUST NOT have side effects.
- **[ARCH-043]** Query naming: `Get{Entity}Query`, `Get{Entities}Query` (e.g., `GetProductQuery`, `GetProductsQuery`).
- **[ARCH-044]** Query handlers MUST be in the `Queries/` folder of the module.

```csharp
// Query definition
public class GetProductQuery : IQuery<Product>
{
    public Guid Id { get; set; }
    public bool ThrowNotFoundIfNull { get; set; }
}

// Handler
public class GetProductQueryHandler : IQueryHandler<GetProductQuery, Product>
{
    private readonly IProductRepository _repository;

    public async Task<Product> HandleAsync(GetProductQuery query, CancellationToken cancellationToken = default)
    {
        var product = await _repository.FirstOrDefaultAsync(
            _repository.GetQueryableSet().Where(x => x.Id == query.Id));
        
        if (product == null && query.ThrowNotFoundIfNull)
            throw new NotFoundException($"Product {query.Id} not found");
        
        return product;
    }
}
```

### 4.3 Dispatcher

- **[ARCH-050]** MUST use the custom `Dispatcher` class (NOT MediatR).
- **[ARCH-051]** All commands/queries MUST be dispatched via `Dispatcher.DispatchAsync()`.
- **[ARCH-052]** MUST NOT call handlers directly — always go through Dispatcher.

---

## 5. DTO Mapping

### Rules

- **[ARCH-060]** DTOs (Models) MUST be separate from domain entities.
- **[ARCH-061]** Mapping MUST be explicit via extension methods or manual mapping.
- **[ARCH-062]** MUST NOT use implicit casting or automatic mapping without explicit mapping code.
- **[ARCH-063]** Mapping extensions SHOULD be in the module's root or `Models/` folder.

```csharp
// GOOD - Explicit mapping extension
public static class ProductMappingExtensions
{
    public static ProductModel ToModel(this Product entity) => new ProductModel
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description
    };

    public static Product ToEntity(this ProductModel model) => new Product
    {
        Id = model.Id,
        Code = model.Code,
        Name = model.Name,
        Description = model.Description
    };
}
```

---

## 6. Validation

### Rules

- **[ARCH-070]** Input validation MUST occur before dispatching commands/queries.
- **[ARCH-071]** Use FluentValidation or DataAnnotations consistently within a module.
- **[ARCH-072]** Validation errors MUST return HTTP 400 with ProblemDetails format.
- **[ARCH-073]** Business rule validation MUST be in command handlers, not controllers.

---

## 7. Persistence (EF Core)

### 7.1 DbContext Per Module

- **[ARCH-080]** Each module MUST have its own `DbContext` inheriting from `DbContextUnitOfWork<T>`.
- **[ARCH-081]** DbContext MUST apply configurations from `DbConfigurations/` folder.
- **[ARCH-082]** DbContext MUST override `SaveChangesAsync` to set outbox `ActivityId` for distributed tracing.

```csharp
public class ProductDbContext : DbContextUnitOfWork<ProductDbContext>
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetOutboxActivityId();
        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 7.2 Repository Pattern

- **[ARCH-090]** All data access MUST use `IRepository<TEntity, TKey>`.
- **[ARCH-091]** MUST NOT use DbContext directly in handlers/services except for complex queries.
- **[ARCH-092]** Repository MUST expose `IQueryable` via `GetQueryableSet()` for query composition.
- **[ARCH-093]** Bulk operations MUST use repository bulk methods (`BulkInsertAsync`, etc.).

### 7.3 Transactions

- **[ARCH-100]** Transactions MUST be managed via `IUnitOfWork.SaveChangesAsync()`.
- **[ARCH-101]** MUST NOT use explicit transactions unless required for multi-aggregate operations.
- **[ARCH-102]** Cross-module transactions MUST use the outbox pattern for eventual consistency.

### 7.4 Migrations

- **[ARCH-110]** All migrations MUST be applied via `ClassifiedAds.Migrator` project.
- **[ARCH-111]** MUST NOT apply migrations directly in WebAPI startup.
- **[ARCH-112]** Migration naming: `{DateStamp}_{Description}` or EF default naming.

---

## 8. Domain Events and Outbox

### Rules

- **[ARCH-120]** Domain events MUST implement `IDomainEvent`.
- **[ARCH-121]** Event handlers MUST implement `IDomainEventHandler<TEvent>`.
- **[ARCH-122]** Events for external consumption MUST go through the outbox table.
- **[ARCH-123]** Outbox messages MUST include `ActivityId` for distributed tracing.
- **[ARCH-124]** `PublishEventWorker` MUST be used to publish outbox messages to message bus.

```csharp
// Event handler writing to outbox
public class ProductCreatedEventHandler : IDomainEventHandler<EntityCreatedEvent<Product>>
{
    private readonly IRepository<OutboxMessage, Guid> _outboxRepository;
    private readonly ICurrentUser _currentUser;

    public async Task HandleAsync(EntityCreatedEvent<Product> domainEvent, CancellationToken cancellationToken)
    {
        await _outboxRepository.AddOrUpdateAsync(new OutboxMessage
        {
            EventType = EventTypeConstants.ProductCreated,
            TriggeredById = _currentUser.UserId,
            CreatedDateTime = domainEvent.EventDateTime,
            ObjectId = domainEvent.Entity.Id.ToString(),
            Payload = domainEvent.Entity.AsJsonString()
        }, cancellationToken);

        await _outboxRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

---

## 9. Configuration

### Rules

- **[ARCH-130]** Module configuration MUST be in `ConfigurationOptions/{ModuleName}ModuleOptions.cs`.
- **[ARCH-131]** Configuration MUST be bound via `configuration.GetSection("Modules:{ModuleName}").Bind(opt)`.
- **[ARCH-132]** Connection strings MUST be in module options, NOT in root configuration.

---

## Conflict Resolution

If an architecture rule conflicts with a security rule, follow the security rule. Otherwise, follow the priority order in `00-priority.md`.

---

## Checklist (Complete Before PR)

- [ ] Module has its own DbContext and follows folder structure
- [ ] Module registers via `AddXxxModule()` extension method
- [ ] No business logic in controllers — all delegated to Dispatcher
- [ ] CQRS pattern followed: Commands and Queries separated
- [ ] Custom Dispatcher used (not MediatR)
- [ ] DTO mapping is explicit (extension methods or manual)
- [ ] Validation approach consistent with module convention
- [ ] Repository pattern used for all data access
- [ ] Transactions via UnitOfWork only
- [ ] Migrations via Migrator project only
- [ ] Cross-module communication via events/outbox
- [ ] No direct references to other modules' internal types

---

## Good Example: Complete Module Flow

```
POST /api/products (Create Product)
         │
         ▼
┌─────────────────────┐
│  ProductsController │  ← Thin: validates input, returns response
│  [Authorize(...)]   │
└──────────┬──────────┘
           │ _dispatcher.DispatchAsync(new AddUpdateProductCommand { Product = product })
           ▼
┌─────────────────────────────┐
│  AddUpdateProductCommand    │
│  Handler                    │  ← Business logic here
└──────────┬──────────────────┘
           │ _crudService.AddOrUpdateAsync(product)
           ▼
┌─────────────────────────────┐
│  CrudService<Product>       │  ← Saves + dispatches domain event
└──────────┬──────────────────┘
           │ _dispatcher.DispatchAsync(new EntityCreatedEvent<Product>(...))
           ▼
┌─────────────────────────────┐
│  ProductCreatedEventHandler │  ← Writes to audit log + outbox
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│  PublishEventWorker         │  ← Background: publishes to message bus
└─────────────────────────────┘
```
