# Bug Report: Category Chain Failure — `{{categoryId}}` Unresolved & Duplicate Name False Pass

**Date:** 2026-05-02  
**RunId:** `bbbfe911-2106-4d55-87b4-b6dbbf782fd5`  
**Target API:** `https://test-llm-api-testing.onrender.com`  
**Affected test count:** ~20+ failed / skipped  

---

## Tóm tắt nhanh

Hai bug độc lập nhưng xảy ra trong cùng một test run gây ra cascade failure trên toàn bộ chain sử dụng `{{categoryId}}`:

| # | Bug | Failure Code | Ảnh hưởng |
|---|-----|--------------|-----------|
| 1 | Negative test "Create category with duplicate name" chạy độc lập → tạo category mới → HTTP 201 thay vì 409 | `STATUS_CODE_MISMATCH` | 1 test Failed |
| 2 | `categoryId` không bao giờ được extract sau khi "Create category happy path" chạy thành công | `UNRESOLVED_VARIABLE` / `UNRESOLVABLE_PATH_PARAM` | ~19+ test Failed/Skipped |

---

## Bug 1: "Create category with duplicate name" → STATUS_CODE_MISMATCH (got 201, expected 400/401)

### Quan sát trong log

```
TestCaseId=a98e2cd3, Status=Failed
FailureCodes=STATUS_CODE_MISMATCH
FailureDetails=STATUS_CODE_MISMATCH: Mã trạng thái không khớp. Mong đợi: [400, 401], thực tế: 201.
DependencyIds=[]
```

Polly log trước đó:
```
Execution attempt. Result: '201' (POST /api/categories)
```

### Root Cause

Test case negative này được LLM sinh ra **không có dependency** (`DependencyIds=[]`) vào test case "Create category happy path" (`25a58a38`).

Khi orchestrator chạy test `a98e2cd3`:
1. Không có category nào với tên đó tồn tại trước đó (vì không đợi happy path chạy trước)
2. Server nhận POST và tạo category MỚI → trả về **201 Created**
3. Test expect 400/401 (vì mong muốn lỗi duplicate) → **FAIL**

**Lý do "đã có category" vẫn không giúp được gì:**
- Test `25a58a38` ("Create category happy path") đã chạy và passed với HTTP 201 — category ĐÃ tồn tại trên server
- Nhưng test `a98e2cd3` không biết về điều đó vì `DependencyIds=[]`
- Hơn nữa, body của `a98e2cd3` rất có thể dùng một **tên tĩnh khác** với tên mà `25a58a38` đã tạo → server coi là tên mới, tạo thành công

### Chain đúng phải là

```
25a58a38: Create category happy path   → POST /api/categories, extract {{categoryName}}
    ↓ dependency
a98e2cd3: Create category duplicate    → POST /api/categories với body.name = {{categoryName}}
                                         → expect 409 Conflict
```

---

## Bug 2: `{{categoryId}}` UNRESOLVED sau khi "Create category happy path" đã chạy thành công

### Quan sát trong log

Test `25a58a38` (Create category happy path) → **Passed**, HTTP 201, `DependencyIds=[]`.

Ngay sau đó, test `33984a13` (Create product happy path, `DependencyIds=[25a58a38]`) fails:
```
Status=Failed
FailureCodes=UNRESOLVED_VARIABLE
FailureDetails=UNRESOLVED_VARIABLE: Variable '{{categoryId}}' trong Body chưa có giá trị.
HttpStatus=(null)   ← request không được gửi đi
```

Toàn bộ các test sau phụ thuộc vào `33984a13` bị **Skipped** với `DEPENDENCY_FAILED`.  
Và các test dùng `{{categoryId}}` trong path param cũng fail `UNRESOLVABLE_PATH_PARAM`.

### Root Cause (3 tầng)

#### Tầng 1 — `BuildResourceIdVariableName` luôn trả về `null` (hard-coded stub)

File: `ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs`, dòng 992:

```csharp
private static string BuildResourceIdVariableName(string urlOrPath) => null;
```

Stub này được giữ nguyên như một "extension hook", **chưa bao giờ được implement**.

Điều này có nghĩa là trong `ExtractImplicitVariables`:

```csharp
var resourceVariableName = BuildResourceIdVariableName(testCase?.Request?.Url);
// resourceVariableName = null VỚI MỌI URL

if (!string.IsNullOrWhiteSpace(resourceVariableName))  // false — không bao giờ chạy
{
    result[resourceVariableName] = identifierValue;     // không bao giờ set "categoryId"
}

result["id"] = identifierValue;  // CHỈ "id" được set, không phải "categoryId"
```

→ Dù API trả về `{"id": "abc-123"}`, implicit extraction chỉ set `variable_bag["id"] = "abc-123"`, **KHÔNG bao giờ set `variable_bag["categoryId"]`**.

#### Tầng 2 — LLM không sinh extraction rule cho `categoryId` trong test `25a58a38`

Test case "Create category happy path" (`25a58a38`) được LLM sinh ra **không có `TestCaseVariable` entry** nào cho `categoryId`. Nếu có, explicit extraction sẽ chạy:

```csharp
var extracted = _variableExtractor.Extract(response, testCase.Variables, resolvedRequest.Body);
// testCase.Variables = [] (trống) → extracted = {}
```

→ Không có biến nào được extract từ explicit rules.

#### Tầng 3 — `ExtractResponseBodyVariables` cũng không giải quyết được

Hàm `ExtractResponseBodyVariables` dùng `TryExtractObjectValues` để flatten toàn bộ JSON body vào variable bag. Ví dụ với response `{"id": "abc-123", "name": "Electronics"}`:

```
variable_bag["id"]   = "abc-123"
variable_bag["name"] = "Electronics"
```

Nếu API trả về `{"id": "abc-123"}` → chỉ có `variable_bag["id"]`, **KHÔNG có `variable_bag["categoryId"]`**.

Downstream test dùng `{{categoryId}}` → unresolved → fail.

### Minh họa luồng thực tế vs mong đợi

```
MONG ĐỢI:
Test 25a58a38 (Create category, HTTP 201)
  → response: {"id": "abc-123", "name": "Electronics"}
  → extracted: { categoryId: "abc-123" }       ← cần có
  → variable_bag: { categoryId: "abc-123" }

Test 33984a13 (Create product, DependencyIds=[25a58a38])
  → body: { "categoryId": "{{categoryId}}" }
  → resolved: { "categoryId": "abc-123" }      ← OK

THỰC TẾ:
Test 25a58a38 (Create category, HTTP 201)
  → response: {"id": "abc-123", "name": "Electronics"}
  → implicit extraction: { id: "abc-123" }      ← chỉ có "id"
  → explicit extraction: {}                     ← không có rule
  → variable_bag: { id: "abc-123" }             ← KHÔNG có "categoryId"

Test 33984a13 (Create product, DependencyIds=[25a58a38])
  → body: { "categoryId": "{{categoryId}}" }
  → PreExecutionValidator: "categoryId" not in variable_bag
  → FAIL: UNRESOLVED_VARIABLE (request không được gửi)
```

---

## Tại sao unit test `Should_ImplicitlyExtractResourceId_WhenVariableRulesMissing` vẫn pass?

Unit test này mock `_variableExtractorMock` để trả `{}` và assert:
```csharp
updateVariables.Should().ContainKey("categoryId");
updateVariables["categoryId"].Should().Be("cat-777");
```

Nếu `BuildResourceIdVariableName` → `null`, `categoryId` KHÔNG được set trong `ExtractImplicitVariables`. Nhưng response body là `{"data":{"_id":"cat-777"}}`, nên `ExtractResponseBodyVariables` sẽ set `variable_bag["data._id"] = "cat-777"` — không phải `categoryId`.

**→ Unit test này đang FAIL hoặc test assertion đang sai.** Cần kiểm tra lại kết quả CI.

---

## Tóm tắt nguyên nhân gốc rễ

| Vấn đề | Nguyên nhân gốc |
|--------|-----------------|
| Bug 1: Duplicate test → 201 thay vì 409 | LLM không sinh `DependencyIds` cho negative test; body dùng tên khác với happy path |
| Bug 2: `{{categoryId}}` unresolved | `BuildResourceIdVariableName` là null-stub chưa implement + LLM không sinh extraction rule trên happy path |

---

## Hướng solution (đề xuất — chưa implement)

### Solution A: Fix `BuildResourceIdVariableName` (toàn diện nhất)

Implement hàm này để ánh xạ URL → tên biến:

```csharp
private static string BuildResourceIdVariableName(string urlOrPath)
{
    if (string.IsNullOrWhiteSpace(urlOrPath)) return null;

    // Lấy segment cuối của base path (bỏ path params)
    // /api/categories → "category" → "categoryId"
    // /api/products   → "product"  → "productId"
    var path = urlOrPath.Split('?')[0].TrimEnd('/');
    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    
    // Bỏ qua các segment có {param}
    var resourceSegment = segments
        .LastOrDefault(s => !s.StartsWith('{') && !s.EndsWith('}'));
    
    if (string.IsNullOrWhiteSpace(resourceSegment)) return null;

    // "categories" → "category", "products" → "product"
    var singular = Singularize(resourceSegment);
    return singular + "Id";
}
```

**Ưu điểm:** Fix toàn bộ trường hợp ngay cả khi LLM không sinh extraction rule.  
**Rủi ro:** Cần cẩn thận với singularization edge cases.

### Solution B: Fix LLM generation — bắt buộc sinh extraction rule cho happy path POST

Trong `LlmSuggestionMaterializer` / `BoundaryNegativeTestCaseGenerator`, thêm logic: nếu test là `HappyPath` và method là `POST`, tự động thêm `TestCaseVariable`:

```csharp
// Auto-add extraction rule for resource ID
if (testCase.TestType == "HappyPath" && request.HttpMethod == "POST")
{
    testCase.Variables.Add(new TestCaseVariable
    {
        VariableName = BuildResourceIdVariableName(request.Url), // e.g. "categoryId"
        ExtractFrom = ExtractFrom.ResponseHeader,
        HeaderName = "Location",
        Regex = @"([^/?#]+)$",  // last segment of Location header
    });
}
```

**Ưu điểm:** Fix từ nguồn sinh test.  
**Rủi ro:** Cần Location header có mặt trong response.

### Solution C: Fix negative test dependency (cho Bug 1)

Trong LLM prompt hoặc `DependencyEnricher`, thêm rule: test negative cho "duplicate" PHẢI depend vào happy path create của cùng resource.

Khi LLM sinh test "Create category with duplicate name":
- `DependencyIds` phải chứa ID của "Create category happy path"
- Body nên dùng `{{categoryName}}` (được extract từ happy path) hoặc cùng static name

### Solution D (ngắn hạn): Re-generate test cases

Regenerate test suite với n8n webhook để LLM sinh lại extraction rules và dependencies đúng. Đây là giải pháp nhanh không cần code change.

---

## Các test bị ảnh hưởng

| TestCaseId | Tên | Failure |
|-----------|-----|---------|
| `a98e2cd3` | Create category with duplicate name | STATUS_CODE_MISMATCH |
| `33984a13` | Create product happy path | UNRESOLVED_VARIABLE `{{categoryId}}` |
| `c4d60031` | (category body) | UNRESOLVED_VARIABLE |
| `67797916` | (category body) | UNRESOLVED_VARIABLE |
| `d36e7c14` | (category body) | UNRESOLVED_VARIABLE |
| `55942a0e` | (category body) | UNRESOLVED_VARIABLE |
| `b5500750` | (depends on category+product) | UNRESOLVED_VARIABLE |
| `5aed453c` | (depends on category+product) | UNRESOLVED_VARIABLE |
| `48812db1` | (depends on category+product) | UNRESOLVED_VARIABLE |
| `c9e45d77` | (depends on category+product) | UNRESOLVED_VARIABLE |
| `1c72c567` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `11c4450a` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `096121b8` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `9ba1d8a0` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `b836e268` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `6529aa64` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `81f7127d` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `ed5fa85e` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `8b00d0a3` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `12ce0b10` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `6068c1a7` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `d06f893a` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `c270f05f` | Path param `{{categoryId}}` | UNRESOLVABLE_PATH_PARAM |
| `60944413` | Skipped (depends on 33984a13) | DEPENDENCY_FAILED |
| `70873306` | Skipped (depends on 33984a13) | DEPENDENCY_FAILED |
| `89d753fa` | Skipped (depends on 33984a13) | DEPENDENCY_FAILED |
| `3d6fcf16` | Skipped (depends on 33984a13) | DEPENDENCY_FAILED |
| ... | (nhiều test khác) | DEPENDENCY_FAILED |

---

## Files liên quan cần xem khi implement fix

| File | Vai trò |
|------|---------|
| `ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs:992` | `BuildResourceIdVariableName` stub → cần implement |
| `ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs:788` | `ExtractImplicitVariables` — logic extract |
| `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionMaterializer.cs` | Nơi materialize TestCaseVariable từ LLM output |
| `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs` | Prompt generation → cần thêm extraction rule instruction |
| `ClassifiedAds.UnitTests/TestExecution/TestExecutionOrchestratorTests.cs:541` | Unit test `Should_ImplicitlyExtractResourceId` — cần verify xem đang pass hay fail |

---

## Checklist trước khi implement

- [ ] Xác nhận unit test `Should_ImplicitlyExtractResourceId_WhenVariableRulesMissing` đang PASS hay FAIL
- [ ] Xác nhận response format thực tế của `POST /api/categories` trên `test-llm-api-testing.onrender.com` (có `id` field không? ở path nào?)
- [ ] Chọn solution: A (fix orchestrator), B (fix LLM generation), hay cả hai
- [ ] Đối với Bug 1: sửa LLM prompt để negative "duplicate" tests luôn depend vào happy path và dùng `{{resourceName}}` variable
