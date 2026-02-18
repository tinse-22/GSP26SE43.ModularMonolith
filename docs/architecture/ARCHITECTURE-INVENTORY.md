# Architecture Inventory - ClassifiedAds Modular Monolith

> **Generated**: December 27, 2025  
> **Status**: Step 1 of multi-step documentation generation  
> **Source**: Analyzed from source code (no assumptions)

---

## 1. High-Level Directory Tree

```
ClassifiedAds.ModularMonolith/
├── ClassifiedAds.Application/           # Application layer (CQRS handlers, dispatchers)
├── ClassifiedAds.AppHost/               # .NET Aspire orchestration host
├── ClassifiedAds.Background/            # Background worker service (hosted services)
├── ClassifiedAds.Contracts/             # Shared contracts/interfaces between modules
├── ClassifiedAds.CrossCuttingConcerns/  # Cross-cutting utilities (CSV, PDF, HTML, etc.)
├── ClassifiedAds.Domain/                # Domain layer (entities, events, repositories interfaces)
├── ClassifiedAds.Infrastructure/        # Infrastructure implementations (messaging, caching, etc.)
├── ClassifiedAds.Migrator/              # Database migration worker
├── ClassifiedAds.Migrator.Tests/        # Tests for migrator
├── ClassifiedAds.Modules.AuditLog/      # Module: Audit logging
├── ClassifiedAds.Modules.Configuration/ # Module: Configuration management
├── ClassifiedAds.Modules.Identity/      # Module: Identity & user management
├── ClassifiedAds.Modules.Notification/  # Module: Email, SMS, Web notifications
├── ClassifiedAds.Modules.Product/       # Module: Product catalog (sample domain)
├── ClassifiedAds.Modules.Product.EndToEndTests/
├── ClassifiedAds.Modules.Product.IntegrationTests/
├── ClassifiedAds.Modules.Product.UnitTests/
├── ClassifiedAds.Modules.Storage/       # Module: File storage
├── ClassifiedAds.Persistence.PostgreSQL/# PostgreSQL persistence provider
├── ClassifiedAds.ServiceDefaults/       # Aspire shared defaults (telemetry, health checks)
├── ClassifiedAds.WebAPI/                # ASP.NET Core Web API host
├── libs/                                # Native libraries (e.g., libwkhtmltox)
├── docker-compose.yml                   # Local dev environment
└── ClassifiedAds.ModularMonolith.slnx   # Solution file
```

---

## 2. Projects & Responsibilities

### 2.1 Host/Entry Point Projects

| Project | Type | Responsibility |
|---------|------|----------------|
| **ClassifiedAds.WebAPI** | ASP.NET Core Web API | Main HTTP API host. Composes all modules, configures authentication, Swagger, CORS, SignalR. |
| **ClassifiedAds.Background** | Worker Service | Hosts background jobs: outbox publishing, email/SMS sending, message bus consumers. |
| **ClassifiedAds.Migrator** | Worker Service | Database migrations via EF Core + DbUp. Runs all module migrations. |
| **ClassifiedAds.AppHost** | Aspire AppHost | .NET Aspire orchestration for local dev (coordinates WebAPI, Background, Migrator, infrastructure). |
| **ClassifiedAds.ServiceDefaults** | Class Library | Aspire shared defaults: OpenTelemetry, health checks, resilience, service discovery. |

#### Where in code?
- [ClassifiedAds.WebAPI/Program.cs](ClassifiedAds.WebAPI/Program.cs) - Web API composition root
- [ClassifiedAds.Background/Program.cs](ClassifiedAds.Background/Program.cs) - Background worker composition root
- [ClassifiedAds.Migrator/Program.cs](ClassifiedAds.Migrator/Program.cs) - Migration runner
- [ClassifiedAds.AppHost/Program.cs](ClassifiedAds.AppHost/Program.cs) - Aspire orchestration
- [ClassifiedAds.ServiceDefaults/Extensions.cs](ClassifiedAds.ServiceDefaults/Extensions.cs) - Aspire defaults

---

### 2.2 Shared/Core Projects

| Project | Layer | Responsibility |
|---------|-------|----------------|
| **ClassifiedAds.Domain** | Domain | Base entities (`Entity<T>`, `IAggregateRoot`), domain events (`IDomainEvent`, `IDomainEventHandler`), repository interfaces (`IRepository`, `IUnitOfWork`), messaging abstractions. |
| **ClassifiedAds.Application** | Application | CQRS infrastructure (`ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`), `Dispatcher` (command/query/event dispatcher), generic CRUD service, decorators (AuditLog, DatabaseRetry). |
| **ClassifiedAds.Infrastructure** | Infrastructure | Cross-cutting implementations: Messaging (RabbitMQ, Kafka, Azure Service Bus), Caching (Redis, SQL Server, InMemory), Logging (Serilog), Monitoring (OpenTelemetry, Application Insights), Storage (Azure Blob, AWS S3, Local), PDF generation, HTML rendering. |
| **ClassifiedAds.Contracts** | Shared Contracts | Module-to-module interfaces and DTOs. Defines `ICurrentUser`, `IUserService`, `IEmailMessageService`, `IAuditLogService`. Enables loose coupling between modules. |
| **ClassifiedAds.CrossCuttingConcerns** | Utilities | Helper libraries: CSV, Excel, PDF, HTML, DateTime, Exceptions, Locks, Logging abstractions. |
| **ClassifiedAds.Persistence.PostgreSQL** | Persistence | EF Core base repository implementation for PostgreSQL, `DbContext` base classes, bulk operations support. |

#### Where in code?
- [ClassifiedAds.Domain/Entities/](ClassifiedAds.Domain/Entities/) - `Entity.cs`, `IAggregateRoot.cs`
- [ClassifiedAds.Domain/Events/](ClassifiedAds.Domain/Events/) - `IDomainEvent.cs`, `IDomainEventHandler.cs`, `EntityCreatedEvent.cs`
- [ClassifiedAds.Domain/Repositories/](ClassifiedAds.Domain/Repositories/) - `IRepository.cs`, `IUnitOfWork.cs`
- [ClassifiedAds.Domain/Infrastructure/Messaging/](ClassifiedAds.Domain/Infrastructure/Messaging/) - `IMessageBus.cs`, `IMessageSender.cs`, `IOutboxMessagePublisher.cs`
- [ClassifiedAds.Application/Common/Dispatcher.cs](ClassifiedAds.Application/Common/Dispatcher.cs) - CQRS dispatcher
- [ClassifiedAds.Application/ICommandHandler.cs](ClassifiedAds.Application/ICommandHandler.cs) - Command handler interface
- [ClassifiedAds.Application/Decorators/](ClassifiedAds.Application/Decorators/) - Handler decorators
- [ClassifiedAds.Infrastructure/Messaging/](ClassifiedAds.Infrastructure/Messaging/) - Messaging implementations
- [ClassifiedAds.Infrastructure/Caching/](ClassifiedAds.Infrastructure/Caching/) - Caching implementations
- [ClassifiedAds.Infrastructure/Monitoring/](ClassifiedAds.Infrastructure/Monitoring/) - Observability
- [ClassifiedAds.Contracts/Identity/Services/](ClassifiedAds.Contracts/Identity/Services/) - `ICurrentUser.cs`, `IUserService.cs`

---

### 2.3 Module Projects

Each module follows a **vertical slice** structure with its own:
- Entities (domain models)
- DbConfigurations (EF Core mappings)
- Persistence (DbContext, Repositories)
- Commands/Queries (CQRS handlers)
- Controllers (API endpoints)
- EventHandlers (domain event handlers)
- HostedServices (background workers)
- ServiceCollectionExtensions (DI registration)

| Module | Responsibility | Has Own DbContext | Has Outbox |
|--------|---------------|-------------------|------------|
| **ClassifiedAds.Modules.Product** | Product catalog management. Sample business domain. | ✅ `ProductDbContext` | ✅ |
| **ClassifiedAds.Modules.Identity** | User/Role management, ASP.NET Core Identity integration, Auth0/Azure AD B2C providers. | ✅ `IdentityDbContext` | ❌ |
| **ClassifiedAds.Modules.AuditLog** | Centralized audit logging. | ✅ `AuditLogDbContext` | ❌ |
| **ClassifiedAds.Modules.Notification** | Email, SMS, Web push notifications. | ✅ `NotificationDbContext` | ❌ |
| **ClassifiedAds.Modules.Storage** | File upload/download, blob storage abstraction. | ✅ `StorageDbContext` | ✅ |
| **ClassifiedAds.Modules.Configuration** | Application configuration entries. | ✅ `ConfigurationDbContext` | ❌ |

#### Where in code? (Module structure example - Product)
- [ClassifiedAds.Modules.Product/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Product/ServiceCollectionExtensions.cs) - Module registration
- [ClassifiedAds.Modules.Product/Entities/](ClassifiedAds.Modules.Product/Entities/) - `Product.cs`, `OutboxMessage.cs`, `AuditLogEntry.cs`
- [ClassifiedAds.Modules.Product/Persistence/](ClassifiedAds.Modules.Product/Persistence/) - `ProductDbContext`, `ProductRepository`
- [ClassifiedAds.Modules.Product/Commands/](ClassifiedAds.Modules.Product/Commands/) - `AddUpdateProductCommand.cs`, `DeleteProductCommand.cs`, `PublishEventsCommand.cs`
- [ClassifiedAds.Modules.Product/Queries/](ClassifiedAds.Modules.Product/Queries/) - `GetProductsQuery.cs`, `GetProductQuery.cs`
- [ClassifiedAds.Modules.Product/Controllers/](ClassifiedAds.Modules.Product/Controllers/) - API endpoints
- [ClassifiedAds.Modules.Product/EventHandlers/](ClassifiedAds.Modules.Product/EventHandlers/) - `ProductCreatedEventHandler.cs`, `ProductUpdatedEventHandler.cs`, `ProductDeletedEventHandler.cs`
- [ClassifiedAds.Modules.Product/HostedServices/](ClassifiedAds.Modules.Product/HostedServices/) - `PublishEventWorker.cs`
- [ClassifiedAds.Modules.Product/OutBoxEventPublishers/](ClassifiedAds.Modules.Product/OutBoxEventPublishers/) - `AuditLogEntryOutBoxMessagePublisher.cs`

---

## 3. Dependency Overview

### 3.1 Project Reference Graph

```
                    ┌─────────────────────────────────────────┐
                    │        ClassifiedAds.WebAPI             │
                    │  (ASP.NET Core Web API - Entry Point)   │
                    └──────────────────┬──────────────────────┘
                                       │
        ┌──────────────────────────────┼──────────────────────────────┐
        │                              │                              │
        ▼                              ▼                              ▼
┌───────────────┐            ┌─────────────────┐            ┌─────────────────┐
│ Modules.*     │            │ Application     │            │ Infrastructure  │
│ (6 modules)   │            │                 │            │                 │
└───────┬───────┘            └────────┬────────┘            └────────┬────────┘
        │                             │                              │
        │    ┌────────────────────────┼────────────────────────┐     │
        │    │                        │                        │     │
        ▼    ▼                        ▼                        ▼     ▼
┌───────────────────┐         ┌──────────────┐         ┌──────────────────┐
│     Contracts     │         │    Domain    │         │ CrossCuttingConcerns │
│ (Shared DTOs/Interfaces)    │              │         │                  │
└───────────────────┘         └──────────────┘         └──────────────────┘
                                      │
                                      ▼
                              ┌────────────────────┐
                              │ Persistence.*      │
                              │ (PostgreSQL)       │
                              └────────────────────┘
```

### 3.2 Module Dependencies

All modules reference:
- `ClassifiedAds.Application`
- `ClassifiedAds.Contracts`
- `ClassifiedAds.CrossCuttingConcerns`
- `ClassifiedAds.Domain`
- `ClassifiedAds.Infrastructure`
- `ClassifiedAds.Persistence.PostgreSQL`

#### Where in code?
- [ClassifiedAds.Modules.Product/ClassifiedAds.Modules.Product.csproj](ClassifiedAds.Modules.Product/ClassifiedAds.Modules.Product.csproj)

---

## 4. Key Architectural Patterns

### 4.1 CQRS (Command Query Responsibility Segregation)

**Present**: ✅ Yes

Custom lightweight CQRS implementation (no MediatR):
- **Commands**: `ICommand`, `ICommandHandler<TCommand>`
- **Queries**: `IQuery<TResult>`, `IQueryHandler<TQuery, TResult>`
- **Dispatcher**: `Dispatcher` class routes commands/queries to handlers

#### Where in code?
- [ClassifiedAds.Application/ICommandHandler.cs](ClassifiedAds.Application/ICommandHandler.cs)
- [ClassifiedAds.Application/Common/Dispatcher.cs](ClassifiedAds.Application/Common/Dispatcher.cs)
- [ClassifiedAds.Modules.Product/Commands/AddUpdateProductCommand.cs](ClassifiedAds.Modules.Product/Commands/AddUpdateProductCommand.cs)
- [ClassifiedAds.Modules.Product/Queries/GetProductsQuery.cs](ClassifiedAds.Modules.Product/Queries/GetProductsQuery.cs)

---

### 4.2 Domain Events

**Present**: ✅ Yes

- **Interface**: `IDomainEvent`, `IDomainEventHandler<TEvent>`
- **Built-in Events**: `EntityCreatedEvent<T>`, `EntityUpdatedEvent<T>`, `EntityDeletedEvent<T>`
- **Dispatch**: Synchronous dispatch via `Dispatcher.DispatchAsync(IDomainEvent)`
- **Handlers registered**: Assembly-scanned and registered in DI during startup

#### Where in code?
- [ClassifiedAds.Domain/Events/IDomainEvent.cs](ClassifiedAds.Domain/Events/IDomainEvent.cs)
- [ClassifiedAds.Domain/Events/IDomainEventHandler.cs](ClassifiedAds.Domain/Events/IDomainEventHandler.cs)
- [ClassifiedAds.Domain/Events/EntityCreatedEvent.cs](ClassifiedAds.Domain/Events/EntityCreatedEvent.cs)
- [ClassifiedAds.Modules.Product/EventHandlers/ProductCreatedEventHandler.cs](ClassifiedAds.Modules.Product/EventHandlers/ProductCreatedEventHandler.cs)

---

### 4.3 Outbox Pattern

**Present**: ✅ Yes

Each module that needs reliable event publishing has its own `OutboxMessage` table:
- Events saved to outbox in same transaction as domain changes
- `PublishEventWorker` (BackgroundService) polls outbox and publishes to message bus
- Feature toggle (`IOutboxPublishingToggle`) to enable/disable publishing

#### Where in code?
- [ClassifiedAds.Modules.Product/Entities/OutboxMessage.cs](ClassifiedAds.Modules.Product/Entities/OutboxMessage.cs)
- [ClassifiedAds.Modules.Product/HostedServices/PublishEventWorker.cs](ClassifiedAds.Modules.Product/HostedServices/PublishEventWorker.cs)
- [ClassifiedAds.Modules.Product/EventHandlers/ProductCreatedEventHandler.cs](ClassifiedAds.Modules.Product/EventHandlers/ProductCreatedEventHandler.cs) (saves to outbox)
- [ClassifiedAds.Domain/Infrastructure/Messaging/IOutboxMessagePublisher.cs](ClassifiedAds.Domain/Infrastructure/Messaging/IOutboxMessagePublisher.cs)

---

### 4.4 Message Bus / Event Bus

**Present**: ✅ Yes

Multiple providers supported (configurable):
- **RabbitMQ** (default in docker-compose)
- **Kafka**
- **Azure Service Bus**
- **Fake** (for testing)

Abstractions:
- `IMessageBus` - publish messages
- `IMessageSender<T>` - send specific message type
- `IMessageReceiver<TConsumer, T>` - receive messages
- `IMessageBusConsumer<T>` - consumer interface

#### Where in code?
- [ClassifiedAds.Domain/Infrastructure/Messaging/IMessageBus.cs](ClassifiedAds.Domain/Infrastructure/Messaging/IMessageBus.cs)
- [ClassifiedAds.Infrastructure/Messaging/RabbitMQ/](ClassifiedAds.Infrastructure/Messaging/RabbitMQ/) - RabbitMQ implementation
- [ClassifiedAds.Infrastructure/Messaging/Kafka/](ClassifiedAds.Infrastructure/Messaging/Kafka/) - Kafka implementation
- [ClassifiedAds.Infrastructure/Messaging/AzureServiceBus/](ClassifiedAds.Infrastructure/Messaging/AzureServiceBus/) - Azure Service Bus implementation
- [ClassifiedAds.Infrastructure/Messaging/MessagingCollectionExtensions.cs](ClassifiedAds.Infrastructure/Messaging/MessagingCollectionExtensions.cs) - DI registration

---

### 4.5 Clean Architecture Layering

**Present**: ✅ Yes (adapted for modular monolith)

Layer structure (inner to outer):
1. **Domain** - Entities, Events, Repository interfaces (no external dependencies)
2. **Application** - CQRS handlers, Dispatcher, Business logic orchestration
3. **Infrastructure** - External concerns (EF Core, Messaging, Caching, etc.)
4. **Modules** - Vertical slices containing all layers for a bounded context
5. **WebAPI/Background** - Composition roots

#### Where in code?
- [ClassifiedAds.Domain/ClassifiedAds.Domain.csproj](ClassifiedAds.Domain/ClassifiedAds.Domain.csproj) - Only references CrossCuttingConcerns
- [ClassifiedAds.Application/ClassifiedAds.Application.csproj](ClassifiedAds.Application/ClassifiedAds.Application.csproj) - References Domain
- [ClassifiedAds.Infrastructure/ClassifiedAds.Infrastructure.csproj](ClassifiedAds.Infrastructure/ClassifiedAds.Infrastructure.csproj) - References Application, Domain

---

### 4.6 Authentication & Authorization

**Present**: ✅ Yes

- **JWT Bearer** authentication (dual scheme support)
- **IdentityServer** integration (OpenIddict - external project)
- **Custom JWT** validation with certificate-based signing
- **ASP.NET Core Identity** for user/role management
- **External Identity Providers**: Auth0, Azure AD B2C

#### Where in code?
- [ClassifiedAds.WebAPI/Program.cs](ClassifiedAds.WebAPI/Program.cs#L95-L120) - Authentication configuration
- [ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs) - Identity setup
- [ClassifiedAds.Modules.Identity/IdentityProviders/](ClassifiedAds.Modules.Identity/IdentityProviders/) - External providers
- [ClassifiedAds.Modules.Product/Authorization/](ClassifiedAds.Modules.Product/Authorization/) - Authorization policies

---

### 4.7 Observability

**Present**: ✅ Yes

- **Logging**: Serilog with multiple sinks (Console, File, AWS CloudWatch, Application Insights)
- **Distributed Tracing**: OpenTelemetry with exporters (Zipkin, OTLP, Azure Monitor)
- **Metrics**: OpenTelemetry instrumentation (ASP.NET Core, HTTP, EF Core, Runtime, Process)
- **Health Checks**: Custom health checks for dependencies

#### Where in code?
- [ClassifiedAds.Infrastructure/Logging/](ClassifiedAds.Infrastructure/Logging/) - Serilog configuration
- [ClassifiedAds.Infrastructure/Monitoring/](ClassifiedAds.Infrastructure/Monitoring/) - OpenTelemetry, Application Insights
- [ClassifiedAds.Infrastructure/Monitoring/OpenTelemetry/](ClassifiedAds.Infrastructure/Monitoring/OpenTelemetry/) - OTEL setup
- [ClassifiedAds.Infrastructure/HealthChecks/](ClassifiedAds.Infrastructure/HealthChecks/) - Health check utilities

---

### 4.8 Background Workers

**Present**: ✅ Yes

Hosted services in `ClassifiedAds.Background`:
- **PublishEventWorker** (per module) - Outbox pattern publisher
- **SendEmailWorker** - Process email queue
- **SendSmsWorker** - Process SMS queue
- **Message Bus Consumers** - Process incoming messages

#### Where in code?
- [ClassifiedAds.Background/Program.cs](ClassifiedAds.Background/Program.cs#L69-L75) - AddHostedServices* calls
- [ClassifiedAds.Modules.Product/HostedServices/PublishEventWorker.cs](ClassifiedAds.Modules.Product/HostedServices/PublishEventWorker.cs)
- [ClassifiedAds.Modules.Notification/HostedServices/SendEmailWorker.cs](ClassifiedAds.Modules.Notification/HostedServices/SendEmailWorker.cs)
- [ClassifiedAds.Modules.Notification/HostedServices/SendSmsWorker.cs](ClassifiedAds.Modules.Notification/HostedServices/SendSmsWorker.cs)

---

### 4.9 Caching

**Present**: ✅ Yes

Multiple providers supported:
- **InMemory** (MemoryCache)
- **Distributed**: Redis, SQL Server, InMemory distributed
- **Hybrid Cache** (new .NET 9/10 feature)

#### Where in code?
- [ClassifiedAds.Infrastructure/Caching/CachingServiceCollectionExtensions.cs](ClassifiedAds.Infrastructure/Caching/CachingServiceCollectionExtensions.cs)
- [ClassifiedAds.Infrastructure/Caching/CachingOptions.cs](ClassifiedAds.Infrastructure/Caching/CachingOptions.cs)

---

### 4.10 Handler Decorators

**Present**: ✅ Yes

Command/Query handler decorators for cross-cutting concerns:
- **AuditLogCommandDecorator** - Logs command execution
- **AuditLogQueryDecorator** - Logs query execution
- **DatabaseRetry** - Retry logic for transient failures

#### Where in code?
- [ClassifiedAds.Application/Decorators/AuditLog/AuditLogCommandDecorator.cs](ClassifiedAds.Application/Decorators/AuditLog/AuditLogCommandDecorator.cs)
- [ClassifiedAds.Application/Decorators/DatabaseRetry/](ClassifiedAds.Application/Decorators/DatabaseRetry/)
- [ClassifiedAds.Application/Decorators/Mappings.cs](ClassifiedAds.Application/Decorators/Mappings.cs)

---

## 5. Entry Points & DI Composition Roots

### 5.1 WebAPI Composition Root

**File**: [ClassifiedAds.WebAPI/Program.cs](ClassifiedAds.WebAPI/Program.cs)

Key registrations:
```
services
    .AddAuditLogModule(...)
    .AddConfigurationModule(...)
    .AddIdentityModuleCore(...)
    .AddNotificationModule(...)
    .AddProductModule(...)
    .AddStorageModule(...)
    .AddApplicationServices();
```

Module options bound from configuration sections: `Modules:AuditLog`, `Modules:Identity`, etc.

### 5.2 Background Worker Composition Root

**File**: [ClassifiedAds.Background/Program.cs](ClassifiedAds.Background/Program.cs)

Same module registration pattern plus:
- Message bus senders/receivers
- Feature toggles
- Hosted services per module

### 5.3 Migrator Composition Root

**File**: [ClassifiedAds.Migrator/Program.cs](ClassifiedAds.Migrator/Program.cs)

Runs EF Core migrations for all modules:
```
app.MigrateAuditLogDb();
app.MigrateConfigurationDb();
app.MigrateIdentityDb();
app.MigrateNotificationDb();
app.MigrateProductDb();
app.MigrateStorageDb();
```

---

## 6. Docker Compose / Local Dev Dependencies

**File**: [docker-compose.yml](docker-compose.yml)

| Service | Image | Purpose |
|---------|-------|---------|
| `db` | mcr.microsoft.com/mssql/server:2017-latest | SQL Server database |
| `rabbitmq` | rabbitmq:3-management | Message broker |
| `mailhog` | mailhog/mailhog | SMTP testing server |
| `migrator` | classifiedads.modularmonolith.migrator | Database migrations |
| `identityserver` | classifiedads.modularmonolith.identityserver | OpenIddict Identity Server |
| `webapi` | classifiedads.modularmonolith.webapi | Web API |
| `background` | classifiedads.modularmonolith.background | Background workers |

---

## 7. Summary Table

| Pattern/Feature | Present | Location |
|-----------------|---------|----------|
| Modular Monolith | ✅ | `ClassifiedAds.Modules.*` |
| CQRS | ✅ | `ClassifiedAds.Application/Common/Dispatcher.cs` |
| Domain Events | ✅ | `ClassifiedAds.Domain/Events/` |
| Outbox Pattern | ✅ | `**/Entities/OutboxMessage.cs`, `**/HostedServices/PublishEventWorker.cs` |
| Message Bus | ✅ | `ClassifiedAds.Infrastructure/Messaging/` |
| Clean Architecture | ✅ | Domain → Application → Infrastructure layering |
| JWT/OAuth2 | ✅ | `ClassifiedAds.WebAPI/Program.cs` |
| ASP.NET Core Identity | ✅ | `ClassifiedAds.Modules.Identity/` |
| OpenTelemetry | ✅ | `ClassifiedAds.Infrastructure/Monitoring/OpenTelemetry/` |
| Serilog | ✅ | `ClassifiedAds.Infrastructure/Logging/` |
| Health Checks | ✅ | `ClassifiedAds.Infrastructure/HealthChecks/` |
| Distributed Caching | ✅ | `ClassifiedAds.Infrastructure/Caching/` |
| Background Workers | ✅ | `ClassifiedAds.Background/`, module HostedServices |
| Docker Compose | ✅ | `docker-compose.yml` |
| .NET Aspire | ✅ | `ClassifiedAds.AspireAppHost/` |
| Multiple DB Providers | ✅ | `ClassifiedAds.Persistence.*` |
| Rate Limiting | ✅ | `**/RateLimiterPolicies/` |
| SignalR | ✅ | `ClassifiedAds.Modules.Notification/Hubs/NotificationHub.cs` |
| MediatR | ❌ | Custom Dispatcher used instead |
| Integration Events | ✅ | Via Outbox + Message Bus |
| Event Sourcing | ❌ | Not present |
| Saga/Process Manager | ❌ | Not present |

---

## 8. Technology Stack

| Category | Technology |
|----------|------------|
| Framework | .NET 10.0 |
| Web Framework | ASP.NET Core |
| ORM | Entity Framework Core 10.0 |
| Database (default) | SQL Server |
| Message Broker | RabbitMQ / Kafka / Azure Service Bus |
| Caching | Redis / SQL Server / InMemory |
| Logging | Serilog |
| Tracing | OpenTelemetry |
| APM | Azure Application Insights |
| Identity | ASP.NET Core Identity + OpenIddict |
| PDF Generation | DinkToPdf, PuppeteerSharp |
| API Docs | Swashbuckle (Swagger/OpenAPI) |
| Container Orchestration | Docker Compose, .NET Aspire |

---

*End of Architecture Inventory - Step 1 Complete*
