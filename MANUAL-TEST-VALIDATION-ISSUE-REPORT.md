# 📋 BÁO CÁO PHÂN TÍCH VẤN ĐỀ: Manual Test Nhập Form Bậy Bạ Vẫn Pass

**Ngày tạo:** 2026-04-06  
**Module:** `ClassifiedAds.Modules.TestExecution`  
**Mức độ nghiêm trọng:** 🔴 **HIGH** - Ảnh hưởng đến tính chính xác của kết quả test  
**Trạng thái:** Chờ xử lý  
**Repository:** GSP26SE43.ModularMonolith

---

## 📑 MỤC LỤC

1. [Tóm Tắt Vấn Đề](#1-tóm-tắt-vấn-đề)
2. [Phân Tích Chi Tiết](#2-phân-tích-chi-tiết)
3. [Kịch Bản Tái Hiện](#3-kịch-bản-tái-hiện-vấn-đề)
4. [Đánh Giá Tác Động](#4-đánh-giá-tác-động)
5. [Giải Pháp Đề Xuất](#5-giải-pháp-đề-xuất)
6. [Implementation Priority](#6-implementation-priority)
7. [Test Cases Cho Việc Sửa Lỗi](#7-test-cases-cho-việc-sửa-lỗi)
8. [Kết Luận](#8-kết-luận)

---

## 1. TÓM TẮT VẤN ĐỀ

### 1.1. Hiện Tượng

Khi người dùng nhập form với dữ liệu không hợp lệ (bậy bạ, sai định dạng, garbage data), test vẫn được đánh giá là **PASSED** thay vì **FAILED**.

### 1.2. Nguyên Nhân Gốc
Hệ thống thiếu validation ở nhiều layer và sử dụng **"lenient defaults"** - nghĩa là khi không có gì để validate, test mặc định là **PASS**.

### 1.3. Chuỗi Vấn Đề (Root Cause Chain)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    CHUỖI LỖI VALIDATION                                 │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  1️⃣ StartTestRunRequest ────► KHÔNG CÓ VALIDATOR                       │
│      ↓                                                                  │
│  2️⃣ StartTestRunCommand ────► CHỈ VALIDATE BUSINESS LOGIC              │
│      ↓                                                                  │
│  3️⃣ RuleBasedValidator ─────► LENIENT DEFAULTS (null = skip)           │
│      ↓                                                                  │
│  4️⃣ TestCaseValidationResult ─► IsPassed = (Failures.Count == 0)       │
│      ↓                                                                  │
│  📗 KẾT QUẢ: TEST PASS DÙ INPUT INVALID                                 │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. PHÂN TÍCH CHI TIẾT

### 2.1. Architecture Flow của Manual Test Execution

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│TestRunsController│────►│StartTestRunCommand│────►│TestExecutionOrchestrator│
│ (POST /test-runs)│     │     Handler      │     │                          │
└──────────────────┘     └──────────────────┘     └──────────────────────┘
                                                              │
                                                              ▼
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│TestResultCollector│◄────│ RuleBasedValidator │◄────│  HttpTestExecutor      │
│                   │     │  (THE PROBLEM!)   │     │                        │
└──────────────────┘     └──────────────────┘     └──────────────────────┘
```

### 2.2. Vấn Đề Ở Từng Layer

| Layer | File | Vấn Đề | Severity |
|-------|------|--------|----------|
| **Request DTO** | `StartTestRunRequest.cs` | Không có FluentValidation | 🔴 HIGH |
| **Controller** | `TestRunsController.cs` | Chỉ filter `Guid.Empty` | 🟡 MEDIUM |
| **Command Handler** | `StartTestRunCommand.cs` | Chỉ validate business logic | 🟡 MEDIUM |
| **Validator** | `RuleBasedValidator.cs` | Lenient defaults, silent catch | 🔴 HIGH |
| **Result Model** | `TestCaseValidationResult.cs` | Thiếu warnings field | 🟡 MEDIUM |

### 2.3. Chi Tiết Từng Layer

#### 🔴 LAYER 1: StartTestRunRequest (KHÔNG CÓ VALIDATION)

**File:** [`ClassifiedAds.Modules.TestExecution/Models/Requests/StartTestRunRequest.cs`](ClassifiedAds.Modules.TestExecution/Models/Requests/StartTestRunRequest.cs)

```csharp
public class StartTestRunRequest
{
    public Guid? EnvironmentId { get; set; }        // ❌ Không validate
    public List<Guid> SelectedTestCaseIds { get; set; } // ❌ Không validate
}
```

**Vấn đề:**
- ❌ KHÔNG có FluentValidation validator
- ❌ KHÔNG có DataAnnotations (`[Required]`, `[MaxLength]`, etc.)
- ❌ Chấp nhận BẤT KỲ giá trị nào - null, empty list, invalid GUIDs

#### 🔴 LAYER 2: Controller (CHỈ LỌC Guid.Empty)

**File:** [`ClassifiedAds.Modules.TestExecution/Controllers/TestRunsController.cs`](ClassifiedAds.Modules.TestExecution/Controllers/TestRunsController.cs) (lines 47-65)

```csharp
var command = new StartTestRunCommand
{
    TestSuiteId = suiteId,
    CurrentUserId = _currentUser.UserId,
    EnvironmentId = request?.EnvironmentId,
    SelectedTestCaseIds = request?.SelectedTestCaseIds?
        .Where(id => id != Guid.Empty)  // Chỉ filter empty GUID
        .Distinct()
        .ToList(),
};
```

**Vấn đề:**
- ✅ Lọc empty GUIDs và duplicates
- ❌ KHÔNG validate test case IDs thuộc về test suite
- ❌ KHÔNG validate số lượng test cases
- ❌ KHÔNG validate EnvironmentId hợp lệ trước khi gửi command

#### 🔴 LAYER 3: Command Handler (CHỈ BUSINESS LOGIC)

**File:** [`ClassifiedAds.Modules.TestExecution/Commands/StartTestRunCommand.cs`](ClassifiedAds.Modules.TestExecution/Commands/StartTestRunCommand.cs) (lines 59-141)

```csharp
// Chỉ validate business logic:
if (command.TestSuiteId == Guid.Empty) throw ...;
if (suiteContext.CreatedById != command.CurrentUserId) throw ...;
if (suiteContext.Status != "Ready") throw ...;
// ... concurrent limits, monthly quota
```

**Vấn đề:**
- ✅ Validate quyền sở hữu suite
- ✅ Validate trạng thái suite
- ❌ KHÔNG validate `SelectedTestCaseIds` thuộc về suite
- ❌ KHÔNG validate test cases tồn tại

#### 🔴 LAYER 4: RuleBasedValidator (LENIENT DEFAULTS - CỐT LÕI VẤN ĐỀ)

**File:** [`ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs`](ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs)

**Vấn đề #1: Null Expectation = Auto PASS (Lines 46-50)**
```csharp
var expectation = testCase.Expectation;
if (expectation == null)
{
    return result;  // 🔴 result.IsPassed = true (khởi tạo ở line 29)
}
```

**Vấn đề #2: Empty Fields = Skip Validation (Lines 82-85, 315-318, 382-385)**
```csharp
// Status Code - Lines 82-85
if (string.IsNullOrEmpty(expectation.ExpectedStatus))
{
    return;  // 🔴 Skip validation - không add failure!
}

// Headers - Lines 315-318  
if (string.IsNullOrEmpty(expectation.HeaderChecks))
{
    return;  // 🔴 Skip validation - không add failure!
}

// Body Contains - Lines 382-385
if (string.IsNullOrEmpty(expectation.BodyContains))
{
    return;  // 🔴 Skip validation - không add failure!
}

// Tương tự với: BodyNotContains, JsonPathChecks, ResponseSchema, MaxResponseTime
```

**Vấn đề #3: Invalid JSON = Silent Skip (Lines 320-328, 388-395, 432-439, 478-485)**
```csharp
// Lines 320-328 (HeaderChecks)
Dictionary<string, string> headerChecks;
try
{
    headerChecks = JsonSerializer.Deserialize<Dictionary<string, string>>(expectation.HeaderChecks);
}
catch  // 🔴 SILENT CATCH - không log, không add failure!
{
    return;  // 🔴 Skip validation hoàn toàn
}

// Tương tự cho BodyContains, BodyNotContains, JsonPathChecks...
```

**Vấn đề #4: Final Pass Logic (Line 73)**
```csharp
result.IsPassed = result.Failures.Count == 0;
// 🔴 Logic: Nếu tất cả checks bị skip → Failures.Count = 0 → PASS!
```

#### 🟡 LAYER 5: TestCaseValidationResult (THIẾU WARNING SUPPORT)

**File:** [`ClassifiedAds.Modules.TestExecution/Models/TestCaseValidationResult.cs`](ClassifiedAds.Modules.TestExecution/Models/TestCaseValidationResult.cs)

```csharp
public class TestCaseValidationResult
{
    public bool IsPassed { get; set; }
    public bool StatusCodeMatched { get; set; }
    public bool? SchemaMatched { get; set; }
    public bool? HeaderChecksPassed { get; set; }
    public bool? BodyContainsPassed { get; set; }
    public bool? BodyNotContainsPassed { get; set; }
    public bool? JsonPathChecksPassed { get; set; }
    public bool? ResponseTimePassed { get; set; }
    public List<ValidationFailureModel> Failures { get; set; } = new();
    
    // ❌ THIẾU: Warnings list để thông báo về skipped checks
    // ❌ THIẾU: HasWarnings flag
    // ❌ THIẾU: ChecksPerformed counter
    // ❌ THIẾU: ChecksSkipped counter
}
```

#### 📊 Unit Test Xác Nhận Hành Vi Hiện Tại

**File:** [`ClassifiedAds.UnitTests/TestExecution/RuleBasedValidatorTests.cs`](ClassifiedAds.UnitTests/TestExecution/RuleBasedValidatorTests.cs) (lines 428-445)

```csharp
[Fact]
public void Validate_NoExpectation_Should_Pass()  // ⚠️ Test này confirm behavior hiện tại!
{
    // Arrange
    var response = CreateResponse();
    var testCase = new ExecutionTestCaseDto
    {
        TestCaseId = Guid.NewGuid(),
        Expectation = null,  // 🔴 Không có expectation
    };

    // Act
    var result = _validator.Validate(response, testCase);

    // Assert
    result.IsPassed.Should().BeTrue();  // 🔴 Test expect PASS - đây là behavior cần sửa!
}
```

---

## 3. KỊCH BẢN TÁI HIỆN VẤN ĐỀ

### Scenario 1: Test Case Không Có Expectation
```
📥 INPUT: 
   Test case được tạo mà không define expectation (Expectation = null)

📤 EXPECTED: 
   Test FAIL hoặc có WARNING "No expectation defined"

❌ ACTUAL: 
   Test PASS ✅ (SAI!)

🔍 REASON: 
   expectation == null → return result (line 48-50)
   → result.IsPassed = true (khởi tạo line 29)
   → Failures.Count = 0 → PASS
```

### Scenario 2: Expectation Với JSON Sai Format
```
📥 INPUT:
   ExpectedStatus: "abc123"        (không phải JSON array số)
   HeaderChecks: "{invalid json"   (JSON syntax error)
   BodyContains: "not an array"    (không phải JSON array)

📤 EXPECTED:
   Test FAIL với lỗi "Invalid expectation format"

❌ ACTUAL:
   Test PASS ✅ (SAI!)

🔍 REASON:
   JsonException caught silently (lines 325, 393, 438, 483)
   → return; (skip validation hoàn toàn)
   → Failures.Count = 0 → PASS
```

### Scenario 3: Expectation Trống (Empty Strings)
```
📥 INPUT:
   ExpectedStatus: ""
   HeaderChecks: ""
   BodyContains: ""
   BodyNotContains: ""
   JsonPathChecks: ""
   MaxResponseTime: null

📤 EXPECTED:
   Test FAIL hoặc WARNING "All validation checks skipped"

❌ ACTUAL:
   Test PASS ✅ (SAI!)

🔍 REASON:
   Mỗi field empty → IsNullOrEmpty check → return early
   → Tất cả 7 validation checks bị skip
   → Failures.Count = 0 → PASS
```

### Scenario 4: Test Case IDs Không Thuộc Suite
```
📥 INPUT:
   SelectedTestCaseIds: [guid1, guid2, guid3]
   (guid1, guid2 thuộc suite A, guid3 thuộc suite B hoặc không tồn tại)

📤 EXPECTED:
   Error: "Test case guid3 không thuộc suite này"

❌ ACTUAL:
   Request được chấp nhận, guid3 có thể không tìm thấy hoặc execute từ suite khác

🔍 REASON:
   Controller chỉ filter Guid.Empty (line 57)
   Command handler không validate ownership của test case IDs
```

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG

### 4.1. Impact Matrix

| Khía Cạnh | Mức Độ | Mô Tả |
|-----------|--------|-------|
| **Độ tin cậy kết quả test** | 🔴 HIGH | Test có thể pass dù API thực sự fail |
| **User Experience** | 🟡 MEDIUM | User thấy "All tests passed" nhưng không có gì được validate |
| **False Positive Rate** | 🔴 HIGH | Tỉ lệ false positive cao khi expectation không được define đúng |
| **Security** | 🟢 LOW | Không ảnh hưởng trực tiếp đến security |
| **Data Integrity** | 🟡 MEDIUM | Kết quả test không phản ánh đúng thực tế |
| **Business Impact** | 🔴 HIGH | Users có thể deploy code có bug vì tin vào test results sai |

### 4.2. Risk Matrix

```
                 IMPACT
           Low    Medium    High
         ┌──────┬──────────┬──────────┐
    High │      │          │ 🔴       │  ← False Positive, Business
         ├──────┼──────────┼──────────┤
L  Med   │      │ 🟡       │          │  ← UX, Data Integrity
I        ├──────┼──────────┼──────────┤
K  Low   │ 🟢   │          │          │  ← Security
E        └──────┴──────────┴──────────┘
```

---

## 5. GIẢI PHÁP ĐỀ XUẤT

### 5.1. SOLUTION 1: Thêm FluentValidation cho Request (MUST HAVE - P0)

**File mới:** `ClassifiedAds.Modules.TestExecution/Models/Validators/StartTestRunRequestValidator.cs`

```csharp
using FluentValidation;
using System;
using System.Linq;

namespace ClassifiedAds.Modules.TestExecution.Models.Validators;

public class StartTestRunRequestValidator : AbstractValidator<StartTestRunRequest>
{
    public StartTestRunRequestValidator()
    {
        // Validate EnvironmentId format if provided
        When(x => x.EnvironmentId.HasValue, () =>
        {
            RuleFor(x => x.EnvironmentId)
                .NotEqual(Guid.Empty)
                .WithMessage("EnvironmentId không hợp lệ.");
        });

        // Validate SelectedTestCaseIds
        When(x => x.SelectedTestCaseIds != null && x.SelectedTestCaseIds.Count > 0, () =>
        {
            RuleFor(x => x.SelectedTestCaseIds)
                .Must(ids => ids.All(id => id != Guid.Empty))
                .WithMessage("Danh sách test case chứa ID không hợp lệ.")
                .Must(ids => ids.Count <= 1000)
                .WithMessage("Không thể chạy quá 1000 test cases cùng lúc.");
        });
    }
}
```

**Đăng ký trong DI (`ServiceCollectionExtensions.cs`):**
```csharp
using FluentValidation;

public static IServiceCollection AddTestExecutionModule(this IServiceCollection services, ...)
{
    // ... existing registrations ...
    
    // Add FluentValidation
    services.AddValidatorsFromAssemblyContaining<StartTestRunRequestValidator>();
    
    return services;
}
```

### 5.2. SOLUTION 2: Thêm ValidationWarningModel + Update TestCaseValidationResult (MUST HAVE - P0)

**File mới:** `ClassifiedAds.Modules.TestExecution/Models/ValidationWarningModel.cs`

```csharp
namespace ClassifiedAds.Modules.TestExecution.Models;

public class ValidationWarningModel
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string Target { get; set; }
}
```

**Update:** `ClassifiedAds.Modules.TestExecution/Models/TestCaseValidationResult.cs`

```csharp
public class TestCaseValidationResult
{
    public bool IsPassed { get; set; }
    public bool StatusCodeMatched { get; set; }
    public bool? SchemaMatched { get; set; }
    public bool? HeaderChecksPassed { get; set; }
    public bool? BodyContainsPassed { get; set; }
    public bool? BodyNotContainsPassed { get; set; }
    public bool? JsonPathChecksPassed { get; set; }
    public bool? ResponseTimePassed { get; set; }
    public List<ValidationFailureModel> Failures { get; set; } = new();
    
    // 🆕 NEW: Warning support
    public List<ValidationWarningModel> Warnings { get; set; } = new();
    public bool HasWarnings => Warnings.Count > 0;
    
    // 🆕 NEW: Tracking skipped checks
    public int ChecksPerformed { get; set; }
    public int ChecksSkipped { get; set; }
}
```

### 5.3. SOLUTION 3: Sửa RuleBasedValidator Logic (MUST HAVE - P0)

**File:** `ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs`

#### Thay đổi 1: Null Expectation → Warning thay vì Auto PASS

```csharp
// RuleBasedValidator.cs - Lines 46-50 SỬA THÀNH:
var expectation = testCase.Expectation;
if (expectation == null)
{
    // 🆕 THAY ĐỔI: Thêm warning thay vì silent pass
    result.Warnings.Add(new ValidationWarningModel
    {
        Code = "NO_EXPECTATION_DEFINED",
        Message = "Test case không có expectation được định nghĩa. Kết quả không đáng tin cậy.",
    });
    result.ChecksSkipped = 7;  // All 7 possible checks skipped
    _logger.LogWarning(
        "Test case {TestCaseId} has no expectation defined. Result may be unreliable.",
        testCase.TestCaseId);
    return result;  // Vẫn pass (IsPassed=true) nhưng có warning
}
```

#### Thay đổi 2: Track Checks Performed/Skipped + Warn khi All Skipped

```csharp
// Trong method Validate(), sau khi chạy tất cả validation:
var checksPerformed = 0;
var checksSkipped = 0;

// Mỗi validation method return bool indicating if check was performed
if (ValidateStatusCode(response, expectation, result))
    checksPerformed++;
else
    checksSkipped++;

// ... tương tự cho 6 checks còn lại ...

result.ChecksPerformed = checksPerformed;
result.ChecksSkipped = checksSkipped;

// 🆕 NEW: Warn nếu tất cả checks bị skip
if (checksPerformed == 0 && checksSkipped > 0)
{
    result.Warnings.Add(new ValidationWarningModel
    {
        Code = "ALL_CHECKS_SKIPPED",
        Message = $"Tất cả {checksSkipped} validation checks bị bỏ qua do expectation trống hoặc không hợp lệ.",
    });
    _logger.LogWarning(
        "Test case {TestCaseId}: All {SkippedCount} validation checks were skipped.",
        testCase.TestCaseId, checksSkipped);
}

result.IsPassed = result.Failures.Count == 0;
return result;
```

#### Thay đổi 3: Invalid JSON → Fail với Error Message (thay vì Silent Skip)

```csharp
// Thay catch silent bằng catch với failure
// VÍ DỤ: ValidateHeaders method
private static bool ValidateHeaders(
    HttpTestResponse response,
    ExecutionTestCaseExpectationDto expectation,
    TestCaseValidationResult result)
{
    if (string.IsNullOrEmpty(expectation.HeaderChecks))
    {
        return false;  // Check skipped
    }

    Dictionary<string, string> headerChecks;
    try
    {
        headerChecks = JsonSerializer.Deserialize<Dictionary<string, string>>(expectation.HeaderChecks);
    }
    catch (JsonException ex)
    {
        // 🆕 NEW: Fail on invalid JSON instead of silent skip
        result.Failures.Add(new ValidationFailureModel
        {
            Code = "INVALID_EXPECTATION_FORMAT",
            Message = $"HeaderChecks không phải JSON hợp lệ: {ex.Message}",
            Target = "HeaderChecks",
            Actual = expectation.HeaderChecks,
        });
        return true;  // Check performed (and failed)
    }

    // ... rest of validation logic ...
    return true;  // Check performed
}

// Tương tự áp dụng cho:
// - ValidateStatusCode (ExpectedStatus)
// - ValidateBodyContains (BodyContains)
// - ValidateBodyNotContains (BodyNotContains)
// - ValidateJsonPathChecks (JsonPathChecks)
```

### 5.4. SOLUTION 4: Validate Test Case IDs Thuộc Suite (SHOULD HAVE - P1)

**Trong StartTestRunCommandHandler:**

```csharp
// Thêm vào sau khi load suite context (sau line 74)
if (selectedIds.Count > 0)
{
    var validTestCaseIds = await _gatewayService.GetTestCaseIdsBySuiteAsync(
        command.TestSuiteId, cancellationToken);
    
    var invalidIds = selectedIds.Except(validTestCaseIds).ToList();
    if (invalidIds.Count > 0)
    {
        var displayIds = string.Join(", ", invalidIds.Take(5));
        var suffix = invalidIds.Count > 5 ? $" và {invalidIds.Count - 5} ID khác" : "";
        throw new ValidationException(
            $"Các test case sau không thuộc suite này: {displayIds}{suffix}");
    }
}
```

**Thêm method vào ITestExecutionReadGatewayService interface:**

```csharp
public interface ITestExecutionReadGatewayService
{
    // ... existing methods ...
    
    /// <summary>
    /// Get all test case IDs belonging to a test suite.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetTestCaseIdsBySuiteAsync(
        Guid testSuiteId, 
        CancellationToken ct = default);
}
```

### 5.5. SOLUTION 5: Thêm Strict Mode Option (NICE TO HAVE - P2)

Cho phép user chọn chế độ nghiêm ngặt khi muốn test FAIL nếu không có expectation:

**Update StartTestRunRequest:**

```csharp
public class StartTestRunRequest
{
    public Guid? EnvironmentId { get; set; }
    public List<Guid> SelectedTestCaseIds { get; set; }
    
    /// <summary>
    /// Strict mode: fail test cases that have no expectation defined.
    /// Default: false (backward compatible - pass with warning)
    /// </summary>
    public bool StrictValidation { get; set; } = false;
}
```

**Update RuleBasedValidator để support strict mode:**

```csharp
public interface IRuleBasedValidator
{
    TestCaseValidationResult Validate(
        HttpTestResponse response,
        ExecutionTestCaseDto testCase,
        ApiEndpointMetadataDto endpointMetadata = null,
        bool strictMode = false);  // 🆕 NEW parameter
}

// Implementation:
if (expectation == null)
{
    if (strictMode)
    {
        result.IsPassed = false;
        result.Failures.Add(new ValidationFailureModel
        {
            Code = "NO_EXPECTATION",
            Message = "Strict mode: Test case phải có expectation được định nghĩa.",
        });
        return result;
    }
    // ... warning logic cho non-strict mode (như solution 3)
}
```

---

## 6. IMPLEMENTATION PRIORITY

### 6.1. Priority Matrix

| Priority | Task | Effort | Impact | Files Cần Sửa |
|----------|------|--------|--------|---------------|
| 🔴 **P0** | Thêm FluentValidation cho StartTestRunRequest | 2h | HIGH | +1 new file, edit 1 |
| 🔴 **P0** | Thêm ValidationWarningModel + Update TestCaseValidationResult | 1h | HIGH | +1 new file, edit 1 |
| 🔴 **P0** | Sửa RuleBasedValidator - null expectation warning | 2h | HIGH | edit 1 |
| 🔴 **P0** | Sửa RuleBasedValidator - invalid JSON → failure | 3h | HIGH | edit 1 |
| 🔴 **P0** | Update unit tests cho RuleBasedValidator | 2h | HIGH | edit 1 |
| 🟡 **P1** | Validate test case IDs thuộc suite | 3h | MEDIUM | edit 2-3 |
| 🟡 **P1** | Track và report skipped checks in result | 2h | MEDIUM | edit 2 |
| 🟢 **P2** | Thêm strict validation mode | 4h | LOW | edit 3-4 |

### 6.2. Sprint Timeline

```
📅 Week 1 (P0 - Critical Fixes):
├── Day 1-2: Request validation + Warning model
│   ├── Create StartTestRunRequestValidator.cs
│   ├── Create ValidationWarningModel.cs
│   └── Update TestCaseValidationResult.cs
├── Day 3-4: RuleBasedValidator fixes
│   ├── Null expectation → warning
│   └── Invalid JSON → failure
└── Day 5: Unit test updates
    └── Update RuleBasedValidatorTests.cs

📅 Week 2 (P1 - Important Improvements):
├── Day 1-2: Test case ID validation
│   ├── Add GetTestCaseIdsBySuiteAsync to gateway
│   └── Add validation in command handler
├── Day 3: Skipped checks tracking
│   └── Add counters and reporting
└── Day 4-5: Integration testing

📅 Week 3 (P2 - Nice to Have):
└── Day 1-5: Strict mode implementation (if time permits)
```

### 6.3. Files To Change (Summary)

```
ClassifiedAds.Modules.TestExecution/
├── Models/
│   ├── Requests/
│   │   └── StartTestRunRequest.cs (UPDATE - add StrictValidation)
│   ├── Validators/
│   │   └── StartTestRunRequestValidator.cs (NEW)
│   ├── ValidationWarningModel.cs (NEW)
│   └── TestCaseValidationResult.cs (UPDATE - add Warnings, counters)
├── Services/
│   ├── RuleBasedValidator.cs (UPDATE - major changes)
│   └── IRuleBasedValidator.cs (UPDATE - add strictMode param)
├── Commands/
│   └── StartTestRunCommand.cs (UPDATE - add test case ID validation)
└── ServiceCollectionExtensions.cs (UPDATE - add validator registration)

ClassifiedAds.Contracts/
└── TestGeneration/
    └── Services/
        └── ITestExecutionReadGatewayService.cs (UPDATE)

ClassifiedAds.Modules.TestGeneration/
└── Services/
    └── TestExecutionReadGatewayService.cs (UPDATE)

ClassifiedAds.UnitTests/
└── TestExecution/
    └── RuleBasedValidatorTests.cs (UPDATE - major test changes)
```

---

## 7. TEST CASES CHO VIỆC SỬA LỖI

### 7.1. Unit Tests Cần Thêm/Sửa cho RuleBasedValidator

```csharp
public class RuleBasedValidatorTests_Updated
{
    private readonly RuleBasedValidator _validator;

    public RuleBasedValidatorTests_Updated()
    {
        _validator = new RuleBasedValidator(new Mock<ILogger<RuleBasedValidator>>().Object);
    }

    #region Null Expectation Tests

    [Fact]
    public void Validate_NullExpectation_ShouldPassWithWarning()
    {
        // Arrange
        var response = CreateResponse(statusCode: 200);
        var testCase = new ExecutionTestCaseDto
        {
            TestCaseId = Guid.NewGuid(),
            Expectation = null,
        };

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Code == "NO_EXPECTATION_DEFINED");
        result.ChecksSkipped.Should().Be(7);
        result.ChecksPerformed.Should().Be(0);
    }

    [Fact]
    public void Validate_NullExpectation_StrictMode_ShouldFail()
    {
        // Arrange
        var response = CreateResponse(statusCode: 200);
        var testCase = new ExecutionTestCaseDto
        {
            TestCaseId = Guid.NewGuid(),
            Expectation = null,
        };

        // Act
        var result = _validator.Validate(response, testCase, strictMode: true);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "NO_EXPECTATION");
    }

    #endregion

    #region Invalid JSON Tests

    [Fact]
    public void Validate_InvalidJsonInExpectedStatus_ShouldFail()
    {
        // Arrange
        var response = CreateResponse(statusCode: 200);
        var testCase = CreateTestCase(expectedStatus: "invalid json - not array");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => 
            f.Code == "INVALID_EXPECTATION_FORMAT" && 
            f.Target == "ExpectedStatus");
    }

    [Fact]
    public void Validate_InvalidJsonInHeaderChecks_ShouldFail()
    {
        // Arrange
        var response = CreateResponse();
        var testCase = CreateTestCase(headerChecks: "{invalid json");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => 
            f.Code == "INVALID_EXPECTATION_FORMAT" && 
            f.Target == "HeaderChecks");
    }

    [Fact]
    public void Validate_InvalidJsonInBodyContains_ShouldFail()
    {
        // Arrange
        var response = CreateResponse();
        var testCase = CreateTestCase(bodyContains: "not an array");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => 
            f.Code == "INVALID_EXPECTATION_FORMAT" && 
            f.Target == "BodyContains");
    }

    [Fact]
    public void Validate_InvalidJsonInJsonPathChecks_ShouldFail()
    {
        // Arrange
        var response = CreateResponse(body: "{}");
        var testCase = CreateTestCase(jsonPathChecks: "{not valid json}");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => 
            f.Code == "INVALID_EXPECTATION_FORMAT" && 
            f.Target == "JsonPathChecks");
    }

    #endregion

    #region All Checks Skipped Tests

    [Fact]
    public void Validate_AllFieldsEmpty_ShouldPassWithWarning()
    {
        // Arrange
        var response = CreateResponse();
        var testCase = new ExecutionTestCaseDto
        {
            TestCaseId = Guid.NewGuid(),
            Expectation = new ExecutionTestCaseExpectationDto
            {
                ExpectedStatus = "",
                ResponseSchema = "",
                HeaderChecks = "",
                BodyContains = "",
                BodyNotContains = "",
                JsonPathChecks = "",
                MaxResponseTime = null,
            },
        };

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Code == "ALL_CHECKS_SKIPPED");
        result.ChecksPerformed.Should().Be(0);
        result.ChecksSkipped.Should().Be(7);
    }

    #endregion

    #region Mixed Validation Tests

    [Fact]
    public void Validate_SomeChecksPassSomeSkipped_ShouldReportCorrectCounts()
    {
        // Arrange
        var response = CreateResponse(statusCode: 200, body: """{"name": "test"}""");
        var testCase = CreateTestCase(
            expectedStatus: "[200]",    // Will be checked and pass
            headerChecks: "",           // Will be skipped
            bodyContains: """["test"]"""  // Will be checked and pass
        );

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.HasWarnings.Should().BeFalse();  // Not all skipped
        result.ChecksPerformed.Should().Be(2);
        result.ChecksSkipped.Should().Be(5);
    }

    #endregion

    #region Helper Methods

    private static HttpTestResponse CreateResponse(
        int statusCode = 200,
        string body = null,
        long latencyMs = 100,
        Dictionary<string, string> headers = null)
    {
        return new HttpTestResponse
        {
            StatusCode = statusCode,
            Body = body,
            LatencyMs = latencyMs,
            Headers = headers ?? new Dictionary<string, string>(),
        };
    }

    private static ExecutionTestCaseDto CreateTestCase(
        string expectedStatus = null,
        string responseSchema = null,
        string headerChecks = null,
        string bodyContains = null,
        string bodyNotContains = null,
        string jsonPathChecks = null,
        int? maxResponseTime = null)
    {
        return new ExecutionTestCaseDto
        {
            TestCaseId = Guid.NewGuid(),
            Name = "Test Case",
            Expectation = new ExecutionTestCaseExpectationDto
            {
                ExpectedStatus = expectedStatus,
                ResponseSchema = responseSchema,
                HeaderChecks = headerChecks,
                BodyContains = bodyContains,
                BodyNotContains = bodyNotContains,
                JsonPathChecks = jsonPathChecks,
                MaxResponseTime = maxResponseTime,
            },
        };
    }

    #endregion
}
```

### 7.2. Integration Tests Cần Thêm

```csharp
public class ManualTestValidationIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    
    [Fact]
    public async Task StartTestRun_WithInvalidTestCaseIds_ShouldReturnBadRequest()
    {
        // Arrange
        var suiteId = await CreateTestSuite();
        var request = new StartTestRunRequest
        {
            SelectedTestCaseIds = new List<Guid> { Guid.NewGuid() },  // Random GUID not in suite
        };
        
        // Act
        var response = await _client.PostAsJsonAsync($"/api/test-suites/{suiteId}/test-runs", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        error.Detail.Should().Contain("không thuộc suite này");
    }

    [Fact]
    public async Task StartTestRun_WithEmptyExpectations_ShouldReturnWarnings()
    {
        // Arrange
        var suiteId = await CreateTestSuiteWithEmptyExpectations();
        var request = new StartTestRunRequest();
        
        // Act
        var response = await _client.PostAsJsonAsync($"/api/test-suites/{suiteId}/test-runs", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<TestRunResultModel>();
        result.Cases.Should().AllSatisfy(c => 
        {
            c.Status.Should().Be("Passed");
            // Check warnings exist (will need result model to include warnings)
        });
    }

    [Fact]
    public async Task StartTestRun_StrictMode_WithNoExpectation_ShouldFail()
    {
        // Arrange
        var suiteId = await CreateTestSuiteWithEmptyExpectations();
        var request = new StartTestRunRequest
        {
            StrictValidation = true,
        };
        
        // Act
        var response = await _client.PostAsJsonAsync($"/api/test-suites/{suiteId}/test-runs", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<TestRunResultModel>();
        result.Cases.Should().AllSatisfy(c => c.Status.Should().Be("Failed"));
    }
}
```

---

## 8. KẾT LUẬN

### 8.1. Tóm Tắt Vấn Đề

Vấn đề "nhập form bậy bạ mà vẫn test thành công" xuất phát từ **philosophy thiết kế quá lenient** - ưu tiên "không gây lỗi" hơn "validate đúng". Cụ thể:

| # | Vấn đề | Location | Hậu quả |
|---|--------|----------|---------|
| 1 | Không có request validation | `StartTestRunRequest.cs` | Garbage data được accept |
| 2 | Null expectation = auto pass | `RuleBasedValidator.cs:48` | Test pass mà không validate gì |
| 3 | Empty fields = skip silently | `RuleBasedValidator.cs` nhiều chỗ | Tất cả checks bị skip |
| 4 | Invalid JSON = silent catch | `RuleBasedValidator.cs` nhiều chỗ | Parse error không được report |
| 5 | Pass logic chỉ check failures | `RuleBasedValidator.cs:73` | 0 failures = pass dù 0 checks |
| 6 | Không validate test case ownership | `StartTestRunCommand.cs` | Có thể execute test từ suite khác |

### 8.2. Expected Outcome Sau Khi Fix

| Scenario | Before (Current) | After (Fixed) |
|----------|------------------|---------------|
| Null expectation | ✅ PASS | ✅ PASS + ⚠️ WARNING |
| Empty expectation fields | ✅ PASS | ✅ PASS + ⚠️ WARNING "All checks skipped" |
| Invalid JSON in expectation | ✅ PASS | ❌ FAIL + Error message |
| Test case IDs from other suite | ✅ Executed | ❌ ValidationException |
| Strict mode + no expectation | N/A | ❌ FAIL |

### 8.3. Recommended Approach

1. **Immediate (Week 1):** Implement P0 fixes
   - FluentValidation cho StartTestRunRequest
   - RuleBasedValidator warning/failure changes
   - Unit test updates

2. **Short-term (Week 2):** Implement P1 improvements
   - Test case ID ownership validation  
   - Skipped check tracking và reporting

3. **Long-term (Week 3+):** Consider P2 enhancements
   - Strict validation mode cho enterprise users
   - UI improvements để hiển thị warnings

### 8.4. Risk Mitigation

| Risk | Mitigation |
|------|------------|
| **Breaking existing tests** | Default behavior (non-strict) maintains pass status; chỉ thêm warnings |
| **Performance impact** | Gateway call cho test case ID validation có thể cache |
| **Backward compatibility** | Strict mode là opt-in, teams có thể migrate gradually |
| **Developer confusion** | Clear documentation và logging cho mọi warning/failure codes |

---

## 📎 APPENDIX

### A. Related Files Quick Reference

| File | Purpose |
|------|---------|
| [`StartTestRunRequest.cs`](ClassifiedAds.Modules.TestExecution/Models/Requests/StartTestRunRequest.cs) | Request DTO (cần thêm validator) |
| [`TestRunsController.cs`](ClassifiedAds.Modules.TestExecution/Controllers/TestRunsController.cs) | API endpoint (chỉ filter Guid.Empty) |
| [`StartTestRunCommand.cs`](ClassifiedAds.Modules.TestExecution/Commands/StartTestRunCommand.cs) | Command handler (business logic only) |
| [`RuleBasedValidator.cs`](ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs) | **Core issue - lenient defaults** |
| [`TestCaseValidationResult.cs`](ClassifiedAds.Modules.TestExecution/Models/TestCaseValidationResult.cs) | Result model (thiếu warnings) |
| [`TestExecutionOrchestrator.cs`](ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs) | Execution orchestrator |
| [`TestResultCollector.cs`](ClassifiedAds.Modules.TestExecution/Services/TestResultCollector.cs) | Result collection logic |
| [`RuleBasedValidatorTests.cs`](ClassifiedAds.UnitTests/TestExecution/RuleBasedValidatorTests.cs) | Existing unit tests (confirm current behavior) |

### B. Error Codes Reference (Proposed)

| Code | Type | Description |
|------|------|-------------|
| `NO_EXPECTATION_DEFINED` | Warning | Test case không có expectation |
| `ALL_CHECKS_SKIPPED` | Warning | Tất cả validation checks bị skip |
| `INVALID_EXPECTATION_FORMAT` | Failure | JSON trong expectation field không hợp lệ |
| `NO_EXPECTATION` | Failure | Strict mode: bắt buộc phải có expectation |
| `TEST_CASE_NOT_IN_SUITE` | Failure | Test case ID không thuộc suite được chọn |

---

*Report được tạo vào 2026-04-06*  
*Module: ClassifiedAds.Modules.TestExecution*  
*Repository: GSP26SE43.ModularMonolith*  
*Author: AI Assistant*
