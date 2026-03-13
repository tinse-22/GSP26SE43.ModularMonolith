# PHASE 3 PROMPT - Validation And Result Collection For FE-07/08

Implement the deterministic validator, result persistence, and API models/query surface after the execution engine exists.

## Scope

Project allowed:

- `ClassifiedAds.Modules.TestExecution`
- related unit/integration tests

## Files To Add

- `Models/TestRunModel.cs`
- `Models/TestRunResultModel.cs`
- `Models/TestCaseRunResultModel.cs`
- `Models/ValidationFailureModel.cs`
- `Models/TestCaseValidationResult.cs`
- `Services/IRuleBasedValidator.cs`
- `Services/RuleBasedValidator.cs`
- `Services/ITestResultCollector.cs`
- `Services/TestResultCollector.cs`
- `Queries/GetTestRunsQuery.cs`
- `Queries/GetTestRunQuery.cs`
- `Queries/GetTestRunResultsQuery.cs`

## Rule-Based Validator

Validation order:

1. transport error short-circuit -> `HTTP_REQUEST_ERROR`
2. status code
3. response schema
4. header checks
5. body contains
6. body not contains
7. JSONPath checks
8. max response time

Implementation rules:

- parse expectation JSON only once per validation call
- support simple JSONPath subset only; fail cleanly on unsupported path
- response schema fallback comes from `ApiEndpointMetadataDto.ResponseSchemaPayloads` when expectation schema empty
- use a dedicated schema-validation package if no local validator already exists
- do not use LLM or heuristic text guessing

## Result Collector

Responsibilities:

- convert internal results to `TestRunResultModel`
- truncate body preview to `65536` chars
- mask sensitive extracted variables
- serialize detail payload to distributed cache under `testrun:{id}:results`
- set `ResultsExpireAt`
- update `TestRun` counters and final status

Run status rules:

- `Completed` when `FailedCount == 0`
- `Failed` when `FailedCount > 0`

## Query Handlers

### GetTestRunsQuery

- paging mandatory
- DB projection only
- optional `status` filter

### GetTestRunQuery

- one-row summary read

### GetTestRunResultsQuery

- load `TestRun`
- verify access
- read cache by `RedisKey`
- if cache missing and `ResultsExpireAt < UtcNow` -> controlled `RUN_RESULTS_EXPIRED`

## Tests

Add unit tests for:

- status mismatch
- schema mismatch
- header mismatch
- body contains/not contains failures
- JSONPath equality
- response time exceeded
- cache write + summary update
- cache expired -> `RUN_RESULTS_EXPIRED`
- sensitive extracted variable masking

If package restore is needed for schema validation, keep it limited to `ClassifiedAds.Modules.TestExecution.csproj`.
