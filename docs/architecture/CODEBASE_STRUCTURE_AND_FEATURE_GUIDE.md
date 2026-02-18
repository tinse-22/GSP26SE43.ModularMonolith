# Cấu Trúc Codebase & Hướng Dẫn Implement Feature Mới

> Tài liệu mô tả chi tiết kiến trúc Modular Monolith của dự án ClassifiedAds và quy trình từng bước khi implement một feature mới.

---

## Mục Lục

1. [Tổng Quan Kiến Trúc](#1-tổng-quan-kiến-trúc)
2. [Cấu Trúc Thư Mục Dự Án](#2-cấu-trúc-thư-mục-dự-án)
3. [Các Layer Chính](#3-các-layer-chính)
4. [Cấu Trúc Bên Trong Một Module](#4-cấu-trúc-bên-trong-một-module)
5. [Design Patterns Được Sử Dụng](#5-design-patterns-được-sử-dụng)
6. [Quy Trình Implement Feature Mới (Step-by-Step)](#6-quy-trình-implement-feature-mới-step-by-step)
7. [Ví Dụ Minh Họa](#7-ví-dụ-minh-họa)
8. [Checklist Tóm Tắt](#8-checklist-tóm-tắt)

---

## 1. Tổng Quan Kiến Trúc

Dự án sử dụng kiến trúc **Modular Monolith** — mỗi business domain được đóng gói thành một **module độc lập** (project riêng), nhưng tất cả chạy trong cùng **một process** (WebAPI host).

```
┌─────────────────────────────────────────────────────┐
│                   ClassifiedAds.WebAPI               │  ← Host duy nhất
│                   (Program.cs)                       │
├──────────┬──────────┬──────────┬──────────┬─────────┤
│ Identity │ Storage  │ Subscr.  │ AuditLog │Notif.   │  ← Các Modules
│ Module   │ Module   │ Module   │ Module   │Module   │
├──────────┴──────────┴──────────┴──────────┴─────────┤
│              ClassifiedAds.Contracts                 │  ← Giao tiếp giữa modules
├─────────────────────────────────────────────────────┤
│              ClassifiedAds.Application               │  ← CQRS Dispatcher
├─────────────────────────────────────────────────────┤
│              ClassifiedAds.Domain                    │  ← Base entities, interfaces
├─────────────────────────────────────────────────────┤
│              ClassifiedAds.Infrastructure            │  ← Cross-cutting concerns
├─────────────────────────────────────────────────────┤
│         ClassifiedAds.Persistence.PostgreSQL         │  ← EF Core base (DbContext, Repository)
├─────────────────────────────────────────────────────┤
│                    PostgreSQL DB                     │  ← Mỗi module có schema riêng
└─────────────────────────────────────────────────────┘
```

**Nguyên tắc cốt lõi:**
- Mỗi module có **DbContext riêng** với **schema riêng** trong cùng database
- Modules giao tiếp qua **Contracts** (interfaces/DTOs), không reference trực tiếp nhau
- Sử dụng **CQRS** (Command/Query Responsibility Segregation) qua `Dispatcher`
- Mỗi module tự đăng ký services thông qua `ServiceCollectionExtensions`

---

## 2. Cấu Trúc Thư Mục Dự Án

```
ClassifiedAds.ModularMonolith/
│
├── ClassifiedAds.Domain/                    # 🏗️ DOMAIN LAYER
│   ├── Entities/
│   │   ├── Entity.cs                        # Base class: Id, RowVersion, CreatedDateTime, UpdatedDateTime
│   │   ├── IAggregateRoot.cs                # Marker interface cho Aggregate Root
│   │   ├── IHasKey.cs                       # Interface có Id
│   │   └── ITrackable.cs                    # Interface tracking thời gian
│   ├── Repositories/
│   │   ├── IRepository.cs                   # Generic repository interface
│   │   ├── IUnitOfWork.cs                   # Unit of Work interface (transaction)
│   │   └── IConcurrencyHandler.cs           # Xử lý optimistic concurrency
│   ├── Events/                              # Domain events
│   ├── Infrastructure/                      # Infrastructure interfaces (messaging, etc.)
│   └── ValueObjects/                        # Value objects
│
├── ClassifiedAds.Application/               # 📋 APPLICATION LAYER (CQRS)
│   ├── ICommandHandler.cs                   # interface ICommandHandler<TCommand>
│   ├── Common/
│   │   ├── ICommand.cs                      # Marker interface cho Command
│   │   ├── IQuery.cs                        # interface IQuery<TResult>
│   │   ├── IQueryHandler.cs                 # interface IQueryHandler<TQuery, TResult>
│   │   └── Dispatcher.cs                    # Dispatch commands/queries tới handlers
│   ├── Decorators/                          # Command/Query decorators (logging, validation)
│   └── FeatureToggles/                      # Feature toggle support
│
├── ClassifiedAds.Contracts/                 # 📝 CONTRACTS (Giao tiếp giữa modules)
│   ├── Subscription/
│   │   ├── DTOs/                            # Data Transfer Objects dùng chung
│   │   ├── Enums/                           # Enums dùng chung
│   │   └── Services/                        # Interface services dùng chung
│   │       └── ISubscriptionLimitGatewayService.cs
│   ├── Identity/
│   ├── AuditLog/
│   ├── Notification/
│   └── Storage/
│
├── ClassifiedAds.Persistence.PostgreSQL/    # 💾 PERSISTENCE BASE
│   ├── DbContextRepository.cs              # Generic Repository implementation (EF Core)
│   ├── DbContextUnitOfWork.cs              # UnitOfWork base (transaction management)
│   └── ClassifiedAds.Persistence.PostgreSQL.csproj
│
├── ClassifiedAds.Infrastructure/            # ⚙️ INFRASTRUCTURE (Cross-cutting)
│   ├── Messaging/                           # Message bus (RabbitMQ, etc.)
│   ├── Caching/                             # Redis/Memory cache
│   ├── Logging/                             # Structured logging
│   ├── Monitoring/                          # OpenTelemetry
│   ├── Notification/                        # Email/SMS services
│   ├── Storages/                            # File storage (S3, Azure Blob, etc.)
│   └── ...                                  # Nhiều cross-cutting concerns khác
│
├── ClassifiedAds.CrossCuttingConcerns/      # 🔄 Cross-cutting utilities
│
├── ╔══════════════════════════════════════╗
│   ║         CÁC MODULES                 ║
│   ╚══════════════════════════════════════╝
│
├── ClassifiedAds.Modules.Identity/          # 👤 Module Quản lý User/Auth
├── ClassifiedAds.Modules.Subscription/      # 💳 Module Quản lý Subscription/Payment
├── ClassifiedAds.Modules.Storage/           # 📁 Module Quản lý File Storage
├── ClassifiedAds.Modules.AuditLog/          # 📊 Module Audit Logging
├── ClassifiedAds.Modules.Notification/      # 🔔 Module Thông báo
├── ClassifiedAds.Modules.Configuration/     # ⚙️ Module Configuration
├── ClassifiedAds.Modules.LlmAssistant/      # 🤖 Module AI/LLM
├── ClassifiedAds.Modules.TestGeneration/    # 🧪 Module Test Generation
├── ClassifiedAds.Modules.TestExecution/     # ▶️ Module Test Execution
├── ClassifiedAds.Modules.TestReporting/     # 📈 Module Test Reporting
├── ClassifiedAds.Modules.ApiDocumentation/  # 📄 Module API Documentation
│
├── ClassifiedAds.WebAPI/                    # 🌐 HOST (Entry point)
│   ├── Program.cs                           # Đăng ký tất cả modules, middleware
│   ├── appsettings.json                     # Configuration
│   └── Dockerfile
│
├── ClassifiedAds.AppHost/                   # 🚀 .NET Aspire Host (orchestration)
├── ClassifiedAds.ServiceDefaults/           # Service defaults cho Aspire
├── ClassifiedAds.Background/                # Background workers
├── ClassifiedAds.Migrator/                  # Database migration tool
│
├── ClassifiedAds.UnitTests/                 # 🧪 Unit Tests
├── ClassifiedAds.IntegrationTests/          # 🧪 Integration Tests
│
└── docs/                                    # 📖 Documentation
```

---

## 3. Các Layer Chính

### 3.1 Domain Layer (`ClassifiedAds.Domain`)

Chứa các **base abstractions** mà tất cả modules đều kế thừa:

```csharp
// Base Entity — tất cả entities đều kế thừa từ đây
public abstract class Entity<TKey> : IHasKey<TKey>, ITrackable
{
    public TKey Id { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; }  // Optimistic concurrency
    public DateTimeOffset CreatedDateTime { get; set; }
    public DateTimeOffset? UpdatedDateTime { get; set; }
}

// Repository Interface — generic, dùng cho mọi entity
public interface IRepository<TEntity, TKey>
    where TEntity : Entity<TKey>, IAggregateRoot
{
    IUnitOfWork UnitOfWork { get; }
    Task AddOrUpdateAsync(TEntity entity, ...);
    Task AddAsync(TEntity entity, ...);
    void Delete(TEntity entity);
    IQueryable<TEntity> GetQueryableSet();
    // + Bulk operations, query helpers
}

// Unit of Work — quản lý transactions
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(...);
    Task BeginTransactionAsync(...);
    Task CommitTransactionAsync(...);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, ...);
}
```

### 3.2 Application Layer (`ClassifiedAds.Application`)

Implement **CQRS pattern** — tách biệt đọc (Query) và ghi (Command):

```csharp
// Command — thay đổi state
public interface ICommand { }
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

// Query — chỉ đọc dữ liệu
public interface IQuery<TResult> { }
public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

// Dispatcher — dispatch command/query tới đúng handler
public class Dispatcher
{
    Task DispatchAsync(ICommand command, ...);
    Task<TResult> DispatchAsync<TResult>(IQuery<TResult> query, ...);
}
```

### 3.3 Persistence Layer (`ClassifiedAds.Persistence.PostgreSQL`)

Cung cấp **base implementation** cho Repository và UnitOfWork bằng EF Core:

```csharp
// Base DbContext có sẵn transaction management
public class DbContextUnitOfWork<TDbContext> : DbContext, IUnitOfWork { ... }

// Base Repository implementation bằng EF Core
public class DbContextRepository<TDbContext, TEntity, TKey> : IRepository<TEntity, TKey> { ... }
```

### 3.4 Contracts Layer (`ClassifiedAds.Contracts`)

**Giao diện giao tiếp giữa các modules** — không chứa implementation:

```
ClassifiedAds.Contracts/
├── Subscription/
│   ├── DTOs/IncrementUsageRequest.cs      # DTO dùng chung
│   ├── Enums/LimitType.cs                 # Enum dùng chung
│   └── Services/ISubscriptionLimitGatewayService.cs  # Interface dùng chung
├── Identity/
│   └── ...
```

> **Quy tắc:** Module A muốn gọi Module B → Module A chỉ reference `Contracts`, KHÔNG reference trực tiếp Module B.

---

## 4. Cấu Trúc Bên Trong Một Module

Lấy ví dụ **Module Subscription** — module phức tạp nhất trong dự án:

```
ClassifiedAds.Modules.Subscription/
│
├── 📦 ClassifiedAds.Modules.Subscription.csproj  # Project file + dependencies
│
├── 🏗️ Entities/                    # DOMAIN ENTITIES (riêng cho module)
│   ├── UserSubscription.cs          # Entity chính, kế thừa Entity<Guid>, IAggregateRoot
│   ├── SubscriptionPlan.cs          # Plan entity
│   ├── PlanLimit.cs                 # Giới hạn của plan
│   ├── SubscriptionHistory.cs       # Lịch sử thay đổi subscription
│   ├── UsageTracking.cs             # Theo dõi usage
│   ├── PaymentTransaction.cs        # Giao dịch thanh toán
│   ├── PaymentIntent.cs             # Payment intent
│   ├── AuditLogEntry.cs             # Audit log riêng cho module
│   └── OutboxMessage.cs             # Outbox pattern cho integration events
│
├── 📋 Commands/                     # CQRS — WRITE operations
│   ├── AddUpdateSubscriptionCommand.cs      # Tạo/cập nhật subscription
│   ├── CancelSubscriptionCommand.cs         # Hủy subscription
│   ├── AddUpdatePlanCommand.cs              # CRUD plan
│   ├── DeletePlanCommand.cs
│   ├── AddPaymentTransactionCommand.cs      # Ghi nhận thanh toán
│   ├── CreatePayOsCheckoutCommand.cs        # Tạo checkout session
│   ├── HandlePayOsWebhookCommand.cs         # Xử lý webhook
│   └── ...
│
├── 🔍 Queries/                      # CQRS — READ operations
│   ├── GetSubscriptionQuery.cs              # Lấy 1 subscription
│   ├── GetCurrentSubscriptionByUserQuery.cs # Subscription hiện tại của user
│   ├── GetPlansQuery.cs                     # Danh sách plans
│   ├── GetPlanQuery.cs                      # Chi tiết 1 plan
│   ├── GetPaymentTransactionsQuery.cs       # Lịch sử thanh toán
│   └── ...
│
├── 🎮 Controllers/                  # API ENDPOINTS
│   ├── SubscriptionsController.cs   # /api/subscriptions/*
│   ├── PlansController.cs           # /api/plans/*
│   └── PaymentsController.cs        # /api/payments/*
│
├── 📊 Models/                       # VIEW MODELS / DTOs (request/response)
│   ├── SubscriptionModel.cs         # Response model
│   ├── CreateUpdateSubscriptionModel.cs  # Request model
│   ├── PlanModel.cs
│   ├── *MappingConfiguration.cs     # AutoMapper/Mapster configs
│   └── ...
│
├── 💾 Persistence/                  # DATABASE (EF Core)
│   ├── SubscriptionDbContext.cs     # DbContext riêng, schema "subscription"
│   └── Repository.cs               # Repository kế thừa DbContextRepository
│
├── 🗃️ DbConfigurations/            # ENTITY CONFIGURATIONS (Fluent API)
│   ├── UserSubscriptionConfiguration.cs
│   ├── SubscriptionPlanConfiguration.cs
│   ├── PlanLimitConfiguration.cs
│   └── ...                          # Mỗi entity có 1 configuration file
│
├── 🔐 Authorization/               # PERMISSIONS
│   └── Permissions.cs               # Định nghĩa quyền: GetSubscription, CreatePlan, etc.
│
├── ⚙️ ConfigurationOptions/        # MODULE OPTIONS
│   ├── SubscriptionModuleOptions.cs # Options chính của module
│   ├── ConnectionStringsOptions.cs  # Connection string
│   └── PayOsOptions.cs             # PayOS config
│
├── 🔧 Services/                     # DOMAIN/APPLICATION SERVICES
│   ├── IPayOsService.cs            # Interface
│   ├── PayOsService.cs             # Implementation
│   └── SubscriptionLimitGatewayService.cs  # Implements contract interface
│
├── 📡 EventHandlers/                # DOMAIN EVENT HANDLERS
│   ├── PlanCreatedEventHandler.cs
│   ├── PlanUpdatedEventHandler.cs
│   └── PlanDeletedEventHandler.cs
│
├── 🔄 IntegrationEvents/           # INTEGRATION EVENTS (cross-module)
│   └── PaymentAndSubscriptionOutboxEvents.cs
│
├── 📤 Outbox/                       # OUTBOX PATTERN
│   └── OutboxMessageFactory.cs
│
├── 📤 OutBoxEventPublishers/        # PUBLISH OUTBOX MESSAGES
│   ├── AuditLogEntryOutBoxMessagePublisher.cs
│   ├── PaymentSubscriptionOutBoxMessagePublisher.cs
│   └── PlanOutBoxMessagePublisher.cs
│
├── ⏰ HostedServices/               # BACKGROUND WORKERS
│   ├── PublishEventWorker.cs        # Publish outbox messages
│   └── ReconcilePayOsCheckoutWorker.cs
│
├── 🚦 RateLimiterPolicies/         # RATE LIMITING
│   ├── RateLimiterPolicyNames.cs
│   └── DefaultRateLimiterPolicy.cs
│
├── 📦 Constants/                    # CONSTANTS
│   └── EventTypeConstants.cs
│
└── 🔌 ServiceCollectionExtensions.cs  # DI REGISTRATION (entry point của module)
```

### Dependency Flow của mỗi Module

```
Module.csproj references:
  ├── ClassifiedAds.Application      (CQRS interfaces, Dispatcher)
  ├── ClassifiedAds.Contracts         (Shared interfaces/DTOs)
  ├── ClassifiedAds.CrossCuttingConcerns
  ├── ClassifiedAds.Domain            (Base Entity, IRepository, IUnitOfWork)
  ├── ClassifiedAds.Infrastructure    (Messaging, Caching, etc.)
  └── ClassifiedAds.Persistence.PostgreSQL  (EF Core base implementations)
```

---

## 5. Design Patterns Được Sử Dụng

| Pattern | Mô tả | Vị trí |
|---------|--------|--------|
| **CQRS** | Tách Read (Query) và Write (Command) | `Commands/`, `Queries/` trong mỗi module |
| **Repository** | Abstract data access | `Domain/Repositories/IRepository.cs` → `Persistence.PostgreSQL/DbContextRepository.cs` |
| **Unit of Work** | Quản lý transaction | `Domain/Repositories/IUnitOfWork.cs` → `Persistence.PostgreSQL/DbContextUnitOfWork.cs` |
| **Mediator/Dispatcher** | Dispatch command/query tới handler | `Application/Common/Dispatcher.cs` |
| **Outbox Pattern** | Reliable event publishing | `Outbox/`, `OutBoxEventPublishers/`, `HostedServices/` |
| **Module Pattern** | Self-contained business modules | Mỗi `ClassifiedAds.Modules.*` project |
| **Decorator** | Cross-cutting concerns cho handlers | `Application/Decorators/` |
| **Options Pattern** | Configuration management | `ConfigurationOptions/` trong mỗi module |

---

## 6. Quy Trình Implement Feature Mới (Step-by-Step)

### Trường hợp 1: Feature thuộc Module đã có

Ví dụ: Thêm tính năng "Upgrade Subscription" vào module Subscription.

```
Thứ tự implement:
═══════════════════════════════════════════════════════════════

 BƯỚC 1 ──→ Entity / Domain Model
 BƯỚC 2 ──→ DbConfiguration (EF Core Fluent API)
 BƯỚC 3 ──→ Migration (Database)
 BƯỚC 4 ──→ Models (Request/Response DTOs)
 BƯỚC 5 ──→ Command + CommandHandler (nếu là write operation)
        ──→ Query + QueryHandler (nếu là read operation)
 BƯỚC 6 ──→ Controller Action (API Endpoint)
 BƯỚC 7 ──→ Authorization / Permissions
 BƯỚC 8 ──→ Service Collection Registration (DI)
 BƯỚC 9 ──→ Unit Tests
 BƯỚC 10 ──→ Integration Tests
```

#### BƯỚC 1: Entity / Domain Model

Tạo hoặc cập nhật entity trong `Entities/`:

```csharp
// Modules.Subscription/Entities/UpgradeRequest.cs
public class UpgradeRequest : Entity<Guid>, IAggregateRoot
{
    public Guid UserId { get; set; }
    public Guid FromPlanId { get; set; }
    public Guid ToPlanId { get; set; }
    public UpgradeStatus Status { get; set; }
    public decimal ProratedAmount { get; set; }
    
    // Navigation properties
    public SubscriptionPlan FromPlan { get; set; }
    public SubscriptionPlan ToPlan { get; set; }
}
```

> **Lưu ý:** Entity PHẢI kế thừa `Entity<Guid>` và implement `IAggregateRoot`.

#### BƯỚC 2: DbConfiguration (EF Core Fluent API)

Tạo configuration trong `DbConfigurations/`:

```csharp
// Modules.Subscription/DbConfigurations/UpgradeRequestConfiguration.cs
public class UpgradeRequestConfiguration : IEntityTypeConfiguration<UpgradeRequest>
{
    public void Configure(EntityTypeBuilder<UpgradeRequest> builder)
    {
        builder.ToTable("UpgradeRequests");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProratedAmount).HasPrecision(10, 2);
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.FromPlan).WithMany().HasForeignKey(x => x.FromPlanId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToPlan).WithMany().HasForeignKey(x => x.ToPlanId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
```

Thêm `DbSet` vào DbContext:

```csharp
// Modules.Subscription/Persistence/SubscriptionDbContext.cs
public DbSet<UpgradeRequest> UpgradeRequests { get; set; }
```

#### BƯỚC 3: Migration (Database)

```bash
# Tạo migration
dotnet ef migrations add AddUpgradeRequests \
  --project ClassifiedAds.Modules.Subscription \
  --startup-project ClassifiedAds.WebAPI

# Hoặc chạy migration qua Migrator
dotnet run --project ClassifiedAds.Migrator
```

#### BƯỚC 4: Models (Request/Response DTOs)

```csharp
// Modules.Subscription/Models/UpgradeRequestModel.cs (Response)
public class UpgradeRequestModel
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FromPlanName { get; set; }
    public string ToPlanName { get; set; }
    public decimal ProratedAmount { get; set; }
    public string Status { get; set; }
}

// Modules.Subscription/Models/CreateUpgradeRequestModel.cs (Request)
public class CreateUpgradeRequestModel
{
    public Guid ToPlanId { get; set; }
}
```

#### BƯỚC 5: Command + Handler HOẶC Query + Handler

**Nếu là WRITE operation (tạo/sửa/xóa):**

```csharp
// Modules.Subscription/Commands/CreateUpgradeRequestCommand.cs
public class CreateUpgradeRequestCommand : ICommand
{
    public Guid UserId { get; set; }
    public CreateUpgradeRequestModel Model { get; set; }
    public Guid SavedId { get; set; }  // Output
}

public class CreateUpgradeRequestCommandHandler : ICommandHandler<CreateUpgradeRequestCommand>
{
    private readonly IRepository<UpgradeRequest, Guid> _repository;
    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;

    public CreateUpgradeRequestCommandHandler(
        IRepository<UpgradeRequest, Guid> repository,
        IRepository<UserSubscription, Guid> subscriptionRepository)
    {
        _repository = repository;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task HandleAsync(CreateUpgradeRequestCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Validate
        // 2. Load current subscription
        // 3. Calculate prorated amount
        // 4. Create UpgradeRequest entity
        // 5. Save trong transaction
        await _repository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.AddAsync(entity, ct);
            await _repository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);
        
        command.SavedId = entity.Id;
    }
}
```

**Nếu là READ operation (đọc dữ liệu):**

```csharp
// Modules.Subscription/Queries/GetUpgradeRequestQuery.cs
public class GetUpgradeRequestQuery : IQuery<UpgradeRequestModel>
{
    public Guid Id { get; set; }
}

public class GetUpgradeRequestQueryHandler : IQueryHandler<GetUpgradeRequestQuery, UpgradeRequestModel>
{
    private readonly IRepository<UpgradeRequest, Guid> _repository;

    public async Task<UpgradeRequestModel> HandleAsync(
        GetUpgradeRequestQuery query, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.FirstOrDefaultAsync(
            _repository.GetQueryableSet().Where(x => x.Id == query.Id));
        
        if (entity == null) throw new NotFoundException(...);
        
        return new UpgradeRequestModel { ... }; // Map entity → model
    }
}
```

#### BƯỚC 6: Controller Action (API Endpoint)

```csharp
// Modules.Subscription/Controllers/SubscriptionsController.cs (thêm action mới)

[Authorize(Permissions.CreateUpgradeRequest)]
[HttpPost("upgrade")]
[ProducesResponseType(StatusCodes.Status201Created)]
public async Task<ActionResult<UpgradeRequestModel>> CreateUpgradeRequest(
    [FromBody] CreateUpgradeRequestModel model)
{
    var userId = User.GetUserId();  // Lấy từ JWT claims
    
    var command = new CreateUpgradeRequestCommand
    {
        UserId = userId,
        Model = model
    };
    
    await _dispatcher.DispatchAsync(command);
    
    var result = await _dispatcher.DispatchAsync(
        new GetUpgradeRequestQuery { Id = command.SavedId });
    
    return Created($"/api/subscriptions/upgrade/{command.SavedId}", result);
}
```

#### BƯỚC 7: Authorization / Permissions

```csharp
// Modules.Subscription/Authorization/Permissions.cs — thêm permission mới
public static class Permissions
{
    // ... existing permissions
    public const string CreateUpgradeRequest = "Permissions.Subscription.CreateUpgradeRequest";
}
```

#### BƯỚC 8: DI Registration

```csharp
// Modules.Subscription/ServiceCollectionExtensions.cs — đăng ký repository mới
services.AddScoped<IRepository<UpgradeRequest, Guid>, Repository<UpgradeRequest, Guid>>();
```

> **Lưu ý:** Command/Query handlers được tự động đăng ký qua `AddApplicationServices()` (assembly scanning).

#### BƯỚC 9-10: Tests

```
ClassifiedAds.UnitTests/
└── Subscription/
    └── CreateUpgradeRequestCommandHandlerTests.cs

ClassifiedAds.IntegrationTests/
└── Subscription/
    └── UpgradeRequestApiTests.cs
```

---

### Trường hợp 2: Feature cần Module hoàn toàn mới

Ví dụ: Tạo module "Reporting".

```
Thứ tự implement:
═══════════════════════════════════════════════════════════════

 BƯỚC 1  ──→ Tạo Project (.csproj) + thêm vào solution
 BƯỚC 2  ──→ Tạo Entities
 BƯỚC 3  ──→ Tạo DbContext + DbConfigurations
 BƯỚC 4  ──→ Tạo Repository (kế thừa DbContextRepository)
 BƯỚC 5  ──→ Tạo ConfigurationOptions
 BƯỚC 6  ──→ Tạo ServiceCollectionExtensions (DI registration)
 BƯỚC 7  ──→ Tạo Models (DTOs)
 BƯỚC 8  ──→ Tạo Commands + Queries
 BƯỚC 9  ──→ Tạo Controllers
 BƯỚC 10 ──→ Tạo Authorization/Permissions
 BƯỚC 11 ──→ (Optional) Tạo Contracts nếu module khác cần giao tiếp
 BƯỚC 12 ──→ Đăng ký module trong WebAPI/Program.cs
 BƯỚC 13 ──→ Tạo Migration
 BƯỚC 14 ──→ Tests
```

#### BƯỚC 1: Tạo Project

```bash
dotnet new classlib -n ClassifiedAds.Modules.Reporting -f net10.0
dotnet sln add ClassifiedAds.Modules.Reporting

# Thêm project references
cd ClassifiedAds.Modules.Reporting
dotnet add reference ../ClassifiedAds.Application
dotnet add reference ../ClassifiedAds.Contracts
dotnet add reference ../ClassifiedAds.CrossCuttingConcerns
dotnet add reference ../ClassifiedAds.Domain
dotnet add reference ../ClassifiedAds.Infrastructure
dotnet add reference ../ClassifiedAds.Persistence.PostgreSQL
```

Cấu trúc thư mục cần tạo:

```
ClassifiedAds.Modules.Reporting/
├── Authorization/
│   └── Permissions.cs
├── Commands/
├── ConfigurationOptions/
│   ├── ConnectionStringsOptions.cs
│   └── ReportingModuleOptions.cs
├── Constants/
├── Controllers/
├── DbConfigurations/
├── Entities/
├── Models/
├── Persistence/
│   ├── ReportingDbContext.cs
│   └── Repository.cs
├── Queries/
├── RateLimiterPolicies/
├── Services/
└── ServiceCollectionExtensions.cs
```

#### BƯỚC 6: ServiceCollectionExtensions

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReportingModule(
        this IServiceCollection services,
        Action<ReportingModuleOptions> configureOptions)
    {
        var settings = new ReportingModuleOptions();
        configureOptions(settings);

        services.AddDbContext<ReportingDbContext>(options =>
            options.UseNpgsql(settings.ConnectionStrings.Default, sql => { ... }));

        // Register repositories
        services.AddScoped<IRepository<Report, Guid>, Repository<Report, Guid>>();

        return services;
    }

    public static IMvcBuilder AddReportingModule(this IMvcBuilder builder)
        => builder.AddApplicationPart(Assembly.GetExecutingAssembly());
}
```

#### BƯỚC 12: Đăng ký trong Program.cs

```csharp
// ClassifiedAds.WebAPI/Program.cs

// 1. Đăng ký controllers
services.AddControllers()
    // ... existing modules
    .AddReportingModule();  // ← THÊM MỚI

// 2. Đăng ký services
services
    // ... existing modules
    .AddReportingModule(opt =>  // ← THÊM MỚI
    {
        opt.ConnectionStrings = new ConnectionStringsOptions
        {
            Default = connectionString
        };
    });
```

---

### Trường hợp 3: Feature cần giao tiếp giữa modules

Khi Module A cần sử dụng data/service từ Module B:

```
Thứ tự implement:
═══════════════════════════════════════════════════════════════

 BƯỚC 1  ──→ Định nghĩa Interface + DTOs trong ClassifiedAds.Contracts
 BƯỚC 2  ──→ Implement interface trong Module B (provider)
 BƯỚC 3  ──→ Đăng ký implementation trong Module B ServiceCollectionExtensions
 BƯỚC 4  ──→ Inject interface vào Module A (consumer)
```

```csharp
// BƯỚC 1: ClassifiedAds.Contracts/Reporting/Services/IReportDataService.cs
public interface IReportDataService
{
    Task<ReportDataDTO> GetSubscriptionReportDataAsync(Guid userId);
}

// BƯỚC 2: ClassifiedAds.Modules.Subscription/Services/ReportDataService.cs
public class ReportDataService : IReportDataService
{
    public async Task<ReportDataDTO> GetSubscriptionReportDataAsync(Guid userId) { ... }
}

// BƯỚC 3: Subscription ServiceCollectionExtensions
services.AddScoped<IReportDataService, ReportDataService>();

// BƯỚC 4: Reporting module Command/Query inject IReportDataService
public class GenerateReportCommandHandler : ICommandHandler<GenerateReportCommand>
{
    private readonly IReportDataService _reportDataService;  // From Contracts
    // ...
}
```

---

## 7. Ví Dụ Minh Họa

### Flow hoàn chỉnh: API Request → Response

```
Client gửi POST /api/subscriptions/upgrade
    │
    ▼
┌─ SubscriptionsController ─────────────────────┐
│  - Nhận request, parse model                   │
│  - Tạo CreateUpgradeRequestCommand             │
│  - Gọi _dispatcher.DispatchAsync(command)      │
└────────────────────────┬───────────────────────┘
                         │
                         ▼
┌─ Dispatcher ───────────────────────────────────┐
│  - Resolve ICommandHandler<CreateUpgrade...>   │
│    từ DI container                             │
│  - Gọi handler.HandleAsync(command)            │
└────────────────────────┬───────────────────────┘
                         │
                         ▼
┌─ CreateUpgradeRequestCommandHandler ───────────┐
│  - Validate business rules                     │
│  - Sử dụng IRepository<UpgradeRequest, Guid>   │
│  - Tạo entity, gọi repository.AddAsync()       │
│  - Gọi UnitOfWork.SaveChangesAsync()           │
│  - Set command.SavedId = entity.Id              │
└────────────────────────┬───────────────────────┘
                         │
                         ▼
┌─ IRepository (implemented by Repository) ──────┐
│  - Repository kế thừa DbContextRepository      │
│  - Sử dụng SubscriptionDbContext (EF Core)     │
│  - Schema: "subscription"                      │
│  - Table: "UpgradeRequests"                    │
└────────────────────────┬───────────────────────┘
                         │
                         ▼
                   PostgreSQL DB
                   (schema: subscription)
```

---

## 8. Checklist Tóm Tắt

### Khi thêm feature vào module đã có:

- [ ] **Entity** — Tạo/cập nhật entity kế thừa `Entity<Guid>, IAggregateRoot`
- [ ] **DbConfiguration** — Tạo `IEntityTypeConfiguration<T>` (table name, indexes, constraints)
- [ ] **DbContext** — Thêm `DbSet<T>` vào module DbContext
- [ ] **Migration** — Chạy `dotnet ef migrations add`
- [ ] **Models** — Tạo request/response DTOs trong `Models/`
- [ ] **Command/Query** — Tạo command + handler (write) hoặc query + handler (read)
- [ ] **Controller** — Thêm API endpoint mới
- [ ] **Permissions** — Thêm permission trong `Authorization/Permissions.cs`
- [ ] **DI Registration** — Đăng ký repository mới trong `ServiceCollectionExtensions.cs`
- [ ] **Tests** — Viết unit tests và integration tests

### Khi tạo module mới:

- [ ] Tất cả ở trên, **CỘNG THÊM:**
- [ ] **Project** — Tạo `.csproj` với đúng project references
- [ ] **DbContext** — Tạo module DbContext kế thừa `DbContextUnitOfWork<T>`, với schema riêng
- [ ] **Repository** — Tạo `Repository<T, TKey>` kế thừa `DbContextRepository`
- [ ] **ConfigurationOptions** — Tạo module options class
- [ ] **ServiceCollectionExtensions** — Tạo extension methods đăng ký DI
- [ ] **Program.cs** — Đăng ký module trong WebAPI host

### Khi cần giao tiếp giữa modules:

- [ ] **Contracts** — Tạo interface + DTOs trong `ClassifiedAds.Contracts/{ModuleName}/`
- [ ] **Implementation** — Implement interface trong module provider
- [ ] **Registration** — Đăng ký trong provider module's `ServiceCollectionExtensions`
- [ ] **Usage** — Inject interface trong consumer module

---

> **Ghi nhớ quan trọng:**
> - Modules **KHÔNG** reference trực tiếp nhau — chỉ thông qua `Contracts`
> - Mỗi module có **schema** riêng trong database
> - Command/Query handlers được **tự động đăng ký** qua assembly scanning
> - Luôn dùng **`Dispatcher`** để dispatch, không gọi handler trực tiếp
> - Entity phải đánh dấu **`IAggregateRoot`** mới dùng được với `IRepository`
