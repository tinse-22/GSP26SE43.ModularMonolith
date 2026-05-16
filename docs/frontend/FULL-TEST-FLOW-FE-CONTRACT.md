# Full Test Flow FE Contract

Cap nhat lan cuoi: 2026-05-16

Tai lieu nay la flow FE nen bam khi noi cac API test generation hien tai. Muc tieu la tranh goi thieu param, doc sai response value, hoac goi nham callback endpoint.

## 0. Nguyen tac chung

- Tat ca endpoint FE-facing ben duoi dung `Authorization: Bearer <JWT>`.
- FE khong goi endpoint callback n8n:
  - `POST /api/test-suites/{suiteId}/test-cases/from-ai`
  - `POST /api/test-generation/llm-suggestions/callback/{jobId}`
- Moi mutation co `rowVersion` thi FE phai gui rowVersion moi nhat tu response gan nhat.
- Khi endpoint generate tra `202 Accepted`, FE phai poll `GET /api/test-suites/{suiteId}/generation-status?jobId={jobId}`.
- Khi endpoint LLM suggestion generate tra `source=local-draft`, FE phai render draft ngay, khong coi do la loi.

## 1. Required IDs FE phai co truoc

FE can co cac id sau tu cac man hinh truoc:

| Field | Lay tu dau | Bat buoc cho |
|---|---|---|
| `projectId` | ApiDocumentation project | Load specifications/endpoints |
| `specificationId` | selected specification | order proposal, LLM suggestions, old sync generate mode |
| `suiteId` | TestSuite da tao o FE-04 | moi route test generation |
| `endpointIds[]` | selected endpoints | proposal scope/order |
| `proposalId` | order proposal response | reorder/approve/reject |
| `proposal.rowVersion` | proposal response moi nhat | reorder/approve/reject |
| `refinementJobId` | LLM suggestion generate response | poll n8n refinement |
| `jobId` | generate tests response | poll full test generation |
| `suggestionId` + `suggestion.rowVersion` | suggestion list/detail | review approve/reject/modify |

## 2. Phase A - tao API order proposal

### 2.1 Create proposal

```http
POST /api/test-suites/{suiteId}/order-proposals
Content-Type: application/json
```

Request:

```json
{
  "specificationId": "0314ea2a-b24d-40c8-b880-a9e520fb5b84",
  "selectedEndpointIds": [
    "d3fa19ce-9e5d-4c47-89fa-95f1a0145d6d",
    "f7e273ac-7ef5-4f6c-93fb-694327f28664"
  ],
  "source": "Ai",
  "llmModel": "deepseek-chat",
  "reasoningNote": "Optional note shown to user"
}
```

Required values:

- `specificationId`: non-empty Guid.
- `selectedEndpointIds`: co the empty; backend fallback selected endpoints da luu trong suite.
- `source`: string enum `Ai | User | System | Imported`. Neu omit, backend default `Ai`.
- `llmModel`: optional, max 100 chars.
- `reasoningNote`: optional, max 2000 chars.

Response `201 Created`: `ApiTestOrderProposalModel`

```json
{
  "proposalId": "2546f586-669d-405e-a91d-a010d7b1ec5e",
  "testSuiteId": "7f081164-ba5d-455e-9bdd-150acbf105fa",
  "proposalNumber": 2,
  "status": "Pending",
  "source": "Ai",
  "proposedOrder": [
    {
      "endpointId": "d3fa19ce-9e5d-4c47-89fa-95f1a0145d6d",
      "httpMethod": "POST",
      "path": "/api/auth/login",
      "orderIndex": 1,
      "dependsOnEndpointIds": [],
      "reasonCodes": ["AUTH_FIRST"],
      "isAuthRelated": true
    }
  ],
  "userModifiedOrder": null,
  "appliedOrder": null,
  "aiReasoning": "Auth endpoint first.",
  "consideredFactors": {},
  "reviewedById": null,
  "reviewedAt": null,
  "reviewNotes": null,
  "appliedAt": null,
  "rowVersion": "AQIDBAUGBwg="
}
```

FE rules:

- Render `userModifiedOrder ?? proposedOrder` as current order.
- Save `proposalId` and `rowVersion`.
- `orderIndex` is 1-based and backend can normalize it again after reorder.

### 2.2 Load latest proposal

```http
GET /api/test-suites/{suiteId}/order-proposals/latest
```

Returns latest proposal by proposal number. It can be `Pending`, `Rejected`, `Superseded`, `Approved`, `ModifiedAndApproved`, or `Applied`; FE must check `status`.

### 2.3 Reorder proposal

```http
PUT /api/test-suites/{suiteId}/order-proposals/{proposalId}/reorder
Content-Type: application/json
```

Request:

```json
{
  "rowVersion": "AQIDBAUGBwg=",
  "orderedEndpointIds": [
    "f7e273ac-7ef5-4f6c-93fb-694327f28664",
    "d3fa19ce-9e5d-4c47-89fa-95f1a0145d6d"
  ],
  "reviewNotes": "User moved login before protected endpoint."
}
```

Required values:

- `rowVersion`: required base64 string.
- `orderedEndpointIds`: required list of endpoint IDs, should include the same endpoint set in the desired order.
- `reviewNotes`: optional max 4000 chars.

Response `200 OK`: updated `ApiTestOrderProposalModel`.

FE must replace local `rowVersion` with response `rowVersion`.

### 2.4 Approve proposal

```http
POST /api/test-suites/{suiteId}/order-proposals/{proposalId}/approve
Content-Type: application/json
```

Request:

```json
{
  "rowVersion": "BQUFBQUFBQU=",
  "reviewNotes": "Approved order."
}
```

Required values:

- `rowVersion`: required base64 string.
- `reviewNotes`: optional max 4000 chars.

Response `200 OK`: `ApiTestOrderProposalModel`.

Expected approved values:

- `status`: usually `Approved` or `ModifiedAndApproved`.
- `appliedOrder`: non-empty when gate is ready.
- `appliedAt`: not null when applied.

### 2.5 Gate status

```http
GET /api/test-suites/{suiteId}/order-gate-status
```

Response:

```json
{
  "testSuiteId": "7f081164-ba5d-455e-9bdd-150acbf105fa",
  "isGatePassed": true,
  "reasonCode": null,
  "activeProposalId": "2546f586-669d-405e-a91d-a010d7b1ec5e",
  "activeProposalStatus": 3,
  "orderSize": 3,
  "evaluatedAt": "2026-05-16T06:10:00Z"
}
```

Important:

- `activeProposalStatus` can be numeric in this model.
- FE should enable generation only when `isGatePassed === true`.
- If `isGatePassed === false`, show `reasonCode` and route user back to order review.

## 3. Phase B - LLM suggestion review flow

Dung flow nay neu FE muon user review LLM-suggested boundary/negative scenarios truoc khi materialize thanh test cases.

### 3.1 Generate draft + start async refinement

```http
POST /api/test-suites/{suiteId}/llm-suggestions/generate
Content-Type: application/json
```

Request:

```json
{
  "specificationId": "0314ea2a-b24d-40c8-b880-a9e520fb5b84",
  "forceRefresh": false,
  "algorithmProfile": {
    "enableBoundary": true,
    "enableNegative": true,
    "enableSecurity": true,
    "enablePerformance": true
  }
}
```

Required values:

- `specificationId`: non-empty Guid.
- `forceRefresh`: optional bool, default `false`.
- `algorithmProfile`: optional. If FE does not have toggles, omit it or send defaults.

Response `201 Created`: `GenerateLlmSuggestionPreviewResultModel`.

Values FE must read:

```json
{
  "testSuiteId": "7f081164-ba5d-455e-9bdd-150acbf105fa",
  "totalSuggestions": 3,
  "endpointsCovered": 3,
  "llmModel": "local-draft",
  "llmTokensUsed": null,
  "fromCache": false,
  "source": "local-draft",
  "refinementStatus": "pending",
  "refinementJobId": "a465978a-2bf6-40bf-b2a8-a0c596545bdd",
  "generatedAt": "2026-05-16T06:10:00Z",
  "suggestions": []
}
```

FE rules:

- Render draft suggestions immediately from `suggestions`.
- If `suggestions` is empty but `totalSuggestions > 0`, call `GET /llm-suggestions` to get persisted rows.
- If `refinementJobId` exists, start polling generation status.
- Do not treat `local-draft` as error.
- Do not show a 6-minute blocking spinner.

### 3.2 Poll LLM refinement job

```http
GET /api/test-suites/{suiteId}/generation-status?jobId={refinementJobId}
```

Response values:

```json
{
  "jobId": "a465978a-2bf6-40bf-b2a8-a0c596545bdd",
  "testSuiteId": "7f081164-ba5d-455e-9bdd-150acbf105fa",
  "status": "WaitingForCallback",
  "queuedAt": "2026-05-16T06:10:00Z",
  "triggeredAt": "2026-05-16T06:10:02Z",
  "completedAt": null,
  "testCasesGenerated": null,
  "errorMessage": null,
  "webhookName": "generate-llm-suggestions"
}
```

Status handling:

| status | FE behavior |
|---|---|
| `Queued` | show `Starting refinement`, poll again in 2-3s |
| `Triggering` | show `Sending to n8n`, poll again in 2-3s |
| `WaitingForCallback` | show `Refining`, poll again in 5s |
| `Completed` | stop poll, call `GET /llm-suggestions?reviewStatus=Pending` |
| `Failed` | stop poll, keep draft, show `Refine failed`, allow regenerate |
| `Cancelled` | stop poll, keep current suggestions |

Always pass `jobId`. If FE omits `jobId`, backend may return latest job for suite, which can be a different generation job.

### 3.3 List suggestions

```http
GET /api/test-suites/{suiteId}/llm-suggestions?reviewStatus=Pending&testType=Negative&endpointId={endpointId}
```

Query params:

- `reviewStatus`: optional string enum. Common values: `Pending`, `Approved`, `Rejected`, `ModifiedAndApproved`, `Superseded`.
- `testType`: optional string enum. Common values: `HappyPath`, `Boundary`, `Negative`, `Performance`, `Security`.
- `endpointId`: optional Guid.
- `includeDeleted`: optional bool, default `false`.

Response: `LlmSuggestionModel[]`.

Important fields:

- `id`: suggestion id for review route.
- `rowVersion`: required for review mutation.
- `reviewStatus`: only `Pending` suggestions can be reviewed.
- `suggestedRequest`, `suggestedExpectation`, `modifiedContent`: JSON string, FE must `JSON.parse`.
- `suggestedVariables`, `suggestedTags`: already arrays.
- `currentUserFeedback`, `feedbackSummary`: present mostly in list/detail.

### 3.4 Review one suggestion

```http
PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review
Content-Type: application/json
```

Approve request:

```json
{
  "action": "Approve",
  "rowVersion": "AQIDBAUGBwg=",
  "reviewNotes": "Looks good."
}
```

Reject request:

```json
{
  "action": "Reject",
  "rowVersion": "AQIDBAUGBwg=",
  "reviewNotes": "Not needed for this suite."
}
```

Modify and approve request:

```json
{
  "action": "Modify",
  "rowVersion": "AQIDBAUGBwg=",
  "reviewNotes": "Adjust expected status.",
  "modifiedContent": {
    "name": "POST /api/auth/login - invalid password returns 401",
    "description": "Use existing user with wrong password.",
    "testType": "Negative",
    "priority": "High",
    "tags": ["negative", "auth"],
    "request": {
      "httpMethod": "POST",
      "url": "/api/auth/login",
      "headers": {
        "Content-Type": "application/json"
      },
      "pathParams": {},
      "queryParams": {},
      "body": "{\"email\":\"user@example.com\",\"password\":\"wrong\"}"
    },
    "expectation": {
      "expectedStatus": [401],
      "bodyContains": ["invalid"],
      "bodyNotContains": [],
      "responseSchema": null,
      "headerChecks": {},
      "jsonPathChecks": {},
      "maxResponseTime": 1000
    },
    "variables": []
  }
}
```

Rules:

- `action`: `Approve | Reject | Modify`.
- `rowVersion`: required for all actions.
- `reviewNotes`: required when `Reject`.
- `modifiedContent`: required when `Modify`.
- `Modify` creates real test case and sets `reviewStatus = ModifiedAndApproved`.
- `Approve` creates real test case and sets `reviewStatus = Approved`.
- `Reject` does not create test case.
- After success, FE should update row from response or refetch list/test cases.

## 4. Phase C - direct test-case generation flow

Dung flow nay neu FE muon generate real `TestCase` rows directly, not via LLM suggestion review.

### 4.1 Generate all test cases through unified n8n workflow

Preferred route when BE config `UseDotnetIntegrationWorkflowForGeneration=true`:

```http
POST /api/test-suites/{suiteId}/generate-tests
```

No request body.

Response `202 Accepted`:

```json
{
  "jobId": "d07d3e2d-d2be-44ed-bb51-d178a52445f2",
  "testSuiteId": "7f081164-ba5d-455e-9bdd-150acbf105fa",
  "mode": "callback",
  "message": "Da tao job..."
}
```

Then poll:

```http
GET /api/test-suites/{suiteId}/generation-status?jobId={jobId}
```

When `status = Completed`, call:

```http
GET /api/test-suites/{suiteId}/test-cases
```

### 4.2 Generate happy path endpoint

```http
POST /api/test-suites/{suiteId}/test-cases/generate-happy-path
Content-Type: application/json
```

Request:

```json
{
  "specificationId": "0314ea2a-b24d-40c8-b880-a9e520fb5b84",
  "forceRegenerate": false
}
```

Response depends on BE config:

- If unified n8n workflow is enabled: `202 Accepted` with `GenerateTestsAcceptedResponse`, then FE polls `generation-status`.
- If disabled: `201 Created` with `GenerateHappyPathResultModel`.

FE must support both.

`GenerateHappyPathResultModel` shape:

```json
{
  "testSuiteId": "7f081164-ba5d-455e-9bdd-150acbf105fa",
  "totalGenerated": 3,
  "endpointsCovered": 3,
  "llmModel": "deepseek-chat",
  "tokensUsed": 1500,
  "generatedAt": "2026-05-16T06:20:00Z",
  "testCases": [
    {
      "testCaseId": "27ea8043-ef67-465e-9202-b898b3af81e8",
      "endpointId": "d3fa19ce-9e5d-4c47-89fa-95f1a0145d6d",
      "name": "Login succeeds",
      "httpMethod": "POST",
      "path": "/api/auth/login",
      "orderIndex": 1,
      "variableCount": 1
    }
  ]
}
```

### 4.3 Generate boundary/negative endpoint

```http
POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative
Content-Type: application/json
```

Request:

```json
{
  "specificationId": "0314ea2a-b24d-40c8-b880-a9e520fb5b84",
  "forceRegenerate": false,
  "includePathMutations": false,
  "includeBodyMutations": false,
  "includeLlmSuggestions": true
}
```

Response depends on BE config:

- If unified n8n workflow is enabled: `202 Accepted` with `GenerateTestsAcceptedResponse`, then FE polls `generation-status`.
- If disabled: `201 Created` with `GenerateBoundaryNegativeResultModel`.

`GenerateBoundaryNegativeResultModel` extra counters:

- `pathMutationCount`
- `bodyMutationCount`
- `llmSuggestionCount`
- `llmModel`
- `llmTokensUsed`

## 5. Phase D - read generated test cases

### 5.1 List test cases

```http
GET /api/test-suites/{suiteId}/test-cases?testType=HappyPath&includeDisabled=false&includeDeleted=false
```

Query params:

- `testType`: optional string enum `HappyPath | Boundary | Negative | Performance | Security`.
- `includeDisabled`: optional bool, default `false`.
- `includeDeleted`: optional bool, default `false`.

Response: `TestCaseModel[]`, sorted by `orderIndex asc`.

Important model notes:

- `request.headers`, `request.pathParams`, `request.queryParams`: JSON string, parse client-side.
- `expectation.expectedStatus`, `expectation.headerChecks`, `expectation.bodyContains`, `expectation.bodyNotContains`, `expectation.jsonPathChecks`: JSON string, parse client-side.
- `dependsOnIds`: array of test case IDs.
- `coveredRequirements`: SRS traceability if available.
- `rowVersion`: exists but current update/delete routes shown here do not require FE to send it.

### 5.2 Detail test case

```http
GET /api/test-suites/{suiteId}/test-cases/{testCaseId}
```

Use detail route when opening editor/debug view so FE has full request, expectation, variables, dependencies, and SRS context.

## 6. Error handling map

| Status | Typical reason | FE action |
|---|---|---|
| `400` | invalid Guid, missing rowVersion, invalid action, pending suggestions exist, quota exceeded | show problem message; for pending suggestions offer continue or regenerate with `forceRefresh=true` |
| `404` | suite/proposal/job/suggestion/test case not found | refetch parent screen; disable current action |
| `409 ORDER_CONFIRMATION_REQUIRED` | order gate not approved | navigate to order proposal approval screen |
| `409 CONCURRENCY_CONFLICT` | stale rowVersion | refetch latest proposal/suggestion and retry |
| poll `Failed` | n8n trigger/callback failed | keep draft/current list; allow retry |

## 7. FE state machine summary

```text
Select suite/spec/endpoints
  -> POST order-proposals
  -> optional PUT reorder
  -> POST approve
  -> GET order-gate-status
  -> if review-first:
       POST llm-suggestions/generate
       render source=local-draft
       poll generation-status(refinementJobId)
       on Completed -> GET llm-suggestions
       user reviews suggestions
       approved suggestions create TestCase rows
       GET test-cases
  -> if direct generation:
       POST generate-tests or POST test-cases/generate-*
       if 202 -> poll generation-status(jobId)
       on Completed -> GET test-cases
```

## 8. Common FE mistakes to avoid

- Do not call callback endpoints from browser.
- Do not omit `jobId` when polling `/generation-status`.
- Do not wait for n8n inside `POST /llm-suggestions/generate`.
- Do not treat `llmModel=local-draft` as failed generation.
- Do not reuse stale `rowVersion` after reorder/approve/review response.
- Do not assume `activeProposalStatus` is string; in gate status it can be numeric.
- Do not send invalid enum casing for display filters unless FE intentionally wants backend to ignore the filter.
- Do not forget to parse JSON-string fields before rendering request/expectation editors.
