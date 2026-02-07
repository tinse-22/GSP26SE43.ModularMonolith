# FE-14-01: Admin Subscription Plan Management

> **Parent Requirement**: FE-14 (Subscription & Billing Management)
> **Module**: `ClassifiedAds.Modules.Subscription`
> **Role**: Admin only
> **Reference Module**: `ClassifiedAds.Modules.Product` (follow all conventions)

---

## 1. Overview

Implement CRUD API endpoints for **Admin** to manage Subscription Plans (`SubscriptionPlan`) and their associated Plan Limits (`PlanLimit`). This is the foundational feature that enables the billing system — plans must exist before users can subscribe.

### Business Context

```
Admin creates plans → Users see plans → Users subscribe → System enforces limits
```

This task covers the **first step only**: Admin creates and manages plans.

---

## 2. Existing Data Layer (Already Implemented)

### 2.1 Entities (DO NOT MODIFY)

The following entities already exist in `ClassifiedAds.Modules.Subscription/Entities/`:

#### SubscriptionPlan.cs
```csharp
public class SubscriptionPlan : Entity<Guid>, IAggregateRoot
{
    public string Name { get; set; }           // Internal name: "Free", "Pro", "Enterprise"
    public string DisplayName { get; set; }     // UI display name
    public string Description { get; set; }
    public decimal? PriceMonthly { get; set; }
    public decimal? PriceYearly { get; set; }
    public string Currency { get; set; }        // "USD", "VND"
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
```

#### PlanLimit.cs
```csharp
public class PlanLimit : Entity<Guid>, IAggregateRoot
{
    public Guid PlanId { get; set; }
    public LimitType LimitType { get; set; }
    public int? LimitValue { get; set; }
    public bool IsUnlimited { get; set; }
    public SubscriptionPlan Plan { get; set; }
}

public enum LimitType
{
    MaxProjects = 0,
    MaxEndpointsPerProject = 1,
    MaxTestCasesPerSuite = 2,
    MaxTestRunsPerMonth = 3,
    MaxConcurrentRuns = 4,
    RetentionDays = 5,
    MaxLlmCallsPerMonth = 6,
    MaxStorageMB = 7
}
```

### 2.2 Existing Infrastructure (DO NOT MODIFY)

- **DbContext**: `SubscriptionDbContext` (schema: `subscription`)
- **Repository**: `Repository<T, TKey>` registered for all entities
- **DbConfigurations**: All entity configurations exist
- **ServiceCollectionExtensions**: Repository DI registrations exist

---

## 3. What To Implement

### 3.1 Files to Create

Follow the **Product module** conventions exactly. Create these files:

```
ClassifiedAds.Modules.Subscription/
├── Authorization/
│   └── Permissions.cs                         # NEW
├── Commands/
│   ├── AddUpdatePlanCommand.cs                # NEW - Create/Update plan + limits
│   ├── DeletePlanCommand.cs                   # NEW - Soft delete (IsActive = false)
│   └── PublishEventsCommand.cs                # NEW - Outbox publisher
├── Controllers/
│   └── PlansController.cs                     # NEW - Admin CRUD endpoints
├── EventHandlers/
│   ├── PlanCreatedEventHandler.cs             # NEW
│   ├── PlanUpdatedEventHandler.cs             # NEW
│   └── PlanDeletedEventHandler.cs             # NEW
├── Models/
│   ├── PlanModel.cs                           # NEW - Response DTO
│   ├── CreateUpdatePlanModel.cs               # NEW - Request DTO
│   └── PlanModelMappingConfiguration.cs       # NEW - Mapping extensions
├── Queries/
│   ├── GetPlanQuery.cs                        # NEW - Get single plan by ID
│   ├── GetPlansQuery.cs                       # NEW - Get all plans (with filters)
│   └── GetAuditEntriesQuery.cs                # NEW - Plan audit log
└── HostedServices/
    └── PublishEventWorker.cs                  # NEW - Background outbox publisher
```

### 3.2 Files to Modify

```
ClassifiedAds.Modules.Subscription/
└── ServiceCollectionExtensions.cs             # MODIFY - Register new services
```

---

## 4. API Endpoints Specification

### 4.1 Plans CRUD (Admin Only)

Base path: `api/plans`

| Method | Path | Description | Permission |
|--------|------|-------------|------------|
| `GET` | `/api/plans` | List all plans (with optional filters) | `Permission:GetPlans` |
| `GET` | `/api/plans/{id}` | Get plan by ID (includes limits) | `Permission:GetPlans` |
| `POST` | `/api/plans` | Create a new plan with limits | `Permission:AddPlan` |
| `PUT` | `/api/plans/{id}` | Update plan and its limits | `Permission:UpdatePlan` |
| `DELETE` | `/api/plans/{id}` | Deactivate plan (set IsActive = false) | `Permission:DeletePlan` |
| `GET` | `/api/plans/{id}/auditlogs` | Get audit trail for a plan | `Permission:GetPlans` |

### 4.2 Request/Response Models

#### CreateUpdatePlanModel (Request DTO)

```csharp
public class CreateUpdatePlanModel
{
    public string Name { get; set; }            // Required, unique
    public string DisplayName { get; set; }     // Required
    public string Description { get; set; }     // Optional
    public decimal? PriceMonthly { get; set; }  // null = free
    public decimal? PriceYearly { get; set; }   // null = free
    public string Currency { get; set; }        // Default "USD"
    public bool IsActive { get; set; }          // Default true
    public int SortOrder { get; set; }          // Display order
    public List<PlanLimitModel> Limits { get; set; } // Plan limits
}
```

#### PlanModel (Response DTO)

```csharp
public class PlanModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public decimal? PriceMonthly { get; set; }
    public decimal? PriceYearly { get; set; }
    public string Currency { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedDateTime { get; set; }
    public DateTimeOffset? UpdatedDateTime { get; set; }
    public List<PlanLimitModel> Limits { get; set; }
}
```

#### PlanLimitModel (Nested DTO)

```csharp
public class PlanLimitModel
{
    public Guid? Id { get; set; }               // null for new limits
    public string LimitType { get; set; }       // Enum name as string
    public int? LimitValue { get; set; }        // null when IsUnlimited
    public bool IsUnlimited { get; set; }
}
```

### 4.3 Query Parameters for GET /api/plans

| Parameter | Type | Description |
|-----------|------|-------------|
| `isActive` | `bool?` | Filter by active status |
| `search` | `string?` | Search by Name or DisplayName |

---

## 5. Business Rules

### 5.1 Validation Rules

| Rule | Description |
|------|-------------|
| **V-01** | `Name` is required, max 50 chars, must be unique (case-insensitive) |
| **V-02** | `DisplayName` is required, max 100 chars |
| **V-03** | `Description` max 500 chars |
| **V-04** | `PriceMonthly` and `PriceYearly` must be >= 0 when provided |
| **V-05** | `Currency` must be a valid 3-letter ISO code; defaults to "USD" |
| **V-06** | `SortOrder` must be >= 0 |
| **V-07** | Each `LimitType` can appear at most once per plan |
| **V-08** | `LimitValue` must be > 0 when `IsUnlimited` is false |
| **V-09** | If `IsUnlimited` is true, `LimitValue` should be set to null |

### 5.2 Delete Behavior

| Rule | Description |
|------|-------------|
| **D-01** | Delete is a **soft delete** — sets `IsActive = false`, does NOT remove the record |
| **D-02** | A plan with active subscribers (`UserSubscription.Status` in [Trial, Active, PastDue]) **cannot** be deleted; return `409 Conflict` |
| **D-03** | Raise `EntityDeletedEvent<SubscriptionPlan>` on soft delete |

### 5.3 Update Behavior

| Rule | Description |
|------|-------------|
| **U-01** | On update, replace all limits — delete old `PlanLimit` records, insert new ones |
| **U-02** | `Name` uniqueness is checked excluding the plan being updated |
| **U-03** | Raise `EntityUpdatedEvent<SubscriptionPlan>` on update |

---

## 6. Implementation Steps (Ordered)

Follow this exact order. Each step should compile before moving to the next.

### Step 1: Authorization/Permissions.cs

```csharp
namespace ClassifiedAds.Modules.Subscription.Authorization;

public static class Permissions
{
    public const string GetPlans = "Permission:GetPlans";
    public const string AddPlan = "Permission:AddPlan";
    public const string UpdatePlan = "Permission:UpdatePlan";
    public const string DeletePlan = "Permission:DeletePlan";
}
```

### Step 2: Models/

Create `PlanModel.cs`, `CreateUpdatePlanModel.cs`, `PlanLimitModel.cs`, and `PlanModelMappingConfiguration.cs`.

Mapping extensions pattern (follow Product module):
```csharp
public static class PlanModelMappingConfiguration
{
    public static PlanModel ToModel(this SubscriptionPlan entity, List<PlanLimit> limits) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        // ... map all properties
        Limits = limits?.Select(l => l.ToModel()).ToList() ?? []
    };

    public static PlanLimitModel ToModel(this PlanLimit entity) => new()
    {
        Id = entity.Id,
        LimitType = entity.LimitType.ToString(),
        LimitValue = entity.LimitValue,
        IsUnlimited = entity.IsUnlimited
    };
}
```

### Step 3: Commands/

#### AddUpdatePlanCommand.cs
- Accept `SubscriptionPlan` entity and `List<PlanLimit>`
- Handler logic:
  1. Validate name uniqueness via repository query
  2. If new: `Repository.AddAsync(plan)` then `Repository.AddAsync(limit)` for each limit
  3. If update: `Repository.UpdateAsync(plan)`, delete old limits, add new limits
  4. `UnitOfWork.SaveChangesAsync()`
  5. Dispatch created/updated domain event

#### DeletePlanCommand.cs
- Accept `SubscriptionPlan` entity
- Handler logic:
  1. Check no active subscribers exist (query `UserSubscription` where `PlanId == plan.Id` and status in [Trial, Active, PastDue])
  2. If active subscribers exist, throw `ValidationException` with message
  3. Set `plan.IsActive = false`, `Repository.UpdateAsync(plan)`
  4. `UnitOfWork.SaveChangesAsync()`
  5. Dispatch deleted domain event

#### PublishEventsCommand.cs
- Follow Product module outbox pattern exactly

### Step 4: Queries/

#### GetPlanQuery.cs
- Input: `Guid Id`, `bool ThrowNotFoundIfNull`
- Returns: `PlanModel` (plan + limits)
- Query plan by ID, include limits from `PlanLimit` repository

#### GetPlansQuery.cs
- Input: `bool? IsActive`, `string? Search`
- Returns: `List<PlanModel>`
- Query with optional filters, order by `SortOrder, Name`

#### GetAuditEntriesQuery.cs
- Follow Product module pattern exactly

### Step 5: EventHandlers/

Create handlers for `EntityCreatedEvent<SubscriptionPlan>`, `EntityUpdatedEvent<SubscriptionPlan>`, `EntityDeletedEvent<SubscriptionPlan>`.

Each handler:
1. Write `AuditLogEntry` (PlanId, action description)
2. Write `OutboxMessage` with event payload
3. Save changes

### Step 6: HostedServices/PublishEventWorker.cs

Follow Product module `PublishEventWorker` exactly — poll outbox, publish to message bus, mark as published.

### Step 7: Controllers/PlansController.cs

```csharp
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class PlansController : ControllerBase
{
    private readonly Dispatcher _dispatcher;

    // GET api/plans
    [Authorize(Permissions.GetPlans)]
    [HttpGet]
    public async Task<ActionResult<List<PlanModel>>> Get(
        [FromQuery] bool? isActive,
        [FromQuery] string? search) { ... }

    // GET api/plans/{id}
    [Authorize(Permissions.GetPlans)]
    [HttpGet("{id}")]
    public async Task<ActionResult<PlanModel>> Get(Guid id) { ... }

    // POST api/plans
    [Authorize(Permissions.AddPlan)]
    [HttpPost]
    public async Task<ActionResult<PlanModel>> Post(
        [FromBody] CreateUpdatePlanModel model) { ... }

    // PUT api/plans/{id}
    [Authorize(Permissions.UpdatePlan)]
    [HttpPut("{id}")]
    public async Task<ActionResult<PlanModel>> Put(
        Guid id,
        [FromBody] CreateUpdatePlanModel model) { ... }

    // DELETE api/plans/{id}
    [Authorize(Permissions.DeletePlan)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id) { ... }

    // GET api/plans/{id}/auditlogs
    [Authorize(Permissions.GetPlans)]
    [HttpGet("{id}/auditlogs")]
    public async Task<ActionResult<List<object>>> GetAuditLogs(Guid id) { ... }
}
```

### Step 8: Update ServiceCollectionExtensions.cs

Add to `AddSubscriptionModule`:
- Register authorization policies for all 4 permissions
- Register `PublishEventWorker` as hosted service
- Ensure `AddApplicationPart` includes controllers from this assembly

---

## 7. Technical Constraints

| Constraint | Detail |
|------------|--------|
| **Pattern** | Follow CQRS via `Dispatcher` — controller NEVER accesses repository directly |
| **Mapping** | Use static extension methods (`ToModel()`, `ToEntity()`), NOT AutoMapper |
| **Schema** | All tables in `subscription` schema (already configured) |
| **Events** | Use domain events + outbox pattern for audit logging |
| **Errors** | Use `NotFoundException` (404), `ValidationException` (400/409) from shared layer |
| **Auth** | All endpoints require authentication; individual actions use permission-based `[Authorize]` |
| **No breaking changes** | Do NOT modify existing entities, DB configurations, or DbContext |

---

## 8. Example API Responses

### POST /api/plans — Create Plan

**Request:**
```json
{
  "name": "Pro",
  "displayName": "Pro Plan",
  "description": "For professional teams",
  "priceMonthly": 29.99,
  "priceYearly": 299.99,
  "currency": "USD",
  "isActive": true,
  "sortOrder": 2,
  "limits": [
    { "limitType": "MaxProjects", "limitValue": 10, "isUnlimited": false },
    { "limitType": "MaxTestRunsPerMonth", "limitValue": 500, "isUnlimited": false },
    { "limitType": "MaxLlmCallsPerMonth", "limitValue": 200, "isUnlimited": false },
    { "limitType": "MaxStorageMB", "limitValue": 1024, "isUnlimited": false },
    { "limitType": "MaxEndpointsPerProject", "limitValue": 50, "isUnlimited": false },
    { "limitType": "MaxTestCasesPerSuite", "limitValue": 100, "isUnlimited": false },
    { "limitType": "MaxConcurrentRuns", "limitValue": 3, "isUnlimited": false },
    { "limitType": "RetentionDays", "limitValue": 90, "isUnlimited": false }
  ]
}
```

**Response (201 Created):**
```json
{
  "id": "a1b2c3d4-...",
  "name": "Pro",
  "displayName": "Pro Plan",
  "description": "For professional teams",
  "priceMonthly": 29.99,
  "priceYearly": 299.99,
  "currency": "USD",
  "isActive": true,
  "sortOrder": 2,
  "createdDateTime": "2026-02-08T10:00:00Z",
  "updatedDateTime": null,
  "limits": [
    { "id": "...", "limitType": "MaxProjects", "limitValue": 10, "isUnlimited": false },
    { "id": "...", "limitType": "MaxTestRunsPerMonth", "limitValue": 500, "isUnlimited": false }
  ]
}
```

### DELETE /api/plans/{id} — When Plan Has Active Subscribers

**Response (409 Conflict):**
```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "Cannot deactivate plan 'Pro' because it has 5 active subscriber(s). Migrate subscribers to another plan first."
}
```

---

## 9. Suggested Default Plans (Seed Data)

After implementation, these plans should be seeded:

| Plan | Monthly | Yearly | MaxProjects | MaxEndpoints | MaxTestRuns | MaxLlmCalls | Storage |
|------|---------|--------|-------------|--------------|-------------|-------------|---------|
| **Free** | $0 | $0 | 1 | 10 | 50/mo | 20/mo | 100 MB |
| **Pro** | $29.99 | $299.99 | 10 | 50 | 500/mo | 200/mo | 1 GB |
| **Enterprise** | $99.99 | $999.99 | Unlimited | Unlimited | Unlimited | 1000/mo | 10 GB |

> Seed data implementation is optional for this task — can be done separately.

---

## 10. Acceptance Criteria

- [ ] All 6 API endpoints return correct responses
- [ ] Permission-based authorization works (Admin-only)
- [ ] Plan name uniqueness is enforced
- [ ] PlanLimits are saved/updated/returned with the plan
- [ ] Soft delete prevents deletion of plans with active subscribers
- [ ] Audit log entries are created for create/update/delete actions
- [ ] Outbox messages are published for all plan changes
- [ ] Solution builds with `dotnet build` — no errors
- [ ] All existing tests continue to pass
- [ ] New unit tests cover command handlers and query handlers

---

## 11. Files Reference Quick List

| Order | Action | File Path |
|-------|--------|-----------|
| 1 | CREATE | `Subscription/Authorization/Permissions.cs` |
| 2 | CREATE | `Subscription/Models/PlanLimitModel.cs` |
| 3 | CREATE | `Subscription/Models/PlanModel.cs` |
| 4 | CREATE | `Subscription/Models/CreateUpdatePlanModel.cs` |
| 5 | CREATE | `Subscription/Models/PlanModelMappingConfiguration.cs` |
| 6 | CREATE | `Subscription/Commands/AddUpdatePlanCommand.cs` |
| 7 | CREATE | `Subscription/Commands/DeletePlanCommand.cs` |
| 8 | CREATE | `Subscription/Commands/PublishEventsCommand.cs` |
| 9 | CREATE | `Subscription/Queries/GetPlanQuery.cs` |
| 10 | CREATE | `Subscription/Queries/GetPlansQuery.cs` |
| 11 | CREATE | `Subscription/Queries/GetAuditEntriesQuery.cs` |
| 12 | CREATE | `Subscription/EventHandlers/PlanCreatedEventHandler.cs` |
| 13 | CREATE | `Subscription/EventHandlers/PlanUpdatedEventHandler.cs` |
| 14 | CREATE | `Subscription/EventHandlers/PlanDeletedEventHandler.cs` |
| 15 | CREATE | `Subscription/HostedServices/PublishEventWorker.cs` |
| 16 | CREATE | `Subscription/Controllers/PlansController.cs` |
| 17 | MODIFY | `Subscription/ServiceCollectionExtensions.cs` |
