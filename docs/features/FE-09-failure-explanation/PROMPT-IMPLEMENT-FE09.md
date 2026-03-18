# TASK: Implement FE-09 - On-Demand LLM Failure Explanation

## CONTEXT

You are implementing FE-09 in a .NET 10 Modular Monolith codebase (ASP.NET Core Web API, EF Core, PostgreSQL, CQRS via Dispatcher). Read `AGENTS.md` at repository root before touching code.

Primary module: `ClassifiedAds.Modules.LlmAssistant`
Supporting modules: `ClassifiedAds.Modules.TestExecution`, `ClassifiedAds.Modules.ApiDocumentation`
Contract project: `ClassifiedAds.Contracts`

Read these spec files first:

- `docs/features/FE-09-failure-explanation/requirement.json`
- `docs/features/FE-09-failure-explanation/workflow.json`
- `docs/features/FE-09-failure-explanation/contracts.json`
- `docs/features/FE-09-failure-explanation/implementation-map.json`
- `docs/features/FE-09-failure-explanation/README.md`

For future scope only, also be aware of:

- `docs/features/FE-09-acceptance-criteria/FE-09-01/requirement.json`

That async queue workflow is NOT the implementation target for this pass.

## HARD CONSTRAINTS

- MUST keep FE-09 v1 synchronous and on-demand.
- MUST NOT implement RabbitMQ consumer, Background worker, retry queue, or dead-letter workflow in this pass.
- MUST NOT add new entities, tables, or EF migrations.
- MUST reuse `LlmInteraction` and `LlmSuggestionCache`.
- MUST keep deterministic pass/fail owned by FE-08. FE-09 is advisory only.
- MUST NOT let `ClassifiedAds.Modules.LlmAssistant` access `TestExecutionDbContext`, `TestRun` repository, or `IDistributedCache` directly.
- MUST add a cross-module read gateway in `ClassifiedAds.Contracts/TestExecution`, implemented inside `ClassifiedAds.Modules.TestExecution`.
- MUST sanitize secrets before prompt building, audit logging, and cache writes.
- MUST reuse existing permission string `Permission:GetTestRuns` for FE-09 v1.
- MUST default to ASCII in new files.

## REQUIRED API SURFACE

Implement these endpoints in `ClassifiedAds.Modules.LlmAssistant`:

1. `GET /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
   - Permission: `Permission:GetTestRuns`
   - Response: `200 OK` + `FailureExplanationModel`
   - If no cached explanation: controlled `404` via `NotFoundException` message prefixed with `FAILURE_EXPLANATION_NOT_FOUND:`

2. `POST /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
   - Permission: `Permission:GetTestRuns`
   - Request body: none
   - Response: `200 OK` + `FailureExplanationModel`
   - Behavior: return cached explanation if fingerprint hit; otherwise generate live explanation and cache it

## ARCHITECTURE TO IMPLEMENT

### 1. New cross-module failure read gateway

Create new contract DTOs/interfaces under `ClassifiedAds.Contracts/TestExecution/`:

- `DTOs/TestFailureExplanationContextDto.cs`
- `Services/ITestFailureReadGatewayService.cs`

Implement the gateway in `ClassifiedAds.Modules.TestExecution/Services/TestFailureReadGatewayService.cs`.

The gateway must:

- load suite access context via existing `ITestExecutionReadGatewayService.GetSuiteAccessContextAsync(testSuiteId)`
- load `TestRun` by `suiteId + runId`
- read detailed run payload from TestExecution cache using existing FE-07/08 storage pattern
- bubble `RUN_RESULTS_EXPIRED` if cache payload is gone or expired
- locate the requested test case inside cached results
- reject non-failed cases with `TEST_CASE_NOT_FAILED`
- load original test case definition by calling existing `ITestExecutionReadGatewayService.GetExecutionContextAsync(testSuiteId, null)` once and filtering by `testCaseId`
- return DTO only

Do NOT expose entities or cache implementation types.

### 2. LlmAssistant runtime services

Add these services:

- `ILlmFailureExplainer` / `LlmFailureExplainer`
- `IFailureExplanationFingerprintBuilder` / `FailureExplanationFingerprintBuilder`
- `IFailureExplanationSanitizer` / `FailureExplanationSanitizer`
- `IFailureExplanationPromptBuilder` / `FailureExplanationPromptBuilder`
- `ILlmFailureExplanationClient` / `N8nFailureExplanationClient` (or equivalent provider-specific implementation)

Rationale:

- keep failure-context access separate from LLM runtime
- keep fingerprinting separate from prompt construction
- keep secret masking separate from provider logic
- keep provider I/O separate from orchestration

### 3. Reuse cache and audit tables

- `LlmInteraction.InteractionType = FailureExplanation`
- `LlmSuggestionCache.SuggestionType = FailureExplanation`
- no new table
- no new migration

Cache rules:

- endpoint-based cache entry when `EndpointId` exists
- use `Guid.Empty` as fallback cache partition when `EndpointId == null`
- `CacheKey` must be deterministic from sanitized failure context
- TTL = 24h

### 4. API + CQRS surface

Add:

- `Controllers/FailureExplanationsController.cs`
- `Commands/ExplainTestFailureCommand.cs`
- `Queries/GetFailureExplanationQuery.cs`
- `Models/FailureExplanationModel.cs`

Add config under `Modules:LlmAssistant` for provider/model/webhook/timeout/cache TTL.

## IMPLEMENTATION ORDER

1. Contracts/TestExecution failure-read DTOs + interface
2. TestExecution failure-read gateway + DI
3. LlmAssistant models + runtime services + cache/audit reuse
4. LlmAssistant command/query/controller + config
5. Unit tests
6. Targeted verification

## DETAILED REQUIREMENTS

### A. Failure read gateway behavior

Method signature:

```csharp
Task<TestFailureExplanationContextDto> GetFailureExplanationContextAsync(
    Guid testSuiteId,
    Guid runId,
    Guid testCaseId,
    CancellationToken ct = default);
```

Behavior rules:

1. Load suite access context first.
2. Load run by `runId` and `testSuiteId`.
3. Validate run exists.
4. Validate `RedisKey` exists and `ResultsExpireAt` is not expired.
5. Read cached `TestRunResultModel`.
6. Find requested case in `Cases`.
7. If not found -> `NotFoundException`.
8. If `case.Status != "Failed"` -> `ConflictException("TEST_CASE_NOT_FAILED", ...)`
9. Load original test case definition from `GetExecutionContextAsync(testSuiteId, null)`.
10. Return DTO with:
    - suite/project/spec/user ownership context
    - run info and resolved environment name
    - original test case request/expectation
    - actual failed result including failure reasons and validation flags

### B. Fingerprint and cache rules

Fingerprint must include sanitized versions of:

- `endpointId`
- `resolvedUrl`
- `httpStatusCode`
- `failureReasons` codes/targets/expected/actual
- expectation payload
- response body preview

Required properties:

- same deterministic failure context -> same cache key
- masked-secret differences must not leak raw secret values
- cache lookup happens before prompt building

### C. Prompt and provider contract

Prompt must clearly separate:

- deterministic verdict from FE-08
- original test definition
- actual response/result
- endpoint metadata when available
- instruction that the model must NOT decide pass/fail

Provider response must be structured JSON with:

```json
{
  "summaryVi": "string",
  "possibleCauses": ["string"],
  "suggestedNextActions": ["string"],
  "confidence": "Low|Medium|High",
  "model": "string",
  "tokensUsed": 0
}
```

If provider returns invalid payload:

- do not cache
- throw controlled FE-09 error

### D. Audit and graceful degradation

For live generation:

- save `LlmInteraction`
- cache explanation

Graceful behavior:

- audit save failure -> log warning, still return explanation
- cache save failure -> log warning, still return explanation
- provider failure -> no cache write, bubble controlled error

### E. API semantics

`GET`:

- owner must match current user via `CreatedById`
- must only return cached explanation
- if cache miss -> `NotFoundException` message prefixed with `FAILURE_EXPLANATION_NOT_FOUND:`

`POST`:

- owner must match current user via `CreatedById`
- cache hit -> return cached model with `Source = "cache"`
- cache miss -> live generation with `Source = "live"`

Both endpoints must bubble:

- `RUN_RESULTS_EXPIRED`
- `TEST_CASE_NOT_FAILED`

## TESTS

Add at least these unit test groups:

- `TestFailureReadGatewayServiceTests`
- `FailureExplanationFingerprintBuilderTests`
- `FailureExplanationSanitizerTests`
- `FailureExplanationPromptBuilderTests`
- `LlmFailureExplainerTests`
- `ExplainTestFailureCommandHandlerTests`
- `GetFailureExplanationQueryHandlerTests`

## VERIFICATION

At minimum run:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.LlmAssistant'
```

And:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.TestExecution'
```

If the repo naming differs for the new tests, use the nearest targeted filter and report exactly what you ran.

## DONE CRITERIA

- GET and POST explanation APIs exist and are wired
- failure-read gateway exists in Contracts/TestExecution and TestExecution module
- LlmAssistant runtime reuses cache + audit tables
- no migrations were added
- secrets are masked before prompt/audit/cache
- targeted tests were added and executed
