# PHASE 1 PROMPT - Report Read Gateway For FE-10

Implement only the cross-module read layer needed by FE-10. Do not start TestReporting renderer/controller work yet.

## Scope

Projects allowed:

- `ClassifiedAds.Contracts`
- `ClassifiedAds.Modules.TestExecution`
- related unit tests in `ClassifiedAds.UnitTests/TestExecution`

## Goal

Create a contract-safe way for `ClassifiedAds.Modules.TestReporting` to read deterministic run-report context from `TestExecution` without accessing `TestExecutionDbContext` or distributed cache directly.

## Files To Add

Under `ClassifiedAds.Contracts/TestExecution/`:

- `DTOs/TestRunReportContextDto.cs`
- `Services/ITestRunReportReadGatewayService.cs`

Under `ClassifiedAds.Modules.TestExecution/Services/`:

- `TestRunReportReadGatewayService.cs`

## Required Gateway Method

```csharp
Task<TestRunReportContextDto> GetReportContextAsync(
    Guid testSuiteId,
    Guid runId,
    int recentHistoryLimit = 5,
    CancellationToken ct = default);
```

## Behavior Rules

The gateway must do all of this:

1. call existing `ITestExecutionReadGatewayService.GetSuiteAccessContextAsync(testSuiteId)`
2. load `TestRun` by `runId` and `testSuiteId`
3. reject missing run with `NotFoundException`
4. reject run when status is not `Completed` or `Failed` using `ConflictException("REPORT_RUN_NOT_READY", ...)`
5. reuse FE-07/08 cache-read rules:
   - `RedisKey` must exist
   - `ResultsExpireAt` must not be expired
   - distributed cache payload must exist
   - otherwise bubble `ConflictException("RUN_RESULTS_EXPIRED", ...)`
6. deserialize `TestRunResultModel` once
7. call existing `ITestExecutionReadGatewayService.GetExecutionContextAsync(testSuiteId, null)` once
8. load recent run history from the same suite ordered by `RunNumber DESC`
9. clamp `recentHistoryLimit` into a safe bounded range
10. return DTO with:
    - suite access context
    - run info
    - resolved environment name
    - recent run history
    - ordered endpoint ids
    - original test definitions
    - actual run results

## Rules

- Do NOT add new entities or migrations.
- Do NOT change existing TestExecution controller endpoints.
- Do NOT let the contract DTO expose EF entities.
- Do NOT query per test case or per failure reason inside loops.
- It is acceptable in v1 to load the full execution context once and map in memory, because this endpoint is on-demand and outside the hot execution loop.

## DI

Register `ITestRunReportReadGatewayService` in `ClassifiedAds.Modules.TestExecution/ServiceCollectionExtensions.cs`.

## Tests

Add unit tests for:

- missing run -> not found
- non-finished run -> `REPORT_RUN_NOT_READY`
- missing cached results -> `RUN_RESULTS_EXPIRED`
- expired cached results -> `RUN_RESULTS_EXPIRED`
- successful mapping returns definitions + actual results + recent history

Stop after this phase. Do not add TestReporting renderer/controller code yet.
