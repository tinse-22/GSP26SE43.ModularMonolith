# BE Runtime Contract Confirmation

Tài liệu này chốt contract runtime thực tế từ backend code để FE có thể nối full luồng ổn định.

> Nguồn xác nhận: controller + DTO/model thực tế trong backend.

---

## 1) `POST /api/test-suites/{suiteId}/llm-suggestions/generate`

### Confirmed from backend code

- **Controller**: `ClassifiedAds.Modules.TestGeneration.Controllers.LlmSuggestionsController`
- **Action**: `GeneratePreview`
- **Route**: `POST /api/test-suites/{suiteId}/llm-suggestions/generate`
- **Request body**: `GenerateLlmSuggestionPreviewRequest`
- **Response type**: `GenerateLlmSuggestionPreviewResultModel`
- **Status code**: `201 Created`
- **Response shape**: **wrapper object**

### Runtime response JSON shape

```json
{
  "testSuiteId": "7b3b2d0f-1f22-4b3f-a90f-1a4b8fd6f9d2",
  "totalSuggestions": 3,
  "endpointsCovered": 2,
  "llmModel": "gemini-3-flash-preview",
  "llmTokensUsed": 1248,
  "fromCache": false,
  "generatedAt": "2026-04-21T10:00:00+07:00",
  "suggestions": [
    {
      "id": "b8d1a89c-1f5f-4e45-a3dc-3c2d907b0d4d",
      "testSuiteId": "7b3b2d0f-1f22-4b3f-a90f-1a4b8fd6f9d2",
      "endpointId": "e0ef4a8f-0ef3-4d8d-a1a4-6f0f1f4c1111",
      "cacheKey": "suite:7b3b2d0f-1f22-4b3f-a90f-1a4b8fd6f9d2:endpoint:e0ef4a8f-0ef3-4d8d-a1a4-6f0f1f4c1111:type:BoundaryNegative",
      "displayOrder": 1,
      "suggestionType": "BoundaryNegative",
      "testType": "API",
      "suggestedName": "POST /users - invalid email format",
      "suggestedDescription": "Verify API rejects invalid email values.",
      "suggestedRequest": "{\"method\":\"POST\",\"path\":\"/users\",\"body\":{\"email\":\"invalid\"}}",
      "suggestedExpectation": "{\"expectedStatus\":400,\"assertions\":[\"validation error\"]}",
      "suggestedVariables": [],
      "suggestedTags": ["boundary", "validation"],
      "priority": "High",
      "reviewStatus": "Pending",
      "reviewedById": null,
      "reviewedAt": null,
      "reviewNotes": null,
      "modifiedContent": null,
      "appliedTestCaseId": null,
      "llmModel": "gemini-3-flash-preview",
      "tokensUsed": 412,
      "isDeleted": false,
      "deletedAt": null,
      "deletedById": null,
      "createdDateTime": "2026-04-21T10:00:00+07:00",
      "updatedDateTime": null,
      "rowVersion": "AAAAAAAB9xk=",
      "currentUserFeedback": null,
      "feedbackSummary": {
        "helpfulCount": 0,
        "notHelpfulCount": 0
      }
    }
  ]
}
```

### Confirmed field names

Top-level:
- `testSuiteId`
- `totalSuggestions`
- `endpointsCovered`
- `llmModel`
- `llmTokensUsed`
- `fromCache`
- `generatedAt`
- `suggestions`

Suggestion item:
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

### `reviewStatus` enum values thực tế

Confirmed from backend entity/model conversion:

- `Pending`
- `Approved`
- `Rejected`
- `ModifiedAndApproved`
- `Superseded`

### FE integration note

- FE **must read `suggestions[]` from wrapper object**.
- FE **must not hard-code array response**.
- `rowVersion` is returned as Base64 string.
- `suggestedRequest` / `suggestedExpectation` are stringified JSON payloads in runtime response.

---

## 2) `GET /api/projects/{projectId}/specifications/{specId}`

### Confirmed from backend code

- **Controller**: `SpecificationsController`
- **Route**: `GET /api/projects/{projectId}/specifications/{specId}`
- **Response type**: `SpecificationDetailModel`
- **Status code**: `200 OK`

### FE usage

- FE poll parse status sau upload spec.
- FE cần đọc `parseStatus` từ response.

### Need code confirmation

- Sample runtime JSON của `SpecificationDetailModel` chưa được trích ở đây.
- Nếu FE cần hard-code toàn bộ field, cần mở model thực tế.

---

## 3) `GET /api/projects/{projectId}/specifications/{specId}/endpoints`

### Confirmed from backend code

- **Controller**: `EndpointsController`
- **Route**: `GET /api/projects/{projectId}/specifications/{specId}/endpoints`
- **Response type**: `List<EndpointModel>`
- **Status code**: `200 OK`

### FE usage

- FE fetch all endpoints để chọn endpoint cho test suite.
- FE đang dùng `pageSize=9999` theo contract report, nhưng backend controller hiện không có pagination params.

### Important note

- Endpoint response shape must be confirmed from `EndpointModel` before hard-coding client fields.

---

## 4) `POST /api/projects/{projectId}/test-suites`

### Confirmed from backend code

- **Controller**: `TestSuitesController`
- **Route**: `POST /api/projects/{projectId}/test-suites`
- **Request body**: `CreateTestSuiteScopeRequest`
- **Response type**: `TestSuiteScopeModel`
- **Status code**: `201 Created`

### Request body fields thực tế

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

### Runtime contract notes

- `Name` is required in C# model.
- `ApiSpecId` is `Guid?` in BE model, but FE should treat it as required.
- `GenerationType` is serialized with `JsonStringEnumConverter`.
- `EndpointBusinessContexts` is a dictionary keyed by `Guid`.

### Need code confirmation

- Exact response JSON of `TestSuiteScopeModel` should be checked before FE hard-codes all fields.

---

## 5) `POST /api/test-suites/{suiteId}/order-proposals`

### Confirmed from backend code

- **Controller**: `TestOrderController`
- **Route**: `POST /api/test-suites/{suiteId}/order-proposals`
- **Response**: proposal model with `id`, `rowVersion`, `status`

### FE usage

- FE auto-calls this right after suite creation.
- FE expects approval flow to continue immediately.

### Need code confirmation

- Exact request/response DTO name was not included in this excerpt.
- FE should confirm required fields, especially `SpecificationId`, `SelectedEndpointIds`, `Source`, and optional `LlmModel`.

---

## 6) `POST /api/test-suites/{suiteId}/order-proposals/{proposalId}/approve`

### Confirmed from backend code

- **Controller**: `TestOrderController`
- **Route**: `POST /api/test-suites/{suiteId}/order-proposals/{proposalId}/approve`
- **Response**: `200 OK`
- **Request body**: includes `RowVersion` and optional `ReviewNotes`

### Runtime rule

- `RowVersion` is the optimistic concurrency token.
- FE must send the latest `rowVersion` from propose response.

---

## 7) `POST /api/test-suites/{suiteId}/llm-suggestions/generate`

### Already confirmed above

- Wrapper object
- `201 Created`
- `suggestions[]` array inside wrapper
- `reviewStatus` enum values as above

---

## 8) `GET /api/test-suites/{suiteId}/llm-suggestions`

### Confirmed from backend code

- **Controller**: `LlmSuggestionsController`
- **Route**: `GET /api/test-suites/{suiteId}/llm-suggestions`
- **Query params**:
  - `reviewStatus`
  - `testType`
  - `endpointId`
  - `includeDeleted`
- **Response type**: `List<LlmSuggestionModel>`
- **Status code**: `200 OK`

### Runtime shape for each suggestion

Use the same fields as in section 1 suggestion item.

---

## 9) `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review`

### Confirmed from backend code

- **Route**: `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review`
- **Request body**: `ReviewLlmSuggestionRequest`
- **Response type**: `LlmSuggestionModel`
- **Status code**: `200 OK`
- **409 Conflict**: possible when `rowVersion` is stale

### Runtime review actions

- `Approve`
- `Reject`
- `Modify`

### FE note

- FE must send `rowVersion`.
- If FE uses stale rowVersion, backend can return `409 Conflict`.

---

## 10) `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-review`

### Confirmed from backend code

- **Route**: `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-review`
- **Request body**: `BulkReviewLlmSuggestionsRequest`
- **Response type**: `BulkReviewLlmSuggestionsResultModel`
- **Status code**: `200 OK`

### Need code confirmation

- Full response sample should be extracted if FE wants to render bulk result counters.

---

## 11) `POST /api/test-suites/{suiteId}/generate-tests`

### Confirmed from backend code

- **Controller**: `TestCasesController` / generation flow in TestGeneration module
- **Route**: `POST /api/test-suites/{suiteId}/generate-tests`
- **Status code**: `202 Accepted`
- **Response**: accepted job model containing `jobId` and generation mode/message

### FE note

- FE should poll status endpoint using returned `jobId`.

### Need code confirmation

- Exact DTO fields of accepted response and generation-status response should be checked before FE hard-codes them.

---

## 12) `GET /api/test-suites/{suiteId}/generation-status?jobId=...`

### Confirmed from backend code

- Status endpoint exists in backend generation flow.

### FE expected statuses

- `Queued`
- `Triggering`
- `WaitingForCallback`
- `Completed`
- `Failed`

### Need code confirmation

- Exact backend enum/string values and progress mapping must be checked against runtime DTO.

---

## 13) `POST /api/test-suites/{suiteId}/test-runs`

### Confirmed from backend code

- **Controller**: `TestRunsController`
- **Route**: `POST /api/test-suites/{suiteId}/test-runs`
- **Request body**: `StartTestRunRequest`
- **Response type**: `TestRunResultModel`
- **Status code**: `201 Created`

### Runtime request body

```json
{
  "environmentId": "f6c9b7d0-4a6b-4b54-8f9f-6f1b2c0dd101",
  "selectedTestCaseIds": ["..."],
  "strictValidation": true
}
```

### Actual BE request model

```csharp
public class StartTestRunRequest
{
    public Guid? EnvironmentId { get; set; }
    public List<Guid> SelectedTestCaseIds { get; set; }
    public bool StrictValidation { get; set; }
}
```

### FE integration note

- `environmentId` should be treated as required by FE.
- `strictValidation` should always be sent.
- `selectedTestCaseIds` may be null in backend model, but FE should send explicitly when selecting a subset.

---

## 14) `GET /api/test-suites/{suiteId}/test-runs/{runId}/results`

### Confirmed from backend code

- **Controller**: `TestRunsController`
- **Route**: `GET /api/test-suites/{suiteId}/test-runs/{runId}/results`
- **Response type**: `TestRunResultModel`
- **Status code**: `200 OK`

### Actual runtime DTO fields

Backend model:

```csharp
public class TestRunResultModel
{
    public TestRunModel Run { get; set; }
    public string ResultsSource { get; set; } = "cache";
    public DateTimeOffset ExecutedAt { get; set; }
    public string ResolvedEnvironmentName { get; set; }
    public List<TestCaseRunResultModel> Cases { get; set; } = new();
}
```

### Confirmed field names

- `run`
- `resultsSource`
- `executedAt`
- `resolvedEnvironmentName`
- `cases`

### `resultsSource` values

From FE contract and runtime usage, the expected values are:
- `live`
- `cached`
- `unavailable`

### Actual case item DTO fields

```csharp
public class TestCaseRunResultModel
{
    public Guid TestCaseId { get; set; }
    public Guid? EndpointId { get; set; }
    public string Name { get; set; }
    public string TestType { get; set; }
    public int OrderIndex { get; set; }
    public string Status { get; set; }
    public int? HttpStatusCode { get; set; }
    public long DurationMs { get; set; }
    public string ResolvedUrl { get; set; }
    public string HttpMethod { get; set; }
    public string BodyType { get; set; }
    public string RequestBody { get; set; }
    public Dictionary<string, string> QueryParams { get; set; }
    public int TimeoutMs { get; set; }
    public string ExpectedStatus { get; set; }
    public Dictionary<string, string> RequestHeaders { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; set; }
    public string ResponseBodyPreview { get; set; }
    public List<ValidationFailureModel> FailureReasons { get; set; }
    public List<ValidationWarningModel> Warnings { get; set; }
    public int ChecksPerformed { get; set; }
    public int ChecksSkipped { get; set; }
    public Dictionary<string, string> ExtractedVariables { get; set; }
    public List<Guid> DependencyIds { get; set; }
    public List<Guid> SkippedBecauseDependencyIds { get; set; }
    public bool StatusCodeMatched { get; set; }
    public bool? SchemaMatched { get; set; }
    public bool? HeaderChecksPassed { get; set; }
    public bool? BodyContainsPassed { get; set; }
    public bool? BodyNotContainsPassed { get; set; }
    public bool? JsonPathChecksPassed { get; set; }
    public bool? ResponseTimePassed { get; set; }
}
```

### FE note

- FE should read `resultsSource` exactly, not `results_source`.
- FE deterministic branch should use the boolean check fields above.

---

## 15) `GET/POST /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`

### Confirmed from backend code

- **Controller**: `FailureExplanationsController`
- **GET route**: `GET /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
- **POST route**: `POST /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
- **Response type**: `FailureExplanationModel`
- **Status code**: `200 OK`

### FE note

- POST body is empty.
- Backend derives context from `suiteId`, `runId`, `testCaseId`.

### Need code confirmation

- Exact fields of `FailureExplanationModel` should be checked before hard-coding UI rendering.

---

## 16) `POST /api/test-suites/{suiteId}/test-runs/{runId}/reports`

### Confirmed from backend code

- **Controller**: `TestReportsController`
- **Route**: `POST /api/test-suites/{suiteId}/test-runs/{runId}/reports`
- **Request body**: `GenerateTestReportRequest`
- **Response type**: `TestReportModel`
- **Status code**: `201 Created`

### Download endpoint

- `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download`
- Returns binary file via `File(...)`

### FE note

- FE can safely treat download endpoint as blob.
- Exact `TestReportModel` response shape should be confirmed if UI renders metadata before download.

---

## 17) SignalR runtime contract

### Backend confirmation

- Backend contains `NotificationHub : Hub`
- SignalR hub exists, but event/method names still need direct runtime confirmation from hub registration and broadcaster code.

### FE expected contract

- Subscribe by `runId`
- Event name: `TestRunStatusChanged`
- Payload:

```json
{
  "testRunId": "...",
  "status": "Completed",
  "completedCount": 10,
  "totalCount": 10,
  "failedCount": 2
}
```

### Need code confirmation

- Exact hub route
- Exact client method name
- Exact event name
- Exact payload casing

---

## Final FE integration guidance

### Safe to hard-code now

- `POST /api/test-suites/{suiteId}/llm-suggestions/generate` returns **201** and **wrapper object**
- `reviewStatus` enum values:
  - `Pending`
  - `Approved`
  - `Rejected`
  - `ModifiedAndApproved`
  - `Superseded`
- `POST /api/test-suites/{suiteId}/test-runs` returns **201**
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/results` returns `resultsSource` and `cases[]`
- `GET/POST /failures/{testCaseId}/explanation` exists
- `POST /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download` returns blob

### Need extra confirmation before FE hard-codes

- Exact `TestSuiteScopeModel`
- Exact `GenerateLlmSuggestionPreviewRequest`
- Exact `GenerateLlmSuggestionPreviewResultModel` runtime casing if serializer config differs
- SignalR contract
- Generation job status DTO values
- Report model fields
- Failure explanation model fields
