# ClassifiedAds Modular Monolith - Comprehensive Project Guide

> **Purpose**: A single-source-of-truth document for developers and AI agents to understand, navigate, and extend the ClassifiedAds Modular Monolith codebase.
>
> **Target Audience**: Developers new to the codebase, AI coding agents implementing features.
>
> **Tech Stack**: .NET 10 / C# / PostgreSQL / RabbitMQ / EF Core

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [High-Level End-to-End Flow](#2-high-level-end-to-end-flow)
3. [Repository Layout & Key Directories](#3-repository-layout--key-directories)
4. [Architecture Deep Dive](#4-architecture-deep-dive)
5. [Configuration & Runtime](#5-configuration--runtime)
6. [Database & Persistence](#6-database--persistence)
7. [Migrations: Step-by-Step Guide](#7-migrations-step-by-step-guide)
8. [Testing Strategy & How-To](#8-testing-strategy--how-to)
9. [Add a Small Feature Example](#9-add-a-small-feature-example)
10. [Common Developer Workflows](#10-common-developer-workflows)
11. [Appendix: Index of Key Files](#11-appendix-index-of-key-files)

---

## 1. Executive Summary

### What This Project Is

ClassifiedAds Modular Monolith is a **.NET 10 template application** demonstrating how to build a well-structured modular monolith architecture. It serves as both a learning resource and a production-ready foundation for building enterprise applications.

### What Problem It Solves

The project addresses the challenge of building maintainable, scalable applications without the operational complexity of microservices. It provides:

- **Clear module boundaries** preventing spaghetti code
- **Single database** with logical separation via table naming conventions
- **Reliable event-driven communication** via the outbox pattern
- **Straightforward local development** with Docker Compose

### Key Architectural Patterns

| Pattern | Description |
|---------|-------------|
| **Modular Monolith** | Single deployment with well-defined module boundaries |
| **Single Database** | One PostgreSQL database with module-specific tables |
| **CQRS** | Command Query Responsibility Segregation via custom `Dispatcher` |
| **Clean Architecture** | Domain → Application → Infrastructure layering |
| **Outbox Pattern** | Reliable event publishing with transactional consistency |
| **Domain Events** | Decoupled side effects via `EntityCreatedEvent<T>`, etc. |
| **Repository Pattern** | `IRepository<T, TKey>` abstraction over EF Core |
| **Unit of Work** | Transaction management via `IUnitOfWork` |

### High-Level Module Overview

| Module | Purpose |
|--------|---------|
| **Product** | Sample business domain (product catalog) - reference implementation |
| **Identity** | User/Role management, ASP.NET Core Identity, external providers |
| **Storage** | File upload/download, blob storage abstraction |
| **Notification** | Email, SMS, Web push notifications |
| **AuditLog** | Centralized audit logging |
| **Configuration** | Application configuration entries |

### How Code Is Organized

```
ClassifiedAds.ModularMonolith/
├── Hosts/               → Entry points (WebAPI, Background, Migrator)
├── Core/                → Shared layers (Domain, Application, Infrastructure)
├── Modules/             → Vertical slices (Product, Identity, Storage, etc.)
├── Persistence/         → Database providers (PostgreSQL)
├── Contracts/           → Shared interfaces/DTOs between modules
└── CrossCuttingConcerns → Utilities (CSV, PDF, Excel, DateTime, etc.)
```

### Navigating the Codebase

1. **Start with**: [ClassifiedAds.WebAPI/Program.cs](ClassifiedAds.WebAPI/Program.cs) - composition root
2. **Reference module**: [ClassifiedAds.Modules.Product](ClassifiedAds.Modules.Product) - shows all patterns
3. **CQRS patterns**: [ClassifiedAds.Application](ClassifiedAds.Application) - Dispatcher, handlers
4. **Domain abstractions**: [ClassifiedAds.Domain](ClassifiedAds.Domain) - entities, events, interfaces

---

## 2. High-Level End-to-End Flow

### Request Lifecycle Overview

Every HTTP request follows this path:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           HTTP REQUEST LIFECYCLE                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Client                                                                      │
│    │                                                                         │
│    ▼                                                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                      ASP.NET Core Pipeline                           │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │    │
│  │  │  Exception   │→ │   Routing    │→ │     CORS     │              │    │
│  │  │   Handler    │  │              │  │              │              │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘              │    │
│  │         │                                    │                      │    │
│  │         ▼                                    ▼                      │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │    │
│  │  │Authentication│→ │Authorization │→ │ Rate Limiter │              │    │
│  │  │  (JWT)       │  │ (Permission) │  │              │              │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘              │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         Controller Action                            │    │
│  │  • Receives request                                                  │    │
│  │  • Maps to Command/Query                                             │    │
│  │  • Calls Dispatcher                                                  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                           Dispatcher                                 │    │
│  │  • Resolves handler from DI                                          │    │
│  │  • Invokes handler                                                   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     Command/Query Handler                            │    │
│  │  • Business logic execution                                          │    │
│  │  • Repository operations                                             │    │
│  │  • Domain event dispatch (for commands)                              │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                      Repository / DbContext                          │    │
│  │  • EF Core operations                                                │    │
│  │  • SaveChangesAsync()                                                │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     Domain Event Handlers                            │    │
│  │  • Audit logging                                                     │    │
│  │  • Outbox message creation                                           │    │
│  │  • Secondary side effects                                            │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│                             HTTP Response                                    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Logging and Correlation IDs

- **Structured Logging**: Serilog with `ILogger<T>` injection
- **Correlation IDs**: Automatically propagated via `Activity.Current?.Id`
- **Outbox Tracing**: `ActivityId` stored in outbox messages for distributed tracing

```csharp
// Correlation ID is automatically captured in outbox
outbox.ActivityId = System.Diagnostics.Activity.Current?.Id;
```

### Error Handling Strategy

| Error Type | HTTP Status | Handling |
|------------|-------------|----------|
| Validation errors | 400 Bad Request | Model binding / FluentValidation |
| Not found | 404 Not Found | `NotFoundException` thrown in handler |
| Authorization | 403 Forbidden | `[Authorize]` attribute |
| Server errors | 500 Internal Server Error | `GlobalExceptionHandler` |

**Global Exception Handler**: [ClassifiedAds.Infrastructure/Web/ExceptionHandlers/GlobalExceptionHandler.cs](ClassifiedAds.Infrastructure/Web/ExceptionHandlers/GlobalExceptionHandler.cs)

### Cross-Cutting Concerns Location

| Concern | Location |
|---------|----------|
| Middleware pipeline | [ClassifiedAds.WebAPI/Program.cs](ClassifiedAds.WebAPI/Program.cs) |
| Exception handling | [ClassifiedAds.Infrastructure/Web/ExceptionHandlers/](ClassifiedAds.Infrastructure/Web/ExceptionHandlers/) |
| Logging | [ClassifiedAds.Infrastructure/Logging/](ClassifiedAds.Infrastructure/Logging/) |
| Monitoring/Tracing | [ClassifiedAds.Infrastructure/Monitoring/](ClassifiedAds.Infrastructure/Monitoring/) |
| Caching | [ClassifiedAds.Infrastructure/Caching/](ClassifiedAds.Infrastructure/Caching/) |
| Rate limiting | Per-module in `RateLimiterPolicies/` folders |

---

## 3. Repository Layout & Key Directories

### Solution Structure

```
D:\GSP26SE43.ModularMonolith\
│
├── ClassifiedAds.ModularMonolith.slnx    # Solution file
├── docker-compose.yml                     # Local development environment
├── .env                                   # Environment variables for Docker
├── global.json                            # .NET SDK version pinning
│
├── rules/                                 # ⚠️ STRICT RULES - MUST READ
│   ├── 00-priority.md                     # Rule priority order
│   ├── security.md                        # Security requirements (HIGHEST)
│   ├── architecture.md                    # Architecture rules
│   ├── testing.md                         # Testing requirements
│   ├── coding.md                          # C# coding standards
│   └── git-workflow.md                    # Git conventions
│
├── docs-architecture/                     # Architecture documentation
│   ├── 01-solution-structure.md
│   ├── 02-architecture-overview.md
│   ├── 03-request-lifecycle.md
│   ├── 04-cqrs-and-mediator.md
│   ├── 05-persistence-and-transactions.md
│   ├── 06-events-and-outbox.md
│   ├── 07-modules.md
│   ├── 08-authentication-authorization.md
│   ├── 09-observability-and-crosscutting.md
│   ├── 10-devops-and-local-development.md
│   └── 11-extension-playbook.md
│
├── ClassifiedAds.WebAPI/                  # Main HTTP API host
├── ClassifiedAds.Background/              # Background worker service
├── ClassifiedAds.Migrator/                # Database migration runner
│
├── ClassifiedAds.Application/             # CQRS infrastructure
├── ClassifiedAds.Domain/                  # Domain layer
├── ClassifiedAds.Infrastructure/          # External integrations
├── ClassifiedAds.Contracts/               # Shared interfaces
├── ClassifiedAds.CrossCuttingConcerns/    # Utilities
├── ClassifiedAds.Persistence.PostgreSQL/  # EF Core PostgreSQL
│
├── ClassifiedAds.Modules.AuditLog/
├── ClassifiedAds.Modules.Configuration/
├── ClassifiedAds.Modules.Identity/
├── ClassifiedAds.Modules.Notification/
├── ClassifiedAds.Modules.Product/         # ⭐ Reference module
└── ClassifiedAds.Modules.Storage/
```

### Module Structure Convention (Product as Example)

```
ClassifiedAds.Modules.Product/
├── Authorization/
│   └── Permissions.cs                    # Permission constants
├── Commands/
│   ├── AddUpdateProductCommand.cs        # Create/Update handler
│   ├── DeleteProductCommand.cs           # Delete handler
│   └── PublishEventsCommand.cs           # Outbox publisher
├── ConfigurationOptions/
│   └── ProductModuleOptions.cs           # Module settings
├── Constants/
│   └── EventTypeConstants.cs             # Event type strings
├── Controllers/
│   └── ProductsController.cs             # API endpoints
├── Csv/                                   # CSV import/export
├── DbConfigurations/
│   ├── ProductConfiguration.cs           # EF entity config
│   ├── AuditLogEntryConfiguration.cs
│   └── OutboxMessageConfiguration.cs
├── Entities/
│   ├── Product.cs                        # Main domain entity
│   ├── AuditLogEntry.cs                  # Local audit log
│   └── OutboxMessage.cs                  # Outbox for events
├── EventHandlers/
│   ├── ProductCreatedEventHandler.cs     # Handles EntityCreatedEvent
│   ├── ProductUpdatedEventHandler.cs     # Handles EntityUpdatedEvent
│   └── ProductDeletedEventHandler.cs     # Handles EntityDeletedEvent
├── HostedServices/
│   └── PublishEventWorker.cs             # Background outbox publisher
├── Html/                                  # HTML export
├── Models/
│   └── ProductModel.cs                   # API DTOs
├── OutBoxEventPublishers/                 # Message bus publishers
├── Pdf/                                   # PDF export
├── Persistence/
│   ├── ProductDbContext.cs               # Module's DbContext
│   ├── IProductRepository.cs             # Repository interface
│   └── ProductRepository.cs              # Repository implementation
├── Queries/
│   ├── GetProductQuery.cs                # Get single product
│   ├── GetProductsQuery.cs               # Get all products
│   └── GetAuditEntriesQuery.cs           # Get audit logs
├── RateLimiterPolicies/
│   └── DefaultRateLimiterPolicy.cs       # Rate limiting config
├── ServiceCollectionExtensions.cs         # DI registration entry point
└── ClassifiedAds.Modules.Product.csproj
```

### Key Files Quick Reference

| File | Purpose |
|------|---------|
| `ServiceCollectionExtensions.cs` | DI registration, DbContext setup, module bootstrap |
| `*DbContext.cs` | EF Core DbContext for the module |
| `Permissions.cs` | Authorization permission constants |
| `*Controller.cs` | API endpoints |
| `*Command.cs` | Write operations (CQRS commands) |
| `*Query.cs` | Read operations (CQRS queries) |
| `*EventHandler.cs` | Domain event handlers |

---

## 4. Architecture Deep Dive

### 4.1 Modules and Their Boundaries

#### Module List

Each module has its own DbContext. In **single database** mode, all DbContexts point to the same database.

| Module | DbContext | Tables | Has Outbox |
|--------|-----------|--------|------------|
| **Product** | `ProductDbContext` | Products, AuditLogEntries, OutboxMessages | ✅ |
| **Identity** | `IdentityDbContext` | Users, Roles, UserClaims, DataProtectionKeys | ❌ |
| **Storage** | `StorageDbContext` | FileEntries, OutboxMessages | ✅ |
| **Notification** | `NotificationDbContext` | EmailMessages, SmsMessages | ❌ |
| **AuditLog** | `AuditLogDbContext` | AuditLogEntries | ❌ |
| **Configuration** | `ConfigurationDbContext` | ConfigurationEntries | ❌ |

**DbContext Files**:
- [ClassifiedAds.Modules.Product/Persistence/ProductDbContext.cs](ClassifiedAds.Modules.Product/Persistence/ProductDbContext.cs)
- [ClassifiedAds.Modules.Identity/Persistence/IdentityDbContext.cs](ClassifiedAds.Modules.Identity/Persistence/IdentityDbContext.cs)
- [ClassifiedAds.Modules.Storage/Persistence/StorageDbContext.cs](ClassifiedAds.Modules.Storage/Persistence/StorageDbContext.cs)
- [ClassifiedAds.Modules.Notification/Persistence/NotificationDbContext.cs](ClassifiedAds.Modules.Notification/Persistence/NotificationDbContext.cs)
- [ClassifiedAds.Modules.AuditLog/Persistence/AuditLogDbContext.cs](ClassifiedAds.Modules.AuditLog/Persistence/AuditLogDbContext.cs)
- [ClassifiedAds.Modules.Configuration/Persistence/ConfigurationDbContext.cs](ClassifiedAds.Modules.Configuration/Persistence/ConfigurationDbContext.cs)

#### Forbidden Dependencies (from rules/architecture.md)

- **[ARCH-004]** Modules MUST NOT reference other module's internal types
- **[ARCH-007]** MUST NOT call another module's repository or DbContext directly
- Cross-module communication: Use `ClassifiedAds.Contracts` interfaces or domain events

### 4.2 Layer Responsibilities

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PRESENTATION LAYER                                   │
│  • Controllers, Minimal APIs                                                 │
│  • Request/Response handling                                                 │
│  • Model binding, validation                                                 │
│  • Authorization attributes                                                  │
│  MUST NOT: Contain business logic, direct DB access                         │
├─────────────────────────────────────────────────────────────────────────────┤
│                         APPLICATION LAYER                                    │
│  • Commands, Queries, Handlers                                               │
│  • Dispatcher (CQRS routing)                                                 │
│  • CrudService (generic CRUD with events)                                    │
│  • Business logic orchestration                                              │
│  MUST NOT: Have UI concerns, direct EF Core usage (use Repository)          │
├─────────────────────────────────────────────────────────────────────────────┤
│                           DOMAIN LAYER                                       │
│  • Entities (Entity<T>, IAggregateRoot)                                      │
│  • Domain Events (IDomainEvent, IDomainEventHandler)                         │
│  • Repository Interfaces (IRepository, IUnitOfWork)                          │
│  • Value Objects                                                             │
│  MUST NOT: Have any external dependencies                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                       INFRASTRUCTURE LAYER                                   │
│  • EF Core DbContext implementations                                         │
│  • Repository implementations                                                │
│  • Messaging (RabbitMQ, Kafka, Azure Service Bus)                            │
│  • Caching (Redis, InMemory)                                                 │
│  • External service integrations                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 4.3 CQRS Implementation

This project uses a **custom Dispatcher** (NOT MediatR).

#### Command (Write Operations)

```csharp
// Definition
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

    public async Task HandleAsync(AddUpdateProductCommand command, CancellationToken ct = default)
    {
        await _productService.AddOrUpdateAsync(command.Product, ct);
    }
}
```

#### Query (Read Operations)

```csharp
// Definition
public class GetProductQuery : IQuery<Product>
{
    public Guid Id { get; set; }
    public bool ThrowNotFoundIfNull { get; set; }
}

// Handler
public class GetProductQueryHandler : IQueryHandler<GetProductQuery, Product>
{
    private readonly IProductRepository _repository;

    public async Task<Product> HandleAsync(GetProductQuery query, CancellationToken ct = default)
    {
        var product = await _repository.FirstOrDefaultAsync(
            _repository.GetQueryableSet().Where(x => x.Id == query.Id));
        
        if (query.ThrowNotFoundIfNull && product == null)
            throw new NotFoundException($"Product {query.Id} not found.");
        
        return product;
    }
}
```

#### Dispatcher Usage

```csharp
// In Controller
[HttpGet("{id}")]
public async Task<ActionResult<Product>> Get(Guid id)
{
    var product = await _dispatcher.DispatchAsync(new GetProductQuery 
    { 
        Id = id, 
        ThrowNotFoundIfNull = true 
    });
    return Ok(product.ToModel());
}
```

### 4.4 Domain Events & Outbox Pattern

#### Event Flow

```
CrudService.AddOrUpdateAsync()
    │
    ├── Repository.AddAsync(entity)
    ├── UnitOfWork.SaveChangesAsync()      ← Entity saved
    │
    └── Dispatcher.DispatchAsync(EntityCreatedEvent<T>)
            │
            └── EventHandler.HandleAsync()
                    │
                    ├── AuditLogRepository.Add()   ← Audit log
                    ├── OutboxRepository.Add()     ← Outbox message
                    └── SaveChangesAsync()
```

#### Outbox Message Structure

```csharp
public class OutboxMessage : Entity<Guid>, IAggregateRoot
{
    public string EventType { get; set; }      // e.g., "ProductCreated"
    public Guid TriggeredById { get; set; }    // User who triggered
    public string ObjectId { get; set; }       // Related entity ID
    public string Payload { get; set; }        // JSON serialized data
    public bool Published { get; set; }        // Has been sent to message bus
    public string ActivityId { get; set; }     // Distributed tracing correlation
}
```

#### Background Publishing

`PublishEventWorker` (BackgroundService) polls the outbox and publishes to message bus:

```csharp
// Simplified flow
while (!cancellationToken.IsCancellationRequested)
{
    var events = _outboxRepository.GetQueryableSet()
        .Where(x => !x.Published)
        .Take(50)
        .ToList();

    foreach (var eventLog in events)
    {
        await _messageBus.SendAsync(new PublishingOutboxMessage { ... });
        eventLog.Published = true;
        await _outboxRepository.UnitOfWork.SaveChangesAsync();
    }

    await Task.Delay(10000);  // Poll every 10 seconds
}
```

### 4.5 Inter-Module Communication

#### Synchronous (via Contracts)

```csharp
// ClassifiedAds.Contracts/Identity/Services/ICurrentUser.cs
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
}

// Used in any module
public class ProductCreatedEventHandler
{
    private readonly ICurrentUser _currentUser;  // Injected from Contracts

    public async Task HandleAsync(EntityCreatedEvent<Product> domainEvent)
    {
        var userId = _currentUser.UserId;  // Cross-module access via contract
    }
}
```

#### Asynchronous (via Events + Outbox)

- Events published to outbox → Background worker → Message bus
- Other modules can subscribe to message bus topics

---

## 5. Configuration & Runtime

### 5.1 Application Startup

**Composition Root**: [ClassifiedAds.WebAPI/Program.cs](ClassifiedAds.WebAPI/Program.cs)

```csharp
// 1. Module Registration
services
    .AddAuditLogModule(opt => configuration.GetSection("Modules:AuditLog").Bind(opt))
    .AddConfigurationModule(opt => configuration.GetSection("Modules:Configuration").Bind(opt))
    .AddIdentityModuleCore(opt => configuration.GetSection("Modules:Identity").Bind(opt))
    .AddNotificationModule(opt => configuration.GetSection("Modules:Notification").Bind(opt))
    .AddProductModule(opt => configuration.GetSection("Modules:Product").Bind(opt))
    .AddStorageModule(opt => configuration.GetSection("Modules:Storage").Bind(opt))
    .AddApplicationServices();

// 2. MVC Controllers from Modules
services.AddControllers()
    .AddAuditLogModule()
    .AddProductModule()
    // ... etc

// 3. Middleware Pipeline
app.UseExceptionHandler(options => { });
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
```

### 5.2 Environment Configuration

#### Configuration Hierarchy (priority order)

1. `appsettings.json` (base)
2. `appsettings.{Environment}.json` (environment-specific)
3. Environment variables
4. User secrets (development only)

#### Current Configuration Structure (Database-per-Module)

> **IMPORTANT**: The codebase currently uses **database-per-module** configuration. Each module has its own connection string pointing to a separate database. This section documents BOTH the current state AND the recommended single-database approach.

**Current State** - Found in [ClassifiedAds.WebAPI/appsettings.json](ClassifiedAds.WebAPI/appsettings.json):

```json
{
  "Modules": {
    "Product": {
      "ConnectionStrings": {
        "Default": "Host=127.0.0.1;Port=5432;Database=ClassifiedAds_Product;Username=postgres;Password=<YOUR_PASSWORD>"
      }
    },
    "Identity": {
      "ConnectionStrings": {
        "Default": "Host=127.0.0.1;Port=5432;Database=ClassifiedAds_Identity;Username=postgres;Password=<YOUR_PASSWORD>"
      }
    },
    "AuditLog": {
      "ConnectionStrings": {
        "Default": "Host=127.0.0.1;Port=5432;Database=ClassifiedAds_AuditLog;Username=postgres;Password=<YOUR_PASSWORD>"
      }
    }
    // ... etc for each module
  }
}
```

#### Target State: Single Database Configuration

For a **single database** approach, configure all modules to use the same connection string:

```json
{
  "ConnectionStrings": {
    "Default": "Host=127.0.0.1;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
  },
  "Modules": {
    "Product": {
      "ConnectionStrings": {
        "Default": "${ConnectionStrings:Default}",
        "MigrationsAssembly": "ClassifiedAds.Migrator"
      }
    },
    "Identity": {
      "ConnectionStrings": {
        "Default": "${ConnectionStrings:Default}",
        "MigrationsAssembly": "ClassifiedAds.Migrator"
      }
    },
    "AuditLog": {
      "ConnectionStrings": {
        "Default": "${ConnectionStrings:Default}",
        "MigrationsAssembly": "ClassifiedAds.Migrator"
      }
    },
    "Configuration": {
      "ConnectionStrings": {
        "Default": "${ConnectionStrings:Default}",
        "MigrationsAssembly": "ClassifiedAds.Migrator"
      }
    },
    "Notification": {
      "ConnectionStrings": {
        "Default": "${ConnectionStrings:Default}",
        "MigrationsAssembly": "ClassifiedAds.Migrator"
      }
    },
    "Storage": {
      "ConnectionStrings": {
        "Default": "${ConnectionStrings:Default}",
        "MigrationsAssembly": "ClassifiedAds.Migrator"
      }
    }
  }
}
```

Or simply point all module connection strings to the same database name:

```json
{
  "Modules": {
    "Product": {
      "ConnectionStrings": {
        "Default": "Host=127.0.0.1;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
      }
    },
    "Identity": {
      "ConnectionStrings": {
        "Default": "Host=127.0.0.1;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
      }
    }
    // ... all modules use Database=ClassifiedAds
  }
}
```

### 5.3 Secrets Strategy

| Environment | Storage |
|-------------|---------|
| Development | User Secrets (`dotnet user-secrets`) |
| Docker | `.env` file + environment variables |
| Production | Environment variables / Key Vault |

**From rules/security.md**:
- **[SEC-001]** Secrets MUST NOT be hardcoded in source code
- **[SEC-003]** Use `dotnet user-secrets` for development

```powershell
# Initialize user secrets
dotnet user-secrets init --project ClassifiedAds.WebAPI

# Set a secret
dotnet user-secrets set "Modules:Identity:Providers:Auth0:ClientSecret" "secret-value"
```

### 5.4 Module Registration Pattern

Each module provides a `ServiceCollectionExtensions.cs`.

**File**: [ClassifiedAds.Modules.Product/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Product/ServiceCollectionExtensions.cs)

```csharp
public static IServiceCollection AddProductModule(
    this IServiceCollection services, 
    Action<ProductModuleOptions> configureOptions)
{
    var settings = new ProductModuleOptions();
    configureOptions(settings);

    // Bind options to DI
    services.Configure(configureOptions);

    // Register DbContext - connects to the database specified in settings
    services.AddDbContext<ProductDbContext>(options => 
        options.UseNpgsql(settings.ConnectionStrings.Default, sql =>
        {
            if (!string.IsNullOrEmpty(settings.ConnectionStrings.MigrationsAssembly))
            {
                sql.MigrationsAssembly(settings.ConnectionStrings.MigrationsAssembly);
            }
        }));

    // Register Repositories
    services.AddScoped<IRepository<Product, Guid>, Repository<Product, Guid>>();
    services.AddScoped<IProductRepository, ProductRepository>();

    // Auto-register handlers (commands, queries, events)
    services.AddMessageHandlers(Assembly.GetExecutingAssembly());

    // Authorization policies
    services.AddAuthorizationPolicies(Assembly.GetExecutingAssembly());

    return services;
}
```

---

## 6. Database & Persistence

### 6.1 Database Strategy Overview

This project uses **multiple DbContexts** that all connect to a **single PostgreSQL database** with **schema-per-module isolation**. Each module has its own schema within the shared database.

### 6.2 Current State: Single Database with Schema Isolation

The repository is configured with a **single shared database** (`ClassifiedAds`) where each module's tables reside in their own PostgreSQL schema.

**Connection String Configuration** (in appsettings.json files):
```json
{
  "ConnectionStrings": {
    "Default": "Host=127.0.0.1;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
  }
}
```

| Module | Schema Name | DbContext |
|--------|-------------|-----------|
| Product | `product` | `ProductDbContext` |
| Identity | `identity` | `IdentityDbContext` |
| Storage | `storage` | `StorageDbContext` |
| Notification | `notification` | `NotificationDbContext` |
| AuditLog | `auditlog` | `AuditLogDbContext` |
| Configuration | `configuration` | `ConfigurationDbContext` |

### 6.3 Benefits of Schema-per-Module

1. **Simplified Operations**: Single database to backup, restore, monitor
2. **Reduced Complexity**: Single connection string across all modules
3. **Better Resource Usage**: Single connection pool shared across modules
4. **Maintained Isolation**: Schemas provide logical separation between modules
5. **Easier Cross-Module Queries**: If needed in the future (via qualified table names like `product."Products"`)

### 6.4 Table Names per Schema

| Schema | Tables |
|--------|--------|
| `product` | `Products`, `AuditLogEntries`, `OutboxMessages`, `ArchivedOutboxMessages` |
| `identity` | `Users`, `Roles`, `UserClaims`, `UserRoles`, `UserLogins`, `UserTokens`, `RoleClaims`, `DataProtectionKeys` |
| `storage` | `FileEntries`, `DeletedFileEntries`, `AuditLogEntries`, `OutboxMessages`, `ArchivedOutboxMessages` |
| `notification` | `EmailMessages`, `ArchivedEmailMessages`, `SmsMessages`, `EmailMessageAttachments` |
| `auditlog` | `AuditLogEntries`, `IdempotentRequests` |
| `configuration` | `ConfigurationEntries`, `LocalizationEntries` |

### 6.5 Entity Base Class

**File**: [ClassifiedAds.Domain/Entities/Entity.cs](ClassifiedAds.Domain/Entities/Entity.cs)

```csharp
public abstract class Entity<TKey> : IHasKey<TKey>, ITrackable
{
    public TKey Id { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }  // Optimistic concurrency

    public DateTimeOffset CreatedDateTime { get; set; }
    public DateTimeOffset? UpdatedDateTime { get; set; }
}
```

### 6.5 DbContext Pattern

**File**: [ClassifiedAds.Modules.Product/Persistence/ProductDbContext.cs](ClassifiedAds.Modules.Product/Persistence/ProductDbContext.cs)

```csharp
// ClassifiedAds.Modules.Product/Persistence/ProductDbContext.cs
public class ProductDbContext : DbContextUnitOfWork<ProductDbContext>
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("product");  // Schema isolation
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SetOutboxActivityId();  // For distributed tracing
        return await base.SaveChangesAsync(ct);
    }
}
```

### 6.6 Repository Pattern

**Interface**: [ClassifiedAds.Domain/Repositories/IRepository.cs](ClassifiedAds.Domain/Repositories/IRepository.cs)

```csharp
public interface IRepository<TEntity, TKey> : IConcurrencyHandler<TEntity>
    where TEntity : Entity<TKey>, IAggregateRoot
{
    IUnitOfWork UnitOfWork { get; }
    IQueryable<TEntity> GetQueryableSet();
    Task AddOrUpdateAsync(TEntity entity, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    void Delete(TEntity entity);
    Task<T> FirstOrDefaultAsync<T>(IQueryable<T> query);
    Task<List<T>> ToListAsync<T>(IQueryable<T> query);
    // Bulk operations
    Task BulkInsertAsync(IReadOnlyCollection<TEntity> entities, CancellationToken ct = default);
    Task BulkUpdateAsync(IReadOnlyCollection<TEntity> entities, Expression<Func<TEntity, object>> columnNamesSelector, CancellationToken ct = default);
    Task BulkDeleteAsync(IReadOnlyCollection<TEntity> entities, CancellationToken ct = default);
}
```

**Implementation**: [ClassifiedAds.Persistence.PostgreSQL/DbContextRepository.cs](ClassifiedAds.Persistence.PostgreSQL/DbContextRepository.cs)

```csharp
public class DbContextRepository<TDbContext, TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : Entity<TKey>, IAggregateRoot
    where TDbContext : DbContext, IUnitOfWork
{
    private readonly TDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    protected DbSet<TEntity> DbSet => _dbContext.Set<TEntity>();

    public IUnitOfWork UnitOfWork => _dbContext;

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        entity.CreatedDateTime = _dateTimeProvider.OffsetNow;
        await DbSet.AddAsync(entity, ct);
    }

    public Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        entity.UpdatedDateTime = _dateTimeProvider.OffsetNow;
        return Task.CompletedTask;
    }
}
```

### 6.7 Transaction Management

**Interface**: [ClassifiedAds.Domain/Repositories/IUnitOfWork.cs](ClassifiedAds.Domain/Repositories/IUnitOfWork.cs)

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDisposable> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
}
```

#### Transaction Boundaries in Single Database

With a **single database**, cross-module transactions are possible but **strongly discouraged** per architecture rules:

```csharp
// WITHIN a module (ALLOWED) - Single DbContext transaction
await _productRepository.AddAsync(product);
await _auditLogRepository.AddAsync(auditEntry);  // Same module's audit log
await _productRepository.UnitOfWork.SaveChangesAsync();  // Atomic

// CROSS-MODULE (NOT RECOMMENDED) - Different DbContexts
// Even with single DB, each DbContext has its own transaction scope
// Use domain events + outbox pattern for cross-module consistency instead
```

**Eventual Consistency Pattern (Recommended)**:
```
1. Save entity to module's DbContext
2. Save OutboxMessage to same DbContext (atomic)
3. Background worker publishes outbox → Message Bus
4. Other modules consume messages and update their data
```

### 6.8 Key Persistence Files

| File | Purpose |
|------|---------|
| [ClassifiedAds.Persistence.PostgreSQL/DbContextUnitOfWork.cs](ClassifiedAds.Persistence.PostgreSQL/DbContextUnitOfWork.cs) | Base DbContext with IUnitOfWork |
| [ClassifiedAds.Persistence.PostgreSQL/DbContextRepository.cs](ClassifiedAds.Persistence.PostgreSQL/DbContextRepository.cs) | Generic repository implementation |
| [ClassifiedAds.Domain/Repositories/IRepository.cs](ClassifiedAds.Domain/Repositories/IRepository.cs) | Repository interface |
| [ClassifiedAds.Domain/Repositories/IUnitOfWork.cs](ClassifiedAds.Domain/Repositories/IUnitOfWork.cs) | Unit of work interface |

---

## 7. Migrations: Step-by-Step Guide

### 7.1 Migration Architecture

The project uses **EF Core Code-First migrations** with:
- **Startup Project**: `ClassifiedAds.Migrator` (contains migration host)
- **Migration Assembly**: Migrations are stored in `ClassifiedAds.Migrator` project
- **DbContexts**: Each module has its own DbContext

### 7.2 Current vs Single Database Migrations

#### Current State (Database-per-Module)
Each DbContext migrates to its own database. The Migrator calls each module's migration method.

#### Single Database Migration Strategy
When using a single database, all DbContexts create their tables in the **same database**. Key considerations:

1. **No table name collisions**: Each module uses unique table names
2. **Ordered migrations**: Run migrations in dependency order
3. **Same connection string**: All modules point to `Database=ClassifiedAds`

### 7.3 Prerequisites

```powershell
# Install EF Core tools (one-time)
dotnet tool install --global dotnet-ef --version 10.0.0

# Verify installation
dotnet ef --version
```

### 7.4 Creating a Migration

```powershell
# Navigate to solution root
cd D:\GSP26SE43.ModularMonolith

# Create migration for Product module
dotnet ef migrations add AddProductDescription `
    --context ProductDbContext `
    --project ClassifiedAds.Modules.Product `
    --startup-project ClassifiedAds.Migrator `
    -o Migrations/ProductDb

# Create migration for Identity module
dotnet ef migrations add AddUserProfile `
    --context IdentityDbContext `
    --project ClassifiedAds.Modules.Identity `
    --startup-project ClassifiedAds.Migrator `
    -o Migrations/IdentityDb
```

### 7.5 Applying Migrations

#### Option 1: Using the Migrator Project (Recommended)

**File**: [ClassifiedAds.Migrator/Program.cs](ClassifiedAds.Migrator/Program.cs)

```powershell
# Apply all pending migrations for all modules
dotnet run --project ClassifiedAds.Migrator
```

The Migrator runs each module's migration with retry logic:

```csharp
Policy.Handle<Exception>().WaitAndRetry([
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(20),
    TimeSpan.FromSeconds(30),
])
.Execute(() =>
{
    app.MigrateAuditLogDb();
    app.MigrateConfigurationDb();
    app.MigrateIdentityDb();
    app.MigrateNotificationDb();
    app.MigrateProductDb();
    app.MigrateStorageDb();
});
```

Each `MigrateXxxDb()` method calls `Database.Migrate()` on the respective DbContext:

```csharp
// From ServiceCollectionExtensions.cs
public static void MigrateProductDb(this IHost app)
{
    using var serviceScope = app.Services.CreateScope();
    serviceScope.ServiceProvider.GetRequiredService<ProductDbContext>().Database.Migrate();
}
```

#### Option 2: Using EF CLI

```powershell
dotnet ef database update `
    --context ProductDbContext `
    --project ClassifiedAds.Migrator
```

### 7.6 Single Database Migration Workflow

When all modules share a single database:

1. **Ensure unique table names** in each module's `DbConfigurations/`
2. **Update connection strings** to use the same database
3. **Run Migrator** - all tables created in one database

```powershell
# For single database, ensure .env or appsettings has:
# Modules__Product__ConnectionStrings__Default="Host=db;Database=ClassifiedAds;..."
# Modules__Identity__ConnectionStrings__Default="Host=db;Database=ClassifiedAds;..."
# (all modules point to same Database=ClassifiedAds)

dotnet run --project ClassifiedAds.Migrator
```

### 7.7 Migration in Different Environments

| Environment | How Migrations Run |
|-------------|-------------------|
| Development | `dotnet run --project ClassifiedAds.Migrator` |
| Docker | `migrator` service runs before `webapi` (depends_on) |
| CI/CD | Run Migrator as a job step before deployment |
| Production | Run Migrator container/job before API deployment |

### 7.8 Common Migration Pitfalls

1. **Wrong startup project**: Always use `--startup-project ClassifiedAds.Migrator`
2. **Missing MigrationsAssembly**: Ensure options include:
   ```csharp
   opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
   ```
3. **Multiple DbContexts with same name**: Each module MUST have uniquely named DbContext
4. **Pending migrations in PR**: Always run migrations locally before pushing
5. **Single DB table conflicts**: When using single database, ensure table names are unique across modules

### 7.9 Reverting Migrations

```powershell
# Revert to specific migration
dotnet ef database update PreviousMigrationName `
    --context ProductDbContext `
    --project ClassifiedAds.Migrator

# Remove last migration (if not applied)
dotnet ef migrations remove `
    --context ProductDbContext `
    --project ClassifiedAds.Modules.Product `
    --startup-project ClassifiedAds.Migrator
```

---

## 8. Testing Strategy & How-To

### 8.1 Test Project Status

> **NOTE**: As of this writing, test projects are **not present** in the current repository. The patterns below are documented per [rules/testing.md](rules/testing.md) for implementation guidance.

Expected test project structure per module:

```
ClassifiedAds.Modules.Product.UnitTests/          # Unit tests
ClassifiedAds.Modules.Product.IntegrationTests/   # Integration tests
```

### 8.2 Test Frameworks (from rules/testing.md)

| Purpose | Library |
|---------|---------|
| Test Framework | xUnit |
| Assertions | FluentAssertions |
| Mocking | Moq or NSubstitute |
| API Testing | Microsoft.AspNetCore.Mvc.Testing |
| Test Containers | Testcontainers (recommended for integration) |

### 8.3 Test Naming Convention

**From rules/testing.md [TEST-010]**:

```
{MethodUnderTest}_Should{ExpectedBehavior}_When{Condition}
```

Examples:
```csharp
GetProduct_ShouldReturnProduct_WhenProductExists()
GetProduct_ShouldThrowNotFoundException_WhenProductDoesNotExist()
CreateProduct_ShouldReturnCreated_WhenModelIsValid()
```

### 8.4 Unit Test Example

```csharp
public class GetProductQueryHandlerTests
{
    private readonly Mock<IProductRepository> _repositoryMock;
    private readonly GetProductQueryHandler _handler;

    public GetProductQueryHandlerTests()
    {
        _repositoryMock = new Mock<IProductRepository>();
        _handler = new GetProductQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnProduct_WhenProductExists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var expectedProduct = new Product 
        { 
            Id = productId, 
            Code = "P001", 
            Name = "Test Product" 
        };
        
        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<IQueryable<Product>>()))
            .ReturnsAsync(expectedProduct);

        var query = new GetProductQuery { Id = productId, ThrowNotFoundIfNull = false };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(productId);
        result.Code.Should().Be("P001");
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowNotFoundException_WhenProductNotFoundAndFlagSet()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<IQueryable<Product>>()))
            .ReturnsAsync((Product)null);

        var query = new GetProductQuery { Id = productId, ThrowNotFoundIfNull = true };

        // Act
        Func<Task> act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

### 8.5 Integration Test Database Strategy (Single Database)

**From rules/testing.md [TEST-070]**:

For **single database** testing, use one of these approaches:

#### Option A: Testcontainers PostgreSQL (Recommended)

```csharp
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("ClassifiedAds_Test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Run migrations on single test database
        // All module tables created in this one database
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

#### Option B: Shared Local Database

```csharp
// Use a dedicated test database
// Connection: "Host=localhost;Database=ClassifiedAds_Test;..."
// Clean up between tests using Respawn or transaction rollback
```

### 8.6 Integration Test Example

```csharp
public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_ShouldReturnOk_WhenProductsExist()
    {
        // Arrange (seed data if needed)

        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductModel>>();
        products.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_ShouldReturnCreated_WhenModelIsValid()
    {
        // Arrange
        var model = new ProductModel 
        { 
            Code = "NEW001", 
            Name = "New Product", 
            Description = "Test" 
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }
}
```

### 8.7 Custom WebApplicationFactory (Single Database)

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Single test database connection string
    private const string TestConnectionString = 
        "Host=localhost;Port=5432;Database=ClassifiedAds_Test;Username=postgres;Password=<YOUR_PASSWORD>";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all real DbContext registrations
            RemoveDbContextRegistrations<ProductDbContext>(services);
            RemoveDbContextRegistrations<IdentityDbContext>(services);
            RemoveDbContextRegistrations<StorageDbContext>(services);
            // ... remove other DbContexts

            // Re-register all DbContexts pointing to SINGLE test database
            services.AddDbContext<ProductDbContext>(options =>
                options.UseNpgsql(TestConnectionString));
            services.AddDbContext<IdentityDbContext>(options =>
                options.UseNpgsql(TestConnectionString));
            services.AddDbContext<StorageDbContext>(options =>
                options.UseNpgsql(TestConnectionString));
            // ... add other DbContexts with same connection string

            // Configure test authentication
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            // Create/migrate test database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            
            // Migrate all modules to the single test database
            scope.ServiceProvider.GetRequiredService<ProductDbContext>().Database.Migrate();
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.Migrate();
            // ... migrate other contexts
        });
    }

    private static void RemoveDbContextRegistrations<TContext>(IServiceCollection services) 
        where TContext : DbContext
    {
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(DbContextOptions<TContext>));
        if (descriptor != null)
            services.Remove(descriptor);
    }
}
```

### 8.8 Database Cleanup Strategies

| Strategy | Pros | Cons |
|----------|------|------|
| **Respawn** | Fast, reliable cleanup | Requires package |
| **Transaction Rollback** | Very fast | Complex with multiple DbContexts |
| **Recreate Database** | Clean slate | Slow |
| **Truncate Tables** | Fast | May have FK issues |

```csharp
// Respawn example
private readonly Respawner _respawner;

public async Task ResetDatabaseAsync()
{
    await _respawner.ResetAsync(TestConnectionString);
}
```

### 8.9 Running Tests

```powershell
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Run with filter
dotnet test --filter "FullyQualifiedName~GetProduct"
```

### 8.10 Coverage Requirements

**From rules/testing.md**:
- **[TEST-040]** Minimum: 80% line coverage, 80% branch coverage
- **[TEST-041]** Command handlers: 100% coverage
- **[TEST-042]** Query handlers: 100% coverage

---

## 9. Add a Small Feature Example

### Scenario: Add a "Category" Property to Product

We'll add a `Category` field to the Product entity, demonstrating the full flow.

### Step 1: Modify the Entity

```csharp
// ClassifiedAds.Modules.Product/Entities/Product.cs
public class Product : Entity<Guid>, IAggregateRoot
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }  // NEW
}
```

### Step 2: Update EF Configuration

```csharp
// ClassifiedAds.Modules.Product/DbConfigurations/ProductConfiguration.cs
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Category).HasMaxLength(100);  // NEW
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
```

### Step 3: Create Migration

```powershell
dotnet ef migrations add AddProductCategory `
    --context ProductDbContext `
    --project ClassifiedAds.Modules.Product `
    --startup-project ClassifiedAds.Migrator `
    -o Migrations/ProductDb
```

### Step 4: Update DTO (Model)

```csharp
// ClassifiedAds.Modules.Product/Models/ProductModel.cs
public class ProductModel
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }  // NEW
}
```

### Step 5: Update Mapping Extensions

```csharp
// ClassifiedAds.Modules.Product/Models/ProductModel.cs (or separate mapping file)
public static class ProductMappingExtensions
{
    public static ProductModel ToModel(this Product entity) => new ProductModel
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        Category = entity.Category  // NEW
    };

    public static Product ToEntity(this ProductModel model) => new Product
    {
        Id = model.Id,
        Code = model.Code,
        Name = model.Name,
        Description = model.Description,
        Category = model.Category  // NEW
    };
}
```

### Step 6: Update Controller (if needed)

The existing `PUT` endpoint needs to map the new field:

```csharp
// ClassifiedAds.Modules.Product/Controllers/ProductsController.cs
[HttpPut("{id}")]
public async Task<ActionResult> Put(Guid id, [FromBody] ProductModel model)
{
    var product = await _dispatcher.DispatchAsync(new GetProductQuery 
    { 
        Id = id, 
        ThrowNotFoundIfNull = true 
    });

    product.Code = model.Code;
    product.Name = model.Name;
    product.Description = model.Description;
    product.Category = model.Category;  // NEW

    await _dispatcher.DispatchAsync(new AddUpdateProductCommand { Product = product });

    return Ok(product.ToModel());
}
```

### Step 7: Add Validation (Optional)

```csharp
// Using DataAnnotations in ProductModel
public class ProductModel
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Code { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Name { get; set; }
    
    [StringLength(2000)]
    public string Description { get; set; }
    
    [StringLength(100)]
    public string Category { get; set; }  // NEW with validation
}
```

### Step 8: Unit Test

```csharp
[Fact]
public async Task HandleAsync_ShouldSaveProductWithCategory_WhenCategoryProvided()
{
    // Arrange
    var product = new Product 
    { 
        Id = Guid.NewGuid(), 
        Code = "P001", 
        Name = "Test",
        Category = "Electronics"  // NEW
    };
    var command = new AddUpdateProductCommand { Product = product };

    // Act
    await _handler.HandleAsync(command, CancellationToken.None);

    // Assert
    _crudServiceMock.Verify(
        s => s.AddOrUpdateAsync(
            It.Is<Product>(p => p.Category == "Electronics"), 
            CancellationToken.None), 
        Times.Once);
}
```

### Step 9: Integration Test

```csharp
[Fact]
public async Task Post_ShouldReturnCreated_WhenProductHasCategory()
{
    // Arrange
    var model = new ProductModel 
    { 
        Code = "CAT001", 
        Name = "Laptop", 
        Category = "Electronics" 
    };

    // Act
    var response = await _client.PostAsJsonAsync("/api/products", model);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    
    var created = await response.Content.ReadFromJsonAsync<ProductModel>();
    created.Category.Should().Be("Electronics");
}
```

### Step 10: Apply Migration

```powershell
dotnet run --project ClassifiedAds.Migrator
```

### Complete Checklist for Feature

- [x] Entity modified
- [x] EF Configuration updated
- [x] Migration created
- [x] DTO/Model updated
- [x] Mapping updated
- [x] Controller handles new field
- [x] Validation added
- [x] Unit test written
- [x] Integration test written
- [x] Migration applied

---

## 10. Common Developer Workflows

### 10.1 Running Locally

```powershell
# 1. Start infrastructure (PostgreSQL, RabbitMQ, MailHog, Redis)
docker-compose up -d db rabbitmq mailhog redis

# 2. Run migrations (creates all module tables)
dotnet run --project ClassifiedAds.Migrator

# 3. Start WebAPI
dotnet run --project ClassifiedAds.WebAPI

# 4. (Optional) Start Background Worker
dotnet run --project ClassifiedAds.Background

# 5. Open Swagger UI
# http://localhost:9002/swagger
```

### 10.2 Single Database Local Setup

For single database development:

1. **Modify `.env`** - Set all modules to use the same database:
   ```dotenv
   Modules__AuditLog__ConnectionStrings__Default="Host=db;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
   Modules__Configuration__ConnectionStrings__Default="Host=db;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
   Modules__Identity__ConnectionStrings__Default="Host=db;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
   Modules__Notification__ConnectionStrings__Default="Host=db;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
   Modules__Product__ConnectionStrings__Default="Host=db;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
   Modules__Storage__ConnectionStrings__Default="Host=db;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
   ```

2. **Run migrations** - All tables created in one database:
   ```powershell
   dotnet run --project ClassifiedAds.Migrator
   ```

### 10.3 Debugging

1. Open solution in Visual Studio / VS Code
2. Set `ClassifiedAds.WebAPI` as startup project
3. Press F5 (ensure infrastructure is running)
4. Breakpoints work in all referenced projects

### 10.4 Running Database Locally

```powershell
# Start PostgreSQL only
docker-compose up -d db

# Connect via any PostgreSQL client:
# Host: localhost
# Port: 5432
# User: postgres
# Password: <YOUR_PASSWORD>
# Database: ClassifiedAds (single DB) or ClassifiedAds_Product, etc. (multi-DB)
```

### 10.5 Code Formatting

**From rules/coding.md [COD-060]**:

```powershell
# Format entire solution
dotnet format

# Check only (no changes)
dotnet format --verify-no-changes
```

### 10.6 Adding a New Module

1. Create project: `dotnet new classlib -n ClassifiedAds.Modules.{Name}`
2. Add to solution: `dotnet sln add ClassifiedAds.Modules.{Name}`
3. Add project references (see existing modules)
4. Create folder structure (see Section 3)
5. Create `ServiceCollectionExtensions.cs`
6. Create `{Name}DbContext.cs`
7. Create `{Name}ModuleOptions.cs`
8. Register in [ClassifiedAds.WebAPI/Program.cs](ClassifiedAds.WebAPI/Program.cs)
9. Register in [ClassifiedAds.Migrator/Program.cs](ClassifiedAds.Migrator/Program.cs)
10. Add connection string in `appsettings.json` (use same DB for single-database)

### 10.7 Adding API Endpoints Safely

**Checklist from rules/architecture.md**:

- [ ] Controller is thin (delegates to Dispatcher)
- [ ] `[Authorize(Permission.X)]` attribute applied
- [ ] `[ProducesResponseType]` for Swagger
- [ ] `[EnableRateLimiting]` applied
- [ ] Logging with structured parameters (no PII)
- [ ] Returns proper HTTP status codes

---

## 11. Appendix: Index of Key Files

### Host/Entry Points

| File | Purpose | Verified |
|------|---------|----------|
| [ClassifiedAds.WebAPI/Program.cs](ClassifiedAds.WebAPI/Program.cs) | Web API composition root, middleware pipeline | ✅ |
| [ClassifiedAds.Background/Program.cs](ClassifiedAds.Background/Program.cs) | Background worker composition root | ✅ |
| [ClassifiedAds.Migrator/Program.cs](ClassifiedAds.Migrator/Program.cs) | Database migration runner with retry logic | ✅ |

### CQRS & Dispatcher

| File | Purpose | Verified |
|------|---------|----------|
| [ClassifiedAds.Application/Common/Dispatcher.cs](ClassifiedAds.Application/Common/Dispatcher.cs) | Custom CQRS dispatcher (NOT MediatR) | ✅ |
| [ClassifiedAds.Application/ApplicationServicesExtensions.cs](ClassifiedAds.Application/ApplicationServicesExtensions.cs) | Handler registration, `AddMessageHandlers()` | ✅ |
| [ClassifiedAds.Application/ICommandHandler.cs](ClassifiedAds.Application/ICommandHandler.cs) | Command handler interface | ✅ |

### Domain Layer

| File | Purpose | Verified |
|------|---------|----------|
| [ClassifiedAds.Domain/Entities/Entity.cs](ClassifiedAds.Domain/Entities/Entity.cs) | Base entity with Id, RowVersion, timestamps | ✅ |
| [ClassifiedAds.Domain/Repositories/IRepository.cs](ClassifiedAds.Domain/Repositories/IRepository.cs) | Repository interface | ✅ |
| [ClassifiedAds.Domain/Repositories/IUnitOfWork.cs](ClassifiedAds.Domain/Repositories/IUnitOfWork.cs) | Unit of work interface | ✅ |

### Persistence Layer

| File | Purpose | Verified |
|------|---------|----------|
| [ClassifiedAds.Persistence.PostgreSQL/DbContextUnitOfWork.cs](ClassifiedAds.Persistence.PostgreSQL/DbContextUnitOfWork.cs) | Base DbContext with IUnitOfWork | ✅ |
| [ClassifiedAds.Persistence.PostgreSQL/DbContextRepository.cs](ClassifiedAds.Persistence.PostgreSQL/DbContextRepository.cs) | Generic repository implementation | ✅ |

### Product Module (Reference Implementation)

| File | Purpose | Verified |
|------|---------|----------|
| [ClassifiedAds.Modules.Product/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Product/ServiceCollectionExtensions.cs) | Module DI registration, `MigrateProductDb()` | ✅ |
| [ClassifiedAds.Modules.Product/Persistence/ProductDbContext.cs](ClassifiedAds.Modules.Product/Persistence/ProductDbContext.cs) | Module's DbContext | ✅ |
| [ClassifiedAds.Modules.Product/Controllers/ProductsController.cs](ClassifiedAds.Modules.Product/Controllers/ProductsController.cs) | API endpoints with Dispatcher | ✅ |
| [ClassifiedAds.Modules.Product/Commands/AddUpdateProductCommand.cs](ClassifiedAds.Modules.Product/Commands/AddUpdateProductCommand.cs) | Command + handler | ✅ |
| [ClassifiedAds.Modules.Product/Queries/GetProductQuery.cs](ClassifiedAds.Modules.Product/Queries/GetProductQuery.cs) | Query + handler | ✅ |
| [ClassifiedAds.Modules.Product/EventHandlers/ProductCreatedEventHandler.cs](ClassifiedAds.Modules.Product/EventHandlers/ProductCreatedEventHandler.cs) | Domain event handler | ✅ |
| [ClassifiedAds.Modules.Product/Authorization/Permissions.cs](ClassifiedAds.Modules.Product/Authorization/Permissions.cs) | Permission constants | ✅ |
| [ClassifiedAds.Modules.Product/HostedServices/PublishEventWorker.cs](ClassifiedAds.Modules.Product/HostedServices/PublishEventWorker.cs) | Outbox background publisher | ✅ |
| [ClassifiedAds.Modules.Product/DbConfigurations/ProductConfiguration.cs](ClassifiedAds.Modules.Product/DbConfigurations/ProductConfiguration.cs) | EF entity configuration | ✅ |

### Cross-Cutting Concerns

| File | Purpose | Verified |
|------|---------|----------|
| [ClassifiedAds.Infrastructure/Web/ExceptionHandlers/GlobalExceptionHandler.cs](ClassifiedAds.Infrastructure/Web/ExceptionHandlers/GlobalExceptionHandler.cs) | Global exception handling | ✅ |

### Configuration Files

| File | Purpose | Verified |
|------|---------|----------|
| [ClassifiedAds.WebAPI/appsettings.json](ClassifiedAds.WebAPI/appsettings.json) | WebAPI configuration | ✅ |
| [ClassifiedAds.Migrator/appsettings.json](ClassifiedAds.Migrator/appsettings.json) | Migrator configuration | ✅ |
| [docker-compose.yml](docker-compose.yml) | Local development environment | ✅ |
| [.env](.env) | Docker environment variables | ✅ |
| [global.json](global.json) | .NET SDK version pinning | ✅ |

### Rules & Architecture Docs

| File | Purpose | Verified |
|------|---------|----------|
| [rules/00-priority.md](rules/00-priority.md) | Rule priority order | ✅ |
| [rules/security.md](rules/security.md) | Security requirements (HIGHEST priority) | ✅ |
| [rules/architecture.md](rules/architecture.md) | Architecture rules | ✅ |
| [rules/testing.md](rules/testing.md) | Testing requirements | ✅ |
| [rules/coding.md](rules/coding.md) | C# coding standards | ✅ |
| [rules/git-workflow.md](rules/git-workflow.md) | Git conventions | ✅ |

---

## Quick Start Commands

```powershell
# Clone and setup
git clone <repo-url>
cd GSP26SE43.ModularMonolith

# Start infrastructure (PostgreSQL, RabbitMQ, etc.)
docker-compose up -d db rabbitmq mailhog redis

# Run migrations (applies to all module databases or single DB)
dotnet run --project ClassifiedAds.Migrator

# Start API (with hot reload)
dotnet watch run --project ClassifiedAds.WebAPI

# Format code
dotnet format
```

### Single Database Quick Setup

```powershell
# 1. Edit .env - set all connection strings to same Database=ClassifiedAds

# 2. Start infrastructure
docker-compose up -d db rabbitmq

# 3. Run migrations - creates all tables in single database
dotnet run --project ClassifiedAds.Migrator

# 4. Start API
dotnet run --project ClassifiedAds.WebAPI
```

---

## Evidence Summary

This guide references the following verified source files:

| Claim | Evidence File |
|-------|---------------|
| DB Connection Strings | [ClassifiedAds.WebAPI/appsettings.json](ClassifiedAds.WebAPI/appsettings.json), [.env](.env) |
| DbContext Names | `ProductDbContext`, `IdentityDbContext`, etc. in `Modules/*/Persistence/` |
| Migration Commands | [ClassifiedAds.Migrator/Program.cs](ClassifiedAds.Migrator/Program.cs) |
| CQRS Dispatcher | [ClassifiedAds.Application/Common/Dispatcher.cs](ClassifiedAds.Application/Common/Dispatcher.cs) |
| Outbox Worker | [ClassifiedAds.Modules.Product/HostedServices/PublishEventWorker.cs](ClassifiedAds.Modules.Product/HostedServices/PublishEventWorker.cs) |
| Exception Handler | [ClassifiedAds.Infrastructure/Web/ExceptionHandlers/GlobalExceptionHandler.cs](ClassifiedAds.Infrastructure/Web/ExceptionHandlers/GlobalExceptionHandler.cs) |
| Auth Permissions | [ClassifiedAds.Modules.Product/Authorization/Permissions.cs](ClassifiedAds.Modules.Product/Authorization/Permissions.cs) |
| Test Infrastructure | **NOT FOUND** - test projects do not exist in current repo |

---

**End of Project Guide**

*Last Updated: December 31, 2025*
*Generated from source code analysis - aligned with rules/ and docs-architecture/*
