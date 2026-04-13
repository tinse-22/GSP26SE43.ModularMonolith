# 3 Prompts để fix toàn bộ ManualResetEventSlim ObjectDisposedException

> Chạy lần lượt Prompt 1 → 2 → 3. Mỗi prompt là một phần độc lập, build + verify trước khi chạy prompt tiếp.

---

## PROMPT 1/3 — P0: Thêm `EnableRetryOnFailure` + `MaxBatchSize(1)` cho 10 modules

```
Đọc file NPGSQL-MANUALRESETEVENT-BUG-REPORT.md để hiểu context.

Task classification: Application code only (không đụng EF model/migration/Docker).

Thực hiện FIX-01 từ report: thêm MaxBatchSize(1) + EnableRetryOnFailure vào 10 module DbContext registration.

Mẫu chuẩn lấy từ ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs (lines 45-48):

```csharp
// Supabase pooler safety: single-statement batches prevent connector state corruption.
sql.MaxBatchSize(1);
sql.EnableRetryOnFailure(
    maxRetryCount: 5,
    maxRetryDelay: TimeSpan.FromSeconds(10),
    errorCodesToAdd: null);
```

10 file cần sửa — thêm 4 dòng trên vào bên trong lambda `UseNpgsql(connectionString, sql => { ... })`, SAU block `CommandTimeout`:

1. ClassifiedAds.Modules.Subscription/ServiceCollectionExtensions.cs
2. ClassifiedAds.Modules.Storage/ServiceCollectionExtensions.cs
3. ClassifiedAds.Modules.Notification/ServiceCollectionExtensions.cs
4. ClassifiedAds.Modules.Configuration/ServiceCollectionExtensions.cs
5. ClassifiedAds.Modules.AuditLog/ServiceCollectionExtensions.cs
6. ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs
7. ClassifiedAds.Modules.TestExecution/ServiceCollectionExtensions.cs
8. ClassifiedAds.Modules.TestReporting/ServiceCollectionExtensions.cs
9. ClassifiedAds.Modules.LlmAssistant/ServiceCollectionExtensions.cs
10. ClassifiedAds.Modules.ApiDocumentation/ServiceCollectionExtensions.cs

Mỗi file có cùng pattern — tìm block:
```csharp
if (settings.ConnectionStrings.CommandTimeout.HasValue)
{
    sql.CommandTimeout(settings.ConnectionStrings.CommandTimeout);
}
```
Thêm ngay SAU closing brace `}` của block đó, TRƯỚC closing `}));` của UseNpgsql lambda.

KHÔNG sửa ClassifiedAds.Modules.Identity — đã có sẵn.
KHÔNG sửa ClassifiedAds.Modules.Product — không có DbContext.

Sau khi sửa xong 10 file:
1. Chạy `dotnet build` toàn solution để verify không lỗi compile
2. Verify bằng cách grep: `rg "EnableRetryOnFailure" ClassifiedAds.Modules.*/ServiceCollectionExtensions.cs` — phải ra 12 kết quả (10 mới + 2 Identity)
3. Verify: `rg "MaxBatchSize" ClassifiedAds.Modules.*/ServiceCollectionExtensions.cs` — phải ra 12 kết quả

Không tạo file markdown report.
```

---

## PROMPT 2/3 — P1: Extract shared helper + Fix background workers

```
Đọc file NPGSQL-MANUALRESETEVENT-BUG-REPORT.md để hiểu context (mục BUG-02, BUG-03, Appendix section 11).

Task classification: Application code only (không đụng EF model/migration/Docker).

Thực hiện 3 việc:

### Việc 1: Extract NpgsqlTransientHelper ra shared location

Hiện tại `IsManualResetEventDisposed` bị duplicate ở 2 nơi:
- ClassifiedAds.Modules.Identity/Controllers/AuthController.cs (line ~966)
- ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs (line ~182)

Tạo class mới:
- File: ClassifiedAds.Persistence.PostgreSQL/NpgsqlTransientHelper.cs
- Content:

```csharp
using System;

namespace ClassifiedAds.Persistence.PostgreSQL;

public static class NpgsqlTransientHelper
{
    /// <summary>
    /// Detects the Npgsql ManualResetEventSlim ObjectDisposedException
    /// caused by Supabase Supavisor connection resets under Npgsql 10.0.x.
    /// </summary>
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

Sau khi tạo file, refactor 2 nơi cũ trong Identity module:
- AuthController.cs: xoá private static method `IsManualResetEventDisposed`, thêm `using ClassifiedAds.Persistence.PostgreSQL;`, đổi call sites thành `NpgsqlTransientHelper.IsManualResetEventDisposed(ex)`
- JwtTokenService.cs: tương tự — xoá duplicate, dùng shared helper

Kiểm tra ClassifiedAds.Modules.Identity.csproj đã reference ClassifiedAds.Persistence.PostgreSQL chưa. Nếu chưa thì thêm ProjectReference.

### Việc 2: Fix CancellationToken trong background workers (BUG-03)

Tìm tất cả `SaveChangesAsync` calls trong background workers/hosted services. Đổi từ truyền `stoppingToken`/`ct` thành `CancellationToken.None` cho các write operations critical:

Files cần kiểm tra:
- ClassifiedAds.Modules.Notification/EmailQueue/EmailSendingWorker.cs
- ClassifiedAds.Modules.Notification/EmailQueue/EmailDbSweepWorker.cs  
- ClassifiedAds.Modules.Storage/HostedServices/PublishEventWorker.cs
- Bất kỳ BackgroundService nào khác có gọi SaveChangesAsync

Pattern:
```csharp
// TRƯỚC (nguy hiểm):
await repository.UnitOfWork.SaveChangesAsync(ct);

// SAU (an toàn):
await repository.UnitOfWork.SaveChangesAsync(CancellationToken.None);
```

Lưu ý: CHỈ đổi cho SaveChangesAsync trong background workers. KHÔNG đổi cho query operations (ToListAsync, FirstOrDefaultAsync) — những cái đó vẫn nên dùng cancellation token.

### Việc 3: Thêm retry logic cho SaveChangesAsync trong background workers (BUG-02)

Cho mỗi background worker có SaveChangesAsync, wrap trong retry pattern:

```csharp
// Thêm using
using ClassifiedAds.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;

// Pattern:
const int maxSaveRetries = 3;
for (int attempt = 1; attempt <= maxSaveRetries; attempt++)
{
    try
    {
        await repository.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        break;
    }
    catch (DbUpdateException ex) when (attempt < maxSaveRetries && NpgsqlTransientHelper.IsManualResetEventDisposed(ex))
    {
        _logger.LogWarning(ex,
            "ManualResetEventSlim disposed on SaveChanges attempt {Attempt}/{Max}, retrying",
            attempt, maxSaveRetries);
        await Task.Delay(200 * attempt);
    }
}
```

Áp dụng cho TẤT CẢ nơi trong background workers gọi SaveChangesAsync.

Sau khi sửa xong:
1. `dotnet build` toàn solution
2. `rg "NpgsqlTransientHelper" --include="*.cs"` — verify helper được dùng ở Identity + background workers
3. `rg "CancellationToken.None" ClassifiedAds.Modules.Notification ClassifiedAds.Modules.Storage --include="*.cs"` — verify background worker saves dùng CancellationToken.None

Không tạo file markdown report.
```

---

## PROMPT 3/3 — P2: Fix GlobalExceptionHandler + Outbox logging

```
Đọc file NPGSQL-MANUALRESETEVENT-BUG-REPORT.md để hiểu context (mục BUG-04, BUG-05).

Task classification: Application code only (không đụng EF model/migration/Docker).

Thực hiện 2 việc:

### Việc 1: Thêm ObjectDisposedException handler vào GlobalExceptionHandler (BUG-04)

File: ClassifiedAds.Infrastructure/Web/ExceptionHandlers/GlobalExceptionHandler.cs

Hiện tại file có chain if-else:
1. NotFoundException → 404
2. ValidationException → 400
3. ConflictException → 409
4. DbUpdateConcurrencyException → 409
5. DbUpdateException + UniqueConstraint → 409
6. else → 500

Thêm một branch MỚI giữa branch 5 (UniqueConstraint) và branch 6 (else), để bắt DbUpdateException wrapping ManualResetEventSlim:

```csharp
else if (exception is DbUpdateException npgsqlTransientEx && IsNpgsqlTransientDisposed(npgsqlTransientEx))
{
    _logger.LogWarning(exception, "[TRANSIENT-NPGSQL] ManualResetEventSlim ObjectDisposedException [{Ticks}-{ThreadId}]",
        DateTime.UtcNow.Ticks, Environment.CurrentManagedThreadId);

    var problemDetails = new ProblemDetails
    {
        Detail = "Loi ket noi tam thoi voi co so du lieu. Vui long thu lai sau vai giay.",
        Instance = null,
        Status = (int)HttpStatusCode.ServiceUnavailable,
        Title = "Service Unavailable",
        Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.4"
    };

    problemDetails.Extensions.Add("message", problemDetails.Detail);
    problemDetails.Extensions.Add("traceId", Activity.Current.GetTraceId());
    problemDetails.Extensions.Add("reasonCode", "TRANSIENT_NPGSQL_CONNECTION");

    response.ContentType = "application/problem+json";
    response.StatusCode = problemDetails.Status.Value;
    response.Headers["Retry-After"] = "2";

    var result = JsonSerializer.Serialize(problemDetails);
    await response.WriteAsync(result, cancellationToken: cancellationToken);

    return true;
}
```

Thêm private helper method vào cuối class (không dùng shared NpgsqlTransientHelper để tránh dependency từ Infrastructure → Persistence.PostgreSQL nếu chưa có):

```csharp
private static bool IsNpgsqlTransientDisposed(Exception exception)
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
        && IsNpgsqlTransientDisposed(exception.InnerException);
}
```

QUAN TRỌNG: Kiểm tra xem ClassifiedAds.Infrastructure.csproj đã reference Microsoft.EntityFrameworkCore chưa (cần cho DbUpdateException). File hiện tại đã using Microsoft.EntityFrameworkCore nên khả năng đã có.

### Việc 2: Thêm warning log cho outbox SaveChanges failure (BUG-05)

7 file DbContext có override SaveChangesAsync với outbox ActivityId pattern. Thêm try-catch logging quanh base.SaveChangesAsync() call:

Files:
1. ClassifiedAds.Modules.Subscription/Persistence/SubscriptionDbContext.cs
2. ClassifiedAds.Modules.Storage/Persistence/StorageDbContext.cs
3. ClassifiedAds.Modules.TestGeneration/Persistence/TestGenerationDbContext.cs
4. ClassifiedAds.Modules.TestExecution/Persistence/TestExecutionDbContext.cs
5. ClassifiedAds.Modules.TestReporting/Persistence/TestReportingDbContext.cs
6. ClassifiedAds.Modules.LlmAssistant/Persistence/LlmAssistantDbContext.cs
7. ClassifiedAds.Modules.ApiDocumentation/Persistence/ApiDocumentationDbContext.cs

Đọc mỗi file trước khi sửa. Tìm pattern hiện tại (ví dụ SubscriptionDbContext):

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // ... outbox ActivityId logic ...
    return await base.SaveChangesAsync(cancellationToken);
}
```

Thêm structured logging khi base.SaveChangesAsync throw, CHỈ log rồi rethrow (không swallow exception — EnableRetryOnFailure từ Prompt 1 sẽ handle retry):

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // ... outbox ActivityId logic giữ nguyên ...
    try
    {
        return await base.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException ex) when (ex.InnerException is ObjectDisposedException ode
        && ode.Message.Contains("ManualResetEventSlim"))
    {
        // Log with TRANSIENT tag for correlation; EnableRetryOnFailure handles retry.
        System.Diagnostics.Debug.WriteLine(
            $"[TRANSIENT-NPGSQL] {GetType().Name}.SaveChangesAsync failed: {ode.Message}");
        throw;
    }
}
```

Lưu ý: Dùng System.Diagnostics.Debug.WriteLine thay vì inject ILogger vào DbContext (giữ constructor đơn giản). Nếu DbContext đã có logger field thì dùng logger thay thế.

Sau khi sửa xong:
1. `dotnet build` toàn solution — PHẢI pass
2. `rg "TRANSIENT-NPGSQL" --include="*.cs"` — verify tag có ở GlobalExceptionHandler + 7 DbContext files
3. `rg "Retry-After" --include="*.cs"` — verify header được set trong GlobalExceptionHandler
4. `rg "ServiceUnavailable" ClassifiedAds.Infrastructure/Web/ExceptionHandlers/GlobalExceptionHandler.cs` — verify 503 response

Không tạo file markdown report.
```

---

## Thứ tự chạy & Verification

| Step | Prompt | Verify sau khi xong |
|:----:|:------:|---------------------|
| 1 | Prompt 1 | `dotnet build` OK + 12 kết quả `EnableRetryOnFailure` |
| 2 | Prompt 2 | `dotnet build` OK + shared helper dùng đúng + background workers có retry |
| 3 | Prompt 3 | `dotnet build` OK + `TRANSIENT-NPGSQL` tag ở 8+ files + 503 response |

Sau khi cả 3 xong:
- Chạy `dotnet build ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj --no-restore` (AGENTS.md gate)
- Test thử 1 API write endpoint để confirm không còn unhandled 500
