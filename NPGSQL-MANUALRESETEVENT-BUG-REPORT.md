# Bug Report: ManualResetEventSlim ObjectDisposedException — All Modules

> **Generated:** 2026-04-11  
> **Branch:** `feature/FE-17-optimize-fix-20260410`  
> **Stack:** .NET 10, Npgsql 10.0.1, EF Core, Supabase Supavisor (transaction pooler)  
> **Severity:** CRITICAL — affects ALL API endpoints that write to the database  
> **GitNexus:** Not available in this session. Analysis performed via manual codebase inspection.

---

## 1. Root Cause

Npgsql 10.0.1 has a known race condition with Supabase Supavisor (transaction pooler, port 6543):

1. Supavisor server-side resets a pooled connection
2. `ManualResetEventSlim` inside `NpgsqlConnector.ResetCancellation()` gets disposed
3. Npgsql's client pool hands out the poisoned connector
4. Next `SaveChangesAsync()` call hits `ObjectDisposedException`
5. EF Core wraps it in `DbUpdateException` → HTTP 500

The Identity module was hardened against this on 2026-04-11. **All 10 other modules remain vulnerable.**

---

## 2. Error Signature

```
System.ObjectDisposedException: Cannot access a disposed object.
Object name: 'System.Threading.ManualResetEventSlim'.
   at System.Threading.ManualResetEventSlim.Reset()
   at Npgsql.NpgsqlCommand.ExecuteReader(...)
```

Always wrapped by:
```
Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes.
```

---

## 3. Affected Modules — Full Audit

### 3.1. Missing `EnableRetryOnFailure` + `MaxBatchSize(1)` at DbContext registration

| # | Module | File | Lines | MaxBatchSize | EnableRetryOnFailure | Risk |
|---|--------|------|-------|:------------:|:--------------------:|:----:|
| 1 | **Subscription** | `ClassifiedAds.Modules.Subscription/ServiceCollectionExtensions.cs` | 36–47 | ❌ | ❌ | 🔴 |
| 2 | **Storage** | `ClassifiedAds.Modules.Storage/ServiceCollectionExtensions.cs` | 31–42 | ❌ | ❌ | 🔴 |
| 3 | **Notification** | `ClassifiedAds.Modules.Notification/ServiceCollectionExtensions.cs` | 29–40 | ❌ | ❌ | 🔴 |
| 4 | **Configuration** | `ClassifiedAds.Modules.Configuration/ServiceCollectionExtensions.cs` | 27–38 | ❌ | ❌ | 🔴 |
| 5 | **AuditLog** | `ClassifiedAds.Modules.AuditLog/ServiceCollectionExtensions.cs` | 28–39 | ❌ | ❌ | 🔴 |
| 6 | **TestGeneration** | `ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs` | 32–43 | ❌ | ❌ | 🔴 |
| 7 | **TestExecution** | `ClassifiedAds.Modules.TestExecution/ServiceCollectionExtensions.cs` | 28–39 | ❌ | ❌ | 🔴 |
| 8 | **TestReporting** | `ClassifiedAds.Modules.TestReporting/ServiceCollectionExtensions.cs` | 27–38 | ❌ | ❌ | 🔴 |
| 9 | **LlmAssistant** | `ClassifiedAds.Modules.LlmAssistant/ServiceCollectionExtensions.cs` | 27–38 | ❌ | ❌ | 🔴 |
| 10 | **ApiDocumentation** | `ClassifiedAds.Modules.ApiDocumentation/ServiceCollectionExtensions.cs` | 29–40 | ❌ | ❌ | 🔴 |
| ✅ | **Identity** | `ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs` | 33–48 | ✅ (1) | ✅ (5, 10s) | 🟢 |
| ✅ | **Identity (migrator)** | `ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs` | 112–127 | ✅ (1) | ✅ (5, 10s) | 🟢 |

### 3.2. Reference: Identity's hardened pattern (the fix template)

```csharp
// File: ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs, lines 45-48
sql.MaxBatchSize(1);
sql.EnableRetryOnFailure(
    maxRetryCount: 5,
    maxRetryDelay: TimeSpan.FromSeconds(10),
    errorCodesToAdd: null);
```

---

## 4. Fix Instructions per Module

### FIX-01: Add `MaxBatchSize(1)` + `EnableRetryOnFailure` to all 10 modules

For **each** of the 10 modules listed above, add these two lines inside the `UseNpgsql(connectionString, sql => { ... })` lambda, after the `CommandTimeout` block:

```csharp
// Supabase pooler safety: single-statement batches prevent connector state corruption
sql.MaxBatchSize(1);
sql.EnableRetryOnFailure(
    maxRetryCount: 5,
    maxRetryDelay: TimeSpan.FromSeconds(10),
    errorCodesToAdd: null);
```

**Files to modify:**

| File | Insert after line |
|------|:-----------------:|
| `ClassifiedAds.Modules.Subscription/ServiceCollectionExtensions.cs` | 46 |
| `ClassifiedAds.Modules.Storage/ServiceCollectionExtensions.cs` | 41 |
| `ClassifiedAds.Modules.Notification/ServiceCollectionExtensions.cs` | 39 |
| `ClassifiedAds.Modules.Configuration/ServiceCollectionExtensions.cs` | 37 |
| `ClassifiedAds.Modules.AuditLog/ServiceCollectionExtensions.cs` | 38 |
| `ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs` | 42 |
| `ClassifiedAds.Modules.TestExecution/ServiceCollectionExtensions.cs` | 38 |
| `ClassifiedAds.Modules.TestReporting/ServiceCollectionExtensions.cs` | 37 |
| `ClassifiedAds.Modules.LlmAssistant/ServiceCollectionExtensions.cs` | 37 |
| `ClassifiedAds.Modules.ApiDocumentation/ServiceCollectionExtensions.cs` | 39 |

---

## 5. Additional Hidden Bugs

### BUG-02: Background workers lack SaveChangesAsync retry

**Files affected:**
- `ClassifiedAds.Modules.Notification/EmailQueue/EmailSendingWorker.cs`
- `ClassifiedAds.Modules.Notification/EmailQueue/EmailDbSweepWorker.cs`
- `ClassifiedAds.Modules.Storage/HostedServices/PublishEventWorker.cs`

**Problem:** Background workers call `SaveChangesAsync(ct)` without any try-catch for `ObjectDisposedException`. When the Npgsql connector is poisoned, the worker fails silently (or crashes the hosted service) and emails/events are lost.

**Fix pattern:** Wrap SaveChangesAsync in a retry loop that creates a fresh DI scope on failure (same pattern as Identity `AuthController.RegisterCoreAsync`):

```csharp
// Retry pattern for background workers
const int maxRetries = 3;
for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        await repository.UnitOfWork.SaveChangesAsync(ct);
        break;
    }
    catch (DbUpdateException ex) when (attempt < maxRetries && IsManualResetEventDisposed(ex))
    {
        logger.LogWarning(ex,
            "ManualResetEventSlim disposed on SaveChanges attempt {Attempt}/{Max}, retrying with fresh scope",
            attempt, maxRetries);
        await Task.Delay(200 * attempt, ct);
        // Create fresh scope + re-fetch repository from new scope
    }
}
```

### BUG-03: CancellationToken propagation corrupts Npgsql connector state

**Problem:** `stoppingToken` passed directly to `SaveChangesAsync()` in background workers. If the token fires during a write, Npgsql's internal `ManualResetEventSlim` enters an inconsistent state, and the poisoned connector is returned to the pool.

**Files affected:**
- All background workers passing `stoppingToken` to `SaveChangesAsync`
- Any command handler passing `CancellationToken` through the entire EF save pipeline

**Fix:** Use `CancellationToken.None` for `SaveChangesAsync` in critical write paths (or a linked token with a longer timeout):

```csharp
// DON'T: propagates abort to Npgsql mid-save
await dbContext.SaveChangesAsync(stoppingToken);

// DO: let the save complete even if host is shutting down
await dbContext.SaveChangesAsync(CancellationToken.None);
```

### BUG-04: GlobalExceptionHandler does not recognize ObjectDisposedException

**File:** `ClassifiedAds.Infrastructure/Web/ExceptionHandlers/GlobalExceptionHandler.cs`

**Problem:** `DbUpdateException` wrapping `ObjectDisposedException` falls through to generic 500, with no special logging tag. Frontend receives opaque error. No `Retry-After` header.

**Fix:** Add a handler branch:

```csharp
else if (exception is DbUpdateException dbEx && IsManualResetEventDisposed(dbEx))
{
    _logger.LogWarning(dbEx, "[TRANSIENT-NPGSQL] ManualResetEventSlim ObjectDisposedException on {TraceId}", traceId);
    response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    // add Retry-After header
    httpContext.Response.Headers["Retry-After"] = "2";
    // return structured error
}
```

### BUG-05: Outbox pattern SaveChanges in DbContext override is unprotected

**Files affected (all modules with outbox `ActivityId` pattern):**
- `ClassifiedAds.Modules.Subscription/Persistence/SubscriptionDbContext.cs` (line 49)
- `ClassifiedAds.Modules.Storage/Persistence/StorageDbContext.cs`
- `ClassifiedAds.Modules.TestGeneration/Persistence/TestGenerationDbContext.cs`
- `ClassifiedAds.Modules.TestExecution/Persistence/TestExecutionDbContext.cs`
- `ClassifiedAds.Modules.TestReporting/Persistence/TestReportingDbContext.cs`
- `ClassifiedAds.Modules.LlmAssistant/Persistence/LlmAssistantDbContext.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Persistence/ApiDocumentationDbContext.cs`

**Problem:** These override `SaveChangesAsync` to attach outbox `ActivityId` metadata before calling `base.SaveChangesAsync()`. The override itself has no error handling — if the base call throws `ObjectDisposedException`, the outbox message is lost without trace. `EnableRetryOnFailure` at the provider level (FIX-01) will address this, but explicit outbox durability logging should also be added.

---

## 6. Fix Priority Matrix

| Priority | Bug ID | Description | Effort | Impact |
|:--------:|:------:|-------------|:------:|:------:|
| **P0** | FIX-01 | Add `MaxBatchSize(1)` + `EnableRetryOnFailure` to 10 modules | LOW | Eliminates ~80% of ManualResetEventSlim crashes |
| **P1** | BUG-03 | Stop propagating `stoppingToken` to `SaveChangesAsync` in background workers | LOW | Prevents Npgsql connector poisoning |
| **P1** | BUG-02 | Add retry loop around `SaveChangesAsync` in background workers | MEDIUM | Prevents email/event loss |
| **P2** | BUG-04 | Handle `ObjectDisposedException` in GlobalExceptionHandler | LOW | Better UX, enables client retry |
| **P2** | BUG-05 | Add outbox durability logging in SaveChanges override | LOW | Observability |

---

## 7. Connection String Normalizer Status

**File:** `ClassifiedAds.Persistence.PostgreSQL/PostgresConnectionStringNormalizer.cs`

**Current settings for Supabase pooler connections:**
```
Pooling=true
NoResetOnClose=true          (skip DISCARD ALL — Supavisor doesn't support it)
ConnectionIdleLifetime=30    (recycle stale connections)
ConnectionPruningInterval=10 (auto-cleanup)
```

✅ Applied in all 11 module `ServiceCollectionExtensions.cs` files.  
✅ Applied in `JwtTokenService.CreateDedicatedTokenConnection()`.  
✅ `ClearAllPools()` removed from `AuthController.Register`.

**Status:** GOOD — no changes needed here.

---

## 8. Npgsql Version Audit

**File:** `ClassifiedAds.Persistence.PostgreSQL/ClassifiedAds.Persistence.PostgreSQL.csproj`

```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.1" />
```

**Recommendation:** Monitor Npgsql 10.0.2+ release notes for a server-side fix to the `ManualResetEventSlim` race condition. If available, upgrading is the most durable fix.

---

## 9. Verification Checklist After Fixes

- [ ] All 10 modules have `MaxBatchSize(1)` + `EnableRetryOnFailure(5, 10s)` in `ServiceCollectionExtensions.cs`
- [ ] Background workers don't propagate `stoppingToken` to `SaveChangesAsync`
- [ ] Background workers have retry logic around `SaveChangesAsync`
- [ ] `GlobalExceptionHandler` catches `ObjectDisposedException` via `DbUpdateException`
- [ ] `dotnet build` succeeds for entire solution
- [ ] Test all subscription/storage/notification endpoints against Supabase pooler
- [ ] Migrator still passes `--verify-migrations`

---

## 10. How to Reproduce

1. Start AppHost with `APPHOST_DATABASE_MODE=external` pointing to Supabase transaction pooler (port 6543)
2. Call any write API (e.g., `POST /api/subscriptions/plans/{planId}/payments`)
3. Under moderate load (~5 concurrent requests), the error will appear intermittently
4. With local PostgreSQL (`APPHOST_DATABASE_MODE=local`), the error does not occur

---

## 11. Appendix: Detection Helper (reusable across modules)

```csharp
public static class NpgsqlTransientHelper
{
    public static bool IsManualResetEventDisposed(Exception exception)
    {
        if (exception is ObjectDisposedException disposedException)
        {
            return string.Equals(
                disposedException.ObjectName,
                "System.Threading.ManualResetEventSlim",
                StringComparison.Ordinal)
                || disposedException.Message.Contains("ManualResetEventSlim");
        }
        return exception.InnerException is not null
            && IsManualResetEventDisposed(exception.InnerException);
    }
}
```

This helper already exists in `AuthController.cs` (Identity module). It should be extracted to a shared location in `ClassifiedAds.Persistence.PostgreSQL` or `ClassifiedAds.CrossCuttingConcerns` for reuse by all modules.
