# AI Agent Coding Standards & Guidelines

> **CRITICAL**: This document defines mandatory coding standards for AI Agents working on this codebase.
> **Violation of these rules will result in code rejection.**

---

## Table of Contents

1. [Module Structure Requirements](#1-module-structure-requirements)
2. [Entity Design Rules](#2-entity-design-rules)
3. [CQRS Pattern Implementation](#3-cqrs-pattern-implementation)
4. [Controller Standards](#4-controller-standards)
5. [Repository & Persistence](#5-repository--persistence)
6. [Event Handling & Outbox](#6-event-handling--outbox)
7. [Authorization & Permissions](#7-authorization--permissions)
8. [DTO & Model Mapping](#8-dto--model-mapping)
9. [Dependency Injection Registration](#9-dependency-injection-registration)
10. [Naming Conventions](#10-naming-conventions)
11. [Code Templates](#11-code-templates)

---

## 1. Module Structure Requirements

### ✅ MANDATORY Folder Structure

Every module **MUST** follow this exact structure:

```
ClassifiedAds.Modules.{ModuleName}/
├── Authorization/
│   └── Permissions.cs                    # Permission constants
├── Commands/
│   ├── AddUpdate{Entity}Command.cs       # Create/Update command + handler
│   ├── Delete{Entity}Command.cs          # Delete command + handler
│   └── PublishEventsCommand.cs           # Outbox publisher command
├── ConfigurationOptions/
│   ├── {ModuleName}ModuleOptions.cs      # Module options
│   └── ConnectionStringsOptions.cs       # Connection string options
├── Constants/
│   └── EventTypeConstants.cs             # Event type string constants
├── Controllers/
│   └── {Entity}Controller.cs             # API controller (plural name)
├── DbConfigurations/
│   ├── {Entity}Configuration.cs          # EF Core entity config
│   ├── AuditLogEntryConfiguration.cs     # Audit log config
│   └── OutboxMessageConfiguration.cs     # Outbox config
├── Entities/
│   ├── {Entity}.cs                       # Main domain entity
│   ├── AuditLogEntry.cs                  # Module's audit log entity
│   └── OutboxMessage.cs                  # Module's outbox entity
├── EventHandlers/
│   ├── {Entity}CreatedEventHandler.cs    # Handle EntityCreatedEvent
│   ├── {Entity}UpdatedEventHandler.cs    # Handle EntityUpdatedEvent
│   └── {Entity}DeletedEventHandler.cs    # Handle EntityDeletedEvent
├── HostedServices/
│   └── PublishEventWorker.cs             # Background outbox publisher
├── Models/
│   ├── {Entity}Model.cs                  # API DTO
│   └── {Entity}ModelMappingConfiguration.cs # Entity <-> Model mapping
├── OutBoxEventPublishers/
│   └── {EventType}Publisher.cs           # Message bus publishers
├── Persistence/
│   ├── {ModuleName}DbContext.cs          # Module's DbContext
│   ├── I{Entity}Repository.cs            # Repository interface
│   ├── {Entity}Repository.cs             # Repository implementation
│   └── Repository.cs                     # Generic repository
├── Queries/
│   ├── Get{Entity}Query.cs               # Get single entity query
│   └── Get{Entities}Query.cs             # Get all entities query
├── RateLimiterPolicies/
│   └── DefaultRateLimiterPolicy.cs       # Rate limiting
├── ServiceCollectionExtensions.cs         # DI registration entry point
└── ClassifiedAds.Modules.{ModuleName}.csproj
```

### ❌ FORBIDDEN

- Creating folders outside this structure
- Mixing concerns (e.g., business logic in Controllers)
- Direct database access in Controllers
- Cross-module direct references

---

## 2. Entity Design Rules

### ✅ MANDATORY Pattern

```csharp
// File: Entities/{EntityName}.cs
using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.{ModuleName}.Entities;

public class {EntityName} : Entity<Guid>, IAggregateRoot
{
    public string Property1 { get; set; }
    public string Property2 { get; set; }
    // ... other properties
}
```

### ✅ Rules

| Rule | Description |
|------|-------------|
| **Inherit from `Entity<Guid>`** | All entities MUST inherit from `Entity<Guid>` |
| **Implement `IAggregateRoot`** | Aggregate roots MUST implement `IAggregateRoot` |
| **Use `Guid` for Id** | Primary key MUST be `Guid` |
| **No navigation properties across modules** | Entities MUST NOT reference other module's entities |
| **Properties are public get/set** | Use simple auto-properties |

### ✅ OutboxMessage Entity (Copy exactly)

```csharp
// File: Entities/OutboxMessage.cs
using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.{ModuleName}.Entities;

public class OutboxMessage : OutboxMessageBase, IAggregateRoot
{
}

public class ArchivedOutboxMessage : OutboxMessageBase, IAggregateRoot
{
}

public abstract class OutboxMessageBase : Entity<Guid>
{
    public string EventType { get; set; }
    public Guid TriggeredById { get; set; }
    public string ObjectId { get; set; }
    public string Payload { get; set; }
    public bool Published { get; set; }
    public string ActivityId { get; set; }
}
```

### ✅ AuditLogEntry Entity (Copy exactly)

```csharp
// File: Entities/AuditLogEntry.cs
using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.{ModuleName}.Entities;

public class AuditLogEntry : Entity<Guid>, IAggregateRoot
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string Action { get; set; }
    public string ObjectId { get; set; }
    public string Log { get; set; }
}
```

---

## 3. CQRS Pattern Implementation

### ✅ Command Definition (Write Operations)

```csharp
// File: Commands/AddUpdate{Entity}Command.cs
using ClassifiedAds.Application;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.Commands;

public class AddUpdate{Entity}Command : ICommand
{
    public Entities.{Entity} {Entity} { get; set; }
}

public class AddUpdate{Entity}CommandHandler : ICommandHandler<AddUpdate{Entity}Command>
{
    private readonly ICrudService<Entities.{Entity}> _{entity}Service;

    public AddUpdate{Entity}CommandHandler(ICrudService<Entities.{Entity}> {entity}Service)
    {
        _{entity}Service = {entity}Service;
    }

    public async Task HandleAsync(AddUpdate{Entity}Command command, CancellationToken cancellationToken = default)
    {
        await _{entity}Service.AddOrUpdateAsync(command.{Entity}, cancellationToken);
    }
}
```

### ✅ Delete Command

```csharp
// File: Commands/Delete{Entity}Command.cs
using ClassifiedAds.Application;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.Commands;

public class Delete{Entity}Command : ICommand
{
    public Entities.{Entity} {Entity} { get; set; }
}

public class Delete{Entity}CommandHandler : ICommandHandler<Delete{Entity}Command>
{
    private readonly ICrudService<Entities.{Entity}> _{entity}Service;

    public Delete{Entity}CommandHandler(ICrudService<Entities.{Entity}> {entity}Service)
    {
        _{entity}Service = {entity}Service;
    }

    public async Task HandleAsync(Delete{Entity}Command command, CancellationToken cancellationToken = default)
    {
        await _{entity}Service.DeleteAsync(command.{Entity}, cancellationToken);
    }
}
```

### ✅ Query Definition (Read Operations)

```csharp
// File: Queries/Get{Entity}Query.cs
using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.{ModuleName}.Persistence;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.Queries;

public class Get{Entity}Query : IQuery<Entities.{Entity}>
{
    public Guid Id { get; set; }
    public bool ThrowNotFoundIfNull { get; set; }
}

public class Get{Entity}QueryHandler : IQueryHandler<Get{Entity}Query, Entities.{Entity}>
{
    private readonly I{Entity}Repository _{entity}Repository;

    public Get{Entity}QueryHandler(I{Entity}Repository {entity}Repository)
    {
        _{entity}Repository = {entity}Repository;
    }

    public async Task<Entities.{Entity}> HandleAsync(Get{Entity}Query query, CancellationToken cancellationToken = default)
    {
        var {entity} = await _{entity}Repository.FirstOrDefaultAsync(
            _{entity}Repository.GetQueryableSet().Where(x => x.Id == query.Id));

        if (query.ThrowNotFoundIfNull && {entity} == null)
        {
            throw new NotFoundException($"{Entity} {query.Id} not found.");
        }

        return {entity};
    }
}
```

### ✅ Get All Query

```csharp
// File: Queries/Get{Entities}Query.cs
using ClassifiedAds.Application;
using ClassifiedAds.Modules.{ModuleName}.Persistence;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.Queries;

public class Get{Entities}Query : IQuery<List<Entities.{Entity}>>
{
}

public class Get{Entities}QueryHandler : IQueryHandler<Get{Entities}Query, List<Entities.{Entity}>>
{
    private readonly I{Entity}Repository _{entity}Repository;

    public Get{Entities}QueryHandler(I{Entity}Repository {entity}Repository)
    {
        _{entity}Repository = {entity}Repository;
    }

    public async Task<List<Entities.{Entity}>> HandleAsync(Get{Entities}Query query, CancellationToken cancellationToken = default)
    {
        return await _{entity}Repository.ToListAsync(_{entity}Repository.GetQueryableSet());
    }
}
```

### ❌ FORBIDDEN in Commands/Queries

- Direct DbContext access (use Repository)
- Business logic that should be in Domain Services
- Calling other modules directly
- Returning entities from Commands (Commands return void)

---

## 4. Controller Standards

### ✅ MANDATORY Controller Pattern

```csharp
// File: Controllers/{Entities}Controller.cs
using ClassifiedAds.Application;
using ClassifiedAds.Modules.{ModuleName}.Authorization;
using ClassifiedAds.Modules.{ModuleName}.Commands;
using ClassifiedAds.Modules.{ModuleName}.Models;
using ClassifiedAds.Modules.{ModuleName}.Queries;
using ClassifiedAds.Modules.{ModuleName}.RateLimiterPolicies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.Controllers;

[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class {Entities}Controller : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<{Entities}Controller> _logger;

    public {Entities}Controller(Dispatcher dispatcher, ILogger<{Entities}Controller> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [Authorize(Permissions.Get{Entities})]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<{Entity}Model>>> Get()
    {
        _logger.LogInformation("Getting all {entities}");
        var {entities} = await _dispatcher.DispatchAsync(new Get{Entities}Query());
        var model = {entities}.ToModels();
        return Ok(model);
    }

    [Authorize(Permissions.Get{Entity})]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<{Entity}Model>> Get(Guid id)
    {
        var {entity} = await _dispatcher.DispatchAsync(new Get{Entity}Query { Id = id, ThrowNotFoundIfNull = true });
        var model = {entity}.ToModel();
        return Ok(model);
    }

    [Authorize(Permissions.Add{Entity})]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<{Entity}Model>> Post([FromBody] {Entity}Model model)
    {
        var {entity} = model.ToEntity();
        await _dispatcher.DispatchAsync(new AddUpdate{Entity}Command { {Entity} = {entity} });
        model = {entity}.ToModel();
        return Created($"/api/{entities}/{model.Id}", model);
    }

    [Authorize(Permissions.Update{Entity})]
    [HttpPut("{id}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<{Entity}Model>> Put(Guid id, [FromBody] {Entity}Model model)
    {
        var {entity} = await _dispatcher.DispatchAsync(new Get{Entity}Query { Id = id, ThrowNotFoundIfNull = true });

        // Map properties from model to entity
        {entity}.Property1 = model.Property1;
        {entity}.Property2 = model.Property2;

        await _dispatcher.DispatchAsync(new AddUpdate{Entity}Command { {Entity} = {entity} });

        model = {entity}.ToModel();
        return Ok(model);
    }

    [Authorize(Permissions.Delete{Entity})]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id)
    {
        var {entity} = await _dispatcher.DispatchAsync(new Get{Entity}Query { Id = id, ThrowNotFoundIfNull = true });
        await _dispatcher.DispatchAsync(new Delete{Entity}Command { {Entity} = {entity} });
        return Ok();
    }
}
```

### ✅ Controller Rules

| Rule | Description |
|------|-------------|
| **Thin controllers** | Controllers MUST only dispatch commands/queries |
| **Use Dispatcher** | ALWAYS use `_dispatcher.DispatchAsync()` |
| **No direct repository** | NEVER inject repositories into controllers |
| **No business logic** | Business logic MUST be in handlers |
| **Return DTOs** | NEVER return entities directly, use Models |
| **Use Permissions** | Every action MUST have `[Authorize(Permissions.X)]` |
| **Rate limiting** | MUST have `[EnableRateLimiting]` attribute |

### ❌ FORBIDDEN in Controllers

```csharp
// ❌ WRONG - Direct repository access
public class BadController : ControllerBase
{
    private readonly IProductRepository _repository; // ❌ FORBIDDEN
    
    public async Task<ActionResult> Get()
    {
        var products = await _repository.GetAll(); // ❌ FORBIDDEN
        return Ok(products); // ❌ Returning entities
    }
}
```

---

## 5. Repository & Persistence

### ✅ DbContext Pattern

```csharp
// File: Persistence/{ModuleName}DbContext.cs
using ClassifiedAds.Modules.{ModuleName}.Entities;
using ClassifiedAds.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.Persistence;

public class {ModuleName}DbContext : DbContextUnitOfWork<{ModuleName}DbContext>
{
    public {ModuleName}DbContext(DbContextOptions<{ModuleName}DbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("{modulename}");  // lowercase schema name
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override int SaveChanges()
    {
        SetOutboxActivityId();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetOutboxActivityId();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void SetOutboxActivityId()
    {
        var entities = ChangeTracker.Entries<OutboxMessage>();
        foreach (var entity in entities.Where(e => e.State == EntityState.Added))
        {
            var outbox = entity.Entity;
            if (string.IsNullOrWhiteSpace(outbox.ActivityId))
            {
                outbox.ActivityId = System.Diagnostics.Activity.Current?.Id;
            }
        }
    }
}
```

### ✅ Repository Interface

```csharp
// File: Persistence/I{Entity}Repository.cs
using ClassifiedAds.Domain.Repositories;
using System;

namespace ClassifiedAds.Modules.{ModuleName}.Persistence;

public interface I{Entity}Repository : IRepository<Entities.{Entity}, Guid>
{
    // Add custom methods here if needed
}
```

### ✅ Repository Implementation

```csharp
// File: Persistence/{Entity}Repository.cs
using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using System;

namespace ClassifiedAds.Modules.{ModuleName}.Persistence;

public class {Entity}Repository : Repository<Entities.{Entity}, Guid>, I{Entity}Repository
{
    public {Entity}Repository({ModuleName}DbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }
    
    // Add custom method implementations here
}
```

### ✅ EF Configuration

```csharp
// File: DbConfigurations/{Entity}Configuration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.{ModuleName}.DbConfigurations;

public class {Entity}Configuration : IEntityTypeConfiguration<Entities.{Entity}>
{
    public void Configure(EntityTypeBuilder<Entities.{Entity}> builder)
    {
        builder.ToTable("{Entities}");  // Plural table name
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        
        // Configure properties
        builder.Property(x => x.Property1).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Property2).HasMaxLength(500);
    }
}
```

---

## 6. Event Handling & Outbox

### ✅ Event Handler Pattern

```csharp
// File: EventHandlers/{Entity}CreatedEventHandler.cs
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.ExtensionMethods;
using ClassifiedAds.Domain.Events;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.{ModuleName}.Constants;
using ClassifiedAds.Modules.{ModuleName}.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.{ModuleName}.EventHandlers;

public class {Entity}CreatedEventHandler : IDomainEventHandler<EntityCreatedEvent<Entities.{Entity}>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IRepository<AuditLogEntry, Guid> _auditLogRepository;
    private readonly IRepository<OutboxMessage, Guid> _outboxMessageRepository;

    public {Entity}CreatedEventHandler(
        ICurrentUser currentUser,
        IRepository<AuditLogEntry, Guid> auditLogRepository,
        IRepository<OutboxMessage, Guid> outboxMessageRepository)
    {
        _currentUser = currentUser;
        _auditLogRepository = auditLogRepository;
        _outboxMessageRepository = outboxMessageRepository;
    }

    public async Task HandleAsync(EntityCreatedEvent<Entities.{Entity}> domainEvent, CancellationToken cancellationToken = default)
    {
        // 1. Create audit log
        var auditLog = new AuditLogEntry
        {
            UserId = _currentUser.IsAuthenticated ? _currentUser.UserId : Guid.Empty,
            CreatedDateTime = domainEvent.EventDateTime,
            Action = "CREATED_{ENTITY}",
            ObjectId = domainEvent.Entity.Id.ToString(),
            Log = domainEvent.Entity.AsJsonString(),
        };

        await _auditLogRepository.AddOrUpdateAsync(auditLog, cancellationToken);

        // 2. Create outbox for audit log
        await _outboxMessageRepository.AddOrUpdateAsync(new OutboxMessage
        {
            EventType = EventTypeConstants.AuditLogEntryCreated,
            TriggeredById = _currentUser.UserId,
            CreatedDateTime = auditLog.CreatedDateTime,
            ObjectId = auditLog.Id.ToString(),
            Payload = auditLog.AsJsonString(),
        }, cancellationToken);

        // 3. Create outbox for entity event
        await _outboxMessageRepository.AddOrUpdateAsync(new OutboxMessage
        {
            EventType = EventTypeConstants.{Entity}Created,
            TriggeredById = _currentUser.UserId,
            CreatedDateTime = domainEvent.EventDateTime,
            ObjectId = domainEvent.Entity.Id.ToString(),
            Payload = domainEvent.Entity.AsJsonString(),
        }, cancellationToken);

        // 4. Single atomic save
        await _auditLogRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

### ✅ Event Type Constants

```csharp
// File: Constants/EventTypeConstants.cs
namespace ClassifiedAds.Modules.{ModuleName}.Constants;

internal class EventTypeConstants
{
    public const string {Entity}Created = "{ENTITY}_CREATED";
    public const string {Entity}Updated = "{ENTITY}_UPDATED";
    public const string {Entity}Deleted = "{ENTITY}_DELETED";
    public const string AuditLogEntryCreated = "AUDIT_LOG_ENTRY_CREATED";
}
```

---

## 7. Authorization & Permissions

### ✅ Permissions Definition

```csharp
// File: Authorization/Permissions.cs
namespace ClassifiedAds.Modules.{ModuleName}.Authorization;

public static class Permissions
{
    public const string Get{Entities} = "Permission:Get{Entities}";
    public const string Get{Entity} = "Permission:Get{Entity}";
    public const string Add{Entity} = "Permission:Add{Entity}";
    public const string Update{Entity} = "Permission:Update{Entity}";
    public const string Delete{Entity} = "Permission:Delete{Entity}";
    public const string Get{Entity}AuditLogs = "Permission:Get{Entity}AuditLogs";
}
```

### ✅ Permission Naming Convention

| Action | Permission Name |
|--------|-----------------|
| List all | `Permission:Get{Entities}` (plural) |
| Get single | `Permission:Get{Entity}` (singular) |
| Create | `Permission:Add{Entity}` |
| Update | `Permission:Update{Entity}` |
| Delete | `Permission:Delete{Entity}` |

---

## 8. DTO & Model Mapping

### ✅ Model (DTO) Definition

```csharp
// File: Models/{Entity}Model.cs
using System;

namespace ClassifiedAds.Modules.{ModuleName}.Models;

public class {Entity}Model
{
    public Guid Id { get; set; }
    public string Property1 { get; set; }
    public string Property2 { get; set; }
}
```

### ✅ Mapping Configuration

```csharp
// File: Models/{Entity}ModelMappingConfiguration.cs
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.{ModuleName}.Models;

public static class {Entity}ModelMappingConfiguration
{
    public static IEnumerable<{Entity}Model> ToModels(this IEnumerable<Entities.{Entity}> entities)
    {
        return entities.Select(x => x.ToModel());
    }

    public static {Entity}Model ToModel(this Entities.{Entity} entity)
    {
        if (entity == null)
        {
            return null;
        }

        return new {Entity}Model
        {
            Id = entity.Id,
            Property1 = entity.Property1,
            Property2 = entity.Property2,
        };
    }

    public static Entities.{Entity} ToEntity(this {Entity}Model model)
    {
        return new Entities.{Entity}
        {
            Id = model.Id,
            Property1 = model.Property1,
            Property2 = model.Property2,
        };
    }
}
```

### ❌ FORBIDDEN

- Using AutoMapper (manual mapping only)
- Exposing entities directly in API responses
- Complex logic in mapping methods

---

## 9. Dependency Injection Registration

### ✅ ServiceCollectionExtensions Pattern

```csharp
// File: ServiceCollectionExtensions.cs
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.{ModuleName}.ConfigurationOptions;
using ClassifiedAds.Modules.{ModuleName}.Entities;
using ClassifiedAds.Modules.{ModuleName}.HostedServices;
using ClassifiedAds.Modules.{ModuleName}.Persistence;
using ClassifiedAds.Modules.{ModuleName}.RateLimiterPolicies;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection Add{ModuleName}Module(this IServiceCollection services, Action<{ModuleName}ModuleOptions> configureOptions)
    {
        var settings = new {ModuleName}ModuleOptions();
        configureOptions(settings);

        services.Configure(configureOptions);

        // DbContext
        services.AddDbContext<{ModuleName}DbContext>(options => options.UseNpgsql(settings.ConnectionStrings.Default, sql =>
        {
            if (!string.IsNullOrEmpty(settings.ConnectionStrings.MigrationsAssembly))
            {
                sql.MigrationsAssembly(settings.ConnectionStrings.MigrationsAssembly);
            }
        }));

        // Repositories
        services
            .AddScoped<IRepository<{Entity}, Guid>, Repository<{Entity}, Guid>>()
            .AddScoped<I{Entity}Repository, {Entity}Repository>()
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>();

        // Message handlers (Commands, Queries, Event Handlers)
        services.AddMessageHandlers(Assembly.GetExecutingAssembly());

        // Authorization policies
        services.AddAuthorizationPolicies(Assembly.GetExecutingAssembly());

        // Rate limiting
        services.AddRateLimiter(options =>
        {
            options.AddPolicy<string, DefaultRateLimiterPolicy>(RateLimiterPolicyNames.DefaultPolicy);
        });

        return services;
    }

    public static IMvcBuilder Add{ModuleName}Module(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public static void Migrate{ModuleName}Db(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<{ModuleName}DbContext>().Database.Migrate();
    }

    public static void Migrate{ModuleName}Db(this IHost app)
    {
        using var serviceScope = app.Services.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<{ModuleName}DbContext>().Database.Migrate();
    }

    public static IServiceCollection AddHostedServices{ModuleName}Module(this IServiceCollection services)
    {
        services.AddMessageBusConsumers(Assembly.GetExecutingAssembly());
        services.AddOutboxMessagePublishers(Assembly.GetExecutingAssembly());
        services.AddHostedService<PublishEventWorker>();

        return services;
    }
}
```

---

## 10. Naming Conventions

### ✅ File Naming

| Type | Pattern | Example |
|------|---------|---------|
| Entity | `{EntityName}.cs` | `Product.cs` |
| Command | `{Action}{Entity}Command.cs` | `AddUpdateProductCommand.cs` |
| Query | `Get{Entity}Query.cs` | `GetProductQuery.cs` |
| Query (list) | `Get{Entities}Query.cs` | `GetProductsQuery.cs` |
| Controller | `{Entities}Controller.cs` | `ProductsController.cs` |
| Model | `{Entity}Model.cs` | `ProductModel.cs` |
| Repository Interface | `I{Entity}Repository.cs` | `IProductRepository.cs` |
| Repository | `{Entity}Repository.cs` | `ProductRepository.cs` |
| DbContext | `{ModuleName}DbContext.cs` | `ProductDbContext.cs` |
| Event Handler | `{Entity}{Event}EventHandler.cs` | `ProductCreatedEventHandler.cs` |

### ✅ Namespace Conventions

```csharp
// Entities
namespace ClassifiedAds.Modules.{ModuleName}.Entities;

// Commands
namespace ClassifiedAds.Modules.{ModuleName}.Commands;

// Queries
namespace ClassifiedAds.Modules.{ModuleName}.Queries;

// Controllers
namespace ClassifiedAds.Modules.{ModuleName}.Controllers;

// Models
namespace ClassifiedAds.Modules.{ModuleName}.Models;

// Persistence
namespace ClassifiedAds.Modules.{ModuleName}.Persistence;

// Event Handlers
namespace ClassifiedAds.Modules.{ModuleName}.EventHandlers;
```

### ✅ Variable Naming

| Type | Convention | Example |
|------|------------|---------|
| Private field | `_camelCase` | `_productRepository` |
| Parameter | `camelCase` | `productRepository` |
| Local variable | `camelCase` | `product` |
| Constant | `PascalCase` | `ProductCreated` |
| Property | `PascalCase` | `Product` |

---

## 11. Code Templates

### Quick Reference Checklist

When creating a new module, ensure you have:

- [ ] `Entities/{Entity}.cs`
- [ ] `Entities/OutboxMessage.cs`
- [ ] `Entities/AuditLogEntry.cs`
- [ ] `Commands/AddUpdate{Entity}Command.cs`
- [ ] `Commands/Delete{Entity}Command.cs`
- [ ] `Commands/PublishEventsCommand.cs`
- [ ] `Queries/Get{Entity}Query.cs`
- [ ] `Queries/Get{Entities}Query.cs`
- [ ] `Controllers/{Entities}Controller.cs`
- [ ] `Models/{Entity}Model.cs`
- [ ] `Models/{Entity}ModelMappingConfiguration.cs`
- [ ] `Persistence/{ModuleName}DbContext.cs`
- [ ] `Persistence/I{Entity}Repository.cs`
- [ ] `Persistence/{Entity}Repository.cs`
- [ ] `Persistence/Repository.cs`
- [ ] `DbConfigurations/{Entity}Configuration.cs`
- [ ] `DbConfigurations/OutboxMessageConfiguration.cs`
- [ ] `DbConfigurations/AuditLogEntryConfiguration.cs`
- [ ] `EventHandlers/{Entity}CreatedEventHandler.cs`
- [ ] `EventHandlers/{Entity}UpdatedEventHandler.cs`
- [ ] `EventHandlers/{Entity}DeletedEventHandler.cs`
- [ ] `Authorization/Permissions.cs`
- [ ] `Constants/EventTypeConstants.cs`
- [ ] `ConfigurationOptions/{ModuleName}ModuleOptions.cs`
- [ ] `ConfigurationOptions/ConnectionStringsOptions.cs`
- [ ] `HostedServices/PublishEventWorker.cs`
- [ ] `RateLimiterPolicies/DefaultRateLimiterPolicy.cs`
- [ ] `ServiceCollectionExtensions.cs`

---

## Summary: Do's and Don'ts

### ✅ DO

1. Follow the exact module structure
2. Use Dispatcher for all operations in controllers
3. Keep controllers thin
4. Use DTOs (Models) for API input/output
5. Use manual mapping (not AutoMapper)
6. Implement audit logging via event handlers
7. Use outbox pattern for cross-module communication
8. Add permission checks on every endpoint
9. Use rate limiting
10. Follow naming conventions exactly

### ❌ DON'T

1. Access repositories directly in controllers
2. Return entities from APIs
3. Add business logic in controllers
4. Cross-reference modules directly
5. Skip audit logging
6. Create custom folder structures
7. Use different naming patterns
8. Skip authorization attributes
9. Access other module's DbContext
10. Implement synchronous cross-module calls

---

> **Remember**: When in doubt, look at `ClassifiedAds.Modules.Product` as the reference implementation.
