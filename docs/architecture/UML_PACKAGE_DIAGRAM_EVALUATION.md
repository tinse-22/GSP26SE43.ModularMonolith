# UML Package Diagram — Sub-System Evaluation & Package Explanation

> **Purpose:** Provide the package diagram for each sub-system with overall package diagram(s) and detailed explanation for each package (namespace).
>
> **Last Updated:** 2026-02-25
> **Source of Truth:** Static analysis of `*.csproj` ProjectReferences + `namespace` declarations in `*.cs` files (excluding `bin/obj`).
> **Related:** [UML_PACKAGE_DIAGRAM_SOURCE.md](UML_PACKAGE_DIAGRAM_SOURCE.md) (structural analysis & full diagrams)

---

## Table of Contents

- [UML Package Diagram — Sub-System Evaluation \& Package Explanation](#uml-package-diagram--sub-system-evaluation--package-explanation)
  - [Table of Contents](#table-of-contents)
  - [1. Overall System Package Diagram](#1-overall-system-package-diagram)
    - [Overall Package Diagram (PlantUML)](#overall-package-diagram-plantuml)
    - [Overall Package Diagram (Mermaid)](#overall-package-diagram-mermaid)
  - [2. Sub-System Breakdown](#2-sub-system-breakdown)
    - [2.1 Core Layers Sub-System](#21-core-layers-sub-system)
    - [2.2 Feature Modules Sub-System](#22-feature-modules-sub-system)
    - [2.3 Hosts Sub-System](#23-hosts-sub-system)
    - [2.4 Shared Platform Sub-System](#24-shared-platform-sub-system)
    - [2.5 Client Applications Sub-System (Planned)](#25-client-applications-sub-system-planned)
  - [3. Detailed Package (Namespace) Explanation](#3-detailed-package-namespace-explanation)
    - [3.1 Core Layers](#31-core-layers)
      - [3.1.1 ClassifiedAds.Domain](#311-classifiedadsdomain)
      - [3.1.2 ClassifiedAds.Application](#312-classifiedadsapplication)
      - [3.1.3 ClassifiedAds.Infrastructure](#313-classifiedadsinfrastructure)
      - [3.1.4 ClassifiedAds.Persistence.PostgreSQL](#314-classifiedadspersistencepostgresql)
      - [3.1.5 ClassifiedAds.CrossCuttingConcerns](#315-classifiedadscrosscuttingconcerns)
      - [3.1.6 ClassifiedAds.Contracts](#316-classifiedadscontracts)
    - [3.2 Feature Module: ApiDocumentation](#32-feature-module-apidocumentation)
    - [3.3 Feature Module: TestGeneration](#33-feature-module-testgeneration)
    - [3.4 Feature Module: TestExecution](#34-feature-module-testexecution)
    - [3.5 Feature Module: TestReporting](#35-feature-module-testreporting)
    - [3.6 Feature Module: Identity](#36-feature-module-identity)
    - [3.7 Feature Module: AuditLog](#37-feature-module-auditlog)
    - [3.8 Feature Module: Notification](#38-feature-module-notification)
    - [3.9 Feature Module: Subscription](#39-feature-module-subscription)
    - [3.10 Feature Module: Storage](#310-feature-module-storage)
    - [3.11 Feature Module: Configuration](#311-feature-module-configuration)
    - [3.12 Feature Module: LlmAssistant](#312-feature-module-llmassistant)
    - [3.13 Hosts](#313-hosts)
      - [3.13.1 ClassifiedAds.AppHost](#3131-classifiedadsapphost)
      - [3.13.2 ClassifiedAds.WebAPI](#3132-classifiedadswebapi)
      - [3.13.3 ClassifiedAds.Background](#3133-classifiedadsbackground)
      - [3.13.4 ClassifiedAds.Migrator](#3134-classifiedadsmigrator)
    - [3.14 Contracts (Cross-Module Boundary)](#314-contracts-cross-module-boundary)
  - [4. Module Maturity Assessment](#4-module-maturity-assessment)
    - [Internal Structure Consistency](#internal-structure-consistency)
  - [5. Cross-Reference: Package → Feature Mapping](#5-cross-reference-package--feature-mapping)

---

## 1. Overall System Package Diagram

The system follows a **Modular Monolith with Shared Layered Core** architecture. The overall dependency flow is:

```
Client Applications (planned) → Hosts → Feature Modules → Core Layers
```

### Overall Package Diagram (PlantUML)

```plantuml
@startuml overall_package_diagram
top to bottom direction
skinparam packageStyle rectangle
skinparam shadowing false
skinparam defaultFontSize 11
skinparam padding 4

skinparam package {
  BackgroundColor<<client>> #FFFDE7
  BackgroundColor<<host>> #E3F2FD
  BackgroundColor<<module>> #FFF3E0
  BackgroundColor<<core>> #E8F5E9
  BackgroundColor<<shared>> #F3E5F5
  BorderColor<<client>> #F57F17
  BorderColor<<host>> #1565C0
  BorderColor<<module>> #E65100
  BorderColor<<core>> #2E7D32
  BorderColor<<shared>> #7B1FA2
}

package "Client Applications «planned»" <<client>> {
  [WebApp (SPA)] as WebApp
  note right of WebApp
    Tech stack: TBD
    Roles: Admin, Developer,
    Tester, Viewer
  end note
}

package "Hosts" <<host>> {
  [AppHost] as AH
  [WebAPI] as WAPI
  [Background] as BG
  [Migrator] as MIG
}

package "Shared Platform" <<shared>> {
  [ServiceDefaults] as SD
}

package "Feature Modules" <<module>> {
  package "API Quality Lifecycle" as BC1 {
    [ApiDocumentation] as ADOC
    [TestGeneration] as TGEN
    [TestExecution] as TEXE
    [TestReporting] as TREP
  }
  package "Identity & Collaboration" as BC2 {
    [Identity] as IDN
    [AuditLog] as ALOG
    [Notification] as NOTF
  }
  package "Monetization & Assets" as BC3 {
    [Subscription] as SUB
    [Storage] as STR
  }
  package "Support" as BC4 {
    [Configuration] as CFG
    [LlmAssistant] as LLM
  }
}

package "Core Layers" <<core>> {
  [Application] as APP
  [Domain] as DOM
  [Infrastructure] as INF
  [Persistence.PostgreSQL] as PG
  [Contracts] as CTR
  [CrossCuttingConcerns] as CCC
}

' Client → Host
WebApp ..> WAPI : REST API

' Hosts orchestration
AH --> WAPI
AH --> BG
AH --> MIG

' Hosts → ServiceDefaults
WAPI --> SD
BG --> SD
MIG --> SD

' Hosts → Modules (simplified)
WAPI --> BC1 : composes
WAPI --> BC2 : composes
WAPI --> BC3 : composes
WAPI --> BC4 : composes
BG --> BC2 : composes
BG --> BC3 : composes
MIG --> BC1 : composes
MIG --> BC2 : composes
MIG --> BC3 : composes
MIG --> BC4 : composes

' Modules → Core
BC1 --> APP
BC1 --> DOM
BC1 --> INF
BC1 --> PG
BC1 --> CCC
BC2 --> APP
BC2 --> DOM
BC2 --> INF
BC2 --> PG
BC2 --> CCC
BC3 --> APP
BC3 --> DOM
BC3 --> INF
BC3 --> PG
BC3 --> CCC
BC4 --> APP
BC4 --> DOM
BC4 --> INF
BC4 --> PG
BC4 --> CCC

' Core internal
APP --> DOM
DOM --> CCC
INF --> APP
INF --> DOM
PG --> DOM
PG --> CCC

' Cross-module via Contracts (representative sample)
BC1 ..> CTR : uses
BC2 ..> CTR : uses
BC3 ..> CTR : uses
BC4 ..> CTR : uses

legend right
  |= Layer |= Color |= Description |
  | Client | <#FFFDE7> | Planned SPA (unimplemented) |
  | Hosts | <#E3F2FD> | Composition roots / entry points |
  | Modules | <#FFF3E0> | Bounded context packages |
  | Core | <#E8F5E9> | Shared layered kernel |
  | Shared | <#F3E5F5> | Aspire defaults |
end legend
@enduml
```

### Overall Package Diagram (Mermaid)

```mermaid
graph TB
    subgraph Client["Client Applications «planned»"]
        WebApp["WebApp (SPA)"]
    end

    subgraph Hosts["Hosts"]
        AppHost
        WebAPI
        Background
        Migrator
    end

    subgraph Shared["Shared Platform"]
        ServiceDefaults
    end

    subgraph Modules["Feature Modules"]
        subgraph BC_API["API Quality Lifecycle"]
            ApiDoc["ApiDocumentation"]
            TestGen["TestGeneration"]
            TestExec["TestExecution"]
            TestRep["TestReporting"]
        end
        subgraph BC_ID["Identity & Collaboration"]
            Identity
            AuditLog
            Notification
        end
        subgraph BC_MON["Monetization & Assets"]
            Subscription
            Storage
        end
        subgraph BC_SUP["Support"]
            Configuration
            LlmAssistant
        end
    end

    subgraph Core["Core Layers"]
        Application
        Domain
        Infrastructure
        Postgres["Persistence.PostgreSQL"]
        Contracts
        CCC["CrossCuttingConcerns"]
    end

    WebApp -.->|REST| WebAPI
    AppHost --> WebAPI
    AppHost --> Background
    AppHost --> Migrator
    WebAPI --> ServiceDefaults
    Background --> ServiceDefaults
    Migrator --> ServiceDefaults
    WebAPI --> Modules
    Background --> BC_ID
    Background --> BC_MON
    Migrator --> Modules
    Modules --> Core
    Application --> Domain
    Domain --> CCC
    Infrastructure --> Application
    Infrastructure --> Domain
    Postgres --> Domain
    Postgres --> CCC

    classDef client fill:#FFFDE7,stroke:#F57F17,color:#000
    classDef host fill:#E3F2FD,stroke:#1565C0,color:#000
    classDef module fill:#FFF3E0,stroke:#E65100,color:#000
    classDef core fill:#E8F5E9,stroke:#2E7D32,color:#000
    classDef shared fill:#F3E5F5,stroke:#7B1FA2,color:#000

    class WebApp client
    class AppHost,WebAPI,Background,Migrator host
    class ApiDoc,TestGen,TestExec,TestRep,Identity,AuditLog,Notification,Subscription,Storage,Configuration,LlmAssistant module
    class Application,Domain,Infrastructure,Postgres,Contracts,CCC core
    class ServiceDefaults shared
```

---

## 2. Sub-System Breakdown

### 2.1 Core Layers Sub-System

**Purpose:** Provides the shared layered kernel that all feature modules depend on. Implements Clean Architecture with inward dependency direction.

```plantuml
@startuml core_subsystem
top to bottom direction
skinparam packageStyle rectangle
skinparam shadowing false
skinparam defaultFontSize 12

skinparam package {
  BackgroundColor #E8F5E9
  BorderColor #2E7D32
}

package "Core Layers" {
  package "ClassifiedAds.Application" as APP {
    [Dispatcher]
    [ICommandHandler<T>]
    [IQueryHandler<TQ,TR>]
    [CrudService<T>]
    [Decorators]
  }

  package "ClassifiedAds.Domain" as DOM {
    [Entity (base)]
    [IAggregateRoot]
    [IRepository<T>]
    [IUnitOfWork]
    [IDomainEvent]
    [IMessageBus]
    [Result Pattern]
    [Value Objects]
  }

  package "ClassifiedAds.Infrastructure" as INF {
    [Messaging (RabbitMQ/Kafka/ASB)]
    [Storages (Local/Azure/AWS)]
    [Notification (Email/SMS/Web)]
    [Monitoring (OTel)]
    [Caching (Redis)]
    [Web Middleware]
    [HealthChecks]
  }

  package "ClassifiedAds.Persistence.PostgreSQL" as PG {
    [DbContextRepository<TDb,T>]
    [DbContextUnitOfWork<TDb>]
  }

  package "ClassifiedAds.CrossCuttingConcerns" as CCC {
    [IDateTimeProvider]
    [IDistributedLock]
    [Csv/Excel/Html/Pdf]
    [Exceptions]
    [ExtensionMethods]
  }

  package "ClassifiedAds.Contracts" as CTR {
    [ICurrentUser]
    [IUserService]
    [IAuditLogService]
    [IEmailMessageService]
    [IStorageFileGatewayService]
    [ISubscriptionLimitGatewayService]
    [IApiEndpointMetadataService]
  }
}

APP --> DOM : depends
DOM --> CCC : depends
INF --> APP : depends
INF --> DOM : depends
PG --> DOM : depends
PG --> CCC : depends

note bottom of CTR
  Cross-module communication
  boundary — all modules
  depend on contracts, not
  on each other
end note
@enduml
```

| Package | .cs Files | Role |
|---------|-----------|------|
| `ClassifiedAds.Application` | 26 | CQRS pattern (Command/Query separation), `Dispatcher`, decorator pipeline (audit log, DB retry), generic CRUD service |
| `ClassifiedAds.Domain` | 25 | Core domain: entities, domain events, repository interfaces, messaging contracts, Result pattern, value objects |
| `ClassifiedAds.Infrastructure` | 115 | Concrete implementations: messaging (RabbitMQ/Kafka/Azure Service Bus), file storage (Local/Azure/AWS/Firebase), notifications (Email/SMS/SignalR), monitoring (OpenTelemetry), caching (Redis), web middleware |
| `ClassifiedAds.Persistence.PostgreSQL` | 2 | Generic EF Core `IRepository<T>` and `IUnitOfWork` implementations for PostgreSQL |
| `ClassifiedAds.CrossCuttingConcerns` | 25 | Shared utility interfaces (CSV, Excel, PDF, HTML, DateTime, DistributedLock), exceptions, extension methods |
| `ClassifiedAds.Contracts` | 20 | **Cross-module communication boundary** — 7 service interfaces + 8 DTOs + 2 enums organized by module context |

**Dependency Rules:**
- `Application → Domain` (application orchestrates domain logic)
- `Domain → CrossCuttingConcerns` (domain uses shared utilities)
- `Infrastructure → Application, Domain` (implements abstractions defined in inner layers)
- `Persistence.PostgreSQL → Domain, CrossCuttingConcerns` (implements repository interfaces)
- **No outward dependency** from Domain to Infrastructure (Clean Architecture preserved)

---

### 2.2 Feature Modules Sub-System

**Purpose:** Encapsulates business capabilities as independent bounded context packages. Each module owns its schema, entities, DbContext, and business logic.

```plantuml
@startuml modules_subsystem
left to right direction
skinparam packageStyle rectangle
skinparam shadowing false
skinparam defaultFontSize 10

skinparam package {
  BackgroundColor<<api>> #FFF9C4
  BackgroundColor<<id>> #F3E5F5
  BackgroundColor<<mon>> #FFCCBC
  BackgroundColor<<sup>> #E0F7FA
}

package "API Quality Lifecycle" <<api>> {
  package "ApiDocumentation" {
    [3 Controllers\n11 Commands\n10 Queries\n4 Services\n9 Entities]
  }
  package "TestGeneration" {
    [2 Controllers\n6 Commands\n4 Queries\n9 Services\n4 Algorithms\n11 Entities]
  }
  package "TestExecution" {
    [1 Controller\n2 Commands\n2 Queries\n2 Services\n4 Entities]
  }
  package "TestReporting" {
    [0 Controllers\n0 Commands\n0 Queries\n4 Entities\n«skeleton only»]
  }
}

package "Identity & Collaboration" <<id>> {
  package "Identity" {
    [3 Controllers\n10 Commands\n5 Queries\n8 Services\n8 Entities]
  }
  package "AuditLog" {
    [1 Controller\n3 Queries\n1 Service\n2 Entities]
  }
  package "Notification" {
    [0 Controllers\n2 Commands\n2 Services\n3 Entities\nSignalR Hub]
  }
}

package "Monetization & Assets" <<mon>> {
  package "Subscription" {
    [3 Controllers\n13 Commands\n11 Queries\n3 Services\n9 Entities\nPayOS Integration]
  }
  package "Storage" {
    [1 Controller\n1 Command\n3 Queries\n1 Service\n3 Entities\nFile Gateway]
  }
}

package "Support" <<sup>> {
  package "Configuration" {
    [1 Controller\n2 Entities\nExcel Import/Export]
  }
  package "LlmAssistant" {
    [0 Controllers\n0 Commands\n0 Queries\n4 Entities\n«skeleton only»]
  }
}
@enduml
```

**Cross-Module Coupling (via Contracts only):**

| Source Module | Depends On (via Contracts) | Contract Interface Used |
|---------------|---------------------------|------------------------|
| ApiDocumentation | Identity | `ICurrentUser`, `IUserService` |
| ApiDocumentation | Subscription | `ISubscriptionLimitGatewayService` |
| ApiDocumentation | Storage | `IStorageFileGatewayService` |
| ApiDocumentation | AuditLog | `IAuditLogService` |
| TestGeneration | ApiDocumentation | `IApiEndpointMetadataService` |
| TestGeneration | Identity | `ICurrentUser` |
| TestExecution | Identity | `ICurrentUser` |
| Subscription | Identity | `ICurrentUser`, `IUserService` |
| Subscription | Notification | `IEmailMessageService` |
| Subscription | AuditLog | `IAuditLogService` |
| Storage | Identity | `ICurrentUser` |
| Storage | AuditLog | `IAuditLogService` |
| Identity | Notification | `IEmailMessageService` |
| AuditLog | Identity | `IUserService` |

**Key observation:** Zero direct module-to-module compile-time references. All cross-context communication is mediated through `ClassifiedAds.Contracts`.

---

### 2.3 Hosts Sub-System

**Purpose:** Composition roots that wire modules together and serve as entry points for the application.

```plantuml
@startuml hosts_subsystem
left to right direction
skinparam packageStyle rectangle
skinparam shadowing false
skinparam defaultFontSize 11

skinparam package {
  BackgroundColor<<host>> #E3F2FD
  BorderColor<<host>> #1565C0
}

package "Hosts" <<host>> {
  package "AppHost (.NET Aspire)" {
    [Orchestrates:\nPostgreSQL, RabbitMQ,\nRedis, MailHog\n+ WebAPI, Background, Migrator]
  }

  package "WebAPI (REST)" {
    [JWT Auth + Swagger/Scalar\nSignalR Hub + CORS\n9 modules composed\nExposes 15 controllers]
  }

  package "Background (Workers)" {
    [Outbox publishers\nEmail/SMS senders\nMessage bus consumers\nCron jobs\n5 modules composed]
  }

  package "Migrator (DB)" {
    [EF Core migrations\nDbUp SQL scripts\n10 modules composed\nRun-to-completion]
  }
}
@enduml
```

| Host | .cs Files | Modules Composed | Role |
|------|-----------|-----------------|------|
| **AppHost** | 1 | (orchestrates WebAPI, Background, Migrator) | .NET Aspire orchestrator — provisions PostgreSQL, RabbitMQ, Redis, MailHog; manages service startup order |
| **WebAPI** | 5 | ApiDocumentation, TestGeneration, TestExecution, Identity, AuditLog, Notification, Subscription, Storage, Configuration | REST API host — JWT authentication, Swagger/Scalar docs, SignalR hub, CORS, rate limiting, global exception handling |
| **Background** | 4 | Identity, AuditLog, Notification, Subscription, Storage | Background worker host — outbox message publishing, email/SMS sending, message bus consuming, cron jobs |
| **Migrator** | 53 | ApiDocumentation, TestGeneration, TestExecution, TestReporting, Identity, AuditLog, Notification, Subscription, Storage, Configuration | Database migration runner — EF Core migrations for all module DbContexts + DbUp SQL scripts |

**Notable gap:** `LlmAssistant` has a `DbContext` but is **not composed by any host** (not in WebAPI, not in Background, not in Migrator). This means its migrations are not applied.

---

### 2.4 Shared Platform Sub-System

| Package | .cs Files | Role |
|---------|-----------|------|
| `ClassifiedAds.ServiceDefaults` | 1 | .NET Aspire ServiceDefaults — configures OpenTelemetry tracing/metrics/logging, health check endpoints, service discovery, and HTTP client resilience. Referenced by all 3 runtime hosts. |

---

### 2.5 Client Applications Sub-System (Planned)

| Package | Status | Role |
|---------|--------|------|
| `WebApp (SPA)` | **Not yet implemented** — tech stack TBD | Single-page application for all user roles (Admin, Developer, Tester, Viewer). Communicates exclusively with WebAPI host via REST API. |

**Architectural decision:** Single SPA with role-based routing. Admin, Developer, Tester, and Viewer are user roles (defined in `ClassifiedAds.Modules.Identity`) — not separate applications.

---

## 3. Detailed Package (Namespace) Explanation

### 3.1 Core Layers

#### 3.1.1 ClassifiedAds.Domain

| Namespace | Purpose | Key Types |
|-----------|---------|-----------|
| `ClassifiedAds.Domain.Entities` | Base entity abstractions and tracking interfaces | `Entity<T>` (base), `IAggregateRoot`, `IHasKey<T>`, `ITrackable` |
| `ClassifiedAds.Domain.Events` | Domain event contracts | `IDomainEvent`, `IDomainEventHandler<T>`, `EntityCreatedEvent`, `EntityUpdatedEvent`, `EntityDeletedEvent` |
| `ClassifiedAds.Domain.Repositories` | Repository and unit-of-work abstractions | `IRepository<T>`, `IUnitOfWork`, `IConcurrencyHandler<T>` |
| `ClassifiedAds.Domain.Infrastructure.Messaging` | Message bus contracts for inter-module async communication | `IMessageBus`, `IMessageSender`, `IMessageReceiver`, `IMessageBusConsumer<T>`, `IOutboxMessagePublisher`, `Message`, `MetaData`, `PublishingOutboxMessage` |
| `ClassifiedAds.Domain.Infrastructure.ResultPattern` | Functional result pattern for error handling | `Result`, `Result<T>`, `Error` (record), `Codes` |
| `ClassifiedAds.Domain.ValueObjects` | Immutable value objects | `Address`, `Money` |

#### 3.1.2 ClassifiedAds.Application

| Namespace | Purpose | Key Types |
|-----------|---------|-----------|
| `ClassifiedAds.Application` | CQRS dispatcher and handler interfaces | `Dispatcher`, `ICommand`, `ICommandHandler<T>`, `IQuery<T>`, `IQueryHandler<TQ,TR>`, `ICrudService<T>`, `CrudService<T>` |
| `ClassifiedAds.Application.Common.DTOs` | Shared pagination DTO | `Paged<T>` |
| `ClassifiedAds.Application.Decorators.AuditLog` | Audit logging decorator for commands/queries | `AuditLogAttribute`, `AuditLogCommandDecorator<T>`, `AuditLogQueryDecorator<TQ,TR>` |
| `ClassifiedAds.Application.Decorators.DatabaseRetry` | Database retry decorator with transient fault handling | `DatabaseRetryAttribute`, `DatabaseRetryCommandDecorator<T>`, `DatabaseRetryQueryDecorator<TQ,TR>` |
| `ClassifiedAds.Application.FeatureToggles` | Feature toggle abstraction for outbox publishing | `IOutboxPublishingToggle` |

#### 3.1.3 ClassifiedAds.Infrastructure

| Namespace | Purpose | Key Types |
|-----------|---------|-----------|
| `ClassifiedAds.Infrastructure.Caching` | Cache options (in-memory, distributed, Redis) | `CachingOptions`, `InMemoryCacheOptions`, `DistributedCacheOptions`, `RedisOptions` |
| `ClassifiedAds.Infrastructure.Configuration` | External config providers (SQL, HashiCorp Vault) | `SqlConfigurationProvider`, `HashiCorpVaultConfigurationProvider` |
| `ClassifiedAds.Infrastructure.HostedServices` | Background service base classes | `CronJobBackgroundService`, `MessageBusConsumerBackgroundService` |
| `ClassifiedAds.Infrastructure.Messaging` | Message broker implementations | `RabbitMQSender/Receiver`, `KafkaSender/Receiver`, `AzureServiceBusSender/Receiver`, `FakeSender/Receiver` |
| `ClassifiedAds.Infrastructure.Monitoring` | Observability setup | `OpenTelemetryExtensions`, `CustomTelemetryInitializer` |
| `ClassifiedAds.Infrastructure.Notification.Email` | Email sending implementations | `SmtpClientEmailNotification`, `SendGridEmailNotification`, `FakeEmailNotification` |
| `ClassifiedAds.Infrastructure.Notification.Sms` | SMS sending implementations | `TwilioSmsNotification`, `AzureSmsNotification`, `FakeSmsNotification` |
| `ClassifiedAds.Infrastructure.Notification.Web` | Web push/SignalR | `SignalRNotification`, `FakeWebNotification` |
| `ClassifiedAds.Infrastructure.Storages` | File storage implementations | `LocalFileStorageManager`, `AzureBlobStorageManager`, `AmazonS3StorageManager`, `FirebaseStorageManager` |
| `ClassifiedAds.Infrastructure.Web.Authorization` | Custom authorization policies | `CustomAuthorizationPolicyProvider`, `PermissionRequirement`, `PermissionRequirementHandler` |
| `ClassifiedAds.Infrastructure.Web.Middleware` | HTTP middleware pipeline | `GlobalExceptionHandlerMiddleware`, `IPFilteringMiddleware`, `SecurityHeadersMiddleware`, `LoggingStatusCodeMiddleware` |
| `ClassifiedAds.Infrastructure.Web.ResultMapping` | Map Result<T> to HTTP responses | `ResultExtensions` |
| `ClassifiedAds.Infrastructure.Web.Validation` | Validation problem details factory | `ValidationProblemDetailsFactory` |

#### 3.1.4 ClassifiedAds.Persistence.PostgreSQL

| Namespace | Purpose | Key Types |
|-----------|---------|-----------|
| `ClassifiedAds.Persistence.PostgreSQL` | Generic EF Core implementations for PostgreSQL | `DbContextRepository<TDbContext, TEntity>` (implements `IRepository<T>`), `DbContextUnitOfWork<TDbContext>` (implements `IUnitOfWork`) |

#### 3.1.5 ClassifiedAds.CrossCuttingConcerns

| Namespace | Purpose | Key Types |
|-----------|---------|-----------|
| `ClassifiedAds.CrossCuttingConcerns.Csv` | CSV read/write abstractions | `ICsvReader`, `ICsvWriter` |
| `ClassifiedAds.CrossCuttingConcerns.DateTimes` | DateTime abstraction for testability | `IDateTimeProvider` |
| `ClassifiedAds.CrossCuttingConcerns.Excel` | Excel read/write abstractions | `IExcelReader`, `IExcelWriter` |
| `ClassifiedAds.CrossCuttingConcerns.Exceptions` | Typed exceptions for domain errors | `NotFoundException`, `ConflictException`, `ValidationException` |
| `ClassifiedAds.CrossCuttingConcerns.ExtensionMethods` | Utility extension methods | `StringExtensions`, `HttpClientExtensions`, `IQueryableExtensions`, `GuidExtensions`, etc. |
| `ClassifiedAds.CrossCuttingConcerns.Html` | HTML generation abstraction | `IHtmlWriter` |
| `ClassifiedAds.CrossCuttingConcerns.Locks` | Distributed locking abstraction | `IDistributedLock`, `ILockManager`, `CouldNotAcquireLockException` |
| `ClassifiedAds.CrossCuttingConcerns.Pdf` | PDF generation abstraction | `IPdfWriter` |

#### 3.1.6 ClassifiedAds.Contracts

| Namespace | Purpose | Key Interfaces & DTOs |
|-----------|---------|----------------------|
| `ClassifiedAds.Contracts.Identity.Services` | User identity access across modules | `ICurrentUser`, `IUserService` |
| `ClassifiedAds.Contracts.Identity.DTOs` | | `UserDTO`, `UserQueryOptions` |
| `ClassifiedAds.Contracts.AuditLog.Services` | Audit trail logging across modules | `IAuditLogService` |
| `ClassifiedAds.Contracts.AuditLog.DTOs` | | `AuditLogEntryDTO`, `AuditLogEntryQueryOptions` |
| `ClassifiedAds.Contracts.Notification.Services` | Email/notification sending across modules | `IEmailMessageService`, `IEmailTemplateService` |
| `ClassifiedAds.Contracts.Notification.DTOs` | | `EmailMessageDTO` |
| `ClassifiedAds.Contracts.Storage.Services` | File upload/download across modules | `IStorageFileGatewayService` |
| `ClassifiedAds.Contracts.Storage.DTOs` | | `StorageUploadedFileDTO`, `StorageUploadFileRequest` |
| `ClassifiedAds.Contracts.Storage.Enums` | | `FileCategory` |
| `ClassifiedAds.Contracts.Subscription.Services` | Subscription limit checking across modules | `ISubscriptionLimitGatewayService` |
| `ClassifiedAds.Contracts.Subscription.DTOs` | | `IncrementUsageRequest`, `LimitCheckResultDTO` |
| `ClassifiedAds.Contracts.Subscription.Enums` | | `LimitType` |
| `ClassifiedAds.Contracts.ApiDocumentation.Services` | API metadata access across modules | `IApiEndpointMetadataService` |
| `ClassifiedAds.Contracts.ApiDocumentation.DTOs` | | `ApiEndpointMetadataDto` |

---

### 3.2 Feature Module: ApiDocumentation

**Bounded Context:** API Quality Lifecycle
**FEs Covered:** FE-02, FE-03, FE-11, FE-12, FE-13
**Status:** ✅ Fully Implemented (85 .cs files)

```plantuml
@startuml pkg_apidoc
top to bottom direction
skinparam packageStyle rectangle
skinparam shadowing false

package "ClassifiedAds.Modules.ApiDocumentation" {
  package "Controllers" {
    [ProjectsController]
    [SpecificationsController]
    [EndpointsController]
  }

  package "Commands (11)" {
    [AddUpdateProjectCommand]
    [UploadApiSpecificationCommand]
    [AddUpdateEndpointCommand]
    [ImportCurlCommand]
    [CreateManualSpecificationCommand]
    [ActivateSpecificationCommand]
    [...]
  }

  package "Queries (10)" {
    [GetProjectsQuery]
    [GetSpecificationsQuery]
    [GetEndpointsQuery]
    [GetResolvedUrlQuery]
    [GetPathParamMutationsQuery]
    [...]
  }

  package "Entities (9)" {
    [Project]
    [ApiSpecification]
    [ApiEndpoint]
    [EndpointParameter]
    [EndpointResponse]
    [EndpointSecurityReq]
    [SecurityScheme]
  }

  package "Services" {
    [CurlParser]
    [PathParameterTemplateService]
    [ApiEndpointMetadataService]
  }

  package "Persistence" {
    [ApiDocumentationDbContext]
    [Repository<T>]
  }

  package "EventHandlers + Outbox" {
    [ProjectCreated/Updated/DeletedEventHandler]
    [SpecCreated/Updated/DeletedEventHandler]
    [OutboxMessageFactory]
    [PublishEventWorker]
  }
}
@enduml
```

| Namespace | Purpose |
|-----------|---------|
| `.Controllers` | 3 REST controllers: Projects CRUD, Specifications upload/activate/CRUD, Endpoints CRUD + path param resolution |
| `.Commands` | 11 command handlers: project/spec/endpoint mutation, cURL import, manual spec creation, outbox publish |
| `.Queries` | 10 query handlers: project/spec/endpoint retrieval, resolved URL, path parameter mutations |
| `.Entities` | 9 entities: Project → ApiSpecification → ApiEndpoint → EndpointParameter/Response/SecurityReq, SecurityScheme |
| `.Services` | CurlParser (cURL → API definition), PathParameterTemplateService (567 lines: extract/validate/resolve/mutate path params), ApiEndpointMetadataService (contract implementation) |
| `.Persistence` | Own DbContext (`apidocumentation` schema), Repository |
| `.EventHandlers` | 6 domain event handlers for audit trail integration |
| `.IntegrationEvents` | Outbox events: ProjectCreated/Updated/Archived, SpecUploaded/Activated/Deleted |
| `.OutBoxEventPublishers` | 3 publishers: AuditLogEntry, Project, Specification |
| `.Models` | 16 DTOs for API request/response mapping |
| `.Authorization` | Permission constants for RBAC |
| `.RateLimiterPolicies` | Default rate limiter + policy names |
| `.ConfigurationOptions` | Module options + connection string config |

---

### 3.3 Feature Module: TestGeneration

**Bounded Context:** API Quality Lifecycle
**FEs Covered:** FE-04 (scope), FE-05A (order proposal ✅), FE-05B (test case gen 🔨), FE-06 (boundary/negative 📋)
**Status:** 🔨 ~65% Implemented (71 .cs files)

```plantuml
@startuml pkg_testgen
top to bottom direction
skinparam packageStyle rectangle
skinparam shadowing false

package "ClassifiedAds.Modules.TestGeneration" {
  package "Controllers" {
    [TestOrderController]
    [TestSuitesController]
  }

  package "Algorithms" #FFF9C4 {
    [DependencyAwareTopologicalSorter\n(Kahn's algorithm — KAT paper)]
    [SemanticTokenMatcher\n(5-tier matching — SPDG paper)]
    [SchemaRelationshipAnalyzer\n(Warshall's transitive closure — KAT)]
    [ObservationConfirmationPromptBuilder\n(COmbine/RBCTest paper)]
  }

  package "Commands (6)" {
    [ProposeApiTestOrderCommand]
    [ApproveApiTestOrderCommand]
    [RejectApiTestOrderCommand]
    [ReorderApiTestOrderCommand]
    [AddUpdateTestSuiteScopeCommand]
    [ArchiveTestSuiteScopeCommand]
  }

  package "Queries (4)" {
    [GetLatestApiTestOrderProposalQuery]
    [GetApiTestOrderGateStatusQuery]
    [GetTestSuiteScopeQuery]
    [GetTestSuiteScopesQuery]
  }

  package "Entities (11)" {
    [TestSuite]
    [TestSuiteVersion]
    [TestOrderProposal]
    [TestCase]
    [TestCaseRequest]
    [TestCaseExpectation]
    [TestCaseVariable]
    [TestDataSet]
    [TestCaseChangeLog]
  }

  package "Services (9)" {
    [ApiTestOrderService]
    [ApiTestOrderAlgorithm]
    [ApiTestOrderGateService]
    [TestSuiteScopeService]
    [ApiTestOrderModelMapper]
  }

  package "Persistence" {
    [TestGenerationDbContext]
  }
}
@enduml
```

| Namespace | Purpose |
|-----------|---------|
| `.Controllers` | 2 controllers: TestOrderController (5 endpoints: propose/approve/reject/reorder/gate-status), TestSuitesController (CRUD) |
| `.Algorithms` | Paper-based algorithms: `DependencyAwareTopologicalSorter` (Kahn's — KAT paper), `SemanticTokenMatcher` (5-tier — SPDG paper), `SchemaRelationshipAnalyzer` (Warshall's transitive closure — KAT paper), `ObservationConfirmationPromptBuilder` (COmbine/RBCTest paper) |
| `.Algorithms.Models` | `DependencyEdge`, `TokenMatchResult`, `EndpointPromptContext`, `ParameterPromptContext`, `ResponsePromptContext`, `ObservationConfirmationPrompt` |
| `.Commands` | 6 command handlers: order proposal lifecycle (propose/approve/reject/reorder) + test suite scope management |
| `.Queries` | 4 query handlers: order proposal retrieval, gate status check, suite scope CRUD |
| `.Entities` | 11 entities including `TestCase`, `TestCaseRequest`, `TestCaseExpectation`, `TestCaseVariable`, `TestDataSet` (structure ready for FE-05B) |
| `.Services` | 9 classes: order service (orchestration), algorithm (composition), gate service (blocks generation without approved order), scope service, model mapper |
| `.Persistence` | Own DbContext (`testgeneration` schema) |
| `.Models` | 4 DTOs + 6 request models |

---

### 3.4 Feature Module: TestExecution

**Bounded Context:** API Quality Lifecycle
**FEs Covered:** FE-04 (environment CRUD ✅), FE-07 (execution engine 🔨 20%), FE-08 (validation 📋 0%)
**Status:** 🔨 ~25% Implemented (25 .cs files)

| Namespace | Purpose |
|-----------|---------|
| `.Controllers` | 1 controller: `ExecutionEnvironmentsController` (CRUD for execution environments) |
| `.Commands` | 2 command handlers: `AddUpdateExecutionEnvironmentCommand` (313 lines with auth config validation), `DeleteExecutionEnvironmentCommand` |
| `.Queries` | 2 query handlers: `GetExecutionEnvironmentQuery`, `GetExecutionEnvironmentsQuery` |
| `.Entities` | 4 entities: `ExecutionEnvironment`, `TestRun` (status/counters/timestamps), `AuditLogEntry`, `OutboxMessage` |
| `.Services` | `ExecutionAuthConfigService` (136 lines: Bearer/Basic/ApiKey/OAuth2 config) |
| `.Persistence` | Own DbContext (`testexecution` schema) |

**Missing (planned for FE-07/08):** Test run execution engine, HTTP client executor, test case runner, result collection, dependency chaining, rule-based validation engine.

---

### 3.5 Feature Module: TestReporting

**Bounded Context:** API Quality Lifecycle
**FEs Covered:** FE-10 (reports 📋 5%)
**Status:** 📋 Skeleton Only (13 .cs files)

| Namespace | Purpose |
|-----------|---------|
| `.Entities` | 4 entities: `TestReport` (61 lines), `CoverageMetric` (53 lines), `AuditLogEntry`, `OutboxMessage` |
| `.DbConfigurations` | EF Core entity configurations for all entities |
| `.Persistence` | Own DbContext (`testreporting` schema), `Repository<T>` |
| `.ConfigurationOptions` | Module options + connection string config |

**Missing (planned for FE-10):** Controllers, commands, queries, services, report generation logic, PDF/CSV export.

---

### 3.6 Feature Module: Identity

**Bounded Context:** Identity & Collaboration
**FEs Covered:** FE-01
**Status:** ✅ Fully Implemented (80 .cs files)

| Namespace | Purpose |
|-----------|---------|
| `.Controllers` | 3 controllers: `AuthController` (login/register/refresh/confirm-email/avatar), `RolesController` (CRUD + claims), `UsersController` (CRUD + roles/claims) |
| `.Commands.Roles` | 4 role management commands: AddClaim, AddUpdateRole, DeleteClaim, DeleteRole |
| `.Commands.Users` | 6 user management commands: AddClaim, AddRole, DeleteClaim, DeleteRole, DeleteUser, SyncUsers |
| `.Queries` | 5 query handlers: GetPagedUsers, GetRole, GetRoles, GetUser, GetUsers |
| `.Entities` | 8 entities: User, UserProfile, UserClaim, UserLogin, UserRole, UserToken, Role, RoleClaim |
| `.Services` | 8 service classes: `JwtTokenService` (JWT generation/validation), `CurrentWebUser` (implements `ICurrentUser`), `UserService` (implements `IUserService`), `InMemoryTokenBlacklistService`, `AnonymousUser`, `VietnameseIdentityErrorDescriber` |
| `.IdentityProviders.Auth0` | Auth0 external identity provider integration |
| `.IdentityProviders.Azure` | Azure AD B2C external identity provider integration |
| `.PasswordValidators` | `HistoricalPasswordValidator`, `WeakPasswordValidator` |
| `.RateLimiterPolicies` | Auth-specific rate limiters (auth endpoint, password attempts, default) |
| `.HostedServices` | `DevelopmentIdentityBootstrapper` (dev seed), `SyncUsersWorker` |
| `.Persistence` | Own DbContext (`identity` schema), User/Role repositories with custom queries |

---

### 3.7 Feature Module: AuditLog

**Bounded Context:** Identity & Collaboration
**FEs Covered:** Cross-cutting (supports all modules)
**Status:** ✅ Fully Implemented (17 .cs files)

| Namespace | Purpose |
|-----------|---------|
| `.Controllers` | 1 controller: `AuditLogEntriesController` (paginated audit log query) |
| `.Queries` | 3 query handlers: `GetAuditEntriesQuery`, `GetPagedAuditEntriesQuery`, `GetUsersQuery` |
| `.Entities` | 2 entities: `AuditLogEntry`, `IdempotentRequest` |
| `.Services` | `AuditLogService` (implements `IAuditLogService` from Contracts) |
| `.Persistence` | Own DbContext (`auditlog` schema) |
| `.RateLimiterPolicies` | Audit-specific rate limiter |

---

### 3.8 Feature Module: Notification

**Bounded Context:** Identity & Collaboration
**FEs Covered:** Cross-cutting (supports Identity, Subscription email notifications)
**Status:** ✅ Fully Implemented (29 .cs files)

| Namespace | Purpose |
|-----------|---------|
| `.Commands` | 2 commands: `SendEmailMessagesCommand`, `SendSmsMessagesCommand` |
| `.Entities` | 3 entities: EmailMessage (+ ArchivedEmailMessage), EmailMessageAttachment, SmsMessage (+ ArchivedSmsMessage) |
| `.Services` | `EmailMessageService` (implements `IEmailMessageService`), `EmailTemplateService` (implements `IEmailTemplateService`) |
| `.EmailQueue` | In-memory channel-based email queue: `EmailChannel`, `EmailQueueWriter/Reader`, `EmailSendingWorker`, `EmailDbSweepWorker` |
| `.Hubs` | `NotificationHub` (SignalR hub for real-time web notifications) |
| `.HostedServices` | `SendEmailWorker`, `SendSmsWorker` (background workers for reliable delivery) |
| `.Persistence` | Own DbContext (`notification` schema), Email/SMS repositories |

---

### 3.9 Feature Module: Subscription

**Bounded Context:** Monetization & Assets
**FEs Covered:** FE-14
**Status:** ✅ ~95% Implemented (88 .cs files — largest module)

| Namespace | Purpose |
|-----------|---------|
| `.Controllers` | 3 controllers: `SubscriptionsController` (326 lines), `PlansController` (200 lines), `PaymentsController` (302 lines) |
| `.Commands` | 13 command handlers: plan CRUD, subscription lifecycle (create/cancel/upgrade/downgrade), payment intents, PayOS checkout/webhook/reconciliation, limit consumption (`ConsumeLimitAtomically` with Serializable transaction) |
| `.Queries` | 11 query handlers: plans, subscriptions, payments, usage tracking, audit entries |
| `.Entities` | 9 entities: `SubscriptionPlan`, `PlanLimit`, `UserSubscription`, `SubscriptionHistory`, `PaymentIntent`, `PaymentTransaction`, `UsageTracking`, `AuditLogEntry`, `OutboxMessage` |
| `.Services` | `PayOsService` (HMAC-SHA256 + payment links), `SubscriptionLimitGatewayService` (262 lines — implements `ISubscriptionLimitGatewayService`) |
| `.EventHandlers` | 3 handlers: PlanCreated/Updated/Deleted → audit trail |
| `.IntegrationEvents` | Outbox events: PaymentIntent lifecycle, SubscriptionChanged |
| `.HostedServices` | `PublishEventWorker`, `ReconcilePayOsCheckoutWorker` |
| `.Persistence` | Own DbContext (`subscription` schema) |

---

### 3.10 Feature Module: Storage

**Bounded Context:** Monetization & Assets
**FEs Covered:** Cross-cutting (supports file upload for ApiDocumentation, Identity avatar)
**Status:** ✅ Fully Implemented (33 .cs files)

| Namespace | Purpose |
|-----------|---------|
| `.Controllers` | 1 controller: `FilesController` (upload/download/delete) |
| `.Entities` | 3 entities: `FileEntry` (+ `DeletedFileEntry`), `AuditLogEntry`, `OutboxMessage` |
| `.Services` | `StorageFileGatewayService` (implements `IStorageFileGatewayService`) |
| `.Authorization` | `FileEntryAuthorizationHandler` (resource-based authorization) |
| `.EventHandlers` | 3 handlers: FileEntry Created/Updated/Deleted → audit trail |
| `.MessageBusConsumers` | `WebhookConsumer` (external event processing) |
| `.Persistence` | Own DbContext (`storage` schema) |

---

### 3.11 Feature Module: Configuration

**Bounded Context:** Support
**FEs Covered:** System configuration management
**Status:** ✅ Fully Implemented (21 .cs files)

| Namespace | Purpose |
|-----------|---------|
| `.Controllers` | 1 controller: `ConfigurationEntriesController` |
| `.Entities` | 2 entities: `ConfigurationEntry`, `LocalizationEntry` |
| `.Excel` | Excel import/export abstractions + implementations (ClosedXML, ExcelDataReader) |
| `.Persistence` | Own DbContext (`configuration` schema) |

**Note:** This is the only module that does **not** reference `ClassifiedAds.Contracts` at project level.

---

### 3.12 Feature Module: LlmAssistant

**Bounded Context:** Support
**FEs Covered:** FE-06 (partial), FE-09, FE-15, FE-16, FE-17
**Status:** 📋 Skeleton Only (13 .cs files)

| Namespace | Purpose |
|-----------|---------|
| `.Entities` | 4 entities: `LlmInteraction`, `LlmSuggestionCache` (with `SuggestionType` enum), `AuditLogEntry`, `OutboxMessage` |
| `.DbConfigurations` | EF Core entity configurations |
| `.Persistence` | Own DbContext (`llmassistant` schema) |
| `.ConfigurationOptions` | Module options + connection string config |

**Missing (planned):** Controllers, commands, queries, services, LLM API client, prompt execution runtime, suggestion review UI APIs.

**Known issue:** Has DbContext but is **not referenced by Migrator** — migrations will not be applied.

---

### 3.13 Hosts

#### 3.13.1 ClassifiedAds.AppHost

| Namespace | Purpose |
|-----------|---------|
| *(top-level statements)* | .NET Aspire orchestration. Defines infrastructure resources: `postgres` + `classifiedAdsDb`, `rabbitmq`, `redis`, `mailhog`. Orchestrates services: `migrator` (run first), `webapi` (waits for migrator), `background` (waits for migrator). |

#### 3.13.2 ClassifiedAds.WebAPI

| Namespace | Purpose |
|-----------|---------|
| *(top-level statements in Program.cs)* | REST API entry point. Configures: all module `AddXxxModule()` registrations, JWT Bearer authentication, Swagger/Scalar API docs, SignalR hub (`/hubs/notification`), CORS policies, rate limiting middleware, global exception handling. |
| `.ConfigurationOptions` | `AppSettings`, `AuthenticationOptions` (JWT/IdentityServer), `CORS`, `ModulesOptions` |

#### 3.13.3 ClassifiedAds.Background

| Namespace | Purpose |
|-----------|---------|
| *(top-level statements in Program.cs)* | Background worker entry point. Configures: module registrations for background processing, message bus consumers, outbox publishing workers, email/SMS sending workers. |
| `.ConfigurationOptions` | `AppSettings`, `ModulesOptions` |
| `.Identity` | `CurrentUser` — `ICurrentUser` implementation for non-HTTP context (background workers) |

#### 3.13.4 ClassifiedAds.Migrator

| Namespace | Purpose |
|-----------|---------|
| *(top-level statements in Program.cs)* | Database migration runner. Registers all module DbContexts with `MigrationsAssembly("ClassifiedAds.Migrator")` override. Applies EF Core migrations + DbUp SQL scripts. Runs to completion then exits. |
| `.Migrations.<ModuleName>` | Per-module migration classes: Identity (7), Subscription (6), TestGeneration (2), others (1 each) |

---

### 3.14 Contracts (Cross-Module Boundary)

This is the **most architecturally significant** package — it defines the only allowed interface between modules.

```mermaid
graph LR
    subgraph Contracts
        subgraph "Contracts.Identity"
            ICurrentUser
            IUserService
        end
        subgraph "Contracts.AuditLog"
            IAuditLogService
        end
        subgraph "Contracts.Notification"
            IEmailMessageService
            IEmailTemplateService
        end
        subgraph "Contracts.Storage"
            IStorageFileGatewayService
        end
        subgraph "Contracts.Subscription"
            ISubscriptionLimitGatewayService
        end
        subgraph "Contracts.ApiDocumentation"
            IApiEndpointMetadataService
        end
    end

    ApiDocumentation -->|uses| ICurrentUser
    ApiDocumentation -->|uses| ISubscriptionLimitGatewayService
    ApiDocumentation -->|uses| IStorageFileGatewayService
    ApiDocumentation -->|uses| IAuditLogService

    TestGeneration -->|uses| IApiEndpointMetadataService
    TestGeneration -->|uses| ICurrentUser

    TestExecution -->|uses| ICurrentUser

    Subscription -->|uses| ICurrentUser
    Subscription -->|uses| IUserService
    Subscription -->|uses| IEmailMessageService
    Subscription -->|uses| IAuditLogService

    Storage -->|uses| ICurrentUser
    Storage -->|uses| IAuditLogService

    Identity -->|uses| IEmailMessageService

    AuditLog -->|uses| IUserService

    classDef contract fill:#E8F5E9,stroke:#2E7D32,color:#000
    classDef module fill:#FFF3E0,stroke:#E65100,color:#000

    class ICurrentUser,IUserService,IAuditLogService,IEmailMessageService,IEmailTemplateService,IStorageFileGatewayService,ISubscriptionLimitGatewayService,IApiEndpointMetadataService contract
    class ApiDocumentation,TestGeneration,TestExecution,Subscription,Storage,Identity,AuditLog module
```

**Architectural principle:** Modules consume contract interfaces; the implementing module registers its concrete class. The DI container in the host resolves the dependency at runtime. This ensures:
- Zero compile-time coupling between modules
- Each module can be replaced without affecting consumers
- Contract changes are explicit and tracked

---

## 4. Module Maturity Assessment

| Module | .cs Files | Controllers | Commands | Queries | Services | Entities | Maturity |
|--------|-----------|-------------|----------|---------|----------|----------|----------|
| **ApiDocumentation** | 85 | 3 | 11 | 10 | 4 | 9 | ✅ Production-ready |
| **Identity** | 80 | 3 | 10 | 5 | 8 | 8 | ✅ Production-ready |
| **Subscription** | 88 | 3 | 13 | 11 | 3 | 9 | ✅ Production-ready |
| **TestGeneration** | 71 | 2 | 6 | 4 | 9 | 11 | 🔨 ~65% — FE-05A done, FE-05B/06 in progress |
| **Storage** | 33 | 1 | 1 | 3 | 1 | 3 | ✅ Production-ready |
| **Notification** | 29 | 0 | 2 | 0 | 2 | 3 | ✅ Production-ready (event-driven, no REST API) |
| **TestExecution** | 25 | 1 | 2 | 2 | 2 | 4 | 🔨 ~25% — environment CRUD done, engine missing |
| **Configuration** | 21 | 1 | 0 | 0 | 0 | 2 | ✅ Production-ready |
| **AuditLog** | 17 | 1 | 0 | 3 | 1 | 2 | ✅ Production-ready |
| **TestReporting** | 13 | 0 | 0 | 0 | 0 | 4 | 📋 Skeleton — entities & DbContext only |
| **LlmAssistant** | 13 | 0 | 0 | 0 | 0 | 4 | 📋 Skeleton — entities & DbContext only |
| **TOTAL** | **475** | **15** | **45** | **38** | **30** | **59** | |

### Internal Structure Consistency

All modules follow the **same package convention:**

```
ClassifiedAds.Modules.<Context>/
├── Authorization/            — Permission constants
├── Commands/                 — ICommand + ICommandHandler pairs (CQRS write side)
├── ConfigurationOptions/     — Module options + ConnectionStringsOptions
├── Controllers/              — ASP.NET Core REST controllers
├── DbConfigurations/         — EF Core IEntityTypeConfiguration<T>
├── Entities/                 — Domain entities (module-scoped)
├── Models/                   — DTOs for request/response mapping
│   └── Requests/             — API request models
├── Persistence/              — DbContext + Repository<T>
├── Queries/                  — IQuery + IQueryHandler pairs (CQRS read side)
├── Services/                 — Business logic services
└── ServiceCollectionExtensions.cs  — DI registration entry point
```

**Optional packages (present in some modules):**

| Package | Used By | Purpose |
|---------|---------|---------|
| `Algorithms/` | TestGeneration | Paper-based sorting/matching algorithms |
| `EventHandlers/` | ApiDocumentation, Subscription, Storage | Domain event → audit trail/outbox integration |
| `HostedServices/` | ApiDocumentation, Identity, Notification, Subscription, Storage | Background workers (outbox publish, email send, etc.) |
| `IntegrationEvents/` | ApiDocumentation, Subscription | Outbox event definitions for message bus publishing |
| `OutBoxEventPublishers/` | ApiDocumentation, Subscription, Storage | Typed outbox message publishers |
| `Outbox/` | ApiDocumentation, Subscription | `OutboxMessageFactory` |
| `RateLimiterPolicies/` | ApiDocumentation, Identity, AuditLog, Subscription | Rate limiting configurations |
| `EmailQueue/` | Notification | Channel-based in-memory email queue |
| `Hubs/` | Notification | SignalR real-time hub |
| `IdentityProviders/` | Identity | External identity provider integrations (Auth0, Azure AD) |
| `PasswordValidators/` | Identity | Custom password validation rules |
| `Excel/` | Configuration | Excel import/export |
| `MessageBusConsumers/` | Storage | Message bus event consumers |

---

## 5. Cross-Reference: Package → Feature Mapping

| Package (Namespace Root) | Feature IDs | Scope |
|--------------------------|-------------|-------|
| `ClassifiedAds.Modules.Identity` | FE-01 | Auth, RBAC, user management |
| `ClassifiedAds.Modules.ApiDocumentation` | FE-02, FE-03, FE-11, FE-12, FE-13 | API input management, parsing, cURL import |
| `ClassifiedAds.Modules.TestGeneration` | FE-04 (scope), FE-05, FE-06, FE-15, FE-16, FE-17 | Test case generation, order proposal, LLM review |
| `ClassifiedAds.Modules.TestExecution` | FE-04 (env), FE-07, FE-08 | Test execution engine, rule-based validation |
| `ClassifiedAds.Modules.TestReporting` | FE-10 | Reports, PDF/CSV export |
| `ClassifiedAds.Modules.LlmAssistant` | FE-06 (partial), FE-09 | LLM failure explanations, prompt execution |
| `ClassifiedAds.Modules.Subscription` | FE-14 | Plans, billing, usage tracking, PayOS |
| `ClassifiedAds.Modules.Storage` | (supporting) | File upload/download |
| `ClassifiedAds.Modules.Notification` | (supporting) | Email, SMS, SignalR notifications |
| `ClassifiedAds.Modules.AuditLog` | (supporting) | System-wide audit trail |
| `ClassifiedAds.Modules.Configuration` | (supporting) | App configuration management |
| `ClassifiedAds.Domain` | — | Core domain abstractions |
| `ClassifiedAds.Application` | — | CQRS pattern, dispatcher, decorators |
| `ClassifiedAds.Infrastructure` | — | Technical implementations |
| `ClassifiedAds.Persistence.PostgreSQL` | — | Generic repository/UoW for PostgreSQL |
| `ClassifiedAds.CrossCuttingConcerns` | — | Shared utilities, exceptions |
| `ClassifiedAds.Contracts` | — | Cross-module communication boundary |
| `ClassifiedAds.ServiceDefaults` | — | .NET Aspire observability defaults |
| `ClassifiedAds.WebAPI` | — | REST API host |
| `ClassifiedAds.Background` | — | Background worker host |
| `ClassifiedAds.Migrator` | — | Database migration runner |
| `ClassifiedAds.AppHost` | — | Aspire orchestrator |

---

> **Note:** This document is auto-generated from static analysis of `.csproj` references and `namespace` declarations. For dependency flow diagrams with PlantUML/Mermaid source, see [UML_PACKAGE_DIAGRAM_SOURCE.md](UML_PACKAGE_DIAGRAM_SOURCE.md).
