# REPORT: Cải Thiện Expected Assertion Dựa Trên Swagger + SRS

**Ngày:** 2026-05-02  
**Phạm vi:** `ClassifiedAds.Modules.TestGeneration`  
**Công cụ phân tích:** GitNexus CLI + codebase manual trace  
**Branch:** `feature/FE-17-optimize-fix-20260410`

---

## 1. Tóm Tắt Vấn Đề

Hệ thống hiện tại sinh Expected assertion theo 3 cách **không có căn cứ từ spec**:

| Field | Nguồn hiện tại | Vấn đề |
|---|---|---|
| `expectedStatus` | Hardcode `[400,401,403,404,409,415,422]` trong `GetBoundaryNegativeDefaultStatuses()` | Quá rộng. 409 không liên quan đến "Password minimum length" |
| `bodyContains` | LLM đoán theo convention phổ biến (Rule 15) | API không tuân theo convention → assert sai |
| `$.success = false` | LLM hardcode theo Rule 15b | API không có field `success` → luôn null → fail |

**Root Cause:** `BuildExpectedStatuses()` và các system prompt rules không đọc `metadata.Responses` từ Swagger mặc dù dữ liệu **đã có sẵn** trong `ApiEndpointMetadataDto.Responses`.

---

## 2. Phân Tích Codebase Chi Tiết

### 2.1 Dữ Liệu Đã Có Nhưng Chưa Dùng

**File:** `ClassifiedAds.Contracts/ApiDocumentation/DTOs/ApiEndpointMetadataDto.cs` (line 83)

```csharp
public IReadOnlyCollection<ApiEndpointResponseDescriptorDto> Responses { get; set; } 
    = Array.Empty<ApiEndpointResponseDescriptorDto>();

public class ApiEndpointResponseDescriptorDto
{
    public int StatusCode { get; set; }      // ← 400, 409, 422, v.v.
    public string Description { get; set; }  // ← "Invalid password format"
    public string Schema { get; set; }       // ← {"type":"object","properties":{"success":{"type":"boolean"},"message":{"type":"string"}}}
    public string Examples { get; set; }     // ← {"success":false,"message":"Password too short"}
}
```

Dữ liệu này **được populate đầy đủ** từ Swagger parser tại  
`ClassifiedAds.Modules.ApiDocumentation/Services/ApiEndpointMetadataService.cs` (line 263-274).  
OpenApiSpecificationParser đọc cả `responses` block cho từng operation.

**File:** `ClassifiedAds.Modules.TestGeneration/Entities/SrsRequirement.cs` (line 42-53)

```csharp
/// JSON array: [{"constraint": "password >= 6 chars → 400", "priority": "High"}, ...]
public string TestableConstraints { get; set; }
```

Dữ liệu `TestableConstraints` được LLM sinh khi phân tích SRS, chứa các **business rule cụ thể theo endpoint** — nhưng hiện tại chỉ truyền `Code/Title/Description` vào LLM payload qua `N8nSrsRequirementBrief`, bỏ qua `TestableConstraints`.

### 2.2 Điểm Bug Chính

**File:** `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs`

**Bug 1 - Line 723-728:** Hardcode hoàn toàn, không đọc Swagger:
```csharp
private static List<int> GetBoundaryNegativeDefaultStatuses(string httpMethod)
{
    _ = NormalizeHttpMethod(httpMethod);
    // ← KHÔNG đọc metadata.Responses ở đây
    return new List<int> { 400, 401, 403, 404, 409, 415, 422 };
}
```

`BuildExpectedStatuses()` gọi hàm này và MERGE với LLM output. Kết quả là list đầy đủ 7 code bất kể API thực tế chỉ định nghĩa 400 và 422.

**Bug 2 - Line 394-396:** Payload gửi LLM chỉ có success schema, KHÔNG có 4xx schema riêng:
```csharp
ResponseSchemaPayloads = CompactSchemaPayloads(metadata?.ResponseSchemaPayloads),
// ← ResponseSchemaPayloads chứa ALL schemas gộp lại, không phân biệt status code
// ← LLM không biết schema 400 khác schema 200
```

**Bug 3 - Line 402-421:** `BuildSrsContext()` bỏ `TestableConstraints`:
```csharp
var requirements = context.SrsRequirements
    .Select(r => new N8nSrsRequirementBrief
    {
        Code = r.RequirementCode,
        Title = r.Title,
        Description = TruncateForPayload(r.Description, 500),
        // ← THIẾU: TestableConstraints = r.TestableConstraints
    })
    .ToList();
```

**Bug 4 - `SuggestionRulesBlock` (line 73-76):** Rule 15/15b dạy LLM đoán convention:
```
"15b. MANDATORY: populate expectation.jsonPathChecks with 1 JSONPath assertion
on the error response (e.g. {\"$.success\": \"false\"})."
```
Rule này không nói "dùng schema từ swagger". LLM không có context để biết endpoint có field `success` không.

### 2.3 Flow Hiện Tại vs Flow Cần Có

```
HIỆN TẠI:
Swagger spec → metadata.Responses (đầy đủ)
                          ↓ KHÔNG dùng
LLM prompt ← Rules hardcode ($.success, [400..422])
                          ↓
LLM sinh expectedStatus ← MERGE với hardcode default
LLM sinh jsonPathChecks ← đoán theo convention
                          ↓
TestCase.Expectation (không đáng tin)

CẦN CÓ:
Swagger spec → metadata.Responses (đầy đủ)
                          ↓ ĐỌC
          4xx codes → expectedStatus candidates
          4xx schemas → field names → bodyContains
          4xx schemas → jsonPath assertions
SRS TestableConstraints → business rule constraints
                          ↓
LLM prompt ← context có căn cứ từ spec + SRS
                          ↓
LLM sinh assertions dựa trên spec thực tế
```

---

## 3. Giải Pháp 3 Mức

---

### Mức 1: Đọc Response Codes Từ Swagger Thay Hardcode

**Phạm vi thay đổi:** Nhỏ, isolated, không breaking change.

#### 3.1.1 Thay đổi trong `LlmScenarioSuggester.cs`

**Vấn đề:** `BuildExpectedStatuses()` nhận `source` từ LLM rồi merge với hardcode. Cần truyền thêm codes từ Swagger metadata.

**Thay đổi `BuildExpectedStatuses`:**
```csharp
// BEFORE (line 694-712):
private static List<int> BuildExpectedStatuses(TestType testType, List<int> source, string httpMethod)

// AFTER:
private static List<int> BuildExpectedStatuses(
    TestType testType, 
    List<int> llmSource, 
    string httpMethod,
    IReadOnlyCollection<ApiEndpointResponseDescriptorDto> swaggerResponses = null) // ← MỚI
```

Logic mới:
- Nếu `swaggerResponses` có data → lọc 4xx codes từ spec → dùng thay hardcode default
- Nếu không có → fallback về hardcode cũ (backward compat)
- Luôn merge với `llmSource` (LLM có thể biết thêm codes không có trong spec)

**Thay đổi `ParseScenarios()`:** Truyền `metadata` vào `BuildExpectedStatuses()`:
```csharp
// Trong ParseScenarios(), thêm lookup metadata per scenario:
var swaggerResponses = endpointContracts.TryGetValue(s.EndpointId, out var contract) 
    ? GetSwaggerResponses(s.EndpointId, metadataMap) // cần truyền metadataMap vào
    : null;
var expectedStatuses = BuildExpectedStatuses(parsedType, s.Expectation?.ExpectedStatus, s.Request?.HttpMethod, swaggerResponses);
```

**Thêm helper:**
```csharp
private static List<int> GetSwaggerErrorCodes(
    IReadOnlyCollection<ApiEndpointResponseDescriptorDto> responses)
{
    if (responses == null || responses.Count == 0) return null;
    return responses
        .Where(r => r.StatusCode >= 400 && r.StatusCode <= 599)
        .Select(r => r.StatusCode)
        .Distinct()
        .OrderBy(x => x)
        .ToList();
}
```

**Thay đổi `GetBoundaryNegativeDefaultStatuses()`:**
```csharp
private static List<int> GetBoundaryNegativeDefaultStatuses(
    string httpMethod, 
    List<int> swaggerErrorCodes = null) // ← MỚI
{
    // Nếu Swagger có định nghĩa codes → dùng làm primary (không hardcode)
    if (swaggerErrorCodes != null && swaggerErrorCodes.Count > 0)
    {
        // Vẫn giữ 400 nếu không có trong spec (vì LLM có thể trả về validation error)
        var result = swaggerErrorCodes.ToList();
        if (!result.Contains(400)) result.Insert(0, 400);
        return result;
    }
    // Fallback hardcode
    return new List<int> { 400, 401, 403, 404, 409, 415, 422 };
}
```

#### 3.1.2 Thay đổi trong `N8nBoundaryEndpointPayload.cs`

Thêm field để gửi LLM biết Swagger định nghĩa codes gì:
```csharp
public class N8nBoundaryEndpointPayload
{
    // ... existing fields ...
    
    /// <summary>
    /// Error response descriptors from Swagger (status codes 4xx/5xx).
    /// Keyed by status code string (e.g. "400", "422").
    /// LLM should use these codes ONLY in expectedStatus array.
    /// </summary>
    public Dictionary<string, N8nErrorResponseDescriptor> ErrorResponses { get; set; } = new();
}

public class N8nErrorResponseDescriptor
{
    public string Description { get; set; }
    public string SchemaJson { get; set; }  // raw JSON schema, nullable
    public string ExampleJson { get; set; } // example body, nullable
}
```

#### 3.1.3 Cập nhật payload builder

Trong `BuildN8nPayload()` → `endpointPayloads.Add(new N8nBoundaryEndpointPayload { ... })`:
```csharp
ErrorResponses = BuildErrorResponseDescriptors(metadata?.Responses),
```

```csharp
private static Dictionary<string, N8nErrorResponseDescriptor> BuildErrorResponseDescriptors(
    IReadOnlyCollection<ApiEndpointResponseDescriptorDto> responses)
{
    if (responses == null || responses.Count == 0) return new();
    return responses
        .Where(r => r.StatusCode >= 400)
        .Take(5) // tối đa 5 codes để giảm token
        .ToDictionary(
            r => r.StatusCode.ToString(),
            r => new N8nErrorResponseDescriptor
            {
                Description = r.Description,
                SchemaJson = TruncateForPayload(r.Schema, 800),
                ExampleJson = TruncateForPayload(r.Examples, 400),
            });
}
```

#### 3.1.4 Cập nhật `SuggestionRulesBlock`

Thêm vào Rule 13:
```
"13. expectation.expectedStatus MUST only use status codes that appear in the endpoint's 
     errorResponses map provided in this payload. If errorResponses is empty, use [400].
     NEVER invent status codes not in errorResponses."
```

---

### Mức 2: Đọc 4xx Response Schema → Sinh bodyContains Từ Field Names Thật

**Phạm vi thay đổi:** Trung bình, cần thêm schema parser utility.

#### 3.2.1 Thêm `ErrorResponseSchemaAnalyzer` (class mới)

> **⚠️ Lưu ý Case-Sensitivity:** JSONPath key được tạo từ `prop.Name` — giữ nguyên casing gốc của Swagger schema (có thể là camelCase `success` hoặc PascalCase `Success`). Điều này đúng **nếu API và schema đồng bộ**. Nếu API trả về `success` nhưng schema định nghĩa `Success` (hoặc ngược lại), assertion sẽ fail. Cần kiểm tra casing của từng endpoint khi onboard Swagger spec mới.

**File mới:** `ClassifiedAds.Modules.TestGeneration/Services/ErrorResponseSchemaAnalyzer.cs`

```csharp
/// <summary>
/// Extracts assertion hints from Swagger error response schemas.
/// Used to derive bodyContains and jsonPathChecks from spec instead of LLM guessing.
/// </summary>
internal static class ErrorResponseSchemaAnalyzer
{
    /// <summary>
    /// Extracts top-level field names from a JSON Schema object definition.
    /// Result: ["success", "message", "errors"] → dùng làm bodyContains
    /// </summary>
    public static List<string> ExtractFieldNames(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson)) return new();
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;
            
            // Handle allOf/oneOf
            if (root.TryGetProperty("allOf", out var allOf))
                return ExtractFromArray(allOf);
            
            if (!root.TryGetProperty("properties", out var props) || 
                props.ValueKind != JsonValueKind.Object)
                return new();

            return props.EnumerateObject()
                .Select(p => p.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Take(5)
                .ToList();
        }
        catch { return new(); }
    }

    /// <summary>
    /// Builds JSONPath assertions from schema: {"$.success": "*", "$.message": "*"}
    /// Priority: fields named "success", "message", "error", "code" get asserted first.
    /// </summary>
    public static Dictionary<string, string> BuildJsonPathAssertions(
        string schemaJson, 
        TestType testType)
    {
        var fields = ExtractFieldNames(schemaJson);
        if (fields.Count == 0) return new();

        var priorityFields = new[] { "success", "message", "error", "errors", "code", "status" };
        var ordered = fields
            .OrderBy(f => Array.IndexOf(priorityFields, f.ToLowerInvariant()) is int idx && idx >= 0 ? idx : 999)
            .Take(2)
            .ToList();

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in ordered)
        {
            // DESIGN: giữ nguyên casing gốc từ Swagger schema (camelCase hoặc PascalCase).
            // JSONPath key phải khớp casing với response thực tế. Schema = source of truth.
            // Nếu schema dùng "Success" (PascalCase) thì key là "$.Success" — KHÔNG normalize xuống.
            var isSuccessField = string.Equals(field, "success", StringComparison.OrdinalIgnoreCase);
            var isErrorTest = testType != TestType.HappyPath;
            result[$"$.{field}"] = isSuccessField && isErrorTest ? "false" : "*";
        }
        return result;
    }

    private static List<string> ExtractFromArray(JsonElement arrayElement)
    {
        foreach (var item in arrayElement.EnumerateArray())
        {
            var fields = ExtractFieldNames(item.GetRawText());
            if (fields.Count > 0) return fields;
        }
        return new();
    }
}
```

#### 3.2.2 Dùng trong `BuildN8nPayload()` 

Trong `SuggestionRulesBlock`, thêm rule hướng dẫn LLM dùng schema hints:
```
"15c. SCHEMA-DRIVEN: If errorResponses[statusCode].schemaJson is provided, 
      derive bodyContains from its top-level field names and jsonPathChecks 
      from those same fields. Do NOT invent field names not in the schema."
```

#### 3.2.3 Dùng trong `ParseScenarios()` để post-process LLM output

> **⚠️ "Thanh Gươm Hai Lưỡi" — Repair Logic Warning:**  
> Backend override LLM assertions bằng data từ Swagger spec là cơ chế **chỉ fill khi LLM để trống** (`Count == 0`), không ghi đè khi LLM đã có kết quả.  
> Rủi ro thực sự: nếu **Swagger spec bị outdated** (API đã thêm/đổi field nhưng spec chưa regenerate), repair sẽ điền assertions dựa trên schema cũ → test fail giả dù API hoạt động đúng.  
> **Biện pháp:** Thêm comment XML vào `RepairAssertionsFromSchema()` rõ ràng rằng method này phụ thuộc vào `metadata.Responses` being in sync với runtime API. Team phải đảm bảo chạy Swagger regeneration (hoặc `dotnet build` với Swashbuckle) trước khi trigger test generation.

Sau khi LLM trả về scenarios, nếu `jsonPathChecks` rỗng hoặc chứa field không có trong schema, backend override:

```csharp
// Trong ParseScenarios(), sau khi parse xong parsedScenario:
if (endpointContracts.TryGetValue(parsedScenario.EndpointId, out var contract))
{
    parsedScenario = ContractAwareRequestSynthesizer.RepairScenario(parsedScenario, contract.RequestContext);
    
    // MỚI: Override jsonPathChecks từ Swagger 4xx schema nếu LLM đoán sai
    var swaggerErrorResponses = GetSwaggerErrorResponses(parsedScenario.EndpointId, metadataMap);
    parsedScenario = RepairJsonPathChecks(parsedScenario, swaggerErrorResponses);
}
```

```csharp
private static LlmSuggestedScenario RepairJsonPathChecks(
    LlmSuggestedScenario scenario,
    IReadOnlyCollection<ApiEndpointResponseDescriptorDto> errorResponses)
{
    if (scenario.SuggestedTestType == TestType.HappyPath) return scenario;
    if (errorResponses == null || errorResponses.Count == 0) return scenario;

    // Lấy schema của status code đầu tiên LLM suggest
    var primaryCode = scenario.ExpectedStatusCodes?.FirstOrDefault() ?? scenario.ExpectedStatusCode;
    var matchingResponse = errorResponses
        .FirstOrDefault(r => r.StatusCode == primaryCode)
        ?? errorResponses.FirstOrDefault(r => r.StatusCode >= 400 && r.StatusCode < 500);

    if (matchingResponse == null || string.IsNullOrWhiteSpace(matchingResponse.Schema))
        return scenario;

    // Nếu LLM không sinh jsonPathChecks, dùng từ schema
    if (scenario.SuggestedJsonPathChecks == null || scenario.SuggestedJsonPathChecks.Count == 0)
    {
        scenario.SuggestedJsonPathChecks = ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(
            matchingResponse.Schema, scenario.SuggestedTestType);
    }

    // Nếu LLM không sinh bodyContains, dùng field names từ schema
    if (scenario.SuggestedBodyContains == null || scenario.SuggestedBodyContains.Count == 0)
    {
        scenario.SuggestedBodyContains = ErrorResponseSchemaAnalyzer.ExtractFieldNames(matchingResponse.Schema);
    }

    return scenario;
}
```

---

### Mức 3: SRS TestableConstraints → LLM Phân Tích Business Rule → Sinh Assertion Có Căn Cứ

**Phạm vi thay đổi:** Lớn nhất, ảnh hưởng payload model và system prompt.

#### 3.3.1 Thêm `TestableConstraints` vào `N8nSrsRequirementBrief`

**File:** `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs`

```csharp
public class N8nSrsRequirementBrief
{
    public string Code { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    
    /// <summary>
    /// Structured testable constraints extracted by LLM from SRS.
    /// Example: [{"constraint": "password >= 6 chars → 400", "priority": "High"}]
    /// Agent MUST generate scenarios that test each constraint.
    /// </summary>
    public List<SrsTestableConstraintBrief> TestableConstraints { get; set; } = new();  // ← MỚI
}

public class SrsTestableConstraintBrief
{
    public string Constraint { get; set; }   // e.g. "password must be >= 6 characters"
    public string ExpectedOutcome { get; set; } // e.g. "400 Bad Request"
    public string Priority { get; set; }     // High/Medium/Low
}
```

#### 3.3.2 Populate `TestableConstraints` trong `BuildSrsContext()`

**File:** `LlmScenarioSuggester.cs`, method `BuildSrsContext()` (line 397-421):

```csharp
private static N8nSrsContext BuildSrsContext(LlmScenarioSuggestionContext context)
{
    // ... existing code ...
    
    var requirements = context.SrsRequirements
        .Select(r => new N8nSrsRequirementBrief
        {
            Code = r.RequirementCode,
            Title = r.Title,
            Description = TruncateForPayload(r.Description, 400), // giảm xuống để nhường chỗ
            TestableConstraints = DeserializeTestableConstraints(r.TestableConstraints), // ← MỚI
        })
        .ToList();
    // ...
}

private static List<SrsTestableConstraintBrief> DeserializeTestableConstraints(string json)
{
    if (string.IsNullOrWhiteSpace(json)) return new();
    try
    {
        // SrsRequirement.TestableConstraints format: 
        // [{"constraint": "...", "priority": "High"}, ...]
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return new();
        
        var result = new List<SrsTestableConstraintBrief>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var constraint = item.TryGetProperty("constraint", out var c) ? c.GetString() : null;
            var priority = item.TryGetProperty("priority", out var p) ? p.GetString() : "Medium";
            
            if (string.IsNullOrWhiteSpace(constraint)) continue;
            
            // Parse expected outcome từ constraint text (e.g. "password >= 6 → 400")
            var expectedOutcome = ExtractExpectedOutcome(constraint);
            
            result.Add(new SrsTestableConstraintBrief
            {
                Constraint = TruncateForPayload(constraint, 200),
                ExpectedOutcome = expectedOutcome,
                Priority = priority,
            });
        }
        return result.Take(5).ToList(); // max 5 constraints per requirement
    }
    catch { return new(); }
}

private static string ExtractExpectedOutcome(string constraintText)
{
    // Heuristic: nếu constraint có dạng "xxx → 4xx" thì extract
    if (string.IsNullOrWhiteSpace(constraintText)) return null;
    var arrowIdx = constraintText.IndexOf("→", StringComparison.Ordinal);
    if (arrowIdx < 0) arrowIdx = constraintText.IndexOf("->", StringComparison.Ordinal);
    if (arrowIdx >= 0 && arrowIdx < constraintText.Length - 2)
        return constraintText[(arrowIdx + 1)..].Trim();
    return null;
}
```

#### 3.3.3 Thêm Rule Vào `SuggestionRulesBlock`

Thêm Rule 16 sau Rule 15b:
```
"16. SRS-DRIVEN: If srsContext.requirements[].testableConstraints is provided, 
     generate at least 1 scenario per constraint. The scenario's expectedStatus 
     MUST match the constraint's expectedOutcome code. 
     bodyContains must include keywords from the constraint description.
     Tag the scenario with coveredRequirementCodes = [constraint's requirement code].
     Example constraint 'password >= 6 chars → 400' → generate Boundary test 
     with password='12345', expectedStatus=[400], bodyContains=[\"password\",\"minimum\"]."
```

#### 3.3.4 Thêm `EndpointId` Vào `N8nSrsRequirementBrief` Để LLM Biết Map Endpoint

```csharp
public class N8nSrsRequirementBrief
{
    // ...existing...
    
    /// <summary>
    /// Endpoint UUID this requirement maps to (null = global).
    /// LLM MUST only apply this requirement's constraints to this endpointId.
    /// </summary>
    public Guid? EndpointId { get; set; }  // ← MỚI, từ SrsRequirement.EndpointId
}
```

Trong `BuildSrsContext()`:
```csharp
EndpointId = r.EndpointId, // SrsRequirement.EndpointId đã có
```

---

## 4. File Thay Đổi Theo Mức

| File | Mức 1 | Mức 2 | Mức 3 |
|---|---|---|---|
| `LlmScenarioSuggester.cs` | `BuildExpectedStatuses()`, `GetBoundaryNegativeDefaultStatuses()`, `ParseScenarios()`, payload builder | Thêm `RepairJsonPathChecks()`, truyền metadata vào ParseScenarios | `BuildSrsContext()`, `DeserializeTestableConstraints()` |
| `N8nBoundaryNegativePayload.cs` | Thêm `ErrorResponses` field + `N8nErrorResponseDescriptor` | — | Thêm `TestableConstraints` vào `N8nSrsRequirementBrief`, thêm `EndpointId` |
| `ErrorResponseSchemaAnalyzer.cs` (MỚI) | — | Tạo mới hoàn toàn | — |
| `SuggestionRulesBlock` constant | Rule 13 update | Rule 15c thêm | Rule 16 thêm |
| Unit tests | `LlmScenarioSuggesterTests.cs` update | Thêm test cho analyzer | Thêm test SRS constraint |

---

## 5. Rủi Ro & Giảm Thiểu

| Rủi ro | Giảm thiểu |
|---|---|
| Swagger không có 4xx response → list rỗng | Fallback về hardcode cũ, không breaking |
| Schema JSON không parse được | Try-catch graceful, trả về empty list |
| Token overflow do thêm error schemas | `TruncateForPayload(r.Schema, 800)` + giới hạn 5 codes |
| **Token overflow (Mức 3)** — constraints phức tạp nhiều trường | `TruncateForPayload(constraint, 200)` + `Take(5)` per requirement đã handle |
| LLM bỏ qua SRS constraints | Post-process backend `RepairJsonPathChecks()` không phụ thuộc LLM compliance |
| `SrsRequirement.TestableConstraints` null | Default empty list, không throw |
| **Case-sensitivity** — schema dùng `Success` (PascalCase), assertion dùng `$.success` | JSONPath key GIỮ NGUYÊN casing từ schema (`prop.Name`). Nếu schema và API đồng bộ thì đúng. Nếu không đồng bộ thì assertion sai — cần review schema casing khi onboard API mới |
| **Swagger spec outdated** — API đã đổi nhưng spec chưa update | `RepairAssertionsFromSchema()` override LLM bằng data từ spec cũ → test fail giả. **Yêu cầu bắt buộc: Swagger phải được regenerate/update đồng bộ với code trước khi chạy test generation** |

---

## 6. Thứ Tự Implement Khuyến Nghị

1. **Mức 1 trước** — ít rủi ro nhất, mang lại giá trị lớn nhất (expectedStatus đúng)
2. **Mức 2 sau** — cần Mức 1 xong để có `ErrorResponses` trong payload
3. **Mức 3 cuối** — phụ thuộc data `TestableConstraints` đã được populate đúng từ SRS analysis flow

---

## 7. Implementation Prompt Cho AI Agent

Prompt này dành để copy-paste trực tiếp vào AI Agent sau khi đã review solution trên.

---

```
=== AI AGENT IMPLEMENTATION PROMPT ===
=== SPEC-DRIVEN ASSERTION IMPROVEMENT ===
=== REVIEW CAREFULLY BEFORE EXECUTING ===

## CONTEXT

Codebase: D:\GSP26SE43.ModularMonolith (.NET 10 Modular Monolith, C#)
Branch: feature/FE-17-optimize-fix-20260410
Objective: Thay thế hardcode expected assertions bằng data từ Swagger spec + SRS requirements.

## BACKGROUND

Hiện tại `LlmScenarioSuggester.cs` hardcode expected status codes [400,401,403,404,409,415,422] 
cho mọi Boundary/Negative test, dẫn đến các test case có expectedStatus không đúng với API spec thật.
Đồng thời bodyContains và jsonPathChecks được LLM đoán theo convention, không dựa vào Swagger response schema.

Dữ liệu đã có sẵn trong codebase nhưng chưa được dùng:
- `ApiEndpointMetadataDto.Responses` (IReadOnlyCollection<ApiEndpointResponseDescriptorDto>) 
  chứa đầy đủ StatusCode, Description, Schema, Examples từ Swagger.
- `SrsRequirement.TestableConstraints` (string JSON) chứa business rule constraints.

## PHASE 1: ĐỌC SWAGGER RESPONSE CODES THAY HARDCODE

### STEP 1.1 - Thêm field ErrorResponses vào N8nBoundaryEndpointPayload

File: ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs

Trong class N8nBoundaryEndpointPayload, thêm property:
```csharp
/// <summary>
/// Error response descriptors from Swagger (4xx/5xx only).
/// Key = status code string ("400", "422"). Max 5 entries.
/// LLM MUST use these codes ONLY in expectedStatus. 
/// </summary>
public Dictionary<string, N8nErrorResponseDescriptor> ErrorResponses { get; set; } = new();
```

Thêm class mới cùng file:
```csharp
public class N8nErrorResponseDescriptor
{
    public string Description { get; set; }
    public string SchemaJson { get; set; }
    public string ExampleJson { get; set; }
}
```

### STEP 1.2 - Sửa BuildExpectedStatuses trong LlmScenarioSuggester.cs

File: ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs

Tìm method `BuildExpectedStatuses(TestType testType, List<int> source, string httpMethod)`.

Đổi signature thành:
```csharp
private static List<int> BuildExpectedStatuses(
    TestType testType, 
    List<int> llmSource, 
    string httpMethod,
    IReadOnlyCollection<ApiEndpointResponseDescriptorDto> swaggerResponses = null)
```

Sửa phần Boundary/Negative (sau comment "// Boundary/Negative should not accept success statuses."):
```csharp
var nonSuccessStatuses = normalized.Where(code => code < 200 || code >= 300).ToList();

// Extract from Swagger spec if available
var swaggerErrorCodes = swaggerResponses?
    .Where(r => r.StatusCode >= 400 && r.StatusCode <= 599)
    .Select(r => r.StatusCode)
    .Distinct()
    .ToList();

var defaultFallback = (swaggerErrorCodes != null && swaggerErrorCodes.Count > 0)
    ? BuildSwaggerBasedDefaultStatuses(swaggerErrorCodes)
    : GetBoundaryNegativeDefaultStatuses(httpMethod);

return MergeStatuses(nonSuccessStatuses, defaultFallback);
```

Thêm private helper method mới (sau GetBoundaryNegativeDefaultStatuses):
```csharp
private static List<int> BuildSwaggerBasedDefaultStatuses(List<int> swaggerErrorCodes)
{
    var result = swaggerErrorCodes
        .OrderBy(x => x)
        .ToList();
    // Always ensure 400 is present as minimal validation error baseline
    if (!result.Contains(400))
        result.Insert(0, 400);
    return result;
}
```

### STEP 1.3 - Thêm builder method trong LlmScenarioSuggester.cs

Tìm private method BuildN8nPayload. Trong vòng lặp tạo endpointPayloads, thêm:
```csharp
ErrorResponses = BuildErrorResponseDescriptors(metadata?.Responses),
```

Thêm private static method mới:
```csharp
private static Dictionary<string, N8nErrorResponseDescriptor> BuildErrorResponseDescriptors(
    IReadOnlyCollection<ApiEndpointResponseDescriptorDto> responses)
{
    if (responses == null || responses.Count == 0)
        return new Dictionary<string, N8nErrorResponseDescriptor>();

    return responses
        .Where(r => r.StatusCode >= 400 && r.StatusCode <= 599)
        .OrderBy(r => r.StatusCode)
        .Take(5)
        .ToDictionary(
            r => r.StatusCode.ToString(),
            r => new N8nErrorResponseDescriptor
            {
                Description = r.Description,
                SchemaJson = TruncateForPayload(r.Schema, 800),
                ExampleJson = TruncateForPayload(r.Examples, 400),
            });
}
```

### STEP 1.4 - Cập nhật SuggestionRulesBlock constant

Tìm Rule 13 trong SuggestionRulesBlock:
```
"13. expectation.expectedStatus must be an array of integers e.g. [400] or [401] or [404].\n" +
```

Thay thành:
```
"13. expectation.expectedStatus MUST ONLY use status codes defined in the endpoint's errorResponses " +
"map provided in this payload. If errorResponses is empty or missing, use [400]. " +
"NEVER add status codes that are not in errorResponses (e.g. do NOT add 409 if only 400 and 422 are defined).\n" +
```

### STEP 1.5 - Cập nhật ParseScenarios để truyền metadata

Trong method ParseScenarios, signature hiện tại:
```csharp
private IReadOnlyList<LlmSuggestedScenario> ParseScenarios(
    N8nBoundaryNegativeResponse response,
    IReadOnlyDictionary<Guid, EndpointRequestContract> endpointContracts,
    IReadOnlyList<SrsRequirement> srsRequirements = null)
```

Thêm parameter:
```csharp
private IReadOnlyList<LlmSuggestedScenario> ParseScenarios(
    N8nBoundaryNegativeResponse response,
    IReadOnlyDictionary<Guid, EndpointRequestContract> endpointContracts,
    IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap = null,  // ← MỚI
    IReadOnlyList<SrsRequirement> srsRequirements = null)
```

Trong foreach loop parsing scenarios, thay:
```csharp
var expectedStatuses = BuildExpectedStatuses(parsedType, s.Expectation?.ExpectedStatus, s.Request?.HttpMethod);
```

Thành:
```csharp
metadataMap?.TryGetValue(s.EndpointId, out var scenarioMetadata);
var expectedStatuses = BuildExpectedStatuses(
    parsedType, 
    s.Expectation?.ExpectedStatus, 
    s.Request?.HttpMethod,
    scenarioMetadata?.Responses);
```

Tìm nơi gọi ParseScenarios (trong SuggestScenariosAsync):
```csharp
var scenarios = ParseScenarios(n8nResponse, endpointContracts, context.SrsRequirements);
```

Thay thành:
```csharp
var scenarios = ParseScenarios(n8nResponse, endpointContracts, metadataMap, context.SrsRequirements);
```

---

## PHASE 2: ĐỌC 4xx SCHEMA → SINH bodyContains VÀ jsonPathChecks

### STEP 2.1 - Tạo file mới ErrorResponseSchemaAnalyzer.cs

> **Case-Sensitivity Design Decision:** JSONPath key được build từ `prop.Name` (casing gốc từ schema).
> KHÔNG normalize sang camelCase. Lý do: schema = source of truth, nếu schema dùng `Success` thì API
> cũng trả về `Success`. Nếu cần thay đổi, search `"$.{field}"` trong file này.

File mới: ClassifiedAds.Modules.TestGeneration/Services/ErrorResponseSchemaAnalyzer.cs

```csharp
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Extracts bodyContains and jsonPathChecks hints from Swagger error response schemas.
/// Prevents LLM from guessing assertion field names that may not exist in the API.
/// </summary>
internal static class ErrorResponseSchemaAnalyzer
{
    private static readonly string[] PriorityFields = 
        { "success", "message", "error", "errors", "code", "status", "detail" };

    /// <summary>
    /// Extracts top-level property names from a JSON Schema object.
    /// Returns empty list if schema is null/invalid/not an object schema.
    /// </summary>
    public static List<string> ExtractFieldNames(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson)) return new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            return ExtractFromElement(doc.RootElement)
                .Take(5)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Builds JSONPath assertions from error response schema.
    /// For "success" field on error scenario: asserts "false".
    /// For other fields: asserts "*" (field exists).
    /// Returns at most 2 assertions.
    /// </summary>
    public static Dictionary<string, string> BuildJsonPathAssertions(
        string schemaJson,
        TestType testType)
    {
        var fields = ExtractFieldNames(schemaJson);
        if (fields.Count == 0) return new Dictionary<string, string>();

        var ordered = fields
            .OrderBy(f =>
            {
                var idx = Array.IndexOf(PriorityFields, f.ToLowerInvariant());
                return idx >= 0 ? idx : 999;
            })
            .Take(2)
            .ToList();

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in ordered)
        {
            // DESIGN: preserve original casing from Swagger schema (camelCase or PascalCase).
            // JSONPath key MUST match the actual response field casing. Schema is the source of truth.
            // Do NOT normalize to camelCase — if schema uses "Success", key should be "$.Success".
            var isSuccessField = string.Equals(field, "success", StringComparison.OrdinalIgnoreCase);
            var isErrorTest = testType == TestType.Boundary || testType == TestType.Negative;
            result[$"$.{field}"] = isSuccessField && isErrorTest ? "false" : "*";
        }
        return result;
    }

    private static IEnumerable<string> ExtractFromElement(JsonElement element)
    {
        // allOf: merge all sub-schemas
        if (element.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            foreach (var sub in allOf.EnumerateArray())
            {
                foreach (var name in ExtractFromElement(sub))
                    yield return name;
            }
            yield break;
        }

        // oneOf / anyOf: use first variant
        foreach (var keyword in new[] { "oneOf", "anyOf" })
        {
            if (element.TryGetProperty(keyword, out var variants) && variants.ValueKind == JsonValueKind.Array)
            {
                var first = variants.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined)
                {
                    foreach (var name in ExtractFromElement(first))
                        yield return name;
                }
                yield break;
            }
        }

        // Standard object with properties
        if (element.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in props.EnumerateObject())
            {
                if (!string.IsNullOrWhiteSpace(prop.Name))
                    yield return prop.Name;
            }
        }
    }
}
```

### STEP 2.2 - Thêm post-processing trong ParseScenarios

Trong ParseScenarios, sau block `ContractAwareRequestSynthesizer.RepairScenario(...)`, thêm:

```csharp
// Phase 2: Repair assertions from Swagger error schema when LLM left them empty
if (parsedScenario.SuggestedTestType != TestType.HappyPath)
{
    if (metadataMap != null && metadataMap.TryGetValue(parsedScenario.EndpointId, out var meta))
    {
        parsedScenario = RepairAssertionsFromSchema(parsedScenario, meta?.Responses);
    }
}
```

Thêm private static method:
```csharp
private static LlmSuggestedScenario RepairAssertionsFromSchema(
    LlmSuggestedScenario scenario,
    IReadOnlyCollection<ApiEndpointResponseDescriptorDto> swaggerResponses)
{
    if (swaggerResponses == null || swaggerResponses.Count == 0) return scenario;

    // Find the matching Swagger response for this scenario's primary expected code
    var primaryCode = scenario.ExpectedStatusCodes?.FirstOrDefault() 
        ?? scenario.ExpectedStatusCode;
    
    var matchingResponse = swaggerResponses.FirstOrDefault(r => r.StatusCode == primaryCode)
        ?? swaggerResponses.FirstOrDefault(r => r.StatusCode >= 400 && r.StatusCode < 500);

    if (matchingResponse == null) return scenario;

    // Only repair if LLM left empty
    if ((scenario.SuggestedJsonPathChecks == null || scenario.SuggestedJsonPathChecks.Count == 0)
        && !string.IsNullOrWhiteSpace(matchingResponse.Schema))
    {
        scenario.SuggestedJsonPathChecks = ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(
            matchingResponse.Schema, scenario.SuggestedTestType);
    }

    if ((scenario.SuggestedBodyContains == null || scenario.SuggestedBodyContains.Count == 0)
        && !string.IsNullOrWhiteSpace(matchingResponse.Schema))
    {
        scenario.SuggestedBodyContains = ErrorResponseSchemaAnalyzer.ExtractFieldNames(matchingResponse.Schema);
    }

    return scenario;
}
```

---

## PHASE 3: SRS TESTABLE CONSTRAINTS → LLM PHÂN TÍCH BUSINESS RULE

### STEP 3.1 - Mở rộng N8nSrsRequirementBrief

File: ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs

Trong class N8nSrsRequirementBrief, thêm:
```csharp
/// <summary>
/// Structured testable constraints from SRS analysis. Max 5 items.
/// Each describes a specific condition the API must enforce.
/// </summary>
public List<SrsTestableConstraintBrief> TestableConstraints { get; set; } = new();

/// <summary>
/// Endpoint this requirement maps to. LLM must apply constraints to this endpoint only.
/// Null = applies globally to all endpoints.
/// </summary>
public Guid? EndpointId { get; set; }
```

Thêm class mới:
```csharp
public class SrsTestableConstraintBrief
{
    /// <summary>Human-readable constraint (e.g. "password must be >= 6 characters").</summary>
    public string Constraint { get; set; }
    
    /// <summary>Expected API outcome (e.g. "400" or "201"). Null if not specified.</summary>
    public string ExpectedOutcome { get; set; }
    
    public string Priority { get; set; }
}
```

### STEP 3.2 - Populate trong BuildSrsContext

File: ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs

Trong method BuildSrsContext, thay đổi phần tạo requirements:
```csharp
var requirements = context.SrsRequirements
    .Select(r => new N8nSrsRequirementBrief
    {
        Code = r.RequirementCode,
        Title = r.Title,
        Description = TruncateForPayload(r.Description, 400),
        EndpointId = r.EndpointId,                                    // ← MỚI
        TestableConstraints = DeserializeTestableConstraints(r.TestableConstraints), // ← MỚI
    })
    .ToList();
```

Thêm 2 private static methods mới trong LlmScenarioSuggester:
```csharp
private static List<SrsTestableConstraintBrief> DeserializeTestableConstraints(string json)
{
    if (string.IsNullOrWhiteSpace(json)) return new List<SrsTestableConstraintBrief>();
    try
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return new List<SrsTestableConstraintBrief>();

        var result = new List<SrsTestableConstraintBrief>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var constraint = item.TryGetProperty("constraint", out var c) ? c.GetString() : null;
            if (string.IsNullOrWhiteSpace(constraint)) continue;

            var priority = item.TryGetProperty("priority", out var p) ? p.GetString() : "Medium";
            var outcome = ExtractExpectedOutcome(constraint);

            result.Add(new SrsTestableConstraintBrief
            {
                Constraint = TruncateForPayload(constraint, 200),
                ExpectedOutcome = outcome,
                Priority = priority,
            });
        }
        return result.Take(5).ToList();
    }
    catch { return new List<SrsTestableConstraintBrief>(); }
}

private static string ExtractExpectedOutcome(string constraintText)
{
    if (string.IsNullOrWhiteSpace(constraintText)) return null;
    // Pattern: "xxx → 4xx" or "xxx -> 4xx"
    var arrowIdx = constraintText.IndexOf('→');
    if (arrowIdx < 0) arrowIdx = constraintText.IndexOf("->", StringComparison.Ordinal);
    if (arrowIdx >= 0 && arrowIdx < constraintText.Length - 1)
        return constraintText[(arrowIdx + 1)..].Trim();
    return null;
}
```

### STEP 3.3 - Thêm Rule 16 vào SuggestionRulesBlock

Tìm cuối của SuggestionRulesBlock (sau Rule 15b), thêm:
```csharp
"16. SRS-CONSTRAINT-DRIVEN: When srsContext.requirements[n].testableConstraints is non-empty, " +
"generate at least 1 scenario per constraint item. Rules:\n" +
"   - The scenario's endpointId MUST be requirements[n].endpointId (if not null).\n" +
"   - The scenario's expectedStatus MUST match the constraint's expectedOutcome code (parse the number).\n" +
"   - bodyContains MUST include 1-2 keywords from the constraint description.\n" +
"   - coveredRequirementCodes MUST include requirements[n].code.\n" +
"   - Example: constraint='password >= 6 chars → 400' → Boundary test, " +
"     body={password:'12345'}, expectedStatus=[400], bodyContains=['password','minimum'].\n" +
"   - If no testableConstraints, ignore this rule.\n";
```

---

## CHECKLIST TRƯỚC KHI IMPLEMENT

[ ] Đọc và hiểu ApiEndpointResponseDescriptorDto tại:
    ClassifiedAds.Contracts/ApiDocumentation/DTOs/ApiEndpointMetadataDto.cs line 105-114
    
[ ] Verify ApiEndpointMetadataService.cs line 263-274 đang populate Responses đúng từ DB
    (field Schema và Examples được lưu trong EndpointResponse entity)

[ ] Verify SrsRequirement.TestableConstraints format bằng cách chạy query DB:
    SELECT "TestableConstraints" FROM "SrsRequirements" WHERE "TestableConstraints" IS NOT NULL LIMIT 5;

[ ] Xem test file ClassifiedAds.UnitTests/TestGeneration/LlmScenarioSuggesterTests.cs
    để biết cần update test nào sau khi đổi signature ParseScenarios và BuildExpectedStatuses

[ ] Build để verify không có compile error sau mỗi Phase

[ ] Không đổi bất kỳ EF migration hay DB schema nào — chỉ thay đổi application logic

## BUILD COMMAND

```
cd D:\GSP26SE43.ModularMonolith
dotnet build ClassifiedAds.Modules.TestGeneration/ClassifiedAds.Modules.TestGeneration.csproj --no-restore 2>&1 | Select-String "error|warning|succeeded|FAILED" | Select-Object -Last 20
```

## TEST COMMAND

```
cd D:\GSP26SE43.ModularMonolith
dotnet test ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj --filter "FullyQualifiedName~LlmScenarioSuggester" --no-build 2>&1 | Select-Object -Last 30
```

=== END OF PROMPT ===
```

---

## 8. Kết Quả Kỳ Vọng Sau Khi Implement

### Test Case "Boundary: Password with minimum length" (POST /api/auth/register)

**Trước:**
```json
{
  "expectedStatus": [400, 401, 403, 404, 409, 415, 422],
  "bodyContains": ["success", "id"],
  "jsonPathChecks": {"$.success": "true"}
}
```

**Sau (Mức 1+2+3):**
```json
{
  "expectedStatus": [400, 422],  // chỉ codes Swagger định nghĩa cho register endpoint
  "bodyContains": ["message"],   // từ field names của 400 response schema
  "jsonPathChecks": {
    "$.success": "false",        // từ schema + biết đây là error test
    "$.message": "*"             // field message exists
  }
}
```

Với input body `{"email":"x@test.com", "password":"123"}` (ngắn hơn min length):
- API trả 400 → khớp `expectedStatus`
- Body `{"success":false,"message":"Password must be at least 6 characters"}` → `message` found ✓
- `$.success = false` → khớp ✓
- **Kết quả: PASS có nghĩa thật sự, không phải PASS nhờ soft mode**
