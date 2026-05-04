# SRS-Expected Assertion Canonical Workflow

**Ngày phân tích:** 2026-05-02  
**Repo:** `GSP26SE43.ModularMonolith`  
**Mục tiêu:** Xác định chính xác hệ thống hiện tại đã dùng SRS làm expected/oracle đến mức nào, vì sao chưa đủ chuẩn, và mô tả một workflow canonical để AI Agent khác implement lại theo hướng **SRS-first expectation resolution**.

---

## 1. Executive summary

Kết luận ngắn gọn:

1. **Hệ thống hiện tại không phải không biết SRS.** SRS đã được load và đưa vào payload/prompt cho LLM.  
2. **Nhưng SRS chưa phải nguồn expected mang tính authoritative.** Expected assertion thực tế vẫn chủ yếu đến từ LLM output, Swagger fallback, hoặc default hardcode.  
3. **Trong log runtime đang phân tích, vấn đề còn rõ hơn** vì n8n timeout (`HTTP 524`) khiến luồng rơi sang `local-fallback`, nên expected gần như không còn nhận được lợi ích thực chất từ SRS.  
4. Ngoài ra, test run đó còn bị nhiễu bởi **lỗi environment/base URL**: SRS chỉ ra `http://localhost:5000/api`, nhưng runtime lại gọi `https://petstore.swagger.io/v2/...`, làm nhiều case fail do sai môi trường chứ không chỉ do sai expected.

**Canonical target:**  
Expected assertion phải được resolve theo thứ tự ưu tiên:

1. **SRS reviewed constraints**  
2. **Swagger/OpenAPI response metadata**  
3. **LLM suggestion**  
4. **Minimal hardcoded fallback**

---

## 2. Inputs đã đối chiếu

### 2.1 SRS / Test requirements
- [TEST_REQUIREMENTS (1).md](TEST_REQUIREMENTS%20(1).md#L1-L259)

### 2.2 Swagger / OpenAPI
- [swagger.json](swagger.json)

### 2.3 Runtime log
- [test_api_doc.log](test_api_doc.log#L53-L77)
- [test_api_doc.log](test_api_doc.log#L266-L299)

### 2.4 Code paths chính
- [LlmScenarioSuggester.cs](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs#L78-L86)
- [LlmScenarioSuggester.cs](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs#L245-L267)
- [LlmScenarioSuggester.cs](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs#L410-L442)
- [LlmScenarioSuggester.cs](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs#L722-L787)
- [LlmScenarioSuggester.cs](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs#L1682-L1712)
- [TestGenerationPayloadBuilder.cs](ClassifiedAds.Modules.TestGeneration/Services/TestGenerationPayloadBuilder.cs#L270-L298)
- [TestCaseExpectationBuilder.cs](ClassifiedAds.Modules.TestGeneration/Services/TestCaseExpectationBuilder.cs#L28-L52)
- [BoundaryNegativeTestCaseGenerator.cs](ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs#L257-L312)
- [TestExecutionOrchestrator.cs](ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs)

### 2.5 GitNexus CLI verification
- Đã chạy `npx gitnexus status`
- Kết quả: repository `d:\GSP26SE43.ModularMonolith` đang **up-to-date**
- Indexed commit = Current commit = `6b49de4`

### 2.6 Báo cáo đã có sẵn trong repo
- [EXPECTED-ASSERTION-IMPROVEMENT-REPORT.md](EXPECTED-ASSERTION-IMPROVEMENT-REPORT.md)
- [SRS-EXPECTED-FLOW-ANALYSIS.md](SRS-EXPECTED-FLOW-ANALYSIS.md)
- [BUG-REPORT-CATEGORY-CHAIN-FAILURE.md](BUG-REPORT-CATEGORY-CHAIN-FAILURE.md)

---

## 3. SRS chứa gì mà Swagger không đủ thay thế?

Từ [TEST_REQUIREMENTS (1).md](TEST_REQUIREMENTS%20(1).md#L43-L135) và các phần test case chi tiết như [TC-AUTH-REG-001](TEST_REQUIREMENTS%20(1).md#L206-L243), SRS đang chứa nhiều thứ quan trọng hơn Swagger:

### 3.1 Business/validation expectations
- email phải hợp lệ, unique, auto-lowercase
- password tối thiểu 6 ký tự
- category name unique
- price/stock không âm
- `categoryId` phải là ObjectId hợp lệ và phải tồn tại

### 3.2 Exact response expectations
- success body format
- error body format
- validation error body format kiểu Zod với `fieldErrors` / `formErrors`
- expected message semantics như `User registered successfully`, `Validation failed`, `Route not found`

### 3.3 Security expectations
- không trả về password
- JWT/Bearer behavior
- auth-protected endpoints phải bị chặn khi thiếu token

### 3.4 Boundary cases cụ thể
- password = 6 là valid, 5 là invalid
- `price = 0`, `stock = 0` là valid edge cases
- duplicate email / duplicate category name là conflict case

Trong khi đó, `swagger.json` chủ yếu mô tả contract cơ bản: path, method, schema, required fields, `minLength`, `minimum`, và một phần response code. Nó **không đủ giàu ngữ nghĩa** để thay thế SRS làm expected oracle duy nhất.

---

## 4. Workflow hiện tại (as-is), end-to-end

## 4.1 Spec ingestion

Swagger/OpenAPI được parse thành metadata endpoint, bao gồm responses/schema. Điều này được xác nhận gián tiếp qua việc các service test generation có thể truy cập `metadata.Responses`, `ParameterSchemaPayloads`, `ResponseSchemaPayloads`.

Ý nghĩa:
- Hệ thống **đã có** raw material từ Swagger để làm spec-driven assertions.
- Vấn đề chính không nằm ở thiếu dữ liệu đầu vào, mà ở **chưa ưu tiên dùng đúng chỗ**.

## 4.2 SRS ingestion và lưu trữ

Hệ thống đã có `SrsDocument`, `SrsRequirement` và luồng load requirements theo suite. Điều này được dùng khi build payload generation:

- [TestGenerationPayloadBuilder.cs:270-298](ClassifiedAds.Modules.TestGeneration/Services/TestGenerationPayloadBuilder.cs#L270-L298) đưa `SrsRequirements` vào payload gửi ra ngoài.
- [LlmScenarioSuggester.cs:410-442](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs#L410-L442) build `SrsContext`, gồm:
  - document title
  - content markdown/raw content
  - requirement briefs
  - `TestableConstraints` đã deserialize

Kết luận ở bước này:
- **SRS đã có mặt trong generation context.**
- Nhưng chỉ có mặt ở vai trò **context/prompt input**, chưa phải expected oracle bắt buộc downstream.

## 4.3 Prompt/payload assembly cho LLM

LLM prompt hiện tại có rule nói rõ về SRS:
- [LlmScenarioSuggester.cs:78-86](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs#L78-L86)

Rule 16 hiện viết theo hướng:
- nếu `srsContext.requirements[n].testableConstraints` có dữ liệu,
- generate ít nhất 1 scenario cho constraint,
- `expectedStatus` phải match `expectedOutcome` parse từ constraint,
- `bodyContains` lấy keyword từ constraint,
- `coveredRequirementCodes` phải include requirement code.

Điều này nghe đúng hướng, nhưng vẫn có một điểm yếu cốt lõi:

> Rule prompt chỉ là chỉ dẫn cho model. Nó không phải cơ chế enforcement sau khi model trả kết quả hoặc khi model không chạy được.

## 4.4 LLM generation và fallback runtime

Luồng chính:
- [LlmScenarioSuggester.cs:245-267](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs#L245-L267)

Hệ thống gọi webhook `generate-llm-suggestions`. Nếu n8n lỗi transient thì rơi về:
- `Model = "local-fallback"`
- `TokensUsed = 0`
- sau đó hệ thống bù bằng `EnsureAdaptiveCoverage(...)`

Log thực tế xác nhận điều này:
- [test_api_doc.log:58-77](test_api_doc.log#L58-L77)
  - payload ~35 KB
  - webhook trả `524`
  - log ghi rõ: `Falling back to local contract-based synthesis`
  - `Adaptive coverage added 30 fallback scenario(s)`
  - model = `local-fallback`
  - tokens = `0`

Kết luận:
- Khi LLM timeout, toàn bộ ý đồ “SRS-guided generation” gần như mất tác dụng thực tế.
- Nếu fallback không hỏi SRS oracle, expected sẽ quay về generic contract/default behavior.

## 4.5 Parse scenarios và build expected status

Điểm lệch lớn nhất nằm ở đây:
- [LlmScenarioSuggester.cs:722-787](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs#L722-L787)

Hàm `BuildExpectedStatuses(...)` hiện làm như sau:
- nếu HappyPath: ưu tiên 2xx + fallback theo HTTP method
- nếu Boundary/Negative:
  - loại success codes khỏi output model
  - thử đọc error codes từ Swagger responses
  - nếu không có, rơi về hardcode `[400, 401, 403, 404, 409, 415, 422]`

Điểm quan trọng:
- Ở đây **không có bước hỏi SRS constraint** để quyết định expected status.
- Nghĩa là dù prompt đã yêu cầu LLM tôn trọng SRS, sau parse hệ thống vẫn không có tầng policy nào nói rằng:
  - “nếu SRS có authoritative constraint thì override/match theo SRS trước”.

## 4.6 Assertion repair từ Swagger schema

Một lớp fallback khác nằm ở:
- [LlmScenarioSuggester.cs:1682-1712](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs#L1682-L1712)

`RepairAssertionsFromSchema(...)` có nhiệm vụ:
- nếu LLM bỏ trống `jsonPathChecks` hoặc `bodyContains`,
- hệ thống lấy Swagger response schema để tự repair assertions.

Điều này tốt hơn hardcode mù, nhưng vẫn có giới hạn:
- chỉ nhìn **Swagger schema**,
- không nhìn **SRS exact expectations**,
- không thể suy ra đầy đủ semantics như:
  - thông điệp lỗi mong đợi,
  - negative duplicate phải là `409`,
  - validation error body phải có `fieldErrors` / `formErrors`,
  - body success không được chứa `password`.

## 4.7 Final expectation materialization

Bước build entity cuối:
- [TestCaseExpectationBuilder.cs:28-52](ClassifiedAds.Modules.TestGeneration/Services/TestCaseExpectationBuilder.cs#L28-L52)

`TestCaseExpectationBuilder.Build(...)` hiện gần như:
- nhận `N8nTestCaseExpectation source`
- serialize `ExpectedStatus`, `BodyContains`, `BodyNotContains`, `JsonPathChecks`, ...
- nếu source null thì fallback minimal `200`

Điểm quan trọng:
- builder này **không có bước reconcile** giữa SRS / Swagger / LLM.
- nó **không biết nguồn nào authoritative**.
- nó chỉ materialize kết quả đã có.

## 4.8 Rule-based mutation path/body generation

Ngoài luồng LLM, còn có luồng rule-based mutation:
- [BoundaryNegativeTestCaseGenerator.cs:257-312](ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs#L257-L312)

Ở đây expected status được set trực tiếp từ:
- `mutation.GetEffectiveExpectedStatusCodes()`

Kết luận:
- Rule-based path/body mutation hiện tại **không hỏi SRS**.
- Vì vậy kể cả không dùng LLM, hệ thống cũng chưa có SRS-first oracle cho boundary/negative generation.

## 4.9 Review → approve → execute

Log runtime cho thấy:
- generate preview
- approve từng suggestion
- start test run
- execute via orchestrator

Nhưng khi execute, validator chỉ dùng những gì đã được materialize vào `TestCaseExpectation`. Nếu expected đã sai từ đầu, runtime chỉ có thể báo fail, không thể tự “biết lại” SRS.

---

## 5. Bằng chứng log: vì sao case hiện tại càng chứng minh SRS chưa là oracle?

### 5.1 n8n timeout → local fallback
- [test_api_doc.log:68-77](test_api_doc.log#L68-L77)

Các dấu hiệu:
- `HTTP 524`
- `Falling back to local contract-based synthesis`
- `Adaptive coverage added 30 fallback scenario(s)`
- `Model=local-fallback`
- `TokensUsed=0`

Suy ra:
- run này gần như không có LLM-generated intelligence thực tế ở bước suggestion.
- nếu SRS không có tầng enforcement riêng, expected chắc chắn không thể bám SRS một cách đáng tin.

### 5.2 Sai base URL runtime
- SRS chỉ ra base URL: [TEST_REQUIREMENTS (1).md:2-6](TEST_REQUIREMENTS%20(1).md#L2-L6)
- Health check setup: [TEST_REQUIREMENTS (1).md:62-71](TEST_REQUIREMENTS%20(1).md#L62-L71)
- Nhưng log runtime gọi:
  - [test_api_doc.log:273-275](test_api_doc.log#L273-L275)
  - `https://petstore.swagger.io/v2/store/api/auth/register`

Đây là một bug môi trường/routing riêng, cần tách khỏi bug oracle:

- **Bug A:** expected/assertion chưa SRS-first  
- **Bug B:** runtime đang đánh nhầm target API

Nếu không tách riêng hai lỗi này, AI Agent rất dễ sửa sai trọng tâm.

---

## 6. Matrix nguồn tri thức hiện tại

| Loại dữ liệu | SRS | Swagger | LLM output | Runtime fallback hiện tại |
|---|---|---|---|---|
| Endpoint/path/method | Một phần | Mạnh | Có thể dùng | Có |
| Required fields | Một phần | Mạnh | Có thể dùng | Có |
| Validation semantics cụ thể | Mạnh | Yếu-vừa | Có thể đoán | Yếu |
| Exact negative meaning (`409`, duplicate, validation shape) | Mạnh | Có thể thiếu | Có thể đoán sai | Yếu |
| Success body semantics | Mạnh | Vừa | Có thể đoán | Yếu |
| Security assertions | Mạnh | Yếu | Có thể bỏ sót | Yếu |
| Boundary cases nghiệp vụ | Mạnh | Yếu | Có thể bỏ sót | Yếu |
| Guaranteed authoritative expected | Chưa | Chưa | Không | Không |

Kết luận:
- SRS là nguồn giàu ngữ nghĩa nhất.
- Nhưng hệ thống hiện tại vẫn thiếu lớp **Expectation Oracle Resolution** để biến SRS thành expected authoritative.

---

## 7. Root causes

## 7.1 Root cause cấp thiết kế
SRS hiện được dùng như **input cho model**, không phải **policy layer**.

## 7.2 Root cause cấp implementation
1. `BuildExpectedStatuses(...)` chưa hỏi SRS trước.  
2. `RepairAssertionsFromSchema(...)` chỉ biết Swagger.  
3. `TestCaseExpectationBuilder` không reconcile nguồn assertion.  
4. Rule-based generator không có đường vào SRS oracle.  
5. Khi n8n timeout, local fallback không có SRS-aware expectation resolution.

## 7.3 Root cause cấp vận hành
Runtime environment đang chạy sai target base URL, làm nhiễu kết quả verify.

---

## 8. Canonical target workflow (to-be)

Đây là workflow nên được xem là **chuẩn duy nhất** để AI Agent khác implement.

## 8.1 Principle

**LLM chỉ nên sinh scenario/request shape.**  
**Expected assertion phải đi qua một tầng resolver/oracle riêng.**

Resolver này phải có thứ tự ưu tiên:

1. SRS reviewed constraints  
2. Swagger/OpenAPI response metadata  
3. LLM suggestion  
4. Minimal fallback

## 8.2 Proposed canonical flow

### Step 1 — Normalize SRS thành structured constraints
Input:
- `SrsDocument`
- `SrsRequirement`
- `RefinedConstraints` hoặc `TestableConstraints`

Output mong muốn:
- một model structured, ví dụ:
  - requirementCode
  - endpointId hoặc endpoint matcher
  - testType (`HappyPath` / `Boundary` / `Negative`)
  - expectedStatusCodes
  - bodyContains
  - bodyNotContains
  - jsonPathChecks
  - preconditions
  - confidence / reviewed flag

Rule:
- chỉ requirement đã reviewed và đủ dữ liệu mới được xem là authoritative.

### Step 2 — Build generation context như hiện tại
Giữ lại luồng hiện có:
- load Swagger metadata
- load SRS context
- gửi cho LLM khi LLM available

Nhưng nhấn mạnh:
- việc đưa SRS vào payload **không còn là lớp quyết định cuối cùng**.
- nó chỉ giúp LLM sinh scenario hợp lý hơn.

### Step 3 — Generate scenario/request shape
LLM hoặc local fallback có thể sinh:
- scenario name
- request body
- path/query params
- sequence/dependencies
- testType

Nhưng expected assertions từ LLM chỉ được xem là **candidate**, không phải final truth.

### Step 4 — Resolve authoritative expectation
Thêm một tầng service riêng, ví dụ:
- `ISrsExpectationOracle`
- `IExpectationResolver`

Pseudo policy:

```text
for each generated scenario:
  1. find matching reviewed SRS constraint
  2. if found and complete -> use SRS expectation
  3. else use Swagger-derived expectation
  4. else use LLM expectation
  5. else use minimal fallback
```

### Step 5 — Apply same resolver cho local fallback
Đây là điểm rất quan trọng.

Khi n8n timeout:
- vẫn phải generate scenario coverage bằng local logic
- nhưng expected của từng scenario vẫn phải đi qua resolver ở Step 4
- như vậy local fallback vẫn có thể sinh expected bám SRS

### Step 6 — Apply same resolver cho rule-based mutation
`BoundaryNegativeTestCaseGenerator` cũng phải dùng cùng resolver.

Không được để rule-based path/body mutation có expected riêng biệt, tách khỏi canonical policy.

### Step 7 — Materialize final expectation with source tracking
Trước khi lưu `TestCaseExpectation`, phải có metadata nguồn:
- `ExpectationSource = Srs | Swagger | Llm | Default`
- optional: `PrimaryRequirementId` / `RequirementCode`

Mục tiêu:
- reviewer biết assertion đến từ đâu
- runtime log/debug biết vì sao case pass/fail
- hỗ trợ traceability

### Step 8 — Execute bằng stored expectation
`RuleBasedValidator` và `TestExecutionOrchestrator` tiếp tục dùng `TestCaseExpectation` đã materialize.

Không nên dồn logic oracle vào runtime validator nếu có thể tránh; nên resolve sớm ở generation/materialization để:
- dễ review
- dễ audit
- dễ cache
- dễ debug

---

## 9. Implementation seams nên sửa

## 9.1 [LlmScenarioSuggester.cs](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs)

Các điểm chèn chính:

1. **`BuildSrsContext()`**  
   - giữ lại vai trò đóng gói SRS vào prompt/payload
   - không xem đây là nơi enforce expected cuối

2. **`BuildExpectedStatuses()`**  
   - hiện đang ưu tiên LLM + Swagger + default  
   - cần nâng cấp thành gọi resolver/oracle trước

3. **`RepairAssertionsFromSchema()`**  
   - hiện chỉ repair từ Swagger schema  
   - cần hoặc:
     - gọi resolver trước,
     - hoặc chỉ dùng khi SRS không có assertion phù hợp

4. **nhánh local fallback sau `N8nTransientException`**  
   - không được chỉ local-generate scenario rồi gắn expected generic  
   - phải đưa tất cả scenario qua cùng expectation resolver

## 9.2 [TestCaseExpectationBuilder.cs](ClassifiedAds.Modules.TestGeneration/Services/TestCaseExpectationBuilder.cs#L28-L52)

Builder này nên được nâng cấp thành bước materialization cuối cùng sau reconcile.

Cần tránh mô hình hiện tại:
- “source thế nào thì serialize thế ấy”

Nên thành:
- nhận `ResolvedExpectation`
- serialize final expectation đã chuẩn hóa
- lưu metadata `ExpectationSource`

## 9.3 [BoundaryNegativeTestCaseGenerator.cs](ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs#L257-L312)

Hiện tại đang set thẳng:
- `ExpectedStatus = mutation.GetEffectiveExpectedStatusCodes()`

Cần đổi thành:
- generate request mutation như hiện tại
- gọi shared expectation resolver
- chỉ khi resolver không match mới dùng mutation defaults

## 9.4 [TestGenerationPayloadBuilder.cs](ClassifiedAds.Modules.TestGeneration/Services/TestGenerationPayloadBuilder.cs#L270-L298)

File này hiện làm tốt việc đưa SRS requirements vào payload.

Điểm cần ghi rõ cho AI Agent:
- **không cần thay đổi lớn file này**
- vấn đề không phải “payload chưa có SRS”
- vấn đề là **downstream chưa enforce SRS**

---

## 10. Đề xuất design ở mức interface/model

## 10.1 Service đề xuất

```csharp
public interface IExpectationResolver
{
    ResolvedExpectation Resolve(GeneratedScenarioContext scenarioContext);
}
```

## 10.2 Model đề xuất

```csharp
public sealed class ResolvedExpectation
{
    public IReadOnlyList<int> ExpectedStatusCodes { get; init; }
    public IReadOnlyList<string> BodyContains { get; init; }
    public IReadOnlyList<string> BodyNotContains { get; init; }
    public IReadOnlyDictionary<string, string> JsonPathChecks { get; init; }
    public ExpectationSource Source { get; init; }
    public Guid? PrimaryRequirementId { get; init; }
    public string RequirementCode { get; init; }
}

public enum ExpectationSource
{
    Srs,
    Swagger,
    Llm,
    Default
}
```

## 10.3 Structured SRS constraint shape đề xuất

```json
{
  "requirementCode": "TC-AUTH-REG-003",
  "endpointKey": "POST /api/auth/register",
  "testType": "Boundary",
  "expectedStatusCodes": [400],
  "bodyContains": ["Validation failed", "password"],
  "bodyNotContains": [],
  "jsonPathChecks": {
    "$.success": "false",
    "$.errors.fieldErrors.password": "*"
  },
  "preconditions": [],
  "priority": "High"
}
```

Lưu ý:
- đây là **shape gợi ý** để AI Agent implement theo.
- không nên để SRS free-text đi thẳng vào `TestCaseExpectation`.

---

## 11. Quy tắc mapping canonical

## 11.1 Khi nào dùng SRS?
Chỉ dùng SRS làm authoritative expected nếu:
- requirement đã reviewed
- constraint map được vào endpoint/scenario hiện tại
- constraint có đủ ít nhất:
  - `expectedStatusCodes`
  - và tối thiểu 1 assertion (`bodyContains`, `bodyNotContains`, hoặc `jsonPathChecks`)

## 11.2 Khi nào rơi về Swagger?
Nếu SRS không đủ hoặc không match:
- lấy response codes và schema từ Swagger
- derive `bodyContains` / `jsonPathChecks` từ schema/example

## 11.3 Khi nào mới tin LLM?
Chỉ dùng LLM expectation nếu:
- SRS không match
- Swagger không đủ dữ liệu
- hoặc cần bổ sung assertion phụ nhưng không được mâu thuẫn với SRS/Swagger

## 11.4 Minimal fallback cuối cùng
Chỉ dùng default hardcode khi:
- không có SRS
- không có Swagger response metadata phù hợp
- không có LLM expectation hữu dụng

---

## 12. Những gì AI Agent KHÔNG nên làm

1. **Không thay toàn bộ generation pipeline.**  
   Chỉ cần thêm lớp expectation normalization/resolution đúng seam.

2. **Không tin tuyệt đối vào prompt rule.**  
   Prompt instruction không thay thế được post-generation policy.

3. **Không để local fallback bỏ qua SRS.**  
   Đây là bug hành vi lớn nhất trong case log hiện tại.

4. **Không trộn bug environment với bug oracle.**  
   Sai base URL phải được sửa riêng, không coi là bằng chứng rằng SRS mapping sai.

5. **Không hardcode thêm default statuses mới** nếu có thể derive từ SRS/Swagger.

---

## 13. Verification plan cho AI Agent

## 13.1 Unit tests

### Resolver priority
- case có SRS reviewed + complete constraint -> chọn `ExpectationSource.Srs`
- case không có SRS match -> chọn `ExpectationSource.Swagger`
- case Swagger thiếu -> rơi về `ExpectationSource.Llm`
- case không còn gì -> `ExpectationSource.Default`

### Mapping correctness
- duplicate email / duplicate category phải resolve đúng status conflict từ SRS nếu SRS nói rõ
- password `< 6` phải resolve đúng validation expected
- happy path register phải assert không có `password` trong response nếu SRS yêu cầu

### Rule-based generation
- path/body mutation vẫn phải dùng resolver chung
- không bị bỏ qua SRS chỉ vì không đi qua LLM

## 13.2 Integration tests

### LLM available
- generate preview với suite có SRS
- approve → materialize test cases
- kiểm tra stored `TestCaseExpectation` mang assertion theo SRS nếu có match

### LLM unavailable / timeout
- mock hoặc force `N8nTransientException`
- hệ thống vẫn generate scenario local
- expected assertions của scenario local vẫn bám SRS thay vì generic defaults

### Swagger-only fallback
- suite không có SRS
- expectation phải được derive từ Swagger chứ không rơi ngay về hardcode

## 13.3 Runtime/log validation

Nên log thêm:
- `ExpectationSource`
- `RequirementCode` nếu source là SRS
- reason khi fallback từ SRS -> Swagger -> LLM -> Default

Mục tiêu:
- nhìn log là biết vì sao assertion hiện tại được chọn.

---

## 14. Separate issue: environment/base URL

AI Agent nên ghi nhận riêng bug này trong implementation hoặc follow-up:

- SRS chỉ ra base URL local: [TEST_REQUIREMENTS (1).md:2-4](TEST_REQUIREMENTS%20(1).md#L2-L4)
- setup yêu cầu health check local: [TEST_REQUIREMENTS (1).md:62-71](TEST_REQUIREMENTS%20(1).md#L62-L71)
- nhưng runtime đang gọi `petstore.swagger.io`: [test_api_doc.log:273-275](test_api_doc.log#L273-L275)

Đây là lỗi cấu hình execution target.  
Nó **không phủ nhận** bug SRS-first oracle, nhưng sẽ làm việc verify implementation rất nhiễu nếu không sửa hoặc cô lập khi test.

---

## 15. Final conclusion

Phán đoán của người dùng là đúng:

- **Hiện tại SRS chưa được dùng làm base expected/oracle theo nghĩa authoritative.**
- SRS đang tồn tại ở vai trò **advisory context cho LLM**.
- Khi LLM fail hoặc timeout, expected càng lộ rõ xu hướng quay về Swagger/default/local heuristics.

Nếu muốn AI Agent implement lại cho chuẩn, hướng đúng nhất là:

1. giữ luồng generation hiện có ở mức tối đa  
2. thêm một **canonical expectation resolver**  
3. cho resolver đó áp dụng thống nhất cho:
   - LLM-generated scenarios
   - local fallback scenarios
   - rule-based mutation scenarios  
4. materialize `TestCaseExpectation` sau khi đã resolve theo thứ tự:  
   **SRS > Swagger > LLM > Default**

---

## 16. Minimal implementation checklist for the next AI Agent

- [ ] Tạo `ExpectationSource` và `ResolvedExpectation`
- [ ] Tạo shared `IExpectationResolver`
- [ ] Parse/normalize SRS constraints thành shape có cấu trúc
- [ ] Hook resolver vào `LlmScenarioSuggester`
- [ ] Hook resolver vào local fallback path
- [ ] Hook resolver vào `BoundaryNegativeTestCaseGenerator`
- [ ] Nâng cấp `TestCaseExpectationBuilder` để materialize resolved expectation
- [ ] Thêm unit tests cho priority SRS > Swagger > LLM > Default
- [ ] Thêm integration test cho `N8nTransientException` fallback
- [ ] Tách verify bug base URL khỏi verify bug expected oracle
