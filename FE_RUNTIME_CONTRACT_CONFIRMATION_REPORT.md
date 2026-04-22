# FE Runtime Contract Confirmation Report

Tài liệu này tổng hợp trạng thái xác nhận contract runtime từ backend để FE có thể nối API sớm nhất có thể.

- Mục tiêu: biết field nào đã chốt, field nào còn cần confirm thêm.
- Nguồn tham chiếu: backend controller, DTO/model thực tế, và các report nội bộ hiện có trong workspace.

---

## 1. Tổng quan trạng thái

### Đã xác nhận đủ để FE bắt đầu nối

- `POST /api/test-suites/{suiteId}/llm-suggestions/generate`
  - Response là **wrapper object**, không phải array thuần.
  - Status code: `201 Created`.
  - FE phải đọc `suggestions[]` từ object cha.

- `POST /api/test-suites/{suiteId}/test-runs`
  - Status code: `201 Created`.
  - Request body chỉ có: `environmentId`, `selectedTestCaseIds`, `strictValidation`.

- `POST /api/test-suites/{suiteId}/test-cases`
  - Request body có thể chứa business/context ở cấp testcase thông qua `Request`, `Expectation`, `Variables`.
  - FE không truyền business rule vào `test run`; nếu đang tạo testcase thì business context phải đi cùng testcase payload.

- `GET /api/test-suites/{suiteId}/test-runs/{runId}/results`
  - Response có các field chính: `run`, `resultsSource`, `executedAt`, `resolvedEnvironmentName`, `cases`.

- `GET/POST /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
  - Endpoint tồn tại.
  - FE có thể gọi để lấy giải thích lỗi khi cần.

- `POST /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download`
  - Trả file binary/blob.

### Đã BE confirm được thêm

- `TestSuiteScopeModel`
- `GenerateLlmSuggestionPreviewRequest`
- `GenerateLlmSuggestionPreviewResultModel`
- `FailureExplanationModel`
- `TestReportModel`
- `EndpointModel`
- `SpecificationDetailModel`
- `NotificationHub : Hub` (có `[Authorize]`)

### Còn cần confirm thêm nếu FE muốn hard-code 100%

- SignalR hub route
- SignalR event name
- SignalR payload shape
- Generation job status DTO / enum values
- Runtime casing nếu client serializer không dùng camelCase chuẩn

---

## 2. Contract đã chốt

### 2.1 Generate LLM suggestions

`POST /api/test-suites/{suiteId}/llm-suggestions/generate`

**Đã xác nhận:**

- `201 Created`
- Response là wrapper object
- FE phải đọc `suggestions[]`
- `reviewStatus` enum thực tế:
  - `Pending`
  - `Approved`
  - `Rejected`
  - `ModifiedAndApproved`
  - `Superseded`

**Field top-level đã thấy trong runtime response:**

- `testSuiteId`
- `totalSuggestions`
- `endpointsCovered`
- `llmModel`
- `llmTokensUsed`
- `fromCache`
- `generatedAt`
- `suggestions`

**Field suggestion item đã thấy trong runtime response:**

- `id`
- `testSuiteId`
- `endpointId`
- `cacheKey`
- `displayOrder`
- `suggestionType`
- `testType`
- `suggestedName`
- `suggestedDescription`
- `suggestedRequest`
- `suggestedExpectation`
- `suggestedVariables`
- `suggestedTags`
- `priority`
- `reviewStatus`
- `reviewedById`
- `reviewedAt`
- `reviewNotes`
- `modifiedContent`
- `appliedTestCaseId`
- `llmModel`
- `tokensUsed`
- `isDeleted`
- `deletedAt`
- `deletedById`
- `createdDateTime`
- `updatedDateTime`
- `rowVersion`
- `currentUserFeedback`
- `feedbackSummary`

### 2.2 Start test run

`POST /api/test-suites/{suiteId}/test-runs`

**Request body runtime:**

```json
{
  "environmentId": "f6c9b7d0-4a6b-4b54-8f9f-6f1b2c0dd101",
  "selectedTestCaseIds": ["..."],
  "strictValidation": true
}
```

**BE model thực tế:**

- `environmentId`: `Guid?`
- `selectedTestCaseIds`: `List<Guid>`
- `strictValidation`: `bool`

**FE note:**

- FE nên coi `environmentId` là required.
- FE nên luôn gửi `strictValidation`.
- FE nên gửi `selectedTestCaseIds` rõ ràng khi chọn subset.
- Không đưa business rule vào request test run.
- Khi tạo testcase, business context/rule phải đi cùng `AddTestCaseRequest`.

### 2.3 Read test run results

`GET /api/test-suites/{suiteId}/test-runs/{runId}/results`

**Confirmed response fields:**

- `run`
- `resultsSource`
- `executedAt`
- `resolvedEnvironmentName`
- `cases`

**Expected `resultsSource` values:**

- `live`
- `cached`
- `unavailable`

**Case item fields đã xác nhận trong backend model:**

- `testCaseId`
- `endpointId`
- `name`
- `testType`
- `orderIndex`
- `status`
- `httpStatusCode`
- `durationMs`
- `resolvedUrl`
- `httpMethod`
- `bodyType`
- `requestBody`
- `queryParams`
- `timeoutMs`
- `expectedStatus`
- `requestHeaders`
- `responseHeaders`
- `responseBodyPreview`
- `failureReasons`
- `warnings`
- `checksPerformed`
- `checksSkipped`
- `extractedVariables`
- `dependencyIds`
- `skippedBecauseDependencyIds`
- `statusCodeMatched`
- `schemaMatched`
- `headerChecksPassed`
- `bodyContainsPassed`
- `bodyNotContainsPassed`
- `jsonPathChecksPassed`
- `responseTimePassed`

### 2.4 Failure explanation

`GET /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`

`POST /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`

**Đã xác nhận:**

- Endpoint tồn tại.
- Response type là `FailureExplanationModel`.
- FE có thể dùng để hiển thị giải thích nguyên nhân fail.

**BE confirm:**

- `testSuiteId`
- `testRunId`
- `testCaseId`
- `endpointId`
- `summaryVi`
- `possibleCauses`
- `suggestedNextActions`
- `confidence`
- `source`
- `provider`
- `model`
- `tokensUsed`
- `latencyMs`
- `generatedAt`
- `failureCodes`

### 2.5 Report download

`POST /api/test-suites/{suiteId}/test-runs/{runId}/reports`

`GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download`

**Đã xác nhận:**

- Download endpoint trả binary/blob.
- FE có thể xử lý như file download bình thường.

**BE confirm:**

- `id`
- `testSuiteId`
- `testRunId`
- `reportType`
- `format`
- `downloadUrl`
- `generatedAt`
- `expiresAt`
- `coverage`

**Note:**

- `downloadUrl` có dạng `/api/test-suites/{testSuiteId}/test-runs/{testRunId}/reports/{reportId}/download`.

---

## 3. Các endpoint còn cần confirm chi tiết field

### 3.1 Test suite creation

`POST /api/projects/{projectId}/test-suites`

**Đã biết:**

- Request body: `CreateTestSuiteScopeRequest`
- Response type: `TestSuiteScopeModel`
- Status code: `201 Created`

**BE request body runtime sample:**

```json
{
  "name": "Suite name",
  "description": "Optional description",
  "apiSpecId": "7b3b2d0f-1f22-4b3f-a90f-1a4b8fd6f9d2",
  "generationType": "Auto",
  "selectedEndpointIds": ["..."],
  "endpointBusinessContexts": {
    "endpoint-id": "Business context text"
  },
  "globalBusinessRules": "Global rules text"
}
```

**BE confirm:**

- `id`
- `projectId`
- `apiSpecId`
- `name`
- `description`
- `generationType`
- `status`
- `approvalStatus`
- `selectedEndpointIds`
- `endpointBusinessContexts` (key là `Guid`, value là `string`)
- `globalBusinessRules`
- `selectedEndpointCount`
- `testCaseCount`
- `createdById`
- `createdDateTime`
- `updatedDateTime`
- `rowVersion` (base64 string)

**Note:**

- `rowVersion` trong response là base64 string.
- `SelectedEndpointIds` và `EndpointBusinessContexts` được giữ nguyên dưới dạng collection.
- Không có business rule config riêng trong testcase; business rule thuộc suite scope (`TestSuiteScopeModel`), không phải testcase payload.

### 3.2 Specifications and endpoints

- `GET /api/projects/{projectId}/specifications/{specId}`
- `GET /api/projects/{projectId}/specifications/{specId}/endpoints`

**BE confirm:**

`SpecificationDetailModel` kế thừa `SpecificationModel` và có thêm:

- `endpointCount`
- `parseErrors`
- `originalFileName`

`EndpointModel` có các field:

- `id`
- `apiSpecId`
- `httpMethod`
- `path`
- `operationId`
- `summary`
- `description`
- `tags`
- `isDeprecated`
- `createdDateTime`
- `updatedDateTime`

**Lưu ý:**

- Nếu FE cần object detail đầy đủ, backend còn có `EndpointDetailModel` với:
  - `resolvedUrl`
  - `parameters`
  - `responses`
  - `securityRequirements`

### 3.3 LLM suggestion review flow

- `GET /api/test-suites/{suiteId}/llm-suggestions`
- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review`
- `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-review`

**Đã biết:**

- Query params của list endpoint gồm:
  - `reviewStatus`
  - `testType`
  - `endpointId`
  - `includeDeleted`
- `PUT review` có thể trả `409 Conflict` nếu `rowVersion` stale
- `rowVersion` phải được FE gửi lên khi review

**Cần confirm:**

- Full request/response DTO của bulk review
- Exact fields của review request nếu FE muốn map form 1-1

### 3.4 Generation flow

- `POST /api/test-suites/{suiteId}/generate-tests`
- `GET /api/test-suites/{suiteId}/generation-status?jobId=...`

**Đã biết:**

- `POST` trả `202 Accepted`
- FE phải poll status bằng `jobId`

**Note:**

- Report hiện tại chưa tìm thấy DTO/status model public riêng cho job status.
- Nếu cần chốt thêm, nên map trực tiếp theo payload runtime từ endpoint status.
### 3.5 SignalR

**BE confirm:**

- Backend có `NotificationHub : Hub`
- Hub được `[Authorize]`
- Route/event/payload shape chưa thấy public trong model file hiện tại

**Cần confirm thêm nếu FE muốn subscribe trực tiếp:**

- Hub route
- Event name
- Payload shape
- Payload casing
- Connection/auth requirement theo implementation startup
---

## 4. FE safe-use rules

### Nên dùng ngay

- `suggestions[]` từ wrapper object của generate LLM suggestions
- `rowVersion` cho optimistic concurrency
- `resultsSource` và `cases[]` từ test run results
- Blob download cho report file

### Có thể hard-code được ngay

- `TestSuiteScopeModel` schema
- `GenerateLlmSuggestionPreviewRequest` schema
- `GenerateLlmSuggestionPreviewResultModel` schema
- `TestReportModel` schema
- `FailureExplanationModel` schema
- `EndpointModel` schema
- `SpecificationDetailModel` schema

### Chỉ còn phần runtime đặc thù chưa chốt

- SignalR payload schema
- SignalR hub route/event names
- Generation status response schema
---

## 5. Kết luận ngắn

Nếu mục tiêu là để FE nối API ngay, thì các luồng sau đã đủ để implement:

1. Generate suggestion
2. Review suggestion
3. Create test run
4. Poll/view test run results
5. Xem failure explanation
6. Download report file

Những phần còn lại chủ yếu là SignalR và generation status runtime; còn các model/DTO chính đã có thể map vào FE.

---

## 6. FE-ready contract reference

Phần này ghi theo dạng field list + TypeScript interface để FE copy dùng ngay.

### 6.1 `GenerateLlmSuggestionPreviewRequest`

```ts
export interface GenerateLlmSuggestionPreviewRequest {
  specificationId: string;
  forceRefresh: boolean;
  algorithmProfile: GenerationAlgorithmProfile;
}
```

### 6.2 `GenerateLlmSuggestionPreviewResultModel`

```ts
export interface GenerateLlmSuggestionPreviewResultModel {
  testSuiteId: string;
  totalSuggestions: number;
  endpointsCovered: number;
  llmModel: string;
  llmTokensUsed: number | null;
  fromCache: boolean;
  generatedAt: string;
  suggestions: LlmSuggestionModel[];
}
```

### 6.3 `LlmSuggestionModel`

```ts
export interface LlmSuggestionModel {
  id: string;
  testSuiteId: string;
  endpointId: string | null;
  cacheKey: string;
  displayOrder: number;
  suggestionType: string;
  testType: string;
  suggestedName: string;
  suggestedDescription: string;
  suggestedRequest: string;
  suggestedExpectation: string;
  suggestedVariables: SuggestionVariableModel[];
  suggestedTags: string[];
  priority: string;
  reviewStatus: string;
  reviewedById: string | null;
  reviewedAt: string | null;
  reviewNotes: string;
  modifiedContent: string;
  appliedTestCaseId: string | null;
  llmModel: string;
  tokensUsed: number | null;
  isDeleted: boolean;
  deletedAt: string | null;
  deletedById: string | null;
  createdDateTime: string;
  updatedDateTime: string | null;
  rowVersion: string | null;
  currentUserFeedback: LlmSuggestionFeedbackModel | null;
  feedbackSummary: LlmSuggestionFeedbackSummaryModel | null;
}
```

### 6.4 `SuggestionVariableModel`

```ts
export interface SuggestionVariableModel {
  variableName: string;
  extractFrom: string;
  jsonPath: string;
  headerName: string;
  regex: string;
  defaultValue: string;
}
```

### 6.5 `TestSuiteScopeModel`

```ts
export interface TestSuiteScopeModel {
  id: string;
  projectId: string;
  apiSpecId: string | null;
  name: string;
  description: string;
  generationType: string;
  status: string;
  approvalStatus: string;
  selectedEndpointIds: string[];
  endpointBusinessContexts: Record<string, string>;
  globalBusinessRules: string;
  selectedEndpointCount: number;
  testCaseCount: number;
  createdById: string;
  createdDateTime: string;
  updatedDateTime: string | null;
  rowVersion: string | null;
}
```

### 6.6 `FailureExplanationModel`

```ts
export interface FailureExplanationModel {
  testSuiteId: string;
  testRunId: string;
  testCaseId: string;
  endpointId: string | null;
  summaryVi: string;
  possibleCauses: string[];
  suggestedNextActions: string[];
  confidence: string;
  source: string;
  provider: string;
  model: string;
  tokensUsed: number;
  latencyMs: number;
  generatedAt: string;
  failureCodes: string[];
}
```

### 6.7 `EndpointModel`

```ts
export interface EndpointModel {
  id: string;
  apiSpecId: string;
  httpMethod: string;
  path: string;
  operationId: string;
  summary: string;
  description: string;
  tags: string;
  isDeprecated: boolean;
  createdDateTime: string;
  updatedDateTime: string | null;
}
```

### 6.8 `SpecificationDetailModel`

```ts
export interface SpecificationDetailModel {
  endpointCount: number;
  parseErrors: string[];
  originalFileName: string;
}
```

### 6.9 `TestReportModel`

```ts
export interface TestReportModel {
  id: string;
  testSuiteId: string;
  testRunId: string;
  reportType: string;
  format: string;
  downloadUrl: string;
  generatedAt: string;
  expiresAt: string | null;
  coverage: CoverageMetricModel | null;
}
```

### 6.10 Request payload `POST /api/test-runs`

```ts
export interface StartTestRunRequest {
  environmentId: string | null;
  selectedTestCaseIds: string[];
  strictValidation: boolean;
}
```

### 6.11 Quick param checklist

- `suiteId`: path, `Guid`
- `projectId`: path, `Guid`
- `runId`: path, `Guid`
- `reportId`: path, `Guid`
- `testCaseId`: path, `Guid`
- `specificationId`: body/path tùy endpoint
- `environmentId`: body, `Guid | null`
- `selectedTestCaseIds`: body, `Guid[]`
- `strictValidation`: body, `boolean`
- `forceRefresh`: body, `boolean`
- `algorithmProfile`: body, object
- `endpointId`: body, `Guid | null` khi tạo testcase
- `request`: body, object khi tạo testcase
- `expectation`: body, object khi tạo testcase
- `variables`: body, `TestCaseVariableInput[]` khi tạo testcase

### 6.12 Notes cho FE

- Các GUID trong request/response dùng string UUID format.
- Các `DateTimeOffset` nên parse theo ISO 8601 string.
- `rowVersion` luôn là base64 string khi đi qua API.
- Nếu FE muốn strict typing, hãy map enum phía client theo string runtime, không ép numeric enum.
- Không có business rule riêng trong testcase payload; rule/config nằm ở suite scope.
- Trong FE docs, ưu tiên dùng các thuật ngữ: `suite scope`, `test run`, `test case`, `report`, `failure explanation`.

### 6.13 Backend config / requirements để FE nối được liền

Mình đã kiểm tra workspace và **không thấy** file config chuẩn kiểu `requirements.txt` cho backend .NET. Dự án này dùng chuẩn .NET solution, nên FE nối được liền khi:

- Có backend API đang chạy
- Có connection string / env đúng cho DB và các service phụ thuộc
- Swagger/API docs hoặc runtime endpoint đã sẵn sàng
- Auth token/credential FE dùng khớp với backend `NotificationHub` và các endpoint có `[Authorize]`

**Các file config hiện có trong workspace cần chú ý:**

- `.env.render`
- các file appsettings/config trong project backend

**Khuyến nghị để FE nối ngay:**

- đảm bảo env/backend chạy với API base URL cố định
- bật CORS cho origin FE
- expose swagger hoặc export OpenAPI nếu FE muốn generate client
- cung cấp token mẫu hoặc auth flow chuẩn nếu endpoint/hub cần auth

### 6.14 JSON Schema tương ứng

#### `GenerateLlmSuggestionPreviewRequest`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "GenerateLlmSuggestionPreviewRequest.schema.json",
  "type": "object",
  "required": ["specificationId", "forceRefresh", "algorithmProfile"],
  "properties": {
    "specificationId": { "type": "string", "format": "uuid" },
    "forceRefresh": { "type": "boolean" },
    "algorithmProfile": { "type": "object" }
  },
  "additionalProperties": false
}
```

#### `GenerateLlmSuggestionPreviewResultModel`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "GenerateLlmSuggestionPreviewResultModel.schema.json",
  "type": "object",
  "required": ["testSuiteId", "totalSuggestions", "endpointsCovered", "llmModel", "fromCache", "generatedAt", "suggestions"],
  "properties": {
    "testSuiteId": { "type": "string", "format": "uuid" },
    "totalSuggestions": { "type": "integer" },
    "endpointsCovered": { "type": "integer" },
    "llmModel": { "type": "string" },
    "llmTokensUsed": { "type": ["integer", "null"] },
    "fromCache": { "type": "boolean" },
    "generatedAt": { "type": "string", "format": "date-time" },
    "suggestions": { "type": "array", "items": { "$ref": "#/definitions/LlmSuggestionModel" } }
  },
  "additionalProperties": false,
  "definitions": {
    "LlmSuggestionModel": {
      "type": "object",
      "required": ["id", "testSuiteId", "displayOrder", "suggestionType", "testType", "suggestedName", "suggestedDescription", "suggestedRequest", "suggestedExpectation", "suggestedVariables", "suggestedTags", "priority", "reviewStatus", "isDeleted", "createdDateTime"],
      "properties": {
        "id": { "type": "string", "format": "uuid" },
        "testSuiteId": { "type": "string", "format": "uuid" },
        "endpointId": { "type": ["string", "null"], "format": "uuid" },
        "cacheKey": { "type": "string" },
        "displayOrder": { "type": "integer" },
        "suggestionType": { "type": "string" },
        "testType": { "type": "string" },
        "suggestedName": { "type": "string" },
        "suggestedDescription": { "type": "string" },
        "suggestedRequest": { "type": "string" },
        "suggestedExpectation": { "type": "string" },
        "suggestedVariables": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "variableName": { "type": "string" },
              "extractFrom": { "type": "string" },
              "jsonPath": { "type": "string" },
              "headerName": { "type": "string" },
              "regex": { "type": "string" },
              "defaultValue": { "type": "string" }
            },
            "additionalProperties": false
          }
        },
        "suggestedTags": { "type": "array", "items": { "type": "string" } },
        "priority": { "type": "string" },
        "reviewStatus": { "type": "string" },
        "reviewedById": { "type": ["string", "null"], "format": "uuid" },
        "reviewedAt": { "type": ["string", "null"], "format": "date-time" },
        "reviewNotes": { "type": "string" },
        "modifiedContent": { "type": "string" },
        "appliedTestCaseId": { "type": ["string", "null"], "format": "uuid" },
        "llmModel": { "type": "string" },
        "tokensUsed": { "type": ["integer", "null"] },
        "isDeleted": { "type": "boolean" },
        "deletedAt": { "type": ["string", "null"], "format": "date-time" },
        "deletedById": { "type": ["string", "null"], "format": "uuid" },
        "createdDateTime": { "type": "string", "format": "date-time" },
        "updatedDateTime": { "type": ["string", "null"], "format": "date-time" },
        "rowVersion": { "type": ["string", "null"] },
        "currentUserFeedback": { "type": ["object", "null"] },
        "feedbackSummary": { "type": ["object", "null"] }
      },
      "additionalProperties": false
    }
  }
}
```

#### `TestSuiteScopeModel`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "TestSuiteScopeModel.schema.json",
  "type": "object",
  "required": ["id", "projectId", "name", "generationType", "status", "approvalStatus", "selectedEndpointIds", "endpointBusinessContexts", "selectedEndpointCount", "testCaseCount", "createdById", "createdDateTime"],
  "properties": {
    "id": { "type": "string", "format": "uuid" },
    "projectId": { "type": "string", "format": "uuid" },
    "apiSpecId": { "type": ["string", "null"], "format": "uuid" },
    "name": { "type": "string" },
    "description": { "type": "string" },
    "generationType": { "type": "string" },
    "status": { "type": "string" },
    "approvalStatus": { "type": "string" },
    "selectedEndpointIds": { "type": "array", "items": { "type": "string", "format": "uuid" } },
    "endpointBusinessContexts": { "type": "object", "additionalProperties": { "type": "string" } },
    "globalBusinessRules": { "type": "string" },
    "selectedEndpointCount": { "type": "integer" },
    "testCaseCount": { "type": "integer" },
    "createdById": { "type": "string", "format": "uuid" },
    "createdDateTime": { "type": "string", "format": "date-time" },
    "updatedDateTime": { "type": ["string", "null"], "format": "date-time" },
    "rowVersion": { "type": ["string", "null"] }
  },
  "additionalProperties": false
}
```

#### `StartTestRunRequest`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "StartTestRunRequest.schema.json",
  "type": "object",
  "required": ["environmentId", "selectedTestCaseIds", "strictValidation"],
  "properties": {
    "environmentId": { "type": ["string", "null"], "format": "uuid" },
    "selectedTestCaseIds": { "type": "array", "items": { "type": "string", "format": "uuid" } },
    "strictValidation": { "type": "boolean" }
  },
  "additionalProperties": false
}
```

**Có** business/context ở cấp testcase khi tạo `AddTestCaseRequest`.

### `AddTestCaseRequest` / `UpdateTestCaseRequest`

```ts
export interface AddTestCaseRequest {
  endpointId: string | null;
  name: string;
  description: string;
  testType: string;
  priority: string;
  isEnabled: boolean;
  tags: string[];
  request: TestCaseRequestInput;
  expectation: TestCaseExpectationInput;
  variables: TestCaseVariableInput[];
}
```

```ts
export interface TestCaseRequestInput {
  httpMethod: string;
  url: string;
  headers: string;
  pathParams: string;
  queryParams: string;
  bodyType: string;
  body: string;
  timeout: number;
}
```

```ts
export interface TestCaseExpectationInput {
  expectedStatus: string;
  responseSchema: string;
  headerChecks: string;
  bodyContains: string;
  bodyNotContains: string;
  jsonPathChecks: string;
  maxResponseTime: number | null;
}
```

```ts
export interface TestCaseVariableInput {
  variableName: string;
  extractFrom: string;
  jsonPath: string;
  headerName: string;
  regex: string;
  defaultValue: string;
}
```

**Terminology chuẩn cho FE:**

- dùng `suite scope` khi nói về config cấp test suite
- dùng `test case` khi nói về payload tạo/sửa testcase
- dùng `test run` khi nói về request chạy test
- không gắn business rule vào `test run`

#### `FailureExplanationModel`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "FailureExplanationModel.schema.json",
  "type": "object",
  "required": ["testSuiteId", "testRunId", "testCaseId", "summaryVi", "possibleCauses", "suggestedNextActions", "confidence", "source", "provider", "model", "tokensUsed", "latencyMs", "generatedAt", "failureCodes"],
  "properties": {
    "testSuiteId": { "type": "string", "format": "uuid" },
    "testRunId": { "type": "string", "format": "uuid" },
    "testCaseId": { "type": "string", "format": "uuid" },
    "endpointId": { "type": ["string", "null"], "format": "uuid" },
    "summaryVi": { "type": "string" },
    "possibleCauses": { "type": "array", "items": { "type": "string" } },
    "suggestedNextActions": { "type": "array", "items": { "type": "string" } },
    "confidence": { "type": "string" },
    "source": { "type": "string" },
    "provider": { "type": "string" },
    "model": { "type": "string" },
    "tokensUsed": { "type": "integer" },
    "latencyMs": { "type": "integer" },
    "generatedAt": { "type": "string", "format": "date-time" },
    "failureCodes": { "type": "array", "items": { "type": "string" } }
  },
  "additionalProperties": false
}
```

#### `TestReportModel`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "TestReportModel.schema.json",
  "type": "object",
  "required": ["id", "testSuiteId", "testRunId", "reportType", "format", "downloadUrl", "generatedAt"],
  "properties": {
    "id": { "type": "string", "format": "uuid" },
    "testSuiteId": { "type": "string", "format": "uuid" },
    "testRunId": { "type": "string", "format": "uuid" },
    "reportType": { "type": "string" },
    "format": { "type": "string" },
    "downloadUrl": { "type": "string" },
    "generatedAt": { "type": "string", "format": "date-time" },
    "expiresAt": { "type": ["string", "null"], "format": "date-time" },
    "coverage": { "type": ["object", "null"] }
  },
  "additionalProperties": false
}
```

#### `EndpointModel`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "EndpointModel.schema.json",
  "type": "object",
  "required": ["id", "apiSpecId", "httpMethod", "path", "operationId", "summary", "description", "tags", "isDeprecated", "createdDateTime"],
  "properties": {
    "id": { "type": "string", "format": "uuid" },
    "apiSpecId": { "type": "string", "format": "uuid" },
    "httpMethod": { "type": "string" },
    "path": { "type": "string" },
    "operationId": { "type": "string" },
    "summary": { "type": "string" },
    "description": { "type": "string" },
    "tags": { "type": "string" },
    "isDeprecated": { "type": "boolean" },
    "createdDateTime": { "type": "string", "format": "date-time" },
    "updatedDateTime": { "type": ["string", "null"], "format": "date-time" }
  },
  "additionalProperties": false
}
```

#### `SpecificationDetailModel`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "SpecificationDetailModel.schema.json",
  "type": "object",
  "required": ["endpointCount", "parseErrors", "originalFileName"],
  "properties": {
    "endpointCount": { "type": "integer" },
    "parseErrors": { "type": "array", "items": { "type": "string" } },
    "originalFileName": { "type": "string" }
  },
  "additionalProperties": true
}
```

#### `AddTestCaseRequest`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "AddTestCaseRequest.schema.json",
  "type": "object",
  "required": ["name", "testType", "priority", "isEnabled", "request", "expectation"],
  "properties": {
    "endpointId": { "type": ["string", "null"], "format": "uuid" },
    "name": { "type": "string" },
    "description": { "type": "string" },
    "testType": { "type": "string" },
    "priority": { "type": "string" },
    "isEnabled": { "type": "boolean" },
    "tags": { "type": "array", "items": { "type": "string" } },
    "request": { "$ref": "#/definitions/TestCaseRequestInput" },
    "expectation": { "$ref": "#/definitions/TestCaseExpectationInput" },
    "variables": { "type": "array", "items": { "$ref": "#/definitions/TestCaseVariableInput" } }
  },
  "additionalProperties": false,
  "definitions": {
    "TestCaseRequestInput": {
      "type": "object",
      "required": ["httpMethod", "url", "bodyType", "timeout"],
      "properties": {
        "httpMethod": { "type": "string" },
        "url": { "type": "string" },
        "headers": { "type": "string" },
        "pathParams": { "type": "string" },
        "queryParams": { "type": "string" },
        "bodyType": { "type": "string" },
        "body": { "type": "string" },
        "timeout": { "type": "integer" }
      },
      "additionalProperties": false
    },
    "TestCaseExpectationInput": {
      "type": "object",
      "properties": {
        "expectedStatus": { "type": "string" },
        "responseSchema": { "type": "string" },
        "headerChecks": { "type": "string" },
        "bodyContains": { "type": "string" },
        "bodyNotContains": { "type": "string" },
        "jsonPathChecks": { "type": "string" },
        "maxResponseTime": { "type": ["integer", "null"] }
      },
      "additionalProperties": false
    },
    "TestCaseVariableInput": {
      "type": "object",
      "required": ["variableName", "extractFrom"],
      "properties": {
        "variableName": { "type": "string" },
        "extractFrom": { "type": "string" },
        "jsonPath": { "type": "string" },
        "headerName": { "type": "string" },
        "regex": { "type": "string" },
        "defaultValue": { "type": "string" }
      },
      "additionalProperties": false
    }
  }
}
```
