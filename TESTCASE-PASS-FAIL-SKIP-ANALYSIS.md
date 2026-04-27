# Phân Tích Cơ Chế Xác Định Kết Quả TestCase: PASS / FAIL / SKIPPED

> **Mục đích tài liệu:** Trả lời triệt để câu hỏi của hội đồng bảo vệ:
> *"Dựa vào yếu tố / nguyên nhân nào để xác định một test case là PASS, FAIL hay SKIPPED?"*

> **Bản rà soát codebase:** 2026-04-27, chạy GitNexus CLI trên repo `GSP26SE43.ModularMonolith`.
> GitNexus index đang up-to-date tại commit `faa0b92` với 1565 files, 2495 symbols, 2580 edges. Lưu ý: trong session này GitNexus MCP không expose resource và GitNexus CLI chỉ trả được node mức File/Section cho repo này, nên phần context/impact theo symbol ngắn không resolve được. Phân tích dưới đây được cross-check bằng GitNexus CLI + đọc trực tiếp code nguồn.

---

## 1. Mục Đích Dự Án (Context Tổng Quan)

Dự án là một **hệ thống kiểm thử API tự động** (Automated API Testing System) xây dựng trên kiến trúc .NET Modular Monolith. Mục tiêu cốt lõi:

| Giai đoạn | Chức năng |
|-----------|-----------|
| **Ingest** | Nhập tài liệu API (OpenAPI/Swagger, Postman, cURL, nhập tay) |
| **Generate** | Sinh test case tự động (happy-path, boundary, negative) bằng rule-based + LLM |
| **Execute** | Chạy test theo thứ tự phụ thuộc (dependency-aware), trích xuất biến giữa các test |
| **Validate** | Đánh giá kết quả bằng rule-based deterministic (LLM **không** ảnh hưởng pass/fail) |
| **Report** | Giải thích thất bại bằng AI, xuất báo cáo PDF/CSV |

**Vấn đề cốt lõi được giải quyết:** Người phát triển API mất nhiều thời gian viết test thủ công và khó kiểm tra toàn diện các edge case. Hệ thống này tự động hóa toàn bộ vòng đời kiểm thử từ spec đến report.

---

## 2. Luồng Xử Lý Một TestCase (Execution Flow)

```
TestExecutionOrchestrator.ExecuteAsync()
        │
        ▼
[1] AnalyzeDependencies()
        │
        ├── Có dependency thất bại?
        │       YES ──► BuildSkippedResult() ──► Status = "Skipped"
        │       NO ──► ExecuteSuccessfulPathAsync()
        │
        ▼
[2] PreExecutionValidator.Validate()  ← kiểm tra TRƯỚC khi gửi HTTP
        │
        ├── Có lỗi cấu hình?
        │       YES ──► Status = "Failed" (không gửi request)
        │       NO ──► tiếp tục
        │
        ▼
[3] VariableResolver.Resolve()
        │
        ├── UnresolvedVariableException?
        │       YES ──► Status = "Failed" (UNRESOLVED_VARIABLE)
        │       NO ──► tạo ResolvedTestCaseRequest
        │
        ▼
[4] HttpTestExecutor.ExecuteAsync()  ← gửi HTTP request thực tế
        │
        ▼
[5] VariableExtractor.Extract()  ← trích xuất biến từ response → VariableBag
        │
        ▼
[6] RuleBasedValidator.Validate()  ← kiểm tra response theo expectation
        │
        └── IsPassed = true ──► Status = "Passed"
            IsPassed = false ──► Status = "Failed"
```

---

## 3. Các Yếu Tố Xác Định Status = "PASSED"

Một test case được đánh dấu **PASSED** khi **tất cả** các điều kiện sau đều thoả mãn:

### 3.1. Pre-Execution Validation — không có lỗi cứng

| Kiểm tra | Mô tả | Failure Code |
|----------|-------|--------------|
| BaseUrl hợp lệ | `ExecutionEnvironment.BaseUrl` không rỗng | `MISSING_BASE_URL` |
| Path params đầy đủ | Mọi `{id}`, `{userId}` trong URL đã có giá trị | `MISSING_PATH_PARAM` |
| Placeholder resolvable | `{{variableName}}` phải tồn tại trong VariableBag | `UNRESOLVABLE_PATH_PARAM` |
| Required query params | Các query param bắt buộc theo OpenAPI spec | `MISSING_REQUIRED_QUERY_PARAM` |
| Request body hợp lệ | Endpoint cần body thì body không được rỗng | `MISSING_REQUIRED_BODY` |

### 3.2. Variable Resolution — không có exception

Tất cả placeholder `{{token}}`, `{{userId}}` trong URL/header/body phải được resolve từ VariableBag. Nếu không → `UNRESOLVED_VARIABLE` → **FAILED**.

### 3.3. RuleBasedValidator — 7 checks, không có failure

| Check # | Tên | Điều kiện PASS |
|---------|-----|----------------|
| 1 | **Status Code** | HTTP status code nằm trong danh sách `ExpectedStatus` |
| 2 | **Response Schema** | Body khớp với JSON Schema được định nghĩa (hoặc fallback từ OpenAPI) |
| 3 | **Header Match** | Các response header được chỉ định có giá trị khớp |
| 4 | **Body Contains** | Response body chứa chuỗi/pattern được yêu cầu |
| 5 | **Body Not Contains** | Response body không chứa chuỗi bị cấm |
| 6 | **JSONPath Checks** | Giá trị tại JSONPath chỉ định khớp với expected value |
| 7 | **Response Time** | Thời gian phản hồi ≤ `MaxResponseTimeMs` |

> **`IsPassed = (Failures.Count == 0)`** — chỉ cần 1 failure là FAILED.

---

## 4. Các Yếu Tố Xác Định Status = "FAILED"

### 4.1. Transport Error (không kết nối được)

```
TransportError != null  →  FAILED  (code: HTTP_REQUEST_ERROR)
```

Ví dụ: timeout, kết nối bị từ chối, server cold-start → hệ thống ghi rõ chi tiết lỗi transport.

### 4.2. Pre-Execution Config Error

Request có vấn đề cấu hình trước khi gửi (xem bảng §3.1) → FAILED ngay, **không gửi HTTP request**.

### 4.3. Unresolved Variable

`{{token}}` chưa có trong VariableBag → `UnresolvedVariableException` → FAILED.

**Nguyên nhân thường gặp:** Test trước đó chưa extract được token (dependency chưa chạy, hoặc extract rule sai).

### 4.4. Validation Failure (sau khi nhận response)

Một hoặc nhiều trong 7 checks của `RuleBasedValidator` trả về failure.

**Bảng các failure code thường gặp:**

| Code | Nguyên nhân |
|------|-------------|
| `STATUS_CODE_MISMATCH` | Server trả 400 nhưng test mong đợi 200 |
| `RESPONSE_SCHEMA_MISMATCH` | Response body thiếu field bắt buộc hoặc sai kiểu dữ liệu |
| `RESPONSE_NOT_JSON` | Response rỗng hoặc không phải JSON khi có schema check |
| `HEADER_MISMATCH` | Header `Content-Type` hoặc custom header không khớp |
| `BODY_CONTAINS_FAILED` | Body phải chứa chuỗi X nhưng không có |
| `JSONPATH_MISMATCH` | `$.data.id` không bằng giá trị expected |
| `RESPONSE_TIME_EXCEEDED` | API phản hồi chậm hơn ngưỡng cho phép |
| `NO_EXPECTATION` | StrictMode = true nhưng test không có expectation |

### 4.5. Strict Mode

Khi `strictValidation = true`:
- Test case **không có expectation** → FAILED (`NO_EXPECTATION`)
- Schema parse lỗi → FAILED thay vì Warning

---

## 5. Các Yếu Tố Xác Định Status = "SKIPPED"

### 5.1. Nguyên nhân duy nhất gây SKIP

```csharp
// TestExecutionOrchestrator.cs — AnalyzeDependencies()
var failedDepIds = testCase.DependencyIds
    .Where(depId =>
        context.CaseResultMap.TryGetValue(depId, out var depResult)
        && !IsDependencySatisfied(depResult))
    .ToList();

if (failedDepIds.Count > 0)
    result = BuildSkippedResult(testCase, attempt, failedDepIds, rootCause);
```

**Một test case bị SKIP khi ít nhất 1 dependency (test case phụ thuộc) KHÔNG được thoả mãn.**

### 5.2. Định nghĩa "Dependency Satisfied" (quan trọng!)

```csharp
private static bool IsDependencySatisfied(TestCaseExecutionResult dep)
{
    // Case 1: dependency PASSED rõ ràng
    if (dep.Status == "Passed") return true;

    // Case 2: dependency nhận HTTP 2xx VÀ chỉ thất bại về expectation mismatch
    // → Data đã được tạo trên server, downstream vẫn có thể dùng được
    return dep.HttpStatusCode is >= 200 and < 300
        && IsDependencyFailureOnlyExpectationMismatch(dep.FailureReasons);
}
```

**Điều này có nghĩa:** Một dependency "Failed" vì `STATUS_CODE_MISMATCH` hoặc `RESPONSE_SCHEMA_MISMATCH` nhưng vẫn nhận HTTP 200 → dependency **được xem là satisfied** → downstream test vẫn CHẠY, không bị SKIP.

Chỉ khi dependency thật sự không tạo được resource (4xx, 5xx, transport error, pre-validation error) thì downstream mới bị SKIP.

### 5.3. Skip Message chi tiết

```
"Test case skipped because dependency failed. 
RootCause=<DependencyName> (<id>) => Status=Failed, HttpStatus=404, 
FailureCodes=STATUS_CODE_MISMATCH, FailureDetails=..."
```

Hệ thống lưu đầy đủ:
- `SkippedCause`: chuỗi mô tả nguyên nhân gốc
- `SkippedBecauseDependencyIds`: danh sách GUID của các dependency thất bại
- `DependencyIds`: toàn bộ dependency của test case này

---

## 6. Giải Pháp Cho SKIPPED — "Solution Is What?"

### 6.1. Tại sao không chạy test bị skip ngay?

SKIP là **quyết định thiết kế có chủ ý**, không phải bug:
- Test case B phụ thuộc test case A tạo resource → nếu A fail, B sẽ gửi request với dữ liệu không tồn tại → kết quả B sẽ sai lệch (false negative/positive)
- Chạy B khi A đã fail sẽ che giấu nguyên nhân thật và làm khó debug

### 6.2. Retry Policy — giải pháp cho transient failure

```csharp
// Khi dependency fail vì transient error (5xx, timeout):
retryPolicy.EnableRetry = true
retryPolicy.MaxRetryAttempts = N   // mặc định có thể cấu hình
```

Hệ thống retry dependency (test A) tối đa `MaxRetryAttempts` lần. Nếu A pass sau retry → B được chạy bình thường.

**Lưu ý:** Chỉ retry khi có `HttpStatusCode` (lỗi transient từ server). Lỗi cấu hình (pre-validation, unresolved variable) → **không retry** vì là deterministic error.

### 6.3. Cascading Replay — giải pháp cho skip theo chuỗi

```csharp
// Sau mỗi test case, hệ thống kiểm tra lại các skipped case
if (effectivePolicy.RerunSkippedCases)
{
    await ReplayEligibleSkippedCasesAsync(orderedCases, context, ct);
}
```

**Flow:**
1. Test A bị skip (vì dep X fail)
2. Test X được retry → PASS
3. Hệ thống detect: A đã skipped, mọi dep của A đã satisfied → **replay A**
4. Cascading: nếu A pass, các test phụ thuộc A cũng được replay

Loop tiếp tục cho đến khi không còn test nào eligible để replay (hoặc đạt `orderedCases.Count + 1` pass để tránh vòng lặp vô hạn).

### 6.4. Hướng Dẫn Debug SKIP

| Triệu chứng | Nguyên nhân | Giải pháp |
|-------------|-------------|-----------|
| Test B skip vì test A status=Failed | A có transport error / pre-validation error | Sửa config environment hoặc test data của A |
| Test B skip vì `{{userId}}` unresolved | Test A không extract được `userId` | Thêm/sửa variable extraction rule trong test A |
| Nhiều test skip theo chuỗi | Root dependency fail | Tìm test có `OrderIndex` nhỏ nhất trong chain, fix nó |
| Test vẫn skip dù dep đã pass trước đó | Dependency result không được propagate | Kiểm tra `DependencyIds` có đúng GUID không |

---

## 7. Sơ Đồ Quyết Định (Decision Tree)

```
START: Chạy TestCase T
          │
          ▼
    T có DependencyIds?
     │           │
    NO           YES
     │            │
     │         Tất cả deps satisfied?
     │          │              │
     │         YES             NO
     │          │               │
     │          │           ► SKIPPED
     │          │           (lưu SkippedCause, SkippedBecauseDependencyIds)
     │          │
     ▼          ▼
PreExecutionValidator.Validate()
     │
  HasErrors?
  │       │
 YES      NO
  │        │
FAILED    VariableResolver.Resolve()
          │
    UnresolvedVariable?
    │              │
   YES             NO
    │               │
  FAILED       HttpTestExecutor.ExecuteAsync()
                    │
             TransportError?
             │           │
            YES           NO
             │             │
           FAILED      VariableExtractor (cập nhật VariableBag)
                            │
                       RuleBasedValidator.Validate() [7 checks]
                            │
                    All checks passed?
                    │              │
                   YES             NO
                    │               │
                 PASSED           FAILED
                                (lưu FailureReasons)
```

---

## 8. Dữ Liệu Được Lưu Cho Mỗi Kết Quả

### PostgreSQL (cold storage — `testexecution.TestCaseResults`)
Lưu vĩnh viễn để tra cứu khi Redis hết hạn:
- `Status`, `HttpStatusCode`, `DurationMs`
- `FailureReasons` (JSONB), `ExtractedVariables` (JSONB)
- `DependencyIds`, `SkippedBecauseDependencyIds` (JSONB arrays)
- `StatusCodeMatched`, `SchemaMatched`, `HeaderChecksPassed`, v.v.

### Redis (hot cache — TTL theo subscription plan)
Lưu toàn bộ `TestRunResultModel` bao gồm:
- Danh sách case results chi tiết
- Attempt tree (retry/replay history)
- Request/response preview (truncated 64KB)

### Tóm tắt TestRun
- `PassedCount`, `FailedCount`, `SkippedCount`
- `TotalDurationMs`
- `Status` của `TestRun` là **lifecycle status**, không phải verdict thuần túy:
  - `Failed` khi có execution failure không nhận được HTTP response (`Failed` và `HttpStatusCode == null`)
  - `Completed` vẫn có thể có `FailedCount > 0` nếu fail là assertion/validation sau khi server đã trả HTTP response
  - Vì vậy UI/report phải đọc `PassedCount`, `FailedCount`, `SkippedCount` để kết luận chất lượng API, không chỉ nhìn `TestRun.Status`

---

## 9. Adaptive Status Matching — Tính Năng Đặc Biệt

Hệ thống có cơ chế **Adaptive Status Match** để xử lý trường hợp LLM sinh test case cho môi trường test Petstore (permissive):

```csharp
private static bool TryApplyAdaptiveStatusMatch(
    HttpTestResponse response,
    List<int> expectedStatuses,
    ExecutionTestCaseDto testCase,
    TestCaseValidationResult result)
```

Nếu test mong đợi `[200]` nhưng server trả `404` do data test không tồn tại trong môi trường demo → hệ thống có thể accept một số trường hợp đặc biệt thay vì fail cứng, giảm false negative cho môi trường development.

**Gap quan trọng:** adaptive mode phù hợp môi trường demo/permissive, nhưng không phù hợp khi cần chứng minh SRS strict. Ví dụ negative test mong `[400, 422]` nhưng API trả `200` hiện có thể được mark Passed với warning `ADAPTIVE_PERMISSIVE_STATUS_MATCH`. Với requirement kiểu "dữ liệu sai phải bị reject", đây phải là `VIOLATED`, không nên là `VALIDATED`.

---

## 10. Mối Quan Hệ Giữa SRS và Kết Quả TestCase

### 10.1. SRS ở đâu trong luồng?

```
SrsDocument (upload/text)
        │
        ▼ (Phase 1 — n8n webhook)
LLM phân tích → SrsRequirement[] (REQ-001, REQ-002, ...)
        │          - TestableConstraints
        │          - Assumptions / Ambiguities
        │          - ConfidenceScore
        │
        ▼ (Phase 1.5 nếu có clarification)
User trả lời SrsRequirementClarification → LLM re-analyze
        │
        ▼ (Phase 2 — test generation)
LLM sinh TestCase[] + tạo TestCaseRequirementLink
        │
        ▼ (Phase 3 — test execution)
TestExecutionOrchestrator → PASS / FAIL / SKIPPED
```

**SRS nằm ở giai đoạn UPSTREAM (sinh test), không nằm trong logic đánh giá PASS/FAIL/SKIP.**

### 10.2. SRS ảnh hưởng gì đến test case?

| Yếu tố SRS | Ảnh hưởng đến execution |
|------------|------------------------|
| `TestableConstraints` (LLM extract) | Quyết định **Expectation** (expected status, schema, body checks) được sinh ra cho test case |
| `RequirementType` (Functional/Performance/Security) | Định hướng loại test: happy-path, boundary, negative, performance |
| `ConfidenceScore` thấp | LLM gắn warning; test case sinh ra có thể có expectation không chắc chắn |
| `Ambiguities` chưa được clarify | Test case có thể thiếu hoặc sai expectation → dẫn đến FAIL sai |
| `SrsRequirementClarification.UserAnswer` | Phase 1.5 re-analyze → cập nhật constraints → test case được regenerate với expectation chính xác hơn |

### 10.3. Traceability Matrix — đánh giá sau khi chạy test

Sau khi execute, hệ thống cung cấp **Traceability Matrix** (`GetSrsTraceabilityQuery`):

```
TraceabilityMatrix {
    TotalRequirements    : N
    CoveredRequirements  : M    ← có ít nhất 1 test case liên kết
    UncoveredRequirements: N-M  ← requirement chưa có test case nào
    CoveragePercent      : M/N × 100%

    Requirements[i] {
        RequirementCode : "REQ-001"
        IsCovered       : true/false  ← testCaseRefs.Any()
        TestCases       : [ { TestCaseId, TraceabilityScore, MappingRationale } ]
    }
}
```

### 10.4. Điểm quan trọng cần nói với hội đồng

> **`IsCovered = true` KHÔNG có nghĩa test case đó PASS.**

`IsCovered` chỉ kiểm tra: *"có test case nào được liên kết với requirement này không?"* (`testCaseRefs.Any()`). Kết quả pass/fail của test case đó là thông tin riêng biệt từ `TestCaseResult.Status`.

| Tình huống | IsCovered | TestCase.Status | Ý nghĩa |
|-----------|-----------|-----------------|---------|
| REQ-001 có test case, test PASS | true | Passed | Requirement hoạt động đúng ✓ |
| REQ-001 có test case, test FAIL | true | Failed | Requirement bị vi phạm — cần fix API |
| REQ-001 có test case, test SKIP | true | Skipped | Chưa verify được do dependency fail |
| REQ-002 không có test case nào | false | — | Requirement chưa được kiểm thử — gap coverage |

### 10.5. Tại sao không gộp SRS vào logic pass/fail?

Thiết kế cố ý tách biệt **deterministic validation** (rule-based) khỏi **semantic requirement matching** (LLM):
- Rule-based validator chạy mỗi test case, cần tốc độ cao và kết quả deterministic
- SRS requirement coverage là báo cáo tổng hợp ở level project, không phải per-request decision
- LLM có thể sai khi map requirement ↔ test case → nếu nhúng vào pass/fail sẽ gây false positive/negative không kiểm soát được

---

## 11. GitNexus Codebase Review — Gap Hiện Có Và Best Solution

### 11.1. Nguyên tắc codebase cần giữ

Từ codebase hiện tại, solution nên giữ các nguyên tắc sau:

| Nguyên tắc | Ý nghĩa khi resolve gap |
|-----------|--------------------------|
| Modular Monolith | Mỗi module sở hữu schema/DbContext riêng (`testgen`, `testexecution`). Không đọc thẳng DbContext của module khác. |
| Cross-module qua `Contracts` gateway | Nếu `TestGeneration` cần execution evidence, tạo interface trong `ClassifiedAds.Contracts.TestExecution`, implement ở `TestExecution`, inject qua DI. |
| Deterministic verdict | LLM không được trực tiếp quyết định PASS/FAIL. LLM chỉ sinh expectation hoặc giải thích failure. |
| EF migration ở Migrator | Nếu thêm cột/entity để persist evidence, tạo migration trong `ClassifiedAds.Migrator` và chạy `--verify-migrations`. |
| Docker/compose wiring | Nếu thêm project/module/runtime dependency, phải update Dockerfile restore layer và compose nếu cần env/runtime service mới. |

### 11.2. Gap #1 — SRS chưa thật sự điều khiển expectation đủ mạnh

Hiện tại `TestGenerationPayloadBuilder` có đưa `SrsRequirements` vào payload generate, nhưng DTO `N8nSrsRequirement` chỉ gồm:

```csharp
Id, Code, Title, Description
```

Trong khi entity/model đã có dữ liệu quan trọng:

```csharp
RequirementType, TestableConstraints, Assumptions, Ambiguities,
ConfidenceScore, RefinedConstraints, RefinedConfidenceScore
```

Điều này tạo gap: tài liệu nói "SRS → TestableConstraints → Expectation", nhưng code generate hiện chưa truyền constraint/refinement đầy đủ cho n8n/LLM.

**Best solution: enrich generation payload, không cần migration nếu chỉ thêm DTO fields.**

Update:

- `GenerateTestCasesCommand.N8nSrsRequirement`
- `TestGenerationPayloadBuilder`
- unified prompt response format/rules
- unit tests của payload builder

Đề xuất contract:

```csharp
public class N8nSrsRequirement
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string RequirementType { get; set; }
    public string EffectiveConstraints { get; set; } // RefinedConstraints ?? TestableConstraints
    public string Assumptions { get; set; }
    public string Ambiguities { get; set; }
    public float? ConfidenceScore { get; set; }
}
```

Sau đó yêu cầu LLM sinh expectation từ `EffectiveConstraints`, và vẫn bắt buộc trả `coveredRequirementIds`.

### 11.3. Gap #2 — TraceabilityMatrix chỉ là coverage, chưa là validation

Hiện tại `TraceabilityTestCaseRef` chỉ có:

```csharp
public class TraceabilityTestCaseRef
{
    public Guid TestCaseId { get; set; }
    public string TestCaseName { get; set; }
    public float? TraceabilityScore { get; set; }
    public string MappingRationale { get; set; }
    // THIẾU: LastRunStatus, LastRunAt, FailureReasons, Evidence
}
```

`GetSrsTraceabilityQuery` hiện chỉ join `SrsRequirement -> TestCaseRequirementLink -> TestCase`. `IsCovered = testCaseRefs.Any()` chỉ trả lời "requirement có test case chưa", chưa trả lời "API đã thỏa mãn requirement chưa".

**Best solution: enrich traceability bằng execution evidence qua contract gateway.**

Không nên để `TestGeneration` reference trực tiếp `ClassifiedAds.Modules.TestExecution`. Thay vào đó:

1. Tạo interface trong `ClassifiedAds.Contracts.TestExecution.Services`, ví dụ `ITestCaseExecutionEvidenceReadGatewayService`.
2. Implement trong `ClassifiedAds.Modules.TestExecution`.
3. Register implementation trong `TestExecutionServiceCollectionExtensions`.
4. Inject interface vào `GetSrsTraceabilityQueryHandler`.
5. Khi WebAPI register cả `AddTestGenerationModule()` và `AddTestExecutionModule()`, query có thể enrich matrix mà vẫn giữ module boundary.

Gateway trả về final result theo `runId` nếu caller truyền, hoặc latest finished run của suite nếu không truyền:

```csharp
public sealed class TestCaseExecutionEvidenceDto
{
    public Guid TestSuiteId { get; set; }
    public Guid TestRunId { get; set; }
    public int RunNumber { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid TestCaseId { get; set; }
    public string Status { get; set; }          // Passed / Failed / Skipped
    public int? HttpStatusCode { get; set; }
    public IReadOnlyList<string> FailureCodes { get; set; }
    public string FailureSummary { get; set; }
}
```

#### Công thức tính Requirement Validation Status

```
RequirementValidationStatus =

  UNCOVERED   nếu  IsCovered = false
                   (không có test case nào)

  UNVERIFIED  nếu  IsCovered = true
               và  chưa có TestRun nào chạy các test case này

  VALIDATED   nếu  IsCovered = true
               và  TẤT CẢ test cases linked → LastRunStatus = "Passed"

  VIOLATED    nếu  IsCovered = true
               và  ÍT NHẤT 1 test case linked → LastRunStatus = "Failed"

  PARTIAL     nếu  IsCovered = true
               và  có mix: một số Passed, một số Skipped (chưa verify được hết)
```

Điều chỉnh thêm cho thực tế codebase:

- `SKIPPED_ONLY`: tất cả linked tests đều skipped → requirement chưa verify được do dependency/root cause.
- `WARNING_ACCEPTED`: test passed nhưng có adaptive warning quan trọng → không dùng làm validated tuyệt đối trong SRS strict mode.
- `INCONCLUSIVE`: không có latest run hoặc result đã hết/cannot reconstruct.

#### Luồng join data đề xuất

```
SrsRequirement (REQ-001)
    │
    ├── TestCaseRequirementLink (TestCase A, B, C)
    │         │
    │         └── JOIN TestCaseResult (từ TestRun mới nhất của suite)
    │               ├── A → Passed
    │               ├── B → Passed
    │               └── C → Failed  ← vi phạm
    │
    └── RequirementValidationStatus = VIOLATED
        FailureEvidence = "Test case C: STATUS_CODE_MISMATCH [expected 201, got 400]"
```

#### Extended TraceabilityMatrix

```csharp
public class TraceabilityTestCaseRef
{
    public Guid TestCaseId { get; set; }
    public string TestCaseName { get; set; }
    public float? TraceabilityScore { get; set; }
    public string MappingRationale { get; set; }

    public string LastRunStatus { get; set; }    // Passed / Failed / Skipped / null
    public Guid? LastRunId { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public int? HttpStatusCode { get; set; }
    public List<string> FailureCodes { get; set; }
    public string FailureSummary { get; set; }
}

public class TraceabilityRequirementRow
{
    public string ValidationStatus { get; set; }
    public string ValidationSummary { get; set; }
}

public class TraceabilityMatrix
{
    public Guid? EvidenceRunId { get; set; }
    public int ValidatedRequirements { get; set; }
    public int ViolatedRequirements { get; set; }
    public int PartialRequirements { get; set; }
    public int UnverifiedRequirements { get; set; }
    public double ValidationPercent { get; set; }
}
```

### 11.4. Gap #3 — `PrimaryRequirementId` có entity field nhưng chưa set

`TestCase.PrimaryRequirementId` đã tồn tại, nhưng `SaveAiGeneratedTestCasesCommandHandler` hiện chỉ tạo `TestCaseRequirementLink` từ `CoveredRequirementIds`, chưa set primary requirement.

**Best solution:**

- Khi DTO có `CoveredRequirementIds`, validate IDs thuộc `suite.SrsDocumentId`.
- Set `PrimaryRequirementId = first valid coveredRequirementId`.
- Tạo link với `TraceabilityScore`/`MappingRationale` từ payload nếu LLM cung cấp, không hard-code mãi `1.0f`.
- Nếu LLM trả requirement ID không thuộc SRS document của suite, reject callback hoặc bỏ qua có log rõ ràng; không để FK lỗi ở cuối transaction.

### 11.5. Gap #4 — SRS strict bị yếu bởi adaptive/permissive validation

`RuleBasedValidator` có adaptive rules:

- success status compatibility (`200/201/202`)
- boundary/negative 4xx compatibility
- permissive negative expected 4xx nhưng actual 2xx

Rule này hữu ích cho demo API lỏng lẻo, nhưng không phù hợp để chứng minh SRS.

**Best solution: tách validation profile.**

Thêm profile ở execution request/policy:

```csharp
public enum ValidationProfile
{
    Default = 0,
    DemoAdaptive = 1,
    SrsStrict = 2
}
```

Với `SrsStrict`:

- `Expectation == null` luôn fail (`NO_EXPECTATION`)
- không áp dụng `ADAPTIVE_PERMISSIVE_STATUS_MATCH`
- warning adaptive quan trọng không được tính là validated tuyệt đối
- explicit `ResponseSchema` mismatch là failure
- fallback schema từ OpenAPI có thể là warning, nhưng không đủ để đánh dấu requirement validated nếu requirement yêu cầu schema cụ thể

Nếu không muốn thêm enum ngay, cách tối thiểu là: khi suite có `SrsDocumentId`, FE gọi run với `strictValidation=true` và backend disable permissive adaptive cho tests linked SRS.

### 11.6. Gap #5 — Retry policy comment và implementation chưa khớp

`TestRunRetryPolicyModel` comment nói retry cho expectation mismatch, nhưng `ShouldRetryCase()` hiện retry mọi case `Failed` có `HttpStatusCode`. Ngược lại, transport timeout/connection error có `HTTP_REQUEST_ERROR` nhưng `HttpStatusCode == null`, nên hiện **không retry**, dù đây mới là lỗi transient điển hình.

**Best solution: tách deterministic failure và retryable transient failure.**

```csharp
private static bool ShouldRetryCase(TestCaseExecutionResult result, TestRunRetryPolicyModel policy)
{
    if (policy == null || policy.MaxRetryAttempts <= 0 || result?.Status != "Failed")
        return false;

    if (result.HttpStatusCode is 408 or 429)
        return true;

    if (result.HttpStatusCode is >= 500 and < 600)
        return true;

    return result.FailureReasons?.Any(f => f.Code == "HTTP_REQUEST_ERROR") == true;
}
```

Không retry:

- `UNRESOLVED_VARIABLE`
- `MISSING_BASE_URL`
- `MISSING_PATH_PARAM`
- `MISSING_REQUIRED_BODY`
- 4xx assertion mismatch do API thật sự reject/accept sai theo SRS

### 11.7. Gap #6 — Dependency satisfaction bị duplicate

`IsDependencySatisfied()` đang tồn tại ở 2 nơi trong `TestExecutionOrchestrator` và `ExecutionContextState`. Logic giống nhau hiện tại, nhưng dễ drift về sau.

**Best solution:** extract thành internal static policy class trong TestExecution:

```csharp
internal static class DependencySatisfactionPolicy
{
    public static bool IsSatisfied(TestCaseExecutionResult dependencyResult) { ... }
}
```

Unit tests nên cover:

- Passed → satisfied
- Failed + HTTP 2xx + only `STATUS_CODE_MISMATCH`/`RESPONSE_SCHEMA_MISMATCH` → satisfied
- Failed + HTTP 4xx/5xx → not satisfied
- Failed + `UNRESOLVED_VARIABLE`/pre-validation → not satisfied
- Skipped → not satisfied

### 11.8. Recommended Implementation Plan

```
P0 — Documentation/clarity
  - Cập nhật docs/UI copy: TestRun.Status là lifecycle, verdict nằm ở counters + case statuses.

P1 — SRS generation correctness
  - Enrich N8nSrsRequirement với EffectiveConstraints/Assumptions/Ambiguities/Confidence.
  - Validate coveredRequirementIds thuộc SRS document của suite.
  - Set TestCase.PrimaryRequirementId.

P1 — SRS validation correctness
  - Thêm ValidationProfile hoặc tối thiểu disable permissive adaptive khi SRS strict.
  - Force strict expectation cho linked SRS tests.

P1 — Requirement validation query
  - Thêm Contracts.TestExecution evidence gateway.
  - Implement gateway trong TestExecution đọc TestRuns + TestCaseResults.
  - Enrich GetSrsTraceabilityQuery thành RequirementValidationStatus.

P2 — Execution robustness
  - Refine retry policy theo transient failures.
  - Extract DependencySatisfactionPolicy.
  - Thêm unit tests + integration tests.

P2 — Persistence/reporting hardening
  - Nếu cần report lâu dài sau Redis TTL: persist Warnings, ChecksPerformed/Skipped, SkippedCause, ExpectedStatus, and optionally attempts.
  - Nếu thêm cột/entity: tạo EF migration trong ClassifiedAds.Migrator và chạy verify migrations.
```

### 11.9. Test Plan Cho Solution

| Scope | Test cần có |
|------|-------------|
| Payload builder | Khi suite có SRS, payload chứa `EffectiveConstraints = RefinedConstraints ?? TestableConstraints`. |
| Save callback | Valid coveredRequirementIds tạo links, set `PrimaryRequirementId`, invalid IDs bị reject/ignored có log. |
| Validator | `SrsStrict`: negative expected 4xx actual 2xx phải fail, không pass adaptive. |
| Traceability | Requirement `VALIDATED/VIOLATED/PARTIAL/UNVERIFIED/UNCOVERED` tính đúng theo latest run hoặc runId chỉ định. |
| Retry | Retry 5xx/408/429/HTTP_REQUEST_ERROR; không retry unresolved variable/pre-validation/4xx SRS assertion. |
| Dependency | Duplicate satisfaction logic được xóa, tests cover cả replay skipped cases. |

---

## 12. Tóm Tắt Cho Hội Đồng

| Câu hỏi | Trả lời ngắn gọn |
|---------|-----------------|
| **PASS dựa vào gì?** | 7 validation checks đều pass: status code, schema, headers, body contains/not-contains, JSONPath, response time |
| **FAIL dựa vào gì?** | Ít nhất 1 trong 7 checks fail, HOẶC lỗi transport/config/unresolved variable xảy ra trước khi gửi request |
| **SKIP dựa vào gì?** | Ít nhất 1 dependency (test case phụ thuộc) không thoả mãn: status Failed VÀ không phải chỉ expectation mismatch với HTTP 2xx |
| **Solution cho SKIP?** | (1) Retry policy cho transient failure; (2) Cascading replay khi dependency recover; (3) Debug root dependency và fix config/data |
| **SRS ảnh hưởng pass/fail không?** | **Có — gián tiếp qua 2 tầng:** (1) SRS → LLM → Expectation (binding generation time); (2) Expectation → RuleBasedValidator → PASS/FAIL (execution time) |
| **Làm sao biết API thoả mãn SRS?** | Requirement Validation Status: join TraceabilityMatrix + TestCaseResult → VALIDATED / VIOLATED / PARTIAL / UNVERIFIED / UNCOVERED |
| **LLM có ảnh hưởng pass/fail không?** | **Không trực tiếp.** LLM dịch SRS → Expectation (generation time) và giải thích failure (sau execution). Quyết định PASS/FAIL luôn là rule-based deterministic |

---

*Phân tích được thực hiện bằng GitNexus CLI (index: 1565 files, 2495 symbols, 2580 edges) — trạng thái: up to date tại commit `faa0b92`.*  
*Các file nguồn chính: `TestExecutionOrchestrator.cs`, `RuleBasedValidator.cs`, `PreExecutionValidator.cs`, `TestResultCollector.cs`, `GetSrsTraceabilityQuery.cs`, `TestGenerationPayloadBuilder.cs`, `SaveAiGeneratedTestCasesCommand.cs`.*
