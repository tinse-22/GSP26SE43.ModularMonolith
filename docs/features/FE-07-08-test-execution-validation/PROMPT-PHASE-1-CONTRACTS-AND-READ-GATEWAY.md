# PHASE 1 PROMPT - Contracts And Read Gateway For FE-07/08

Implement only the cross-module data access layer needed by FE-07/08. Do not start runtime execution services yet.

## Scope

Projects allowed:

- `ClassifiedAds.Contracts`
- `ClassifiedAds.Modules.TestGeneration`
- related unit tests in `ClassifiedAds.UnitTests/TestGeneration`

## Goal

Create a contract-safe way for `ClassifiedAds.Modules.TestExecution` to read execution-ready test suite data from `TestGeneration` without referencing entities or repositories directly.

## Files To Add

Under `ClassifiedAds.Contracts/TestGeneration/`:

- `DTOs/TestSuiteAccessContextDto.cs`
- `DTOs/TestSuiteExecutionContextDto.cs`
- `DTOs/ExecutionTestCaseDto.cs`
- `Services/ITestExecutionReadGatewayService.cs`

Under `ClassifiedAds.Modules.TestGeneration/Services/`:

- `TestExecutionReadGatewayService.cs`

## Required Gateway Methods

```csharp
Task<TestSuiteAccessContextDto> GetSuiteAccessContextAsync(Guid testSuiteId, CancellationToken ct = default);

Task<TestSuiteExecutionContextDto> GetExecutionContextAsync(
    Guid testSuiteId,
    IReadOnlyCollection<Guid> selectedTestCaseIds,
    CancellationToken ct = default);
```

## Behavior Rules

### GetSuiteAccessContextAsync

- load suite by id
- if not found -> `NotFoundException`
- return:
  - `TestSuiteId`
  - `ProjectId`
  - `ApiSpecId`
  - `CreatedById`
  - `Status`
  - `Name`

### GetExecutionContextAsync

Must do all of this:

1. call existing `IApiTestOrderGateService.RequireApprovedOrderAsync(testSuiteId)`
2. load enabled test cases for the suite only
3. if `selectedTestCaseIds` is non-empty:
   - all ids must belong to suite
   - all must be enabled
   - all transitive dependencies must also be selected
4. load requests, expectations, variables, dependencies in batch by `testCaseIds`
5. map to DTOs only
6. order deterministically by:
   - approved endpoint order from FE-05A
   - then `CustomOrderIndex ?? OrderIndex`
   - then `Name`
   - then `Id`

## Query Performance Rules

- absolutely no query-per-test-case loops
- one batch per table max
- in-memory dictionaries/grouping after batch load

## Validation Rules

Use Vietnamese messages for:

- suite not found
- selected test case not found in suite
- selected test case disabled
- dependency missing from selection

Suggested reason code for missing dependency subset:

- `INVALID_TEST_CASE_SELECTION`

## DI

Register `ITestExecutionReadGatewayService` in `ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs`.

## Tests

Add unit tests for:

- gate fail bubbles up conflict
- selected ids outside suite -> validation error
- selected disabled test case -> validation error
- missing dependency closure -> validation error
- deterministic ordering follows approved endpoint order first
- gateway loads all child data in batch-friendly shape

Stop after this phase. Do not add controller/orchestrator/executor code yet.
