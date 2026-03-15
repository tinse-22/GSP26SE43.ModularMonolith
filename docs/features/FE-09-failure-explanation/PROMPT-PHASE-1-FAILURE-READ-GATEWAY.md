# PHASE 1 PROMPT - Failure Read Gateway For FE-09

Implement only the cross-module read layer needed by FE-09. Do not start LlmAssistant prompt/provider/controller work yet.

## Scope

Projects allowed:

- `ClassifiedAds.Contracts`
- `ClassifiedAds.Modules.TestExecution`
- related unit tests in `ClassifiedAds.UnitTests/TestExecution`

## Goal

Create a contract-safe way for `ClassifiedAds.Modules.LlmAssistant` to read deterministic failed-test context from `TestExecution` without accessing `TestExecutionDbContext` or distributed cache directly.

## Files To Add

Under `ClassifiedAds.Contracts/TestExecution/`:

- `DTOs/TestFailureExplanationContextDto.cs`
- `Services/ITestFailureReadGatewayService.cs`

Under `ClassifiedAds.Modules.TestExecution/Services/`:

- `TestFailureReadGatewayService.cs`

## Required Gateway Method

```csharp
Task<TestFailureExplanationContextDto> GetFailureExplanationContextAsync(
    Guid testSuiteId,
    Guid runId,
    Guid testCaseId,
    CancellationToken ct = default);
```

## Behavior Rules

The gateway must do all of this:

1. call existing `ITestExecutionReadGatewayService.GetSuiteAccessContextAsync(testSuiteId)`
2. load `TestRun` by `runId` and `testSuiteId`
3. reject missing run with `NotFoundException`
4. reuse FE-07/08 cache-read rules:
   - `RedisKey` must exist
   - `ResultsExpireAt` must not be expired
   - distributed cache payload must exist
   - otherwise bubble `ConflictException("RUN_RESULTS_EXPIRED", ...)`
5. deserialize `TestRunResultModel`
6. find the requested case by `testCaseId`
7. reject missing case with `NotFoundException`
8. reject case when `Status != "Failed"` using `ConflictException("TEST_CASE_NOT_FAILED", ...)`
9. call existing `ITestExecutionReadGatewayService.GetExecutionContextAsync(testSuiteId, null)` once
10. filter matching original test case definition in memory
11. return DTO with:
    - suite access context
    - run info
    - resolved environment name
    - original request/expectation
    - actual failed result

## Rules

- Do NOT add new entities or migrations.
- Do NOT change existing TestExecution controller endpoints.
- Do NOT let the contract DTO expose EF entities.
- Do NOT query per failure reason or per case inside loops.
- It is acceptable in v1 to load the full execution context once and filter `testCaseId`, because this endpoint is on-demand and outside the hot execution loop.

## DI

Register `ITestFailureReadGatewayService` in `ClassifiedAds.Modules.TestExecution/ServiceCollectionExtensions.cs`.

## Tests

Add unit tests for:

- missing run -> not found
- missing cached results -> `RUN_RESULTS_EXPIRED`
- expired cached results -> `RUN_RESULTS_EXPIRED`
- case not found in cached results -> not found
- case status Passed -> `TEST_CASE_NOT_FAILED`
- successful mapping returns original test case definition + actual failed result

Stop after this phase. Do not add prompt-builder/provider/controller code yet.
