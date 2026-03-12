# FE-06 Assessment Report — Test Case CRUD & API Decomposition

Date: 2026-03-12
Branch: `feature/FE-06-boundary-negative-improvements`
Reviewer: AI Assessment (Claude Opus 4.6)

---

## 1. Executive Summary

| Area | Status | Verdict |
|------|--------|---------|
| Test Generation (Happy-path + Boundary/Negative) | PASS | Fully functional, 172/172 tests pass |
| API Spec Decomposition into Sub-APIs | PASS | OpenAPI/Postman/cURL parsers extract endpoints, parameters, responses, security correctly |
| CRUD for individual test cases (Add/Update/Delete) | **MISSING** | Controller only exposes Generate + Read; no manual CRUD |
| Previous compliance gaps (3 items from 2026-03-09) | **FIXED** | All 3 gaps resolved in current code |

---

## 2. Test Case CRUD Gap Analysis

### 2.1 Current API Surface (`TestCasesController.cs`)

| Endpoint | HTTP Method | Status |
|----------|-------------|--------|
| `/api/test-suites/{suiteId}/test-cases/generate-happy-path` | POST | EXISTS |
| `/api/test-suites/{suiteId}/test-cases/generate-boundary-negative` | POST | EXISTS |
| `/api/test-suites/{suiteId}/test-cases` | GET | EXISTS (list + filter) |
| `/api/test-suites/{suiteId}/test-cases/{testCaseId}` | GET | EXISTS (detail) |
| `/api/test-suites/{suiteId}/test-cases` | POST | **MISSING** (manual add) |
| `/api/test-suites/{suiteId}/test-cases/{testCaseId}` | PUT/PATCH | **MISSING** (update) |
| `/api/test-suites/{suiteId}/test-cases/{testCaseId}` | DELETE | **MISSING** (delete) |
| `/api/test-suites/{suiteId}/test-cases/{testCaseId}/toggle` | PATCH | **MISSING** (enable/disable) |
| `/api/test-suites/{suiteId}/test-cases/{testCaseId}/reorder` | PATCH | **MISSING** (reorder) |

### 2.2 Missing Commands

No `AddTestCaseCommand`, `UpdateTestCaseCommand`, or `DeleteTestCaseCommand` exist in `ClassifiedAds.Modules.TestGeneration/Commands/`.

The only write operations for test cases are:
- `GenerateHappyPathTestCasesCommand` — bulk generate via LLM
- `GenerateBoundaryNegativeTestCasesCommand` — bulk generate via rules + LLM
- `SaveAiGeneratedTestCasesCommand` — callback from n8n to save AI-generated cases

### 2.3 Impact

Users cannot:
- Manually create a test case outside of generation
- Edit a generated test case (name, description, priority, request, expectation)
- Delete a single test case
- Enable/disable individual test cases
- Reorder test cases manually

### 2.4 Existing Entity Support

The `TestCase` entity already supports all necessary fields for CRUD:
- `IsEnabled` (for toggle)
- `CustomOrderIndex` + `IsOrderCustomized` (for manual reorder)
- `LastModifiedById` + `Version` (for update tracking)
- `ChangeLogs` navigation property (for audit trail)

---

## 3. API Spec Decomposition Verification

### 3.1 OpenAPI Spec Parser (`OpenApiSpecificationParser.cs`)

| Capability | Status |
|-----------|--------|
| Detect OpenAPI 3.x vs Swagger 2.0 | PASS |
| Extract paths + operations (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS, TRACE) | PASS |
| Parse path-level + operation-level parameters with merge (operation overrides path) | PASS |
| Map parameter locations (Path, Query, Header, Cookie, Body) | PASS |
| Parse requestBody (OpenAPI 3.x `content.application/json.schema`) | PASS |
| Parse responses (status codes, schemas, headers, examples) | PASS |
| Parse security schemes (HTTP/Bearer, ApiKey, OAuth2, OpenIdConnect, Basic) | PASS |
| Parse security requirements per operation | PASS |
| Extract operationId, summary, description, tags, deprecated | PASS |
| Schema + examples + defaults extraction | PASS |
| Swagger 2.0 body parameter (`"in": "body"`) support | PASS |

### 3.2 Endpoint Metadata Services

| Service | Function | Status |
|---------|----------|--------|
| `ApiEndpointMetadataService` | Provides structured endpoint metadata (method, path, schemas) | PASS |
| `ApiEndpointParameterDetailService` | Provides per-parameter detail (name, type, format, required, default) | PASS |
| `PathParameterTemplateService` | Generates path parameter mutations (empty, specialChars, SQL injection, wrongType, boundary, overflow, etc.) | PASS |
| `PathParameterMutationGatewayService` | Cross-module facade for path mutations | PASS |

---

## 4. Previous Compliance Gaps — Current Status

### Gap #1: `ParameterDetails` not populated in n8n payload

**Status: FIXED**

Evidence:
- `LlmScenarioSuggestionContext.EndpointParameterDetails` exists as `IReadOnlyDictionary<Guid, EndpointParameterDetailDto>` (`ILlmScenarioSuggester.cs:36`)
- `BoundaryNegativeTestCaseGenerator.GenerateAsync` passes `parameterMap` to the context (`BoundaryNegativeTestCaseGenerator.cs:161`)
- `LlmScenarioSuggester.BuildN8nPayload` calls `BuildParameterDetails()` and populates `ParameterDetails` (`LlmScenarioSuggester.cs:207`)
- `BuildParameterDetails` maps all fields: Name, Location, DataType, Format, IsRequired, DefaultValue (`LlmScenarioSuggester.cs:313-332`)

### Gap #2: LLM variables dropped end-to-end

**Status: FIXED**

Evidence:
- `LlmSuggestedScenario.Variables` is `List<N8nTestCaseVariable>` (`ILlmScenarioSuggester.cs:78`)
- `ParseScenarios` maps `s.Variables` into `LlmSuggestedScenario` (`LlmScenarioSuggester.cs:241`)
- `BuildLlmSuggestionTestCase` creates `TestCaseVariable` from `scenario.Variables` with all fields: VariableName, ExtractFrom, JsonPath, HeaderName, Regex, DefaultValue (`BoundaryNegativeTestCaseGenerator.cs:343-358`)

### Gap #3: `TryParseJsonValue` does not handle arrays/objects

**Status: FIXED**

Evidence:
- `ConvertJsonElement` handles all `JsonValueKind` cases (`BodyMutationEngine.cs:402-415`):
  - `Number` → `long` or `double`
  - `True`/`False` → `bool`
  - `String` → `string`
  - `Array` → `List<object>` via recursive call
  - `Object` → `Dictionary<string, object>` via recursive call
  - `Null` → `null`

---

## 5. Test Execution Results

```
dotnet test ClassifiedAds.UnitTests --filter "FullyQualifiedName~TestGeneration"

Passed!  - Failed: 0, Passed: 172, Skipped: 0, Total: 172
```

### Test Coverage Breakdown (FE-06 specific)

| Test Class | Tests | Status |
|-----------|-------|--------|
| `BodyMutationEngineTests` | 16 | All pass |
| `BoundaryNegativeTestCaseGeneratorTests` | 12 | All pass |
| `LlmScenarioSuggesterTests` | 12 | All pass |
| `GenerateBoundaryNegativeTestCasesCommandHandlerTests` | 16 | All pass |

---

## 6. Conclusion & Recommendations

### What is complete and working:
1. FE-06 boundary/negative test case generation (3-pipeline: path mutations, body mutations, LLM suggestions)
2. API spec decomposition from OpenAPI/Swagger/Postman/cURL into structured endpoints and parameters
3. All 3 previously-identified compliance gaps have been resolved
4. 172/172 unit tests pass

### What is MISSING and needs implementation:
**Individual test case CRUD operations (Add, Update, Delete, Toggle, Reorder)**

This is required so users can:
- Manually create custom test cases beyond AI generation
- Edit generated test cases to adjust request/expectation details
- Delete unwanted test cases
- Enable/disable specific test cases
- Manually reorder test cases

---

## 7. Re-Implementation Prompt for AI Agent

### Objective

Implement individual test case CRUD endpoints for `TestCasesController` to complement the existing generation-only API. The entity model already supports all required fields.

### Non-Negotiable Constraints

- Keep all changes inside `ClassifiedAds.Modules.TestGeneration` and `ClassifiedAds.UnitTests/TestGeneration`
- Do NOT add new EF entities or migrations (the `TestCase`, `TestCaseRequest`, `TestCaseExpectation`, `TestCaseVariable` entities already exist)
- Do NOT access other modules' DbContexts or repositories directly
- Reuse existing contracts only
- Follow existing FE-05B and FE-06 code patterns (Dispatcher/Command pattern, Vietnamese error messages, same authorization pattern)
- Maintain the `TestCaseChangeLog` and `Version` tracking pattern already used in FE-06 generation
- Do NOT break any existing tests (172 tests must continue to pass)

### Implementation Tasks

#### Task 1: Add Test Case Manually (`POST`)

Create `AddUpdateTestCaseCommand` and handler:

```
POST /api/test-suites/{suiteId}/test-cases
Authorization: [Authorize(Permissions.GenerateTestCases)] or new permission
```

Request model fields:
- `EndpointId` (optional Guid)
- `Name` (required, max 200 chars)
- `Description` (optional)
- `TestType` (enum: HappyPath, Boundary, Negative, Performance, Security)
- `Priority` (enum: Critical, High, Medium, Low)
- `IsEnabled` (default true)
- `Tags` (optional, JSON array)
- `Request` (sub-object: HttpMethod, Url, Headers, PathParams, QueryParams, BodyType, Body, Timeout)
- `Expectation` (sub-object: ExpectedStatus, ResponseSchema, HeaderChecks, BodyContains, BodyNotContains, JsonPathChecks, MaxResponseTime)
- `Variables` (optional list of variable definitions)

Handler validations:
1. Verify `TestSuiteId` is not empty
2. Verify suite exists and belongs to `CurrentUserId`
3. Verify suite is not archived
4. Create `TestCase`, `TestCaseRequest`, `TestCaseExpectation`, `TestCaseVariable` entities
5. Set `OrderIndex` = max existing OrderIndex + 1
6. Create `TestCaseChangeLog` entry with `ChangeType = "Created"` and `ChangedBy = CurrentUserId`
7. Increment suite version
8. Return `201 Created` with test case detail

#### Task 2: Update Test Case (`PUT`)

Create `UpdateTestCaseCommand` and handler:

```
PUT /api/test-suites/{suiteId}/test-cases/{testCaseId}
Authorization: [Authorize(Permissions.GenerateTestCases)] or new permission
```

Same request model as Add. Handler validations:
1. Verify suite and test case exist
2. Verify ownership
3. Verify suite not archived
4. Update all fields on `TestCase`, `TestCaseRequest`, `TestCaseExpectation`
5. Replace `TestCaseVariable` collection
6. Set `LastModifiedById = CurrentUserId`, increment `Version`
7. Create `TestCaseChangeLog` with `ChangeType = "Updated"`
8. Return `200 OK` with updated test case detail

#### Task 3: Delete Test Case (`DELETE`)

Create `DeleteTestCaseCommand` and handler:

```
DELETE /api/test-suites/{suiteId}/test-cases/{testCaseId}
Authorization: [Authorize(Permissions.GenerateTestCases)] or new permission
```

Handler validations:
1. Verify suite and test case exist
2. Verify ownership
3. Verify suite not archived
4. Delete `TestCaseRequest`, `TestCaseExpectation`, `TestCaseVariable` (cascade), and `TestCase`
5. Create `TestCaseChangeLog` with `ChangeType = "Deleted"`
6. Recalculate `OrderIndex` for remaining test cases in the suite
7. Return `204 No Content`

#### Task 4: Toggle Test Case (`PATCH /toggle`)

```
PATCH /api/test-suites/{suiteId}/test-cases/{testCaseId}/toggle
Authorization: [Authorize(Permissions.GenerateTestCases)]
```

Request: `{ "isEnabled": true/false }`

Handler:
1. Verify suite and test case exist
2. Verify ownership
3. Toggle `IsEnabled`
4. Create `TestCaseChangeLog` with `ChangeType = "Toggled"`
5. Return `200 OK`

#### Task 5: Reorder Test Case (`PATCH /reorder`)

```
PATCH /api/test-suites/{suiteId}/test-cases/reorder
Authorization: [Authorize(Permissions.GenerateTestCases)]
```

Request: `{ "testCaseIds": ["guid1", "guid2", ...] }` (ordered list)

Handler:
1. Verify all test case IDs belong to the suite
2. Verify ownership
3. Set `CustomOrderIndex` = position index, `IsOrderCustomized = true`
4. Create `TestCaseChangeLog` for each with `ChangeType = "Reordered"`
5. Return `200 OK`

### Required Unit Tests

For each new command handler, create tests following the existing pattern in `GenerateBoundaryNegativeTestCasesCommandHandlerTests.cs`:

**AddUpdateTestCaseCommand tests:**
- Throw `ValidationException` when `TestSuiteId` is empty
- Throw `ValidationException` when `Name` is empty or exceeds 200 chars
- Throw `NotFoundException` when suite does not exist
- Throw `ValidationException` when not suite owner
- Throw `ValidationException` when suite is archived
- Successfully create test case with Request, Expectation, Variables
- Create `TestCaseChangeLog` entry
- Increment suite version
- Set correct `OrderIndex`

**UpdateTestCaseCommand tests:**
- Throw `NotFoundException` when test case not found
- Throw `ValidationException` when not suite owner
- Successfully update all fields
- Increment `Version`
- Replace variable collection
- Create `TestCaseChangeLog`

**DeleteTestCaseCommand tests:**
- Throw `NotFoundException` when test case not found
- Throw `ValidationException` when not suite owner
- Successfully delete test case and related entities
- Recalculate `OrderIndex` for remaining cases
- Create `TestCaseChangeLog`

**ToggleTestCaseCommand tests:**
- Toggle `IsEnabled` from true to false
- Toggle `IsEnabled` from false to true
- Create `TestCaseChangeLog`

**ReorderTestCaseCommand tests:**
- Throw `ValidationException` when test case IDs don't belong to suite
- Successfully set `CustomOrderIndex` and `IsOrderCustomized`
- Create `TestCaseChangeLog` for each case

### Permission Constants

Add to `Authorization/Permissions.cs`:
```csharp
public const string AddTestCase = "Permission:AddTestCase";
public const string UpdateTestCase = "Permission:UpdateTestCase";
public const string DeleteTestCase = "Permission:DeleteTestCase";
```

Or reuse existing `Permissions.GenerateTestCases` if the team prefers a simpler permission model.

### Acceptance Checklist

- [ ] `POST /api/test-suites/{suiteId}/test-cases` creates a test case with request, expectation, and variables
- [ ] `PUT /api/test-suites/{suiteId}/test-cases/{id}` updates all test case fields
- [ ] `DELETE /api/test-suites/{suiteId}/test-cases/{id}` removes a test case and recalculates order
- [ ] `PATCH /api/test-suites/{suiteId}/test-cases/{id}/toggle` toggles IsEnabled
- [ ] `PATCH /api/test-suites/{suiteId}/test-cases/reorder` sets custom order
- [ ] All new endpoints have authorization and ownership validation
- [ ] Suite archived check is enforced on all write operations
- [ ] `TestCaseChangeLog` is created for every mutation
- [ ] Suite version is incremented on add/update/delete
- [ ] All existing 172 tests continue to pass
- [ ] New unit tests cover all validation paths and success paths
- [ ] No new EF migrations are required
- [ ] No new cross-module interfaces are introduced
- [ ] Vietnamese error messages are used for user-facing validations
