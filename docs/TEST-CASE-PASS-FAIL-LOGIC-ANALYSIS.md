# PHÂN TÍCH VẤN ĐỀ: Test Case Negative Expected Fail Nhưng Trả Về "Failed" Thay Vì "Passed"

**Ngày:** 2026-04-08  
**Phạm vi:** TestGeneration + TestExecution + Validator Logic  
**Triệu chứng:** _"Happy case thì pass còn mấy cái test case fail mà test ra fail thì nó trả về fail luôn chứ không phải trả về pass"_

---

## 1. TÓM TẮT VẤN ĐỀ

### Triệu chứng
- **Happy case** (positive test): Gọi API thành công → API trả 200 → Test **PASSED** ✅
- **Negative case** (expect API reject): Gọi API với input sai → API trả 400/422 → Test **FAILED** ❌

### Hành vi mong đợi
- **Negative case**: Gọi API với input sai → API **đúng cách reject** → Test **PASSED** ✅

### Vấn đề cốt lõi
> Hệ thống hiện tại không phân biệt:
> - **"API trả về lỗi" (Actual result)**
> - **"Test EXPECTED API trả về lỗi" (Expected behavior)**
>
> Kết quả là mọi response lỗi đều bị đánh dấu "Failed" bất kể đó có phải là expected behavior hay không.

---

## 2. PHÂN TÍCH GỐC RỄ

### 2.1 Logic xác định Pass/Fail hiện tại

**File:** `ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs`

```csharp
// Line 125
result.IsPassed = result.Failures.Count == 0;
```

**Logic này đúng về nguyên tắc:** Test pass khi không có validation failure.

**Vấn đề nằm ở những gì được thêm vào `Failures`.**

### 2.2 Chuỗi nguyên nhân gây ra vấn đề

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        ROOT CAUSE CHAIN                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1. Generator tạo test case với Expected Status hard-coded [400]        │
│                           ↓                                              │
│  2. LLM Suggestion trả về nhiều status nhưng bị rút gọn thành 1 status  │
│                           ↓                                              │
│  3. API thực tế có thể trả 401, 403, 422, 409, 500... (đều đúng)        │
│                           ↓                                              │
│  4. Validator so sánh: Expected [400] vs Actual 422 → MISMATCH          │
│                           ↓                                              │
│  5. Thêm failure "STATUS_CODE_MISMATCH" vào result.Failures             │
│                           ↓                                              │
│  6. IsPassed = (Failures.Count == 0) → FALSE                            │
│                           ↓                                              │
│  7. Test case bị đánh "FAILED" dù API reject đúng nghiệp vụ             │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.3 Chi tiết từng nguyên nhân

#### ❌ Nguyên nhân 1: Expected Status bị hard-code quá cứng

**File:** `ClassifiedAds.Modules.TestGeneration/Services/BodyMutationEngine.cs`

```csharp
// Lines 58-127, 130-253, 255-284
// Tất cả mutation đều hard-code ExpectedStatusCode = 400
ExpectedStatusCode = 400  // Không flexible
```

**Tác động:**
| Loại Mutation | Expected | Actual Có Thể | Kết Quả |
|--------------|----------|---------------|---------|
| missingRequired | [400] | 400, 422 | 422 → FAIL |
| invalidType | [400] | 400, 422 | 422 → FAIL |
| malformedJson | [400] | 400, 500 | 500 → FAIL |
| unauthorized | [400] | 401, 403 | 401 → FAIL |

#### ❌ Nguyên nhân 2: LLM Suggestion bị mất dữ liệu

**File:** `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs`

```csharp
// Line 420-429
// Nếu LLM trả về nhiều status codes, chỉ lấy FirstOrDefault() ?? 400
var expectedStatus = suggestion.ExpectedStatusCodes?.FirstOrDefault() ?? 400;
```

**Tác động:** Nếu LLM trả `[400, 422, 403]`, hệ thống chỉ giữ `400`.

#### ❌ Nguyên nhân 3: Schema validation fallback sai

**File:** `ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs`

```csharp
// Lines 206-222
if (string.IsNullOrWhiteSpace(schemaJson))
{
    if (IsExpectingNon2xxStatus(expectation))
    {
        // ✅ Đã fix: Skip validation cho error responses
        return false;
    }
    
    // ⚠️ Fallback sang success schema cho 2xx cases
    if (endpointMetadata?.ResponseSchemaPayloads != null)
    {
        schemaJson = endpointMetadata.ResponseSchemaPayloads.FirstOrDefault();
    }
}
```

**Điểm tích cực:** Code hiện tại **ĐÃ CÓ** logic skip schema validation cho non-2xx.

**Vấn đề:** Logic này CHỈ HOẠT ĐỘNG nếu `expectation.ExpectedStatus` đúng là non-2xx. Nếu expected status sai → vẫn bị fallback sai schema.

---

## 3. ĐIỂM MẤU CHỐT: THIẾU KHÁI NIỆM "EXPECTED FAILURE"

### Mô hình hiện tại

```
Test Result Status: "Passed" | "Failed" | "Skipped"
```

### Mô hình cần thiết

```
Test Result:
├── ExpectedOutcome: "Success" | "Failure"  // Test này mong đợi API thành công hay thất bại?
├── ActualOutcome: "Success" | "Failure"    // API thực tế thành công hay thất bại?
└── TestStatus: "Passed" | "Failed"         // Test status = (Expected == Actual)
```

### Ví dụ minh họa

| Test Type | Expected Outcome | API Response | Actual Outcome | Test Status |
|-----------|-----------------|--------------|----------------|-------------|
| Happy Path | Success (200) | 200 OK | Success | **PASSED** ✅ |
| Happy Path | Success (200) | 500 Error | Failure | **FAILED** ❌ |
| Negative | Failure (4xx) | 400 Bad Request | Failure | **PASSED** ✅ |
| Negative | Failure (4xx) | 200 OK | Success | **FAILED** ❌ |
| Negative | Failure (400) | 422 Unprocessable | Failure | **PASSED** ✅ (cần fix) |

---

## 4. GIẢI PHÁP ĐỀ XUẤT

### 4.1 Solution 1: Mở rộng Expected Status List (Khuyến nghị - Quick Win)

**Thay đổi:**
```csharp
// Thay vì hard-code 1 status
ExpectedStatusCode = 400

// Cho phép nhiều status hợp lệ theo từng mutation type
ExpectedStatusCodes = GetAllowedStatusCodes(mutationType)
```

**Mapping đề xuất:**
```csharp
public static int[] GetAllowedStatusCodes(MutationType type) => type switch
{
    MutationType.MissingRequired => [400, 422],
    MutationType.InvalidType => [400, 422],
    MutationType.MalformedJson => [400],
    MutationType.Unauthorized => [401, 403],
    MutationType.NonExistent => [404],
    MutationType.Conflict => [409],
    MutationType.TooLarge => [400, 413, 422],
    _ => [400, 422, 500]  // Default fallback
};
```

### 4.2 Solution 2: Thêm Semantic Test Type Awareness

**Thêm logic vào Validator:**
```csharp
public TestCaseValidationResult Validate(...)
{
    // Nếu test type là Negative/Boundary và API trả non-2xx
    // → Có thể considered "expected failure"
    
    var isNegativeTest = testCase.TestType == TestType.Negative 
                      || testCase.TestType == TestType.Boundary;
    var apiReturnedError = response.StatusCode >= 400;
    
    if (isNegativeTest && apiReturnedError)
    {
        // Relaxed validation: chỉ check API có reject không
        // Không cần exact status match
    }
}
```

### 4.3 Solution 3: Centralize Expected Status Policy (Best Long-term)

**Tạo service mới:**
```csharp
public interface IExpectedStatusPolicy
{
    int[] GetAllowedStatuses(
        string mutationType,
        string testType,
        string httpMethod,
        string dataType);
}
```

**Sử dụng:**
```csharp
// Trong Generator
var allowedStatuses = _policy.GetAllowedStatuses(
    mutationType: "missingRequired",
    testType: "Negative",
    httpMethod: "POST",
    dataType: "string"
);

// Generate test case với full list
Expectation = new TestCaseExpectation
{
    ExpectedStatus = JsonSerializer.Serialize(allowedStatuses)
};
```

---

## 5. IMPLEMENTATION PLAN

### Phase 0: Quick Fix (1-2 ngày)
1. **Mở rộng expected status trong BodyMutationEngine**
   - Thay `ExpectedStatusCode = 400` → `ExpectedStatusCodes = [400, 422]`
2. **Preserve multi-status trong LlmScenarioSuggester**
   - Không lấy `FirstOrDefault()`, giữ toàn bộ list

### Phase 1: Policy Centralization (3-5 ngày)
1. Tạo `BoundaryNegativeExpectationPolicy` service
2. Định nghĩa mapping mutationType → allowedStatuses
3. Cập nhật tất cả generators sử dụng policy mới

### Phase 2: Semantic Validation (Optional)
1. Thêm `TestType` awareness vào Validator
2. Cho phép relaxed validation cho negative tests

---

## 6. TÓM TẮT GỐC RỄ VẤN ĐỀ

```
┌──────────────────────────────────────────────────────────────────────┐
│                         ROOT CAUSE SUMMARY                            │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  1. Expected status code bị HARD-CODE quá hẹp: chỉ [400]             │
│     → API trả 422 (đúng) nhưng test fail vì không match              │
│                                                                       │
│  2. LLM suggestions bị MẤT DỮ LIỆU: nhiều status → 1 status          │
│     → Mất đi flexibility của expected outcomes                       │
│                                                                       │
│  3. Không có SEMANTIC AWARENESS về test type                         │
│     → Validator không biết negative test "expect failure"            │
│                                                                       │
│  4. THIẾU KHÁI NIỆM "expected failure vs unexpected failure"         │
│     → Mọi non-match đều bị đánh "Failed"                             │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 7. FILES CẦN SỬA

| File | Vấn đề | Fix |
|------|--------|-----|
| `TestGeneration/Services/BodyMutationEngine.cs` | Hard-code `400` | Dùng `GetAllowedStatusCodes()` |
| `TestGeneration/Services/LlmScenarioSuggester.cs` | `FirstOrDefault()` | Giữ full list |
| `TestGeneration/Services/LlmSuggestionMaterializer.cs` | Single status | Support multi-status |
| `TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs` | Single status in expectation | Serialize full list |
| `TestExecution/Services/RuleBasedValidator.cs` | (Đã OK với multi-status) | Không cần sửa nếu Phase 0 hoàn thành |

---

## 8. KẾT LUẬN

**Vấn đề không phải bug validation logic, mà là BUG DATA:**
- Test cases được generate với expected status quá hẹp
- Validator hoạt động đúng: so sánh expected vs actual
- Nhưng expected value ban đầu đã sai/không đủ flexible

**Fix đúng đắn:**
1. Mở rộng expected status codes khi generate test cases
2. Không hard-code 1 status duy nhất
3. Cho phép API trả nhiều error codes khác nhau mà vẫn PASS

**Tham khảo thêm:** `NEGATIVE-BOUNDARY-FAILURE-ANALYSIS-REPORT.md` đã phân tích chi tiết và có solution phases đầy đủ.
