# FE-18E: Traceability Edit & Manage — FE Handoff

**Version:** 1.0.0  
**Date:** 2026-04-28  
**Base URL:** `http://localhost:5099`  
**Auth:** `Authorization: Bearer <JWT>`

---

## Tổng quan luồng đầy đủ

```
Upload SRS document
    └─> POST /api/projects/{projectId}/srs-documents
            ├─ testSuiteId: gắn vào suite ngay khi tạo (khuyến nghị)
            └─> srsDocumentId

Trigger LLM analysis
    └─> POST /api/projects/{projectId}/srs-documents/{srsDocumentId}/analyze
            └─> jobId

Poll job status
    └─> GET /api/projects/{projectId}/srs-documents/{srsDocumentId}/analysis-jobs/{jobId}
            └─> status: Queued|Triggering|Processing|Completed|Failed

Xem requirements đã extract
    └─> GET /api/projects/{projectId}/srs-documents/{srsDocumentId}/requirements

Review & chỉnh sửa requirement
    ├─> PATCH /api/projects/{projectId}/srs-documents/{srsDocumentId}/requirements/{requirementId}
    ├─> GET   /api/projects/{projectId}/srs-documents/{srsDocumentId}/requirements/{requirementId}
    ├─> POST  /api/projects/{projectId}/srs-documents/{srsDocumentId}/requirements    ← MỚI
    └─> DELETE /api/projects/{projectId}/srs-documents/{srsDocumentId}/requirements/{requirementId}  ← MỚI

Generate test cases (liên kết tự động vào requirements qua LLM)
    └─> POST /api/test-suites/{suiteId}/llm-suggestions/generate → review → save

Xem traceability matrix (read-only)
    └─> GET /api/projects/{projectId}/test-suites/{suiteId}/traceability?testRunId={optional}

Tạo / xóa link thủ công
    ├─> POST   /api/projects/{projectId}/test-suites/{suiteId}/traceability/links   ← MỚI
    └─> DELETE /api/projects/{projectId}/test-suites/{suiteId}/traceability/links/{linkId}   ← MỚI

Run tests → traceability tự cập nhật ValidationStatus
    └─> POST /api/test-runs (TestExecution module)
```

---

## API: Traceability Matrix (GET — đã có)

```
GET /api/projects/{projectId}/test-suites/{suiteId}/traceability
    ?testRunId={guid} (optional — nếu không truyền dùng latest run)
```

**Response:**
```json
{
  "testSuiteId": "guid",
  "srsDocumentId": "guid | null",
  "totalRequirements": 7,
  "coveredRequirements": 3,
  "uncoveredRequirements": 4,
  "coveragePercent": 42.9,
  "evidenceRunId": "guid | null",
  "validatedRequirements": 2,
  "violatedRequirements": 1,
  "partialRequirements": 0,
  "unverifiedRequirements": 0,
  "skippedOnlyRequirements": 0,
  "inconclusiveRequirements": 0,
  "validationPercent": 66.7,
  "requirements": [
    {
      "requirementId": "guid",
      "requirementCode": "REQ-001",
      "title": "System Health Check",
      "requirementType": 0,
      "confidenceScore": 0.9,
      "isReviewed": true,
      "isCovered": false,
      "validationStatus": 0,
      "validationSummary": "Uncovered",
      "passedTestCaseCount": 0,
      "failedTestCaseCount": 0,
      "skippedTestCaseCount": 0,
      "unverifiedTestCaseCount": 0,
      "testCases": []
    }
  ]
}
```

**ValidationStatus enum:**
| Value | Meaning |
|-------|---------|
| 0 | `Uncovered` — chưa có test case nào linked |
| 1 | `Unverified` — có test case nhưng chưa run |
| 2 | `Validated` — tất cả linked test cases đều Pass |
| 3 | `Violated` — ít nhất 1 test case Fail |
| 4 | `Partial` — có Pass và Skipped, không có Fail |
| 5 | `SkippedOnly` — tất cả test cases bị Skip |
| 6 | `Inconclusive` — không đủ thông tin |

---

## API: MỚI — Tạo manual traceability link

```
POST /api/projects/{projectId}/test-suites/{suiteId}/traceability/links
Content-Type: application/json
Authorization: Bearer <JWT>
```

**Request body:**
```json
{
  "testCaseId": "guid",       // test case phải thuộc suiteId này
  "srsRequirementId": "guid"  // requirement phải thuộc SRS document của suite
}
```

**Success: 201 Created**
```json
{
  "id": "guid",                 // linkId — dùng để DELETE sau này
  "testCaseId": "guid",
  "testCaseName": "Test login with valid credentials",
  "srsRequirementId": "guid",
  "requirementCode": "REQ-002",
  "traceabilityScore": null,    // null vì tạo thủ công (không phải LLM)
  "mappingRationale": "Manual link created by user."
}
```

**Errors:**
- `400` — link đã tồn tại / suite chưa có SRS document gắn / testCaseId không thuộc suite
- `404` — suite / testCase / requirement không tìm thấy

---

## API: MỚI — Xóa traceability link

```
DELETE /api/projects/{projectId}/test-suites/{suiteId}/traceability/links/{linkId}
Authorization: Bearer <JWT>
```

**Success: 204 No Content**

**Errors:**
- `404` — linkId không tồn thấy hoặc không thuộc suite này

---

## API: MỚI — Thêm requirement thủ công

```
POST /api/projects/{projectId}/srs-documents/{srsDocumentId}/requirements
Content-Type: application/json
Authorization: Bearer <JWT>
```

**Request body:**
```json
{
  "title": "string required",
  "description": "string optional",
  "requirementType": 0,
  "testableConstraints": "string | null (jsonb array string)",
  "endpointId": "guid | null"
}
```

**requirementType enum:** 0=Functional, 1=NonFunctional, 2=Security, 3=Performance, 4=Constraint

**Success: 201 Created** → trả về `srsRequirement` object

**Lưu ý:**
- `requirementCode` được auto-generate theo pattern `REQ-{N}` (tiếp theo sau count hiện tại)
- `isReviewed = true` (manually added = đã review)
- `confidenceScore = 1.0`

**Errors:**
- `400` — title trống
- `404` — srsDocumentId không tìm thấy

---

## API: MỚI — Xóa requirement

```
DELETE /api/projects/{projectId}/srs-documents/{srsDocumentId}/requirements/{requirementId}
Authorization: Bearer <JWT>
```

**Success: 204 No Content**

**Side effect:** tất cả `TestCaseRequirementLink` của requirement này cũng bị xóa.

**Errors:**
- `404` — requirement không tìm thấy

---

## API: MỚI — Lấy single requirement

```
GET /api/projects/{projectId}/srs-documents/{srsDocumentId}/requirements/{requirementId}
Authorization: Bearer <JWT>
```

**Success: 200 OK** → trả về `srsRequirement` object

---

## API: Cập nhật requirement (đã có — nhắc lại)

```
PATCH /api/projects/{projectId}/srs-documents/{srsDocumentId}/requirements/{requirementId}
Content-Type: application/json
```

**Request body (tất cả optional):**
```json
{
  "title": "string",
  "testableConstraints": "string",
  "endpointId": "guid | null",
  "isReviewed": true
}
```

---

## Workflow FE: Review & edit requirements

```
1. GET /api/projects/{projectId}/srs-documents/{srsDocumentId}
   → load requirements list từ response.requirements[]

2. User muốn edit 1 requirement:
   PATCH .../requirements/{requirementId}
   body: { isReviewed: true, endpointId: "..." }

3. User muốn xóa 1 requirement:
   DELETE .../requirements/{requirementId}
   → refresh requirements list

4. User muốn thêm requirement mới:
   POST .../requirements
   body: { title: "...", requirementType: 0 }
   → append vào list
```

---

## Workflow FE: Edit traceability links trong matrix

```
1. GET /api/projects/{projectId}/test-suites/{suiteId}/traceability
   → hiển thị matrix, mỗi requirement row có testCases[]

2. User muốn link thêm 1 test case vào requirement:
   POST .../traceability/links
   body: { testCaseId: "...", srsRequirementId: "..." }
   → link.id trả về, save lại để dùng cho DELETE

3. User muốn unlink:
   DELETE .../traceability/links/{linkId}
   → refresh matrix

4. Sau khi run test:
   GET .../traceability?testRunId={runId}
   → validationStatus, passedTestCaseCount, failedTestCaseCount cập nhật
```

---

## Lấy danh sách test cases trong suite (để populate dropdown khi tạo link)

```
GET /api/test-suites/{suiteId}/test-cases
Authorization: Bearer <JWT>
```

Response: `testCase[]` với mỗi item có `id`, `name`, `endpointId`, `isEnabled`.

---

## Lưu ý quan trọng cho FE

| Điều kiện | Hành động |
|-----------|-----------|
| `srsDocumentId == null` trên traceability response | Suite chưa được link với SRS document. Show UI hướng dẫn link SRS |
| `validationStatus == 0 (Uncovered)` | Requirement chưa có test case. Show "Add link" CTA |
| `traceabilityScore == null` trên link | Link do user tạo thủ công, không có LLM score |
| Muốn tạo link nhưng suite không có SRS | Backend trả 400 "Suite nay khong co SRS document". FE cần PATCH suite để attach SRS trước |
| `isReviewed == false` trên requirement | Cần user confirm lại requirement trước khi generate test |

---

## Permissions đã có

| Permission | Dùng cho |
|-----------|---------|
| `Permission:GetSrsDocuments` | GET SRS, GET requirements, GET traceability |
| `Permission:AddSrsDocument` | POST SRS, PATCH SRS |
| `Permission:DeleteSrsDocument` | DELETE SRS |
| `Permission:TriggerSrsAnalysis` | POST analyze, POST refine |
| `Permission:ManageSrsRequirements` | POST/PATCH/DELETE requirements |
| `Permission:GetSrsTraceability` | GET traceability |
| `Permission:ManageTraceabilityLinks` | POST/DELETE traceability links |

> **Status:** Tất cả 7 permissions trên đã được seed vào cả `AdminPermissions` và `UserPermissions` trong `RolePermissionMappings.cs`. Migration `20260427174200_AddSrsTraceabilityPermissions` đã được generate và verify.

---

## API: Cập nhật requirement (PATCH) — Hành vi đầy đủ

```
PATCH /api/projects/{projectId}/srs-documents/{srsDocumentId}/requirements/{requirementId}
Content-Type: application/json
Authorization: Bearer <JWT>
```

**Request body (tất cả optional):**
```json
{
  "title": "string",
  "testableConstraints": "string",
  "endpointId": "guid | null",
  "clearEndpointId": true,
  "isReviewed": true
}
```

### Quy tắc endpoint mapping

| Điều kiện trong request | Hành vi |
|------------------------|---------|
| `clearEndpointId: true` | `endpointId` của requirement được set về `null` (xóa mapping) — bất kể `endpointId` trong body có giá trị hay không |
| `clearEndpointId: false` (hoặc bỏ qua) + `endpointId: "guid"` | `endpointId` được cập nhật sang GUID mới |
| `clearEndpointId: false` (hoặc bỏ qua) + không có `endpointId` | `endpointId` giữ nguyên, không thay đổi |

> **Lý do:** `Guid?` không phân biệt được giữa "FE bỏ qua field" và "FE truyền null có chủ đích". `clearEndpointId: true` là cơ chế explicit để FE unmap endpoint. Pattern này giống hệt `UpdateSrsDocumentRequest.ClearTestSuiteId`.

### Ownership validation

Handler **bắt buộc** validate `SrsDocumentId` thuộc `ProjectId` và `!IsDeleted` trước khi update. Nếu không tìm thấy → `404 Not Found`.

---

## API: Link SRS document đến test suite (PATCH SRS document) — Cross-project guard

```
PATCH /api/projects/{projectId}/srs-documents/{srsDocumentId}
Content-Type: application/json
Authorization: Bearer <JWT> (Permission:AddSrsDocument)
```

**Validation bắt buộc:** Khi `testSuiteId` được cung cấp, BE phải validate `TestSuite.ProjectId == projectId`. Nếu suite không tồn tại hoặc thuộc project khác → `404 Not Found`. Không cho phép cross-project linking.

---

## API: Trigger SRS analysis — Clarification về behavior

```
POST /api/projects/{projectId}/srs-documents/{srsDocumentId}/analyze
Authorization: Bearer <JWT> (Permission:TriggerSrsAnalysis)
```

**Response: 202 Accepted**
```json
{
  "jobId": "guid",
  "message": "Analysis job queued. Poll /analysis-jobs/{jobId} for status."
}
```

> **Quan trọng:** Endpoint này **có thể block** cho đến khi n8n xử lý xong (synchronous-under-202). FE nên hiển thị loading spinner. Sau khi nhận 202, FE có thể poll `/analysis-jobs/{jobId}` để hiển thị status hoặc resume. Response **không phải** `SrsAnalysisJobModel` đầy đủ — chỉ có `{ jobId, message }` (type: `SrsAnalysisAcceptedResponse`).

---

## Testcase Evaluation: Trích dẫn SRS Requirement

Khi đánh giá kết quả pass/fail/skip cho từng test case, cần mapping rõ test case đó cover requirement nào. Dùng traceability matrix từ endpoint:

```
GET /api/projects/{projectId}/test-suites/{suiteId}/traceability?testRunId={runId}
```

### Cách trích dẫn requirement trong kết quả đánh giá

```
Test Case: "POST /api/orders — Happy Path"
  Status: Failed
  Requirement: REQ-003 — "System phải tạo đơn hàng và trả về 201 Created"
  Mapping Rationale: "Validates that order creation with valid payload returns 201"
  Failure: STATUS_CODE_MISMATCH — Expected 201, got 500
  SRS Reference: SRS §3.2 — Order Creation Endpoint
```

### Quy tắc bắt buộc cho LLM generate test cases

| Yêu cầu | Chi tiết |
|---------|---------|
| Đọc requirement trước khi generate | LLM phải đọc `requirements[]` của SRS document, không chỉ dựa vào API shape |
| Boundary từ SRS | Boundary condition phải dựa trên rule trong requirement (VD: `age >= 17`, `price > 0`) |
| Mapping rõ ràng | Mỗi test case phải có `mappingRationale` giải thích vì sao nó cover requirement nào |
| Valid + Invalid + Edge case | Phải cover cả 3 loại cho mỗi constraint trong requirement |
| Permission/business rule | Nếu requirement có rule về role/permission phải có test case cho unauthorized case |

---

## Lưu ý param quan trọng cho AI Agent / LLM

| Param | Đúng | Sai |
|-------|------|-----|
| Xóa endpointId mapping | `{ "clearEndpointId": true }` | `{ "endpointId": null }` — không hoạt động |
| Clear suite link trên SrsDocument | `{ "clearTestSuiteId": true }` | `{ "testSuiteId": null }` — không hoạt động |
| Test suite cross-project | Không được — BE trả 404 | Không truyền `testSuiteId` từ project khác |
| Traceability endpoint | `GET /api/projects/{projectId}/test-suites/{suiteId}/traceability?testRunId={runId}` | Không bỏ `projectId` trong path |
| Analyze response type | `{ "jobId": "guid", "message": "..." }` | Không phải `SrsAnalysisJobModel` đầy đủ |

---

## BE Fixes đã implement (2026-04-28)

### BE18E-001 ✅ Permission seed
- **File sửa:** `ClassifiedAds.Modules.Identity/Authorization/RolePermissionMappings.cs`
- **Thay đổi:** Thêm 7 SRS/traceability permissions vào cả `AdminPermissions` và `UserPermissions`
- **Migration:** `20260427174200_AddSrsTraceabilityPermissions` trong `ClassifiedAds.Migrator/Migrations/Identity/`
- **Verification:** `dotnet ... --verify-migrations` → PASSED

### BE18E-002 ✅ ClearEndpointId + project ownership trong PATCH requirement
- **Files sửa:**
  - `ClassifiedAds.Modules.TestGeneration/Models/Requests/SrsDocumentRequests.cs` — thêm `ClearEndpointId bool`
  - `ClassifiedAds.Modules.TestGeneration/Commands/UpdateSrsRequirementCommand.cs` — thêm `ClearEndpointId`, inject `IRepository<SrsDocument>`, validate ownership, implement clear semantics
  - `ClassifiedAds.Modules.TestGeneration/Controllers/SrsDocumentsController.cs` — truyền `ClearEndpointId` từ request sang command
- **Behavior mới:**
  - Handler validate `SrsDocumentId` thuộc `ProjectId && !IsDeleted` trước khi update
  - `clearEndpointId: true` → set `req.EndpointId = null`
  - `clearEndpointId: false` + `endpointId: guid` → set new value
  - `clearEndpointId: false` + không có `endpointId` → giữ nguyên

### BE18E-003 ✅ Cross-project guard trong UpdateSrsDocument
- **File sửa:** `ClassifiedAds.Modules.TestGeneration/Commands/UpdateSrsDocumentCommand.cs`
- **Thay đổi:** Khi `TestSuiteId` được cung cấp, validate `TestSuite.ProjectId == command.ProjectId`. Nếu không tìm thấy → `NotFoundException`
- **Trước:** Query suite chỉ theo `Id`, không check `ProjectId`
- **Sau:** Query suite theo `Id && ProjectId`, throw 404 nếu cross-project

### BE18E-004 ✅ Analyze endpoint response contract trung thực
- **Files sửa:**
  - `ClassifiedAds.Modules.TestGeneration/Models/SrsDocumentModel.cs` — thêm `SrsAnalysisAcceptedResponse` model
  - `ClassifiedAds.Modules.TestGeneration/Controllers/SrsDocumentsController.cs` — đổi `ProducesResponseType(typeof(SrsAnalysisJobModel))` → `ProducesResponseType(typeof(SrsAnalysisAcceptedResponse))`, cập nhật return type và return value
- **Lưu ý:** Behavior vẫn là synchronous-under-202. Không convert sang true async.

### BE18E-005 ⚠️ Tests (chưa implement)
- Unit/integration tests cho các guard mới chưa được thêm
- Cần bổ sung riêng: test permission mappings, UpdateSrsRequirement wrong-project, clearEndpointId, cross-project suite guard

---

