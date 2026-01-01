# 01 - Solution Structure

> **Purpose**: Understand the project organization, dependencies between projects, and folder conventions used throughout the codebase.

---

## Table of Contents

- [Project Overview](#project-overview)
- [Project Dependency Graph](#project-dependency-graph)
- [Host/Entry Point Projects](#hostentry-point-projects)
- [Core/Shared Projects](#coreshared-projects)
- [Module Projects](#module-projects)
- [Persistence Projects](#persistence-projects)
- [Test Projects](#test-projects)
- [Configuration & Infrastructure Files](#configuration--infrastructure-files)

---

## Project Overview

The solution follows a **Modular Monolith** architecture where the codebase is organized into:

1. **Entry Points** - Host applications (WebAPI, Background Workers, Migrator)
2. **Core/Shared Projects** - Domain, Application, Infrastructure, Contracts, CrossCuttingConcerns
3. **Modules** - Feature-specific vertical slices (Product, Identity, Storage, etc.)
4. **Persistence** - Database provider implementation (PostgreSQL)
5. **Tests** - Unit tests and Integration tests

```
ClassifiedAds.ModularMonolith/
├── ClassifiedAds.WebAPI/                 # ASP.NET Core Web API host
├── ClassifiedAds.Background/             # Background worker service
├── ClassifiedAds.Migrator/               # Database migration runner
│
├── ClassifiedAds.Application/            # CQRS handlers, Dispatcher, services
├── ClassifiedAds.Domain/                 # Entities, Events, Repository interfaces
├── ClassifiedAds.Infrastructure/         # Messaging, Caching, Logging implementations
├── ClassifiedAds.Contracts/              # Shared interfaces and DTOs
├── ClassifiedAds.CrossCuttingConcerns/   # Utilities (CSV, PDF, Excel, etc.)
│
├── ClassifiedAds.Modules.Product/        # Product module (sample domain)
├── ClassifiedAds.Modules.Identity/       # Identity & user management
├── ClassifiedAds.Modules.Storage/        # File storage
├── ClassifiedAds.Modules.Notification/   # Email, SMS, Web notifications
├── ClassifiedAds.Modules.AuditLog/       # Audit logging
├── ClassifiedAds.Modules.Configuration/  # Application configuration
│
├── ClassifiedAds.Persistence.PostgreSQL/ # PostgreSQL EF Core implementation
│
├── ClassifiedAds.UnitTests/              # Unit tests for domain, utilities
├── ClassifiedAds.IntegrationTests/       # Integration tests with Testcontainers
│
├── docs/                                 # General documentation
├── docs-architecture/                    # Architecture documentation
└── rules/                                # Development guidelines and rules
```

### Where in code?

- Solution file: [ClassifiedAds.ModularMonolith.slnx](../ClassifiedAds.ModularMonolith.slnx)

---

## Project Dependency Graph

The following diagram shows how projects reference each other:

```mermaid
graph TB
    subgraph "Entry Points"
        WebAPI[ClassifiedAds.WebAPI]
        Background[ClassifiedAds.Background]
        Migrator[ClassifiedAds.Migrator]
    end

    subgraph "Modules"
        Product[Modules.Product]
        Identity[Modules.Identity]
        Storage[Modules.Storage]
        Notification[Modules.Notification]
        AuditLog[Modules.AuditLog]
        Configuration[Modules.Configuration]
    end

    subgraph "Core"
        App[Application]
        Domain[Domain]
        Infra[Infrastructure]
        Contracts[Contracts]
        CrossCut[CrossCuttingConcerns]
    end

    subgraph "Persistence"
        PostgreSQL[Persistence.PostgreSQL]
    end

    subgraph "Tests"
        UnitTests[UnitTests]
        IntegTests[IntegrationTests]
    end

    WebAPI --> Product
    WebAPI --> Identity
    WebAPI --> Storage
    WebAPI --> Notification
    WebAPI --> AuditLog
    WebAPI --> Configuration
    WebAPI --> Infra

    Background --> Product
    Background --> Identity
    Background --> Storage
    Background --> Notification
    Background --> Infra

    Migrator --> Product
    Migrator --> Identity
    Migrator --> Storage
    Migrator --> Notification
    Migrator --> AuditLog
    Migrator --> Configuration

    Product --> App
    Product --> Contracts
    Product --> Infra
    Product --> PostgreSQL

    Identity --> App
    Identity --> Contracts
    Identity --> Infra
    Identity --> PostgreSQL

    App --> Domain
    App --> CrossCut

    Infra --> App
    Infra --> Domain
    Infra --> CrossCut

    Domain --> CrossCut

    PostgreSQL --> Domain

    UnitTests --> CrossCut
    UnitTests --> Domain
    UnitTests --> Infra

    IntegTests --> WebAPI
```

---

## Host/Entry Point Projects

### ClassifiedAds.WebAPI

**Type**: ASP.NET Core Web API  
**Responsibility**: Main HTTP API host. Composes all modules, configures authentication, Swagger, CORS, and SignalR.

```csharp
// ClassifiedAds.WebAPI/Program.cs (composition root)
services
    .AddAuditLogModule(opt => configuration.GetSection("Modules:AuditLog").Bind(opt))
    .AddConfigurationModule(opt => configuration.GetSection("Modules:Configuration").Bind(opt))
    .AddIdentityModuleCore(opt => configuration.GetSection("Modules:Identity").Bind(opt))
    .AddNotificationModule(opt => configuration.GetSection("Modules:Notification").Bind(opt))
    .AddProductModule(opt => configuration.GetSection("Modules:Product").Bind(opt))
    .AddStorageModule(opt => configuration.GetSection("Modules:Storage").Bind(opt))
    .AddApplicationServices();
```

**Where in code?**: [ClassifiedAds.WebAPI/Program.cs](../ClassifiedAds.WebAPI/Program.cs)

---

### ClassifiedAds.Background

**Type**: .NET Worker Service  
**Responsibility**: Hosts background jobs including outbox publishing, email/SMS sending, and message bus consumers.

```csharp
// ClassifiedAds.Background/Program.cs
static void AddHostedServices(IServiceCollection services)
{
    services.AddHostedServicesIdentityModule();
    services.AddHostedServicesNotificationModule();
    services.AddHostedServicesProductModule();
    services.AddHostedServicesStorageModule();
}
```

**Where in code?**: [ClassifiedAds.Background/Program.cs](../ClassifiedAds.Background/Program.cs)

---

### ClassifiedAds.Migrator

**Type**: .NET Worker Service  
**Responsibility**: Runs EF Core migrations for all modules plus DbUp scripts.

```csharp
// ClassifiedAds.Migrator/Program.cs
app.MigrateAuditLogDb();
app.MigrateConfigurationDb();
app.MigrateIdentityDb();
app.MigrateNotificationDb();
app.MigrateProductDb();
app.MigrateStorageDb();
```

**Where in code?**: [ClassifiedAds.Migrator/Program.cs](../ClassifiedAds.Migrator/Program.cs)

---

### ClassifiedAds.AspireAppHost

**Type**: .NET Aspire AppHost  
**Responsibility**: Orchestrates all services for local development with service discovery and dashboards.

```csharp
// ClassifiedAds.AspireAppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var migrator = builder.AddProject<Projects.ClassifiedAds_Migrator>("ClassifiedAds-Migrator");
var background = builder.AddProject<Projects.ClassifiedAds_Background>("ClassifiedAds-Background");
var webApi = builder.AddProject<Projects.ClassifiedAds_WebAPI>("ClassifiedAds-WebAPI");
```

**Where in code?**: [ClassifiedAds.AspireAppHost/Program.cs](../ClassifiedAds.AspireAppHost/Program.cs)

---

## Core/Shared Projects

### ClassifiedAds.Domain

**Layer**: Domain (innermost)  
**Responsibility**: Contains entity base classes, domain events, repository interfaces, and messaging abstractions. Has no external dependencies except CrossCuttingConcerns.

| Component | Description |
|-----------|-------------|
| `Entity<TKey>` | Base class for all entities with Id, RowVersion, timestamps |
| `IAggregateRoot` | Marker interface for aggregate roots |
| `IDomainEvent` | Base interface for domain events |
| `IRepository<T, TKey>` | Repository abstraction |
| `IUnitOfWork` | Transaction management abstraction |
| `IMessageBus` | Message bus abstraction |

**Where in code?**: [ClassifiedAds.Domain/](../ClassifiedAds.Domain/)

---

### ClassifiedAds.Application

**Layer**: Application  
**Responsibility**: CQRS infrastructure with `Dispatcher`, command/query handlers, `CrudService`, and handler decorators.

| Component | Description |
|-----------|-------------|
| `Dispatcher` | Routes commands, queries, and events to handlers |
| `ICommand`, `ICommandHandler<T>` | Command pattern interfaces |
| `IQuery<T>`, `IQueryHandler<TQuery, TResult>` | Query pattern interfaces |
| `ICrudService<T>` | Generic CRUD operations with event dispatch |
| Decorators | Cross-cutting concerns (AuditLog, DatabaseRetry) |

**Where in code?**: [ClassifiedAds.Application/](../ClassifiedAds.Application/)

---

### ClassifiedAds.Infrastructure

**Layer**: Infrastructure  
**Responsibility**: Implementations for external concerns: messaging, caching, logging, monitoring, storage, notifications.

| Folder | Description |
|--------|-------------|
| `Messaging/` | RabbitMQ, Kafka, Azure Service Bus implementations |
| `Caching/` | Redis, SQL Server, InMemory caching |
| `Logging/` | Serilog configuration |
| `Monitoring/` | OpenTelemetry, Application Insights |
| `Notification/` | Email (SendGrid, SMTP), SMS (Twilio) |
| `Storages/` | Azure Blob, AWS S3, Local file storage |

**Where in code?**: [ClassifiedAds.Infrastructure/](../ClassifiedAds.Infrastructure/)

---

### ClassifiedAds.Contracts

**Layer**: Shared Contracts  
**Responsibility**: Module-to-module interfaces and DTOs. Enables loose coupling between modules.

```csharp
// ClassifiedAds.Contracts/Identity/Services/ICurrentUser.cs
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
}
```

**Where in code?**: [ClassifiedAds.Contracts/](../ClassifiedAds.Contracts/)

---

### ClassifiedAds.CrossCuttingConcerns

**Layer**: Utilities  
**Responsibility**: Helper libraries that can be used across all layers. Contains utilities that don't belong to any specific domain.

| Folder | Description | Key Classes/Interfaces |
|--------|-------------|------------------------|
| `Csv/` | CSV reading/writing abstractions | `ICsvReader`, `ICsvWriter` |
| `Excel/` | Excel file generation (EPPlus) | `IExcelWriter` |
| `Pdf/` | PDF generation abstractions | `IPdfWriter` |
| `Html/` | HTML rendering utilities | `IHtmlGenerator` |
| `DateTimes/` | DateTime provider abstraction | `IDateTimeProvider` |
| `Exceptions/` | Custom exception types | `ValidationException`, `NotFoundException` |
| `Locks/` | Distributed locking abstractions | `IDistributedLock` |
| `Logging/` | Logging extensions | Extension methods for ILogger |
| `ExtensionMethods/` | Common extension methods | String, Collection extensions |

**Where in code?**: [ClassifiedAds.CrossCuttingConcerns/](../ClassifiedAds.CrossCuttingConcerns/)

---

## Module Projects

Each module follows a **vertical slice** structure containing everything needed for that feature:

```
ClassifiedAds.Modules.{ModuleName}/
├── Authorization/           # Permission constants and policies
├── Commands/                # CQRS command handlers
├── ConfigurationOptions/    # Module-specific settings
├── Constants/               # Module constants
├── Controllers/             # API endpoints
├── Csv/                     # CSV import/export handlers
├── DbConfigurations/        # EF Core entity configurations
├── DTOs/                    # Data transfer objects
├── Entities/                # Domain entities (including OutboxMessage)
├── EventHandlers/           # Domain event handlers
├── HostedServices/          # Background workers (e.g., PublishEventWorker)
├── Html/                    # HTML export handlers
├── Migrations/              # EF Core migrations (if in module)
├── Models/                  # API models
├── OutBoxEventPublishers/   # Outbox message publishers
├── Pdf/                     # PDF export handlers
├── Persistence/             # DbContext, Repositories
├── Queries/                 # CQRS query handlers
├── RateLimiterPolicies/     # Rate limiting configuration
└── ServiceCollectionExtensions.cs  # DI registration
```

### Module List

| Module | Responsibility | DbContext | Key Features |
|--------|---------------|-----------|--------------|
| **Product** | Sample business domain (product catalog) | `ProductDbContext` | Outbox pattern, CRUD operations, event publishing |
| **Identity** | User/Role management, ASP.NET Identity | `IdentityDbContext` | User registration, authentication, role management |
| **Storage** | File upload/download, blob storage | `StorageDbContext` | Outbox pattern, file metadata, multiple storage providers |
| **Notification** | Email, SMS, Web push notifications | `NotificationDbContext` | Queued email/SMS, notification templates |
| **AuditLog** | Centralized audit logging | `AuditLogDbContext` | Automatic change tracking, audit queries |
| **Configuration** | Application configuration entries | `ConfigurationDbContext` | Dynamic app settings, feature flags |

**Where in code?**: [ClassifiedAds.Modules.Product/](../ClassifiedAds.Modules.Product/) (reference implementation)

---

## Persistence Projects

### ClassifiedAds.Persistence.PostgreSQL

**Responsibility**: PostgreSQL-specific EF Core implementation. Provides base classes for DbContext with Unit of Work pattern and generic repository.

**Key Components**:

| Component | Description |
|-----------|-------------|
| `DbContextUnitOfWork<T>` | Base DbContext implementing `IUnitOfWork` with transaction support |
| `Repository<T, TKey>` | Generic repository implementation with common CRUD operations |

```csharp
// ClassifiedAds.Persistence.PostgreSQL/DbContextUnitOfWork.cs
public class DbContextUnitOfWork<TDbContext> : DbContext, IUnitOfWork
    where TDbContext : DbContext
{
    public async Task<IDisposable> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, 
        CancellationToken cancellationToken = default)
    {
        _dbContextTransaction = await Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        return _dbContextTransaction;
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _dbContextTransaction.CommitAsync(cancellationToken);
    }
}
```

**Where in code?**: [ClassifiedAds.Persistence.PostgreSQL/](../ClassifiedAds.Persistence.PostgreSQL/)

---

## Test Projects

The solution includes comprehensive test projects for ensuring code quality:

| Project | Type | Scope | Key Technologies |
|---------|------|-------|------------------|
| `ClassifiedAds.UnitTests` | Unit Tests | Domain logic, utilities, exception handling | xUnit, FluentAssertions, Moq |
| `ClassifiedAds.IntegrationTests` | Integration Tests | Full API testing with real database | xUnit, Testcontainers, WebApplicationFactory |

### ClassifiedAds.UnitTests

**Purpose**: Fast, isolated tests for business logic without external dependencies.

**Structure**:
```
ClassifiedAds.UnitTests/
├── CrossCuttingConcerns/
│   ├── ValidationExceptionTests.cs
│   └── NotFoundExceptionTests.cs
├── Domain/
│   └── (Entity tests)
└── Infrastructure/
    └── (Service tests)
```

**Test Stack**:
- **xUnit**: Test framework
- **FluentAssertions**: Readable assertions (`result.Should().Be(expected)`)
- **Moq**: Mocking framework for dependencies
- **coverlet.collector**: Code coverage collection

**Where in code?**: [ClassifiedAds.UnitTests/](../ClassifiedAds.UnitTests/)

### ClassifiedAds.IntegrationTests

**Purpose**: End-to-end tests that verify the full request/response cycle with a real PostgreSQL database.

**Structure**:
```
ClassifiedAds.IntegrationTests/
├── Infrastructure/
│   ├── CustomWebApplicationFactory.cs   # Test server factory
│   ├── PostgreSqlContainerFixture.cs    # Testcontainers fixture
│   ├── IntegrationTestCollection.cs     # xUnit collection definition
│   └── TestAuthHandler.cs               # Test authentication bypass
└── Smoke/
    └── ApplicationSmokeTests.cs         # Basic health/startup tests
```

**Test Stack**:
- **Microsoft.AspNetCore.Mvc.Testing**: WebApplicationFactory for HTTP testing
- **Testcontainers.PostgreSql**: Spins up real PostgreSQL containers
- **Respawn**: Database reset between tests for isolation

**Key Features**:
- Uses real PostgreSQL database via Docker containers
- Bypasses authentication for testing with `TestAuthHandler`
- Automatic database reset between test runs

**Where in code?**: [ClassifiedAds.IntegrationTests/](../ClassifiedAds.IntegrationTests/)

---

## Configuration & Infrastructure Files

| File | Purpose | Description |
|------|---------|-------------|
| `docker-compose.yml` | Local development environment | Defines services: PostgreSQL, RabbitMQ, MailHog |
| `docker-compose.volumes.yml` | Persistent volume configuration | Volume definitions for database persistence |
| `.github/workflows/ci.yml` | GitHub Actions CI pipeline | Build, test, lint, Docker validation |
| `.github/workflows/cd.yml` | GitHub Actions CD pipeline | Release, deploy to staging/production |
| `global.json` | .NET SDK version pinning | Ensures consistent SDK version across team |
| `.editorconfig` | Code style settings | Consistent formatting rules |
| `.env` | Environment variables | Local development secrets (gitignored) |

**Where in code?**: 
- [docker-compose.yml](../docker-compose.yml)
- [.github/workflows/](../.github/workflows/)
- [global.json](../global.json)

---

## Naming Conventions

| Convention | Pattern | Example |
|------------|---------|---------|
| Module projects | `ClassifiedAds.Modules.{ModuleName}` | `ClassifiedAds.Modules.Product` |
| DbContext | `{ModuleName}DbContext` | `ProductDbContext` |
| Repository | `{Entity}Repository` or `Repository<T, TKey>` | `ProductRepository` |
| Command | `{Action}{Entity}Command` | `AddUpdateProductCommand` |
| Query | `Get{Entity/Entities}Query` | `GetProductsQuery` |
| Handler | `{Command/Query}Handler` | `AddUpdateProductCommandHandler` |
| Event Handler | `{Entity}{Action}EventHandler` | `ProductCreatedEventHandler` |
| Controller | `{Entities}Controller` | `ProductsController` |
| Service Extension | `ServiceCollectionExtensions.cs` | Module DI registration |
| Test Class | `{ClassUnderTest}Tests` | `ValidationExceptionTests` |

---

*Next: [02 - Architecture Overview](02-architecture-overview.md)*
