# Phân Tích Vấn Đề: Test Results Cache Unavailable

## 📋 Thông Báo Gốc

```
"Chi tiết kết quả run hiện tại không còn trong cache (resultsSource=unavailable), 
nên không thể tạo Failure Explanation. Hãy chạy lại test run để tạo dữ liệu mới."
```

---

## 🔍 Phân Tích Chi Tiết

### 1. Kiến Trúc Cache Hiện Tại

Hệ thống sử dụng **dual-layer caching strategy**:

```
┌─────────────────────────────────────────────────────────────────┐
│                    TEST EXECUTION FLOW                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Test Run Completes                                             │
│        │                                                        │
│        ▼                                                        │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  TIER 1: Redis Cache (Distributed Cache)                │   │
│  │  ─────────────────────────────────────────────────────  │   │
│  │  • Key: TestRun.RedisKey (unique identifier)            │   │
│  │  • Value: JSON serialized TestRunResultModel            │   │
│  │  • TTL: subscription.RetentionDays (default: 7 days)    │   │
│  │  • Contains: Full test case results, responses, timing  │   │
│  └─────────────────────────────────────────────────────────┘   │
│        │                                                        │
│        ▼                                                        │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  TIER 2: PostgreSQL (Persistent Storage)                │   │
│  │  ─────────────────────────────────────────────────────  │   │
│  │  • Table: TestRuns (summary only)                       │   │
│  │  • Fields: RedisKey, ResultsExpireAt, Status, Counts    │   │
│  │  • Permanent: Run metadata survives cache expiration    │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Ý Nghĩa Của `resultsSource`

| Value | Mô Tả | Điều Kiện |
|-------|-------|-----------|
| `"cache"` | Kết quả được lấy thành công từ Redis | Redis entry còn valid |
| `"unavailable"` | Kết quả không khả dụng | Redis entry expired/missing/corrupted |

### 3. Nguyên Nhân Gốc Rễ

```
┌─────────────────────────────────────────────────────────────────┐
│                    FAILURE SCENARIOS                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ❌ SCENARIO 1: Redis Key Missing                               │
│  ────────────────────────────────────                           │
│  Condition: run.RedisKey == null || empty                       │
│  Cause: Test run failed to save results to Redis                │
│  Location: TestFailureReadGatewayService.cs:98                  │
│                                                                 │
│  ❌ SCENARIO 2: Cache Expired (MOST COMMON)                     │
│  ────────────────────────────────────────────                   │
│  Condition: run.ResultsExpireAt < DateTimeOffset.UtcNow         │
│  Cause: Retention period exceeded (default 7 days)              │
│  Location: TestFailureReadGatewayService.cs:103-106             │
│                                                                 │
│  ❌ SCENARIO 3: Redis Entry Evicted                             │
│  ─────────────────────────────────────                          │
│  Condition: _cache.GetStringAsync() returns null                │
│  Cause: Redis memory pressure / eviction policy                 │
│  Location: TestFailureReadGatewayService.cs:108                 │
│                                                                 │
│  ❌ SCENARIO 4: Corrupted Cache Data                            │
│  ────────────────────────────────────────                       │
│  Condition: JSON deserialization fails                          │
│  Cause: Incomplete write / version mismatch                     │
│  Location: TestFailureReadGatewayService.cs:114-115             │
│                                                                 │
│  ❌ SCENARIO 5: Redis Unavailable During Collection             │
│  ─────────────────────────────────────────────────────          │
│  Condition: Cache write throws exception                        │
│  Cause: Redis connection failure during test execution          │
│  Location: TestResultCollector.cs:128-132                       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 4. Flow Gây Ra Lỗi

```
User requests Failure Explanation
        │
        ▼
GetFailureExplanationQuery.HandleAsync()
        │
        ▼
TestFailureReadGatewayService.GetFailureExplanationContextAsync()
        │
        ├──► Check: run.RedisKey exists?
        │         │
        │         ├── NO ──► throw ConflictException("RUN_RESULTS_EXPIRED")
        │         │
        │         ▼ YES
        │
        ├──► Check: run.ResultsExpireAt > NOW?
        │         │
        │         ├── NO (expired) ──► throw ConflictException("RUN_RESULTS_EXPIRED")
        │         │
        │         ▼ YES
        │
        ├──► Redis.GetStringAsync(run.RedisKey)
        │         │
        │         ├── NULL/ERROR ──► throw ConflictException("RUN_RESULTS_EXPIRED")
        │         │
        │         ▼ JSON string
        │
        ├──► Deserialize JSON to TestRunResultModel
        │         │
        │         ├── FAIL ──► throw ConflictException("RUN_RESULTS_EXPIRED")
        │         │
        │         ▼ SUCCESS
        │
        ▼
Continue to build TestFailureExplanationContextDto
        │
        ▼
LlmFailureExplainer.GetCachedAsync() or ExplainAsync()
```

---

## 🎯 Vấn Đề Thiết Kế Hiện Tại

### Hạn Chế 1: Không Lưu Chi Tiết Vào PostgreSQL

```
HIỆN TẠI:
─────────
• Chi tiết test case results CHỈ lưu trong Redis
• PostgreSQL chỉ lưu summary (counts, status, timing)
• Khi Redis expires → Mất hoàn toàn chi tiết

KẾT QUẢ:
─────────
• Không thể tạo Failure Explanation cho test runs cũ
• Không có historical data để phân tích xu hướng failures
• User phải re-run tests để xem chi tiết
```

### Hạn Chế 2: Không Lưu Failure Explanation Context

```
HIỆN TẠI:
─────────
• LlmSuggestionCache chỉ lưu explanation output
• Không lưu input context (test definition + actual result)
• Khi Redis expires → Không có đủ data để re-generate

KẾT QUẢ:
─────────
• Không thể re-generate explanation mà không có original data
• Cache miss = Must re-run entire test
```

### Hạn Chế 3: TTL Cứng Không Linh Hoạt

```
HIỆN TẠI:
─────────
• TestResults TTL: subscription.RetentionDays (default 7 days)
• FailureExplanation TTL: 24 hours (fixed in config)
• Không có cơ chế extend TTL khi user access

KẾT QUẢ:
─────────
• Important test results có thể expire trước khi analyze xong
• Không có ưu tiên cho failed tests vs passed tests
```

---

## 💡 Best Solutions (Theo Thứ Tự Ưu Tiên)

### Solution 1: Persist Test Case Results vào PostgreSQL (RECOMMENDED)

**Concept**: Lưu chi tiết test case results vào PostgreSQL song song với Redis.

```
┌─────────────────────────────────────────────────────────────────┐
│                    PROPOSED ARCHITECTURE                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Test Run Completes                                             │
│        │                                                        │
│        ▼                                                        │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Redis Cache (Hot Data - Fast Access)                   │   │
│  │  • Same as current                                       │   │
│  │  • TTL: 7 days (configurable)                           │   │
│  │  • Purpose: Quick retrieval for recent runs             │   │
│  └─────────────────────────────────────────────────────────┘   │
│        │                                                        │
│        ▼ (Parallel Write)                                       │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  PostgreSQL (Cold Storage - Permanent)                  │   │
│  │  ─────────────────────────────────────────────────────  │   │
│  │  NEW TABLE: TestCaseResults                             │   │
│  │  • TestRunId (FK)                                       │   │
│  │  • TestCaseId                                           │   │
│  │  • Status (Passed/Failed/Skipped/Error)                 │   │
│  │  • ActualStatusCode                                     │   │
│  │  • ActualResponseBody (compressed)                      │   │
│  │  • FailureReasons (JSON array)                          │   │
│  │  • DurationMs                                           │   │
│  │  • ExecutedAt                                           │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Implementation Steps**:

```sql
-- 1. Create new table
CREATE TABLE "testexecution"."TestCaseResults" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "TestRunId" uuid NOT NULL REFERENCES "testexecution"."TestRuns"("Id"),
    "TestCaseId" uuid NOT NULL,
    "Status" smallint NOT NULL, -- 0=Passed, 1=Failed, 2=Skipped, 3=Error
    "ActualStatusCode" integer,
    "ActualResponseBodyHash" text, -- SHA256 for deduplication
    "ActualResponseBody" bytea, -- Compressed with Brotli
    "FailureReasons" jsonb,
    "DurationMs" integer,
    "ExecutedAt" timestamptz NOT NULL,
    "CreatedDateTime" timestamptz NOT NULL DEFAULT NOW()
);

-- 2. Indexes for common queries
CREATE INDEX "IX_TestCaseResults_TestRunId" ON "testexecution"."TestCaseResults" ("TestRunId");
CREATE INDEX "IX_TestCaseResults_Status" ON "testexecution"."TestCaseResults" ("Status") WHERE "Status" = 1;
```

```csharp
// 3. Modify TestResultCollector.CollectAsync()
public async Task<TestRunResultModel> CollectAsync(
    TestRun run,
    IReadOnlyList<TestCaseExecutionResult> caseResults,
    int retentionDays,
    CancellationToken ct = default)
{
    // ... existing code ...
    
    // NEW: Persist to PostgreSQL (always)
    await PersistTestCaseResultsAsync(run.Id, caseResults, ct);
    
    // Existing: Save to Redis (may fail)
    try
    {
        await _cache.SetStringAsync(run.RedisKey, payload, cacheOptions, ct);
        resultModel.ResultsSource = "cache";
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to cache results, using database fallback");
        resultModel.ResultsSource = "database";
    }
    
    return resultModel;
}
```

**Pros**:
- ✅ Chi tiết results KHÔNG bao giờ mất
- ✅ Failure Explanation có thể tạo bất cứ lúc nào
- ✅ Historical analysis possible
- ✅ Backward compatible (không break existing logic)

**Cons**:
- ⚠️ Tăng database storage
- ⚠️ Cần migration cho data structure

**Estimated Effort**: 3-5 days

---

### Solution 2: Archive Failure Context Before Expiration

**Concept**: Tự động archive failure context vào PostgreSQL trước khi Redis expires.

```
┌─────────────────────────────────────────────────────────────────┐
│                    ARCHIVE FLOW                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Background Job (hourly)                                        │
│        │                                                        │
│        ▼                                                        │
│  Query TestRuns where:                                          │
│    • ResultsExpireAt < NOW + 24 hours                          │
│    • HasFailedCases = true                                      │
│    • NOT already archived                                       │
│        │                                                        │
│        ▼                                                        │
│  For each expiring run:                                         │
│    1. Fetch results from Redis                                  │
│    2. Extract failed cases only                                 │
│    3. Build TestFailureExplanationContextDto                    │
│    4. Save to ArchivedFailureContext table                      │
│    5. Mark run as archived                                      │
│        │                                                        │
│        ▼                                                        │
│  ArchivedFailureContext available forever                       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Pros**:
- ✅ Chỉ lưu failed cases (tiết kiệm storage)
- ✅ Không thay đổi hot path
- ✅ Failure Explanation vẫn hoạt động sau expiration

**Cons**:
- ⚠️ Background job complexity
- ⚠️ 24-hour gap có thể miss data nếu job fails
- ⚠️ Passed test details vẫn mất

**Estimated Effort**: 2-3 days

---

### Solution 3: Extend TTL On Access (LRU-like)

**Concept**: Tự động extend Redis TTL mỗi khi user access results.

```csharp
// GetTestRunResultsQuery.cs
public async Task<TestRunResultModel> HandleAsync(...)
{
    // ... existing fetch logic ...
    
    // NEW: Extend TTL on successful access
    if (resultModel.ResultsSource == "cache")
    {
        var newExpiration = DateTimeOffset.UtcNow.AddDays(retentionDays);
        await _cache.ExtendTtlAsync(run.RedisKey, newExpiration);
        
        // Update PostgreSQL too
        run.ResultsExpireAt = newExpiration;
        await _runRepository.UpdateAsync(run, ct);
    }
    
    return resultModel;
}
```

**Pros**:
- ✅ Frequently accessed results stay longer
- ✅ Minimal code changes
- ✅ Self-cleaning (unused results still expire)

**Cons**:
- ⚠️ Không giải quyết root cause (vẫn có thể expire)
- ⚠️ Unpredictable storage usage
- ⚠️ One-time access runs vẫn bị mất

**Estimated Effort**: 0.5-1 day

---

### Solution 4: Re-run On Demand (Current Workaround)

**Concept**: Cho phép user re-run test case/suite từ UI khi results expired.

```
┌─────────────────────────────────────────────────────────────────┐
│                    USER FLOW                                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  User clicks "View Failure Explanation"                         │
│        │                                                        │
│        ▼                                                        │
│  API returns: resultsSource=unavailable                         │
│        │                                                        │
│        ▼                                                        │
│  UI shows:                                                      │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  ⚠️ Kết quả test run đã hết hạn trong cache.             │  │
│  │                                                          │  │
│  │  [🔄 Chạy lại test case này]  [📋 Chạy lại toàn bộ run]  │  │
│  └──────────────────────────────────────────────────────────┘  │
│        │                                                        │
│        ▼ User clicks                                            │
│                                                                 │
│  POST /api/test-suites/{suiteId}/test-runs                     │
│  { "testCaseIds": ["{testCaseId}"] }  // Single case re-run    │
│        │                                                        │
│        ▼                                                        │
│  New test run created with fresh results                        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Pros**:
- ✅ Đã được suggest trong error message
- ✅ Không cần backend changes
- ✅ Always gets fresh data

**Cons**:
- ⚠️ Poor UX (user must wait for re-run)
- ⚠️ May produce different results (environment changed)
- ⚠️ Không giải quyết historical analysis needs

**Estimated Effort**: 0 (already implemented)

---

## 📊 So Sánh Solutions

| Criteria | Solution 1 | Solution 2 | Solution 3 | Solution 4 |
|----------|------------|------------|------------|------------|
| **Giải quyết root cause** | ✅ Yes | ⚠️ Partial | ❌ No | ❌ No |
| **Historical data** | ✅ Full | ⚠️ Failures only | ❌ No | ❌ No |
| **Implementation effort** | 3-5 days | 2-3 days | 0.5-1 day | 0 |
| **Storage impact** | High | Medium | Variable | None |
| **UX improvement** | ✅ Excellent | ✅ Good | ⚠️ Minimal | ❌ Poor |
| **Backward compatible** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |

---

## 🏆 Recommended Approach

### Phase 1 (Immediate): Solution 3 + Solution 4 Enhancement

```
Effort: 1-2 days
─────────────────

1. Implement TTL extension on access (Solution 3)
   - Frequently viewed results stay longer
   - Reduces occurrence of the error

2. Enhance error message with direct action button (Solution 4)
   - Better UX for when expiration does occur
   - Clear call-to-action
```

### Phase 2 (Short-term): Solution 1

```
Effort: 3-5 days
─────────────────

1. Design TestCaseResults table schema
2. Implement dual-write in TestResultCollector
3. Modify GetTestRunResultsQuery to fallback to PostgreSQL
4. Modify GetFailureExplanationContextAsync to use PostgreSQL
5. Add migration
6. Backfill existing data (optional)
```

### Phase 3 (Long-term): Analytics & Reporting

```
Effort: 5-7 days
─────────────────

1. Build failure trend dashboard
2. Implement failure pattern detection
3. Proactive failure explanation caching
4. Integration with CI/CD pipelines
```

---

## 🔧 Quick Fix (If Needed Immediately)

Nếu cần fix ngay để unblock users, có thể implement workaround trong frontend:

```typescript
// Frontend: When resultsSource=unavailable
const handleUnavailableResults = async (testRunId: string, testCaseId: string) => {
  const confirmed = await showConfirmDialog({
    title: 'Kết quả đã hết hạn',
    message: 'Kết quả test run không còn trong cache. Bạn có muốn chạy lại test case này?',
    confirmText: 'Chạy lại',
    cancelText: 'Hủy',
  });
  
  if (confirmed) {
    // Trigger single test case re-run
    await api.post(`/test-suites/${suiteId}/test-runs`, {
      testCaseIds: [testCaseId],
      inheritEnvironmentFrom: testRunId, // Use same environment
    });
    
    // Poll for completion and show results
  }
};
```

---

## 📚 References

| File | Purpose |
|------|---------|
| `TestResultCollector.cs` | Cache write logic |
| `GetTestRunResultsQuery.cs` | Cache read logic |
| `TestFailureReadGatewayService.cs` | Failure context retrieval |
| `LlmFailureExplainer.cs` | Failure explanation generation |
| `LlmSuggestionCache.cs` | Explanation cache entity |
| `FailureExplanationOptions.cs` | Configuration |

---

## ✅ Kết Luận

Thông báo `"resultsSource=unavailable"` là **expected behavior** khi Redis cache expires. Tuy nhiên, thiết kế hiện tại có limitation là chi tiết test results chỉ tồn tại trong Redis (volatile storage).

**Recommended action**: Implement **Solution 1** (persist to PostgreSQL) để đảm bảo chi tiết test results và failure explanation luôn available, không phụ thuộc vào Redis TTL.

---

*Document created: 2026-04-06*
*Author: AI Assistant*
*Repository: GSP26SE43.ModularMonolith*
