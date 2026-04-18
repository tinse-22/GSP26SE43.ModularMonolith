# FE/BE Auto Test + LLM Integration Guide

## 1. Scope

Tài liệu này là source-of-truth cho Frontend khi tích hợp toàn bộ luồng Auto Test với LLM trong repo hiện tại, bao gồm:

- Project
- API Specification
- Endpoint curation / endpoint helper APIs
- Test Suite scope
- API order proposal / review gate
- Unified test generation callback flow
- Legacy sync generation branch
- LLM suggestion preview / review flow
- Manual test-case CRUD
- Execution environment
- Test run execution
- Run result retrieval
- LLM failure explanation

Nguyên tắc biên soạn:

- Chỉ ghi những gì đã xác minh trực tiếp từ source code.
- Không tự bịa field, status, workflow, hay async behavior.
- Khi runtime behavior phụ thuộc config, tài liệu sẽ ghi rõ default đang được check-in trong repo và phần nào có thể thay đổi theo môi trường.

## 2. Current Checked-In Default Behavior

Những điểm dưới đây là behavior mặc định theo source hiện tại trong repo:

- `Modules:TestGeneration:N8nIntegration:UseDotnetIntegrationWorkflowForGeneration = true` trong `ClassifiedAds.WebAPI` và `ClassifiedAds.Background`.
- Điều đó có nghĩa là trong môi trường mặc định đang check-in:
  - `POST /api/test-suites/{suiteId}/generate-tests`
  - `POST /api/test-suites/{suiteId}/test-cases/generate-happy-path`
  - `POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative`
  đều đi vào cùng một callback-based workflow và trả về `202 Accepted` thay vì `201 Created` sync result.
- Rất quan trọng: khi unified mode đang bật, body của `generate-happy-path` và `generate-boundary-negative` vẫn được bind ở controller nhưng không còn quyết định runtime behavior hiện tại. Controller chỉ queue chung `GenerateTestCasesCommand` bằng `suiteId` và `currentUserId`.
- `POST /api/test-suites/{suiteId}/test-runs` chạy thực thi test đồng bộ và trả về `201 Created` với `TestRunResultModel` ngay sau khi run xong. Không có polling API cho một live run đang chạy.
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/results` chỉ đọc cached detailed results từ Redis. Nếu cache hết hạn, mất cache, Redis read fail, hoặc payload cache không dùng được thì trả `ResultsSource = "unavailable"` và `Cases = []`.
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/results` không tự reconstruct detailed cases từ PostgreSQL. Internal service cho reporting/explanation có fallback PostgreSQL, nhưng public FE endpoint `GET /results` thì không.
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation` chỉ đọc cache explanation. Nếu chưa có cache thì `404`.
- `POST /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation` mới gọi LLM để generate explanation live nếu cần.
- Failure explanation default đang check-in:
  - `Provider = "N8n"`
  - `CacheTtlHours = 24`

## 3. Auth And Error Contract

### 3.1 Auth

- Gần như toàn bộ FE-facing endpoints đều yêu cầu JWT (`[Authorize]`).
- Endpoint callback từ n8n là ngoại lệ duy nhất trong flow chính:
  - `POST /api/test-suites/{suiteId}/test-cases/from-ai`
  - Không dùng JWT.
  - Bảo vệ bằng header `x-callback-api-key`.

### 3.2 Standard Error Envelopes

#### A. Exception-driven errors

Content-Type:

```json
application/problem+json
```

Shape:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "...",
  "message": "...",
  "traceId": "...",
  "reasonCode": "..."
}
```

Ghi chú:

- `message` luôn có cho các lỗi được `GlobalExceptionHandler` xử lý.
- `traceId` luôn có.
- `reasonCode` chỉ có ở một số lỗi conflict / transient / concurrency.
- 503 transient database error có thêm header `Retry-After: 2` và `reasonCode = "TRANSIENT_NPGSQL_CONNECTION"`.

#### B. Model validation errors

Content-Type:

```json
application/problem+json
```

Shape:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/...",
  "traceId": "...",
  "errors": {
    "fieldName": [
      "error 1"
    ]
  }
}
```

Ghi chú:

- ValidationProblemDetails không có `message` top-level như nhánh exception-driven.
- FE phải support cả 2 shape 400 khác nhau.

## 4. Entity Dependency Chain

| Entity | Producer | Consumed By | Critical Fields |
| --- | --- | --- | --- |
| Project | ProjectsController | Specifications, ExecutionEnvironments, TestSuites | `id`, `status`, `activeSpecId`, `baseUrl` |
| Specification | SpecificationsController | Endpoints, TestSuite scope | `id`, `projectId`, `sourceType`, `parseStatus`, `isActive` |
| Endpoint | EndpointsController / spec parser | Test order, test generation, execution metadata | `id`, `apiSpecId`, `httpMethod`, `path`, `operationId` |
| TestSuite | TestSuitesController | Order proposal, suggestions, test cases, runs | `id`, `projectId`, `apiSpecId`, `selectedEndpointIds`, `endpointBusinessContexts`, `globalBusinessRules`, `status`, `approvalStatus`, `rowVersion` |
| TestOrderProposal | TestOrderController | Generation gate, payload builder | `proposalId`, `testSuiteId`, `status`, `source`, `appliedOrder`, `rowVersion` |
| TestGenerationJob | GenerateTestCasesCommand | FE polling via `generation-status` | `jobId`, `testSuiteId`, `status`, `queuedAt`, `triggeredAt`, `completedAt`, `webhookName`, `errorMessage` |
| TestCase | Sync generators / suggestion review / manual CRUD / AI callback | Execution | `id`, `testSuiteId`, `endpointId`, `testType`, `priority`, `isEnabled`, `orderIndex`, `dependsOnIds` |
| LlmSuggestion | GenerateLlmSuggestionPreviewCommand | Suggestion review, feedback, bulk review | `id`, `testSuiteId`, `endpointId`, `reviewStatus`, `rowVersion`, `appliedTestCaseId` |
| ExecutionEnvironment | ExecutionEnvironmentsController | StartTestRun | `id`, `projectId`, `baseUrl`, `variables`, `headers`, `authConfig`, `isDefault`, `rowVersion` |
| TestRun | StartTestRunCommand | Run history, cached results, failure explanation | `id`, `testSuiteId`, `environmentId`, `status`, `resultsExpireAt`, `hasDetailedResults` |
| FailureExplanation | ExplainTestFailureCommand | FE failure analysis screen | `testSuiteId`, `testRunId`, `testCaseId`, `source`, `summaryVi` |

## 5. End-To-End Workflow A-Z

### 5.1 Project And Specification Foundation

1. FE tạo Project.
2. FE có thể update / archive / unarchive / delete project sau đó.
3. FE upload/import/create Specification.
4. FE theo dõi `parseStatus` nếu source upload không parse inline.
5. FE activate Specification cần dùng.
6. FE lấy danh sách Endpoint của spec active hoặc spec được chọn.

Blocking rules:

- Test suite cần `projectId`.
- Test suite auto flow cần `apiSpecId`.
- Order proposal cần endpoints thuộc đúng `apiSpecId`.

### 5.2 Specification Parse Modes

`POST /api/projects/{projectId}/specifications/upload`

- Nếu `sourceType = OpenAPI` và file extension là `.json`:
  - Backend parse inline trong request.
  - Response có thể ra `ParseStatus = Success` hoặc `Failed` ngay.
- Nếu là OpenAPI YAML hoặc Postman:
  - Response create spec với `ParseStatus = Pending`.
  - Parse thật chạy async qua outbox + background worker + `ParseUploadedSpecificationCommand`.
  - FE phải poll `GET /api/projects/{projectId}/specifications/{specId}` hoặc list specs để đợi `Pending -> Success/Failed`.

`POST /manual` và `POST /curl-import`

- Persist đồng bộ.
- `ParseStatus = Success` ngay.
- Không cần polling parse status.

### 5.3 Endpoint Curation Before Suite Creation

- FE có thể manually create / update / delete endpoint dưới một specification đã có.
- `GET /resolved-url` là helper API để resolve URL thật bằng dynamic path/query values FE truyền ở query string.
- `GET /path-param-mutations` là helper API cho boundary/path-param UX.
- Nếu FE cho phép user chỉnh endpoint sau parse, chính endpoint rows hiện tại trong DB sẽ là dữ liệu downstream mà order proposal, generation, execution metadata đọc vào.

### 5.4 Test Suite Shape And Ownership

1. FE tạo test suite với `name`, `apiSpecId`, `generationType`, `selectedEndpointIds`.
2. FE có thể gửi thêm:
   - `endpointBusinessContexts`
   - `globalBusinessRules`
3. FE update suite phải gửi `rowVersion`.
4. FE archive suite bằng `DELETE /api/projects/{projectId}/test-suites/{suiteId}?rowVersion=...`.

Blocking rules:

- Chỉ owner của suite mới update / generate / run / review suggestion trên suite đó.
- `selectedEndpointIds` phải thuộc đúng spec mà suite đang dùng.
- Generation và execution downstream đọc state suite hiện tại trong DB, không đọc local FE draft.

### 5.5 Test Suite And Order Gate

1. FE tạo test suite với `selectedEndpointIds`.
2. FE propose API order.
3. FE lấy latest proposal.
4. FE cho user reorder / approve / reject.
5. Chỉ khi gate pass (`IsGatePassed = true`) thì generation hoặc suggestion preview mới chạy được.

Gate conditions đã xác minh:

- Phải có active proposal.
- Proposal phải ở trạng thái approved / modified-and-approved.
- Applied order không được rỗng.

### 5.6 Current Default Unified Generation Flow

Trong config hiện tại của repo, 3 entrypoint sau đều queue cùng một workflow:

- `POST /api/test-suites/{suiteId}/generate-tests`
- `POST /api/test-suites/{suiteId}/test-cases/generate-happy-path`
- `POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative`

Actual behavior khi flag unified đang bật:

1. API tạo `TestGenerationJob` với `Status = Queued`.
2. Background consumer đổi sang `Triggering`.
3. Background build payload từ approved order, endpoint metadata, prompts, callback URL, callback API key.
4. n8n được trigger qua webhook `generate-test-cases-unified`.
5. Nếu trigger thành công, job sang `WaitingForCallback`.
6. n8n POST kết quả về `POST /api/test-suites/{suiteId}/test-cases/from-ai`.
7. Backend replace toàn bộ test cases hiện có của suite bằng bộ AI-generated mới.
8. Suite được set `Status = Ready`.
9. Generation job được set `Completed`.

Rất quan trọng:

- Khi flag unified bật, request body của `generate-happy-path` và `generate-boundary-negative` không còn quyết định behavior runtime hiện tại.
- Ở runtime path hiện tại, hai controller này chỉ queue chung `GenerateTestCasesCommand`.
- Nếu FE cần một entrypoint canonical trong môi trường hiện tại, nên dùng `POST /api/test-suites/{suiteId}/generate-tests`.

### 5.7 Legacy Sync Generation Branch

Nếu một môi trường nào đó tắt `UseDotnetIntegrationWorkflowForGeneration`, thì:

- `POST /test-cases/generate-happy-path` trả `201 Created` với `GenerateHappyPathResultModel`.
- `POST /test-cases/generate-boundary-negative` trả `201 Created` với `GenerateBoundaryNegativeResultModel`.

Sync branch đã xác minh:

- Happy-path require approved order, spec match, ownership, limit checks.
- Boundary/negative require approved order, ownership, spec match, và ít nhất một source enabled trong `IncludePathMutations`, `IncludeBodyMutations`, `IncludeLlmSuggestions`.
- `ForceRegenerate = false` sẽ block nếu loại test case tương ứng đã tồn tại.

### 5.8 LLM Suggestion Review Flow

Đây là flow riêng, không phải unified callback flow.

1. FE gọi `POST /api/test-suites/{suiteId}/llm-suggestions/generate`.
2. Backend require approved order và check quota LLM.
3. Backend generate preview suggestions, persist dạng `LlmSuggestion` với `ReviewStatus = Pending`.
4. FE list / detail suggestions.
5. User approve / reject / modify-and-approve.
6. Khi approve hoặc modify:
   - Suggestion được materialize thành `TestCase` thật.
   - Suite có thể chuyển sang `Ready`.
7. Khi reject:
   - Không có `TestCase` được tạo.

Additional behavior:

- Nếu `ForceRefresh = false` và đang có pending suggestions thì request bị block.
- Khi generate preview mới, backend supersede các non-materialized suggestions cũ.
- `Review` action:
  - `Approve`, `Reject`, `Modify`
  - `Reject` bắt buộc có `reviewNotes`
  - `Modify` bắt buộc có `modifiedContent`
- Approve lại một suggestion đã materialize là nhánh idempotent.
- Nếu fingerprint của approval mới trùng với fingerprint đã từng materialize trước đó, backend có thể reuse `AppliedTestCaseId` cũ thay vì tạo duplicate test case.
- Bulk review chỉ support `Approve` hoặc `Reject`, với filter theo `suggestionType`, `testType`, `endpointId`.
- Feedback (`Helpful` / `NotHelpful`) là flow riêng, không materialize test case.

### 5.9 Manual Test Case Flow

FE có thể bỏ qua LLM cho từng case bằng manual CRUD:

- Add
- Update
- Delete
- Restore
- Toggle enable/disable
- Reorder
- Bulk delete / bulk restore

Nuance quan trọng:

- `DELETE /api/test-suites/{suiteId}/test-cases/{testCaseId}` trả `200 OK` với `TestCaseModel`, không phải `204`.
- `PATCH /toggle` và `PATCH /reorder` trả `200 OK` với empty body.

### 5.10 Execution Environment And Runtime Resolution

1. FE tạo execution environment.
2. FE update environment phải gửi `rowVersion`; delete environment cũng cần `rowVersion` query param.
3. FE có thể để `environmentId = null` khi start run nếu project đã có default environment.
4. Runtime resolver sẽ inject built-in run variables:
   - `runId`
   - `runSuffix`
   - `runTimestamp`
   - `runUniqueEmail`
   - `testEmail`
   - `runUniquePassword`
   - `testPassword`
5. Runtime resolver resolve auth config thành default headers / default query params:
   - `BearerToken` -> header mặc định, support custom `headerName`
   - `Basic` -> `Authorization: Basic ...`
   - `ApiKey` -> header hoặc query param tùy `apiKeyLocation`
   - `OAuth2ClientCredentials` -> call `tokenUrl`, lấy `access_token`, rồi set `Authorization: Bearer ...`
6. Variable resolver merge environment variables + extracted variables từ các test trước.
7. Nếu unresolved placeholders hoặc unresolved route tokens còn sót, case fail trước khi gửi HTTP.

### 5.11 Test Execution Flow

1. FE gọi `POST /api/test-suites/{suiteId}/test-runs`.
2. Backend validate:
   - suite owner
   - suite status phải là `Ready`
   - selected test cases phải thuộc suite và enabled
   - environment hợp lệ hoặc có default environment
   - concurrent run limit
   - monthly run limit
3. Backend tạo `TestRun` `Pending`, allocate `RunNumber`, tạo `RedisKey` cho run results.
4. Orchestrator chạy tuần tự toàn bộ test cases đã chọn.
5. Case có dependency fail sẽ bị `Skipped`.
6. Kết quả chi tiết được cache vào Redis; summary vẫn persist vào PostgreSQL.
7. API trả luôn `TestRunResultModel` sau khi run xong với status code `201 Created`.

Validation behavior đã xác minh:

- `StrictValidation` nếu omit thì default runtime là `false`.
- `StrictValidation = true`: test case không có expectation sẽ fail.
- `StrictValidation = false`: expectation thiếu chỉ ra warning.
- Runtime validator thực hiện 7 nhóm check: status, schema, headers, body contains, body not contains, json path, response time.

### 5.12 Run Results, Retention, And Read Semantics

- `GET /api/test-suites/{suiteId}/test-runs` trả paged summary.
- `GET /api/test-suites/{suiteId}/test-runs/{runId}` trả summary một run.
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/results` chỉ đọc cache.

`ResultsSource` trong practice ở public FE endpoint:

- `cache`: lấy được detailed results từ Redis.
- `unavailable`: cache không còn, cache hỏng, cache read fail, hoặc cache payload không parse được.

Nuance rất quan trọng:

- `TestRunModel.HasDetailedResults` chỉ là hint được derive từ `ResultsExpireAt > now`, không phải guarantee tuyệt đối rằng Redis vẫn còn payload.
- `GET /results` không tự reconstruct detailed cases từ PostgreSQL.
- Internal `TestRunReportReadGatewayService` có fallback PostgreSQL cho report-building, nhưng đó không phải public FE results endpoint.

### 5.13 Failure Explanation Flow

1. FE có run failed case.
2. FE có thể thử `GET /.../explanation` trước để đọc cache.
3. Nếu `404`, FE gọi `POST /.../explanation` để generate live.
4. Backend build failure context từ run results + test definition + endpoint metadata.
5. LLM response được cache lại.

Behavior đã xác minh:

- Chỉ explain được test case có `Status = Failed`.
- `GET /explanation` chỉ đọc cache; cache miss thì `404`.
- `POST /explanation` mới là nhánh live generation.
- Run results cho explanation context có fallback cache -> PostgreSQL thông qua `TestFailureReadGatewayService`.
- Provider mặc định hiện tại là `N8n`, cache TTL default là `24h`.

## 6. API Contract Map

## 6.1 Projects

- `GET /api/projects`
  - Query: `status`, `search`, `page`, `pageSize`
  - Response: `PaginatedResult<ProjectModel>`
  - Wrapper fields: `items`, `totalCount`, `page`, `pageSize`, `totalPages`

- `GET /api/projects/{id}`
  - Query: `includeArchived`, `includeSpecifications`
  - Response: `ProjectDetailModel`
  - `includeSpecifications = true` mới load `specifications`

- `POST /api/projects`
  - Body: `CreateUpdateProjectModel`
  - Required: `name`
  - Limits: `name <= 200`, `description <= 2000`, `baseUrl <= 500`
  - Response body thực tế: `ProjectDetailModel` shape từ `GetProjectQuery`

- `PUT /api/projects/{id}`
  - Body: `CreateUpdateProjectModel`
  - Response body thực tế: `ProjectDetailModel` shape từ `GetProjectQuery`

- `PUT /api/projects/{id}/archive`
  - Response body thực tế: `GetProjectQuery` result sau khi archive

- `PUT /api/projects/{id}/unarchive`
  - Response body thực tế: `GetProjectQuery` result sau khi unarchive

- `DELETE /api/projects/{id}`
  - Response: `200 OK` empty body

- `GET /api/projects/{id}/auditlogs`
  - Response: mảng audit entries dạng dynamic
  - Mỗi entry chứa dữ liệu chính như `id`, `userName`, `action`, `createdDateTime`, `data`, `highLight`
  - Support API hữu ích cho history UI

## 6.2 Specifications

- `GET /api/projects/{projectId}/specifications`
  - Query: `parseStatus`, `sourceType`, `includeDeleted`
  - Response: `SpecificationModel[]`

- `GET /api/projects/{projectId}/specifications/{specId}`
  - Response: `SpecificationDetailModel`
  - Important fields: `parseStatus`, `endpointCount`, `parseErrors`
  - `OriginalFileName` có trong model nhưng handler hiện tại không populate

- `GET /api/projects/{projectId}/specifications/upload-methods`
  - Response hiện tại chỉ trả một method:
    - `method = "StorageGatewayContract"`
    - `uploadApi = "/api/projects/{projectId}/specifications/upload"`

- `POST /api/projects/{projectId}/specifications/upload`
  - Content-Type: `multipart/form-data`
  - Form fields:
    - `uploadMethod`
    - `file`
    - `name`
    - `sourceType`
    - `version`
    - `autoActivate`
  - Supported file extensions: `.json`, `.yaml`, `.yml`
  - File size max: `10MB`
  - Only valid `sourceType`: `OpenAPI`, `Postman`
  - Response: `SpecificationDetailModel`

- `POST /api/projects/{projectId}/specifications/manual`
  - Body: `CreateManualSpecificationModel`
  - Response: `SpecificationDetailModel`
  - Parse success ngay trong request

- `POST /api/projects/{projectId}/specifications/curl-import`
  - Body: `ImportCurlModel`
  - Response: `SpecificationDetailModel`
  - Parse success ngay trong request

- `PUT /api/projects/{projectId}/specifications/{specId}/activate`
  - Response: `SpecificationDetailModel`

- `PUT /api/projects/{projectId}/specifications/{specId}/deactivate`
  - Response: `SpecificationDetailModel`

- `DELETE /api/projects/{projectId}/specifications/{specId}`
  - Response: `204 No Content`

- `POST /api/projects/{projectId}/specifications/{specId}/restore`
  - Response: `SpecificationDetailModel`

## 6.3 Endpoints

- `GET /api/projects/{projectId}/specifications/{specId}/endpoints`
  - Response: `EndpointModel[]`

- `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
  - Response: `EndpointDetailModel`

- `POST /api/projects/{projectId}/specifications/{specId}/endpoints`
  - Body: `CreateUpdateEndpointModel`
  - Response body thực tế: `EndpointDetailModel`

- `PUT /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
  - Body: `CreateUpdateEndpointModel`
  - Response: `EndpointDetailModel`

- `DELETE /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
  - Response: `204 No Content`

- `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}/resolved-url`
  - Query: dynamic key/value path param values
  - Response: `ResolvedUrlResult`

- `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}/path-param-mutations`
  - Response: `PathParamMutationsResult`
  - Support API hữu ích cho FE boundary-test UX

## 6.4 Test Suites

- `GET /api/projects/{projectId}/test-suites`
  - Response: `TestSuiteScopeModel[]`

- `GET /api/projects/{projectId}/test-suites/{suiteId}`
  - Response: `TestSuiteScopeModel`

- `POST /api/projects/{projectId}/test-suites`
  - Body: `CreateTestSuiteScopeRequest`
  - Required: `name`, `apiSpecId`, `selectedEndpointIds`
  - Optional but supported: `description`, `endpointBusinessContexts`, `globalBusinessRules`
  - `generationType` request accepts enum string
  - Response: `TestSuiteScopeModel`

- `PUT /api/projects/{projectId}/test-suites/{suiteId}`
  - Body: `UpdateTestSuiteScopeRequest`
  - Required thêm: `rowVersion`
  - Response: `TestSuiteScopeModel`

- `DELETE /api/projects/{projectId}/test-suites/{suiteId}?rowVersion=...`
  - Archive suite
  - Response: `204 No Content`

## 6.5 Test Order And Generation Jobs

- `POST /api/test-suites/{suiteId}/order-proposals`
  - Body: `ProposeApiTestOrderRequest`
  - Required: `specificationId`
  - Optional: `selectedEndpointIds`, `source`, `llmModel`, `reasoningNote`
  - Response: `ApiTestOrderProposalModel`

- `GET /api/test-suites/{suiteId}/order-proposals/latest`
  - Response: `ApiTestOrderProposalModel`

- `PUT /api/test-suites/{suiteId}/order-proposals/{proposalId}/reorder`
  - Body: `ReorderApiTestOrderRequest`
  - Required: `rowVersion`, `orderedEndpointIds`

- `POST /api/test-suites/{suiteId}/order-proposals/{proposalId}/approve`
  - Body: `ApproveApiTestOrderRequest`
  - Required: `rowVersion`

- `POST /api/test-suites/{suiteId}/order-proposals/{proposalId}/reject`
  - Body: `RejectApiTestOrderRequest`
  - Required: `rowVersion`, `reviewNotes`

- `GET /api/test-suites/{suiteId}/order-gate-status`
  - Response: `ApiTestOrderGateStatusModel`

- `POST /api/test-suites/{suiteId}/generate-tests`
  - Body: none
  - Response current default: `202 Accepted` với `GenerateTestsAcceptedResponse`
  - Important fields: `jobId`, `testSuiteId`, `mode`, `message`

- `GET /api/test-suites/{suiteId}/generation-status?jobId=...`
  - `jobId` optional; nếu thiếu sẽ lấy latest job của suite
  - Response: `GenerationJobStatusDto`
  - Important fields:
    - `jobId`
    - `testSuiteId`
    - `status`
    - `queuedAt`
    - `triggeredAt`
    - `completedAt`
    - `testCasesGenerated`
    - `errorMessage`
    - `webhookName`

- `POST /api/test-suites/{suiteId}/test-cases/from-ai`
  - AllowAnonymous
  - Header required: `x-callback-api-key`
  - Body: `N8nTestCasesCallbackRequest`
  - Required: `testCases[]` non-empty
  - Dành cho n8n, FE không gọi trực tiếp

## 6.6 Test Cases

- `POST /api/test-suites/{suiteId}/test-cases/generate-happy-path`
  - Body: `GenerateHappyPathTestCasesRequest`
  - Current default response: `202 Accepted` callback mode
  - Legacy sync response when unified flag off: `GenerateHappyPathResultModel`
  - Rất quan trọng: khi unified mode đang bật, body request không còn quyết định runtime path hiện tại

- `POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative`
  - Body: `GenerateBoundaryNegativeTestCasesRequest`
  - Current default response: `202 Accepted` callback mode
  - Legacy sync response when unified flag off: `GenerateBoundaryNegativeResultModel`
  - Rất quan trọng: khi unified mode đang bật, body request không còn quyết định runtime path hiện tại

- `GET /api/test-suites/{suiteId}/test-cases`
  - Query: `testType`, `includeDisabled`, `includeDeleted`
  - Response: `TestCaseModel[]`

- `GET /api/test-suites/{suiteId}/test-cases/{testCaseId}`
  - Response: `TestCaseModel`

- `POST /api/test-suites/{suiteId}/test-cases`
  - Body: `AddTestCaseRequest`
  - Response: `TestCaseModel`

- `PUT /api/test-suites/{suiteId}/test-cases/{testCaseId}`
  - Body: `UpdateTestCaseRequest`
  - Response: `TestCaseModel`

- `DELETE /api/test-suites/{suiteId}/test-cases/{testCaseId}`
  - Response: `TestCaseModel`

- `PATCH /api/test-suites/{suiteId}/test-cases/{testCaseId}/toggle`
  - Body: `ToggleTestCaseRequest`
  - Important field: `isEnabled`
  - Response: `200 OK` empty body

- `PATCH /api/test-suites/{suiteId}/test-cases/reorder`
  - Body: `ReorderTestCasesRequest`
  - Important field: `testCaseIds`
  - Response: `200 OK` empty body

- `POST /api/test-suites/{suiteId}/test-cases/{testCaseId}/restore`
  - Response: `TestCaseModel`

- `POST /api/test-suites/{suiteId}/test-cases/bulk-delete`
  - Body: `BulkDeleteTestCasesRequest`
  - Important field: `testCaseIds[]`
  - Response: `BulkOperationResultModel`

- `POST /api/test-suites/{suiteId}/test-cases/bulk-restore`
  - Body: `BulkRestoreTestCasesRequest`
  - Important field: `testCaseIds[]`
  - Response: `BulkOperationResultModel`

## 6.7 LLM Suggestions

- `POST /api/test-suites/{suiteId}/llm-suggestions/generate`
  - Body: `GenerateLlmSuggestionPreviewRequest`
  - Important fields:
    - `specificationId`
    - `forceRefresh`
    - `algorithmProfile?`
  - Response: `GenerateLlmSuggestionPreviewResultModel`
  - Important result fields:
    - `testSuiteId`
    - `totalSuggestions`
    - `endpointsCovered`
    - `llmModel`
    - `llmTokensUsed`
    - `fromCache`
    - `generatedAt`
    - `suggestions[]`

- `GET /api/test-suites/{suiteId}/llm-suggestions`
  - Query: `reviewStatus`, `testType`, `endpointId`, `includeDeleted`
  - Response: `LlmSuggestionModel[]`

- `GET /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}`
  - Response: `LlmSuggestionModel`

- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review`
  - Body: `ReviewLlmSuggestionRequest`
  - `action` values theo code: `Approve`, `Reject`, `Modify`
  - `rowVersion` required cho concurrency-safe review
  - `reviewNotes` required khi `Reject`
  - `modifiedContent` required khi `Modify`
  - Response: `LlmSuggestionModel`

- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/feedback`
  - Body: `UpsertLlmSuggestionFeedbackRequest`
  - `signal` values: `Helpful`, `NotHelpful`
  - Response: `LlmSuggestionFeedbackModel`

- `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-review`
  - Body: `BulkReviewLlmSuggestionsRequest`
  - `action` values theo code hiện tại: `Approve`, `Reject`
  - Filter support: `filterBySuggestionType`, `filterByTestType`, `filterByEndpointId`
  - `reviewNotes` required khi bulk `Reject`
  - Response: `BulkReviewLlmSuggestionsResultModel`

- `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-delete`
  - Body: `BulkDeleteLlmSuggestionsRequest`
  - Important field: `suggestionIds[]`
  - Response: `BulkOperationResultModel`

- `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-restore`
  - Body: `BulkRestoreLlmSuggestionsRequest`
  - Important field: `suggestionIds[]`
  - Response: `BulkOperationResultModel`

## 6.8 Execution Environments

- `GET /api/projects/{projectId}/execution-environments`
  - Response: `ExecutionEnvironmentModel[]`

- `GET /api/projects/{projectId}/execution-environments/{environmentId}`
  - Response: `ExecutionEnvironmentModel`

- `POST /api/projects/{projectId}/execution-environments`
  - Body: `CreateExecutionEnvironmentRequest`
  - Required: `name`, `baseUrl`
  - Limits: `name <= 100`, `baseUrl <= 500`
  - `baseUrl` phải là URL tuyệt đối `http` hoặc `https`
  - Response: `ExecutionEnvironmentModel`

- `PUT /api/projects/{projectId}/execution-environments/{environmentId}`
  - Body: `UpdateExecutionEnvironmentRequest`
  - Required thêm: `rowVersion`
  - `baseUrl` phải là URL tuyệt đối `http` hoặc `https`
  - Response: `ExecutionEnvironmentModel`

- `DELETE /api/projects/{projectId}/execution-environments/{environmentId}?rowVersion=...`
  - Response: `204 No Content`

Rất quan trọng với auth config:

- GET environment trả `AuthConfig` đã mask secret bằng `******`.
- PUT environment không có cơ chế preserve masked secret.
- FE không được gửi lại `******` như secret thật.
- Khi edit environment có auth secret, FE phải buộc user nhập lại secret đầy đủ hoặc có cơ chế riêng để giữ raw secret ngoài response.

## 6.9 Test Runs

- `POST /api/test-suites/{suiteId}/test-runs`
  - Body: `StartTestRunRequest`
  - Fields:
    - `environmentId?`
    - `selectedTestCaseIds?`
    - `strictValidation`
  - `strictValidation` nếu omit thì runtime default là `false`
  - Response: `TestRunResultModel`
  - Status code: `201 Created`
  - Synchronous execution

- `GET /api/test-suites/{suiteId}/test-runs`
  - Query: `pageNumber`, `pageSize`, `status`
  - Response: `Paged<TestRunModel>`
  - Wrapper fields: `totalItems`, `items`, `page`, `pageSize`, `totalPages`, `hasPreviousPage`, `hasNextPage`

- `GET /api/test-suites/{suiteId}/test-runs/{runId}`
  - Response: `TestRunModel`

- `GET /api/test-suites/{suiteId}/test-runs/{runId}/results`
  - Response: `TestRunResultModel`
  - Important fields:
    - `resultsSource`
    - `cases`

## 6.10 Failure Explanation

- `GET /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
  - Chỉ đọc cache
  - Cache miss => `404`

- `POST /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
  - Generate live nếu chưa có cache
  - Response: `FailureExplanationModel`

## 7. Serialization And Transport Nuances

## 7.1 Response Enums That Are Numeric

Các field dưới đây đang là enum type không có global `JsonStringEnumConverter`, nên FE phải chuẩn bị parse number:

- `TestSuiteScopeModel.GenerationType`
- `TestSuiteScopeModel.Status`
- `TestSuiteScopeModel.ApprovalStatus`
- `ApiTestOrderGateStatusModel.ActiveProposalStatus`

## 7.2 Response Enums That Are Strings

- `ProjectModel.Status`
- `SpecificationModel.SourceType`
- `SpecificationModel.ParseStatus`
- `ApiTestOrderProposalModel.Status`
- `ApiTestOrderProposalModel.Source`
- `ExecutionAuthConfigModel.AuthType`
- `ExecutionAuthConfigModel.ApiKeyLocation`
- `TestCaseModel.TestType`
- `TestCaseModel.Priority`
- `TestCaseModel.Request.HttpMethod`
- `TestCaseModel.Request.BodyType`
- `TestCaseVariableModel.ExtractFrom`
- `LlmSuggestionModel.SuggestionType`
- `LlmSuggestionModel.TestType`
- `LlmSuggestionModel.Priority`
- `LlmSuggestionModel.ReviewStatus`
- `TestRunModel.Status`
- `GenerationJobStatusDto.Status`

## 7.3 Request Enums That Accept Strings

- `CreateTestSuiteScopeRequest.GenerationType`: `Auto`, `Manual`, `LLMAssisted`
- `UpdateTestSuiteScopeRequest.GenerationType`: `Auto`, `Manual`, `LLMAssisted`
- `ProposeApiTestOrderRequest.Source`: `Ai`, `User`, `System`, `Imported`
- `CreateExecutionEnvironmentRequest.AuthConfig.AuthType`: `None`, `BearerToken`, `Basic`, `ApiKey`, `OAuth2ClientCredentials`
- `CreateExecutionEnvironmentRequest.AuthConfig.ApiKeyLocation`: `Header`, `Query`
- `UpdateExecutionEnvironmentRequest.AuthConfig.AuthType`: same as trên
- `UpdateExecutionEnvironmentRequest.AuthConfig.ApiKeyLocation`: same as trên
- `CreateManualSpecificationModel.Endpoints[].Parameters[].DataType`: `string`, `integer`, `number`, `boolean`, `object`, `array`, `uuid`

## 7.4 Request Enums That Are Numeric In JSON

Theo serializer hiện tại của WebAPI, FE nên gửi number cho các field sau:

- `AddTestCaseRequest.TestType`
  - `0 HappyPath`, `1 Boundary`, `2 Negative`, `3 Performance`, `4 Security`
- `AddTestCaseRequest.Priority`
  - `0 Critical`, `1 High`, `2 Medium`, `3 Low`
- `AddTestCaseRequest.Request.HttpMethod`
  - `0 GET`, `1 POST`, `2 PUT`, `3 DELETE`, `4 PATCH`, `5 HEAD`, `6 OPTIONS`
- `AddTestCaseRequest.Request.BodyType`
  - `0 None`, `1 JSON`, `2 FormData`, `3 UrlEncoded`, `4 Raw`, `5 Binary`
- `AddTestCaseRequest.Variables[].ExtractFrom`
  - `0 ResponseBody`, `1 ResponseHeader`, `2 Status`
- `UpdateTestCaseRequest` có cùng quy tắc như `AddTestCaseRequest`

Ghi chú rất quan trọng:

- Đây là current checked-in behavior theo serializer hiện tại.
- Các request model manual test case ở đây không khai báo string enum converter riêng.
- `ClassifiedAds.WebAPI` cũng không add global `JsonStringEnumConverter` trong `AddJsonOptions` hiện tại.

## 7.5 Fields That Are JSON Strings, Not Nested Objects

FE cần parse / stringify đúng các field sau:

- `EndpointModel.Tags`
- `EndpointDetailModel.Parameters[].Schema`
- `EndpointDetailModel.Parameters[].Examples`
- `EndpointDetailModel.Responses[].Schema`
- `EndpointDetailModel.Responses[].Examples`
- `EndpointDetailModel.Responses[].Headers`
- `TestCaseModel.Request.Headers`
- `TestCaseModel.Request.PathParams`
- `TestCaseModel.Request.QueryParams`
- `TestCaseModel.Expectation.ExpectedStatus`
- `TestCaseModel.Expectation.ResponseSchema`
- `TestCaseModel.Expectation.HeaderChecks`
- `TestCaseModel.Expectation.BodyContains`
- `TestCaseModel.Expectation.BodyNotContains`
- `TestCaseModel.Expectation.JsonPathChecks`
- `LlmSuggestionModel.SuggestedRequest`
- `LlmSuggestionModel.SuggestedExpectation`
- `LlmSuggestionModel.ModifiedContent`

Nuance quan trọng:

- `LlmSuggestionModel.SuggestedRequest`, `SuggestedExpectation`, và `ModifiedContent` trong response model đều là string JSON.
- Nhưng `ReviewLlmSuggestionRequest.ModifiedContent` lại là structured object, không phải raw string.
- FE phải parse suggestion string sang object editable trước khi gửi `PUT /review` với action `Modify`.

## 8. Runtime And Validation Details FE Should Know

- Execution run là sequential, không parallel.
- Case có dependency failed sẽ bị `Skipped` và có `SkippedBecauseDependencyIds`.
- Built-in test email/password sẽ được inject nếu environment không cung cấp riêng.
- Với `HappyPath` JSON body, backend có nhánh normalize synthetic email-like literals trong field `email` / `*Email` sang `testEmail` hoặc `runUniqueEmail` khi variable resolution chạy và body match điều kiện internal.
- Response body preview trong run result bị truncate ở `65536` ký tự.
- Extracted variables có key chứa `token`, `secret`, `password`, `apikey` sẽ bị mask thành `******` trong run result.

## 9. FE Integration Checklist

### 9.1 Before Order Proposal

- Đã có `projectId` hợp lệ.
- Đã có `specId` hợp lệ.
- Specification dùng cho auto test đã parse xong (`ParseStatus = Success`).
- FE không giả định upload xong là parse xong; phải check `ParseStatus`.
- FE đã load endpoint list từ đúng `specId`.
- Nếu FE support endpoint editing, phải đảm bảo user đang nhìn state endpoint mới nhất trước khi tạo suite/order.

### 9.2 Before Generation

- Suite dùng đúng `apiSpecId` chứa các endpoint đã chọn.
- `selectedEndpointIds` đều thuộc đúng spec.
- Đã có latest proposal.
- Proposal đã được approve hoặc modified-and-approved.
- `GET /order-gate-status` trả `isGatePassed = true`.
- FE hiểu môi trường hiện tại đang chạy unified callback mode.
- FE poll `GET /generation-status` sau khi nhận `jobId`.
- Nếu FE vẫn dùng `generate-happy-path` hoặc `generate-boundary-negative` trong unified mode, FE không được giả định request body ở đó còn ảnh hưởng runtime path hiện tại.

### 9.3 Before Execution

- Suite đang ở `Ready`.
- Có default environment hoặc FE gửi `environmentId`.
- Nếu update environment có secret auth, FE không gửi lại giá trị mask `******`.
- FE đã disable các case user không muốn run bằng `toggle`, hoặc truyền `selectedTestCaseIds` đúng subset.
- FE hiểu `strictValidation` nếu omit thì runtime default là `false`.

### 9.4 Results And Failure Analysis

- FE đọc `run.resultsExpireAt` và `run.hasDetailedResults` để biết khả năng còn cache.
- FE không coi `hasDetailedResults = true` là guarantee tuyệt đối rằng Redis còn payload.
- FE handle `ResultsSource = "unavailable"` như một state hợp lệ, không coi là parse error.
- FE thử `GET /explanation` trước nếu muốn cache-first UX.
- FE dùng `POST /explanation` nếu cần force live generation.

## 10. Common Integration Mistakes

- Giả định `generate-happy-path` và `generate-boundary-negative` luôn là 2 behavior khác nhau. Trong config hiện tại chúng queue cùng unified workflow.
- Giả định body của `generate-happy-path` hoặc `generate-boundary-negative` vẫn còn quyết định runtime path khi unified mode đang bật.
- Gửi enum dạng string cho `AddTestCaseRequest` hoặc `UpdateTestCaseRequest`. Các field enum ở đó đang là numeric JSON theo serializer hiện tại.
- Ngược lại, parse `ExecutionAuthConfigModel.AuthType` như number. Field này đang là string.
- Quên `rowVersion` khi update suite, reorder proposal, approve/reject proposal, update environment, review suggestion.
- Gửi `Modify` suggestion mà không có `modifiedContent`.
- Gửi `Reject` suggestion mà không có `reviewNotes`.
- Gửi object cho các field backend đang lưu và trả dưới dạng JSON string.
- Gửi lại `******` khi update auth config environment.
- Không poll spec parse khi upload OpenAPI YAML hoặc Postman.
- Không poll generation job khi unified workflow đang bật.
- Kỳ vọng `GET /results` sẽ fallback DB sau khi cache hết hạn.
- Kỳ vọng `hasDetailedResults = true` nghĩa là chắc chắn lấy được detailed result.
- Dùng `GET /explanation` như endpoint generate live. Endpoint đó chỉ đọc cache.

## 11. Source References

Những file sau là nguồn xác minh chính cho tài liệu này:

- `ClassifiedAds.WebAPI/Program.cs`
- `ClassifiedAds.WebAPI/appsettings.json`
- `ClassifiedAds.WebAPI/appsettings.Development.json`
- `ClassifiedAds.Background/appsettings.json`
- `ClassifiedAds.Background/appsettings.Development.json`
- `ClassifiedAds.Infrastructure/Web/ExceptionHandlers/GlobalExceptionHandler.cs`
- `ClassifiedAds.Infrastructure/Web/Validation/ValidationProblemDetailsFactory.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Controllers/ProjectsController.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Controllers/SpecificationsController.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Controllers/EndpointsController.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Commands/UploadApiSpecificationCommand.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Commands/ParseUploadedSpecificationCommand.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Commands/CreateManualSpecificationCommand.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Commands/ImportCurlCommand.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Models/UploadSpecificationModel.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Models/CreateManualSpecificationModel.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Models/ImportCurlModel.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Models/CreateUpdateProjectModel.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Models/CreateUpdateEndpointModel.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Models/EndpointParameterDataType.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Services/ApiEndpointMetadataService.cs`
- `ClassifiedAds.Modules.TestGeneration/Controllers/TestSuitesController.cs`
- `ClassifiedAds.Modules.TestGeneration/Controllers/TestOrderController.cs`
- `ClassifiedAds.Modules.TestGeneration/Controllers/TestCasesController.cs`
- `ClassifiedAds.Modules.TestGeneration/Controllers/LlmSuggestionsController.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/GenerateTestCasesCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/SaveAiGeneratedTestCasesCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/ReviewLlmSuggestionCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/BulkReviewLlmSuggestionsCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/MessageBusConsumers/TriggerTestGenerationConsumer.cs`
- `ClassifiedAds.Modules.TestGeneration/Queries/GetGenerationJobStatusQuery.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/TestGenerationPayloadBuilder.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderService.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/HappyPathTestCaseGenerator.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionReviewService.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/CreateTestSuiteScopeRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/UpdateTestSuiteScopeRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/GenerateHappyPathTestCasesRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/GenerateBoundaryNegativeTestCasesRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/AddTestCaseRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/ReviewLlmSuggestionRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/GenerateLlmSuggestionPreviewRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/BulkReviewLlmSuggestionsRequest.cs`
- `ClassifiedAds.Modules.TestExecution/Controllers/ExecutionEnvironmentsController.cs`
- `ClassifiedAds.Modules.TestExecution/Controllers/TestRunsController.cs`
- `ClassifiedAds.Modules.TestExecution/Commands/AddUpdateExecutionEnvironmentCommand.cs`
- `ClassifiedAds.Modules.TestExecution/Commands/StartTestRunCommand.cs`
- `ClassifiedAds.Modules.TestExecution/Services/ExecutionAuthConfigService.cs`
- `ClassifiedAds.Modules.TestExecution/Services/ExecutionEnvironmentRuntimeResolver.cs`
- `ClassifiedAds.Modules.TestExecution/Services/VariableResolver.cs`
- `ClassifiedAds.Modules.TestExecution/Services/PreExecutionValidator.cs`
- `ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs`
- `ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs`
- `ClassifiedAds.Modules.TestExecution/Services/TestResultCollector.cs`
- `ClassifiedAds.Modules.TestExecution/Services/HttpTestExecutor.cs`
- `ClassifiedAds.Modules.TestExecution/Queries/GetTestRunResultsQuery.cs`
- `ClassifiedAds.Modules.TestExecution/Services/TestFailureReadGatewayService.cs`
- `ClassifiedAds.Modules.TestExecution/Models/ExecutionAuthConfigModel.cs`
- `ClassifiedAds.Modules.TestExecution/Models/TestRunModel.cs`
- `ClassifiedAds.Modules.TestExecution/Models/Requests/CreateExecutionEnvironmentRequest.cs`
- `ClassifiedAds.Modules.TestExecution/Models/Requests/UpdateExecutionEnvironmentRequest.cs`
- `ClassifiedAds.Modules.TestExecution/Models/Requests/StartTestRunRequest.cs`
- `ClassifiedAds.Modules.LlmAssistant/Controllers/FailureExplanationsController.cs`
- `ClassifiedAds.Modules.LlmAssistant/Queries/GetFailureExplanationQuery.cs`
- `ClassifiedAds.Modules.LlmAssistant/Commands/ExplainTestFailureCommand.cs`
- `ClassifiedAds.Modules.LlmAssistant/ConfigurationOptions/FailureExplanationOptions.cs`
- `ClassifiedAds.Modules.LlmAssistant/Services/LlmFailureExplainer.cs`
- `ClassifiedAds.Modules.LlmAssistant/Services/N8nFailureExplanationClient.cs`

## 12. Uncertainties / Cautions

- 403 authorization response shape không được custom trong các module đã đọc; thực tế sẽ phụ thuộc ASP.NET Core auth pipeline.
- Multipart enum binding cho `UploadSpecificationModel` dựa trên ASP.NET Core model binding behavior, không có custom converter riêng ngoài converter của enum model cụ thể.
- Runtime environment có thể override appsettings bằng env vars, vì vậy FE nên coi callback mode là default hiện tại của repo chứ không phải guarantee tuyệt đối cho mọi deployment.
- `SpecificationDetailModel.OriginalFileName` có trong model nhưng query handler hiện tại chưa set giá trị.
- Trong config có key webhook `DotnetIntegration`, nhưng code path hiện tại của unified generation dùng logical name `generate-test-cases-unified`.
- Numeric enum transport ở manual test-case request là checked-in behavior của serializer hiện tại; nếu host sau này thêm global string enum converter thì contract runtime có thể đổi theo host config.