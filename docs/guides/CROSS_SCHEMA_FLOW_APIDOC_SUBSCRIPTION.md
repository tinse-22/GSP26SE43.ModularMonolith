# Cross-Schema Flow Deep Dive (apidoc <-> subscription)

## 1) Muc tieu tai lieu

Tai lieu nay mo ta 1 luong chay that su trong project theo dang:

`A -> B -> C -> D -> ...`

Chon luong:

- Tao Project moi (`POST /api/projects`)
- Kiem tra + consume limit o module Subscription
- Sau do tao du lieu o schema `apidoc`
- Bonus: luong async Outbox sau khi tao Project

## 2) 2 schema trong luong nay

- Schema 1: `apidoc` (Project, OutboxMessages, ...)
- Schema 2: `subscription` (UserSubscriptions, SubscriptionPlans, PlanLimits, UsageTrackings, ...)

Code xac nhan schema:

```csharp
// ClassifiedAds.Modules.ApiDocumentation/Persistence/ApiDocumentationDbContext.cs:30
builder.HasDefaultSchema("apidoc");

// ClassifiedAds.Modules.Subscription/Persistence/SubscriptionDbContext.cs:36
builder.HasDefaultSchema("subscription");
```

Minh hoa migration phia `apidoc`:

```csharp
// ClassifiedAds.Migrator/Migrations/ApiDocumentation/20260214004925_InitialApiDocumentation.cs:216
OwnerId = table.Column<Guid>(type: "uuid", nullable: false),

// ClassifiedAds.Migrator/Migrations/ApiDocumentation/20260214004925_InitialApiDocumentation.cs:230-234
table.ForeignKey(
    name: "FK_Projects_ApiSpecifications_ActiveSpecId",
    column: x => x.ActiveSpecId,
    principalSchema: "apidoc",
    principalTable: "ApiSpecifications",
    principalColumn: "Id",
    onDelete: ReferentialAction.SetNull);
```

## 3) Tong quan ket noi giua 2 schema

Ket noi khong di qua FK DB cross-schema. Thay vao do:

- Cross-module contract: `ISubscriptionLimitGatewayService`
- In-process call qua DI + `Dispatcher`
- Cung 1 database connection string (`ConnectionStrings:Default`) cho cac module

Code:

```csharp
// ClassifiedAds.Contracts/Subscription/Services/ISubscriptionLimitGatewayService.cs:42
Task<LimitCheckResultDTO> TryConsumeLimitAsync(
    Guid userId,
    LimitType limitType,
    decimal incrementValue = 1,
    CancellationToken cancellationToken = default);
```

```csharp
// ClassifiedAds.WebAPI/Program.cs:218,251,255,259,263
var sharedConnectionString = configuration.GetConnectionString("Default");

.AddSubscriptionModule(opt =>
{
    opt.ConnectionStrings.Default = sharedConnectionString;
})
.AddApiDocumentationModule(opt =>
{
    opt.ConnectionStrings.Default = sharedConnectionString;
})
```

## 4) Luong chinh A -> B -> C -> D -> E -> F -> G

### A. HTTP vao WebAPI

Client goi:

`POST /api/projects`

Code:

```csharp
// ClassifiedAds.Modules.ApiDocumentation/Controllers/ProjectsController.cs:84-95
[HttpPost]
public async Task<ActionResult<ProjectModel>> Post([FromBody] CreateUpdateProjectModel model)
{
    var command = new AddUpdateProjectCommand
    {
        Model = model,
        CurrentUserId = _currentUser.UserId,
    };
    await _dispatcher.DispatchAsync(command);
    ...
}
```

### B. Dispatcher resolve command handler

`Dispatcher` tim `ICommandHandler<AddUpdateProjectCommand>` trong DI va goi `HandleAsync`.

Code:

```csharp
// ClassifiedAds.Application/Common/Dispatcher.cs:37-44
public async Task DispatchAsync(ICommand command, CancellationToken cancellationToken = default)
{
    Type type = typeof(ICommandHandler<>);
    Type[] typeArgs = { command.GetType() };
    Type handlerType = type.MakeGenericType(typeArgs);

    dynamic handler = _provider.GetService(handlerType);
    await handler.HandleAsync((dynamic)command, cancellationToken);
}
```

### C. ApiDocumentation command handler goi contract sang Subscription

Trong luc tao project moi, handler goi:

- `TryConsumeLimitAsync(userId, LimitType.MaxProjects, 1)`

Code:

```csharp
// ClassifiedAds.Modules.ApiDocumentation/Commands/AddUpdateProjectCommand.cs:93-95
var limitCheck = await _subscriptionLimitService.TryConsumeLimitAsync(
    command.CurrentUserId,
    LimitType.MaxProjects,
    incrementValue: 1,
    cancellationToken);
```

DI registration cua contract implementation:

```csharp
// ClassifiedAds.Modules.Subscription/ServiceCollectionExtensions.cs:56
services.AddScoped<ISubscriptionLimitGatewayService, SubscriptionLimitGatewayService>();
```

### D. Subscription gateway dispatch vao command noi bo module subscription

Code:

```csharp
// ClassifiedAds.Modules.Subscription/Services/SubscriptionLimitGatewayService.cs:250-257
var command = new ConsumeLimitAtomicallyCommand
{
    UserId = userId,
    LimitType = limitType,
    IncrementValue = incrementValue,
};

await _dispatcher.DispatchAsync(command, cancellationToken);
```

### E. ConsumeLimitAtomicallyCommandHandler xu ly trong schema `subscription`

Handler chay transaction `Serializable`, retry conflict, doc va ghi cac bang subscription:

- `UserSubscriptions` (subscription hien tai)
- `SubscriptionPlans`
- `PlanLimits`
- `UsageTrackings` (consume increment)

Code:

```csharp
// ClassifiedAds.Modules.Subscription/Commands/ConsumeLimitAtomicallyCommand.cs:67-70
command.Result = await _usageTrackingRepository.UnitOfWork.ExecuteInTransactionAsync(
    async ct => await ExecuteAtomicConsume(command, ct),
    IsolationLevel.Serializable,
    cancellationToken);
```

```csharp
// ClassifiedAds.Modules.Subscription/Commands/ConsumeLimitAtomicallyCommand.cs:88-107
var subscription = await _subscriptionRepository.FirstOrDefaultAsync(
    _subscriptionRepository.GetQueryableSet()
        .Where(x => x.UserId == command.UserId && CurrentStatuses.Contains(x.Status))
        .OrderByDescending(x => x.CreatedDateTime));

var plan = await _planRepository.FirstOrDefaultAsync(
    _planRepository.GetQueryableSet().Where(p => p.Id == subscription.PlanId));
```

```csharp
// ClassifiedAds.Modules.Subscription/Commands/ConsumeLimitAtomicallyCommand.cs:134-181
var tracking = await _usageTrackingRepository.FirstOrDefaultAsync(...);
...
IncrementUsageField(command.LimitType, tracking, command.IncrementValue);
...
await _usageTrackingRepository.UnitOfWork.SaveChangesAsync(ct);
```

### F. Quay lai handler ApiDocumentation va tao project o schema `apidoc`

Neu limit allowed, handler tao `Project`:

```csharp
// ClassifiedAds.Modules.ApiDocumentation/Commands/AddUpdateProjectCommand.cs:106-114
var project = new Project
{
    Name = command.Model.Name.Trim(),
    Description = command.Model.Description?.Trim(),
    BaseUrl = command.Model.BaseUrl?.Trim(),
    OwnerId = command.CurrentUserId,
    Status = ProjectStatus.Active,
};

await _projectService.AddAsync(project, cancellationToken);
```

`CrudService.AddAsync` se:

1. save DB
2. phat domain event `EntityCreatedEvent<Project>`

```csharp
// ClassifiedAds.Application/Common/Services/CrudService.cs:50-54
await _repository.AddAsync(entity, cancellationToken);
await _unitOfWork.SaveChangesAsync(cancellationToken);
await _dispatcher.DispatchAsync(new EntityCreatedEvent<T>(entity, DateTime.UtcNow), cancellationToken);
```

### G. Controller tra response

Controller query lai project vua tao va tra `201 Created`.

## 5) Bonus: Luong async Outbox sau khi tao Project

Phan nay khong bat buoc de tao project thanh cong, nhung la luong giao tiep module quan trong.

### H. Domain event handler ghi OutboxMessage (schema `apidoc`)

```csharp
// ClassifiedAds.Modules.ApiDocumentation/EventHandlers/ProjectCreatedEventHandler.cs:41-53
await _outboxMessageRepository.AddOrUpdateAsync(new OutboxMessage
{
    EventType = EventTypeConstants.AuditLogEntryCreated,
    ...
}, cancellationToken);

await _outboxMessageRepository.AddOrUpdateAsync(new OutboxMessage
{
    EventType = EventTypeConstants.ProjectCreated,
    ...
}, cancellationToken);
```

### I. Background worker publish outbox

```csharp
// ClassifiedAds.Modules.ApiDocumentation/HostedServices/PublishEventWorker.cs:56
var publishEventsCommand = new PublishEventsCommand();
await dispatcher.DispatchAsync(publishEventsCommand, cancellationToken);
```

```csharp
// ClassifiedAds.Modules.ApiDocumentation/Commands/PublishEventsCommand.cs:41,56,58
var events = GetPendingEvents();
await _messageBus.SendAsync(outbox, cancellationToken);
eventLog.Published = true;
```

### J. MessageBus route theo (EventSource + EventType)

```csharp
// ClassifiedAds.Domain/Infrastructure/Messaging/MessageBus.cs:116
var key = outbox.EventSource + ":" + outbox.EventType;
```

### K. Outbox publisher goi service module khac (AuditLog)

```csharp
// ClassifiedAds.Modules.ApiDocumentation/OutBoxEventPublishers/AuditLogEntryOutBoxMessagePublisher.cs:38
await _externalAuditLogService.AddAsync(logEntry, outbox.Id);
```

`AuditLogService` co idempotency (khong ghi trung):

```csharp
// ClassifiedAds.Modules.AuditLog/Services/AuditLogService.cs:30-33
var requestProcessed = await _idempotentRequestRepository.GetQueryableSet()
    .AnyAsync(x => x.RequestType == requestType && x.RequestId == requestId);
if (requestProcessed) return;
```

## 6) Sequence tom tat (de doc nhanh)

`Client`
-> `ProjectsController.Post` (ApiDoc)
-> `Dispatcher`
-> `AddUpdateProjectCommandHandler` (ApiDoc)
-> `ISubscriptionLimitGatewayService.TryConsumeLimitAsync` (Contract)
-> `SubscriptionLimitGatewayService` (Subscription)
-> `Dispatcher`
-> `ConsumeLimitAtomicallyCommandHandler` (Subscription, transaction serializable)
-> update `subscription.UsageTrackings`
-> return `IsAllowed`
-> `AddUpdateProjectCommandHandler` tao `apidoc.Projects`
-> `CrudService` phat `EntityCreatedEvent<Project>`
-> `ProjectCreatedEventHandler` ghi `apidoc.OutboxMessages`
-> `PublishEventWorker` + `PublishEventsCommand`
-> `MessageBus`
-> `AuditLogEntryOutboxMessagePublisher`
-> `AuditLogService` ghi `auditlog.AuditLogEntries` (idempotent)

## 7) SQL check nhanh sau khi goi POST /api/projects

Thay `<user-id>` va `<project-id>`:

```sql
-- Kiem tra project moi trong schema apidoc
SELECT "Id", "OwnerId", "Name", "CreatedDateTime"
FROM apidoc."Projects"
WHERE "OwnerId" = '<user-id>'::uuid
ORDER BY "CreatedDateTime" DESC
LIMIT 5;

-- Kiem tra usage da tang trong schema subscription
SELECT "UserId", "ProjectCount", "PeriodStart", "PeriodEnd", "UpdatedDateTime"
FROM subscription."UsageTrackings"
WHERE "UserId" = '<user-id>'::uuid
ORDER BY "UpdatedDateTime" DESC
LIMIT 5;

-- Kiem tra outbox event o schema apidoc (bonus)
SELECT "Id", "EventType", "Published", "CreatedDateTime"
FROM apidoc."OutboxMessages"
ORDER BY "CreatedDateTime" DESC
LIMIT 10;
```

## 8) Diem can nho

- Day la giao tiep **in-process** (khong phai REST call giua module).
- Boundary duoc giu bang **Contracts + Dispatcher + Repository per module**.
- `OwnerId` trong `apidoc.Projects` la reference logic (GUID), khong FK sang schema `identity`.
- Trong migration ApiDocumentation, FK duoc khai bao noi bo schema `apidoc` (vd `Projects.ActiveSpecId -> ApiSpecifications`).
