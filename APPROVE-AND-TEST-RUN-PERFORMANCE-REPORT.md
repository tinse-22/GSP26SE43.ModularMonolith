# Approve API Test Order / Test Run Performance Report

## Scope

This report analyzes why these flows can still feel slow even though they do not call n8n:

- Approve testcase / approve API test order.
- Start / run API tests.

The reviewed code confirms the core observation:

- `ApproveApiTestOrderCommandHandler` validates suite/proposal ownership and status, applies `Approved` or `ModifiedAndApproved`, then saves `TestOrderProposal` and `TestSuite`.
- `StartTestRunCommandHandler` creates a `TestRun`, then directly awaits `_orchestrator.ExecuteAsync(...)`.
- `TestExecutionOrchestrator` resolves execution context, variables, pre-validation, real HTTP execution, validation, retries/replay, and result collection.
- `ClassifiedAds.Modules.TestExecution` does not inject or call `IN8nIntegrationService`.

## Key Source Evidence

### Approve API test order does not call n8n

File: `ClassifiedAds.Modules.TestGeneration/Commands/ApproveApiTestOrderCommand.cs`

- Constructor dependencies are:
  - `IRepository<TestSuite, Guid>`
  - `IRepository<TestOrderProposal, Guid>`
  - `IApiTestOrderService`
  - `ILogger<ApproveApiTestOrderCommandHandler>`
- There is no `IN8nIntegrationService` dependency.
- The handler:
  - loads the suite,
  - validates ownership,
  - loads the proposal,
  - deserializes proposed/user-modified order JSON,
  - updates status and audit fields,
  - saves proposal and suite in a transaction.

n8n is registered in `ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs`, but the approve handler does not use that registration.

### Run test does not call n8n

File: `ClassifiedAds.Modules.TestExecution/Commands/StartTestRunCommand.cs`

- `StartTestRunCommandHandler` has no `IN8nIntegrationService` dependency.
- It creates a `TestRun`, then awaits:

```csharp
command.Result = await _orchestrator.ExecuteAsync(...);
```

File: `ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs`

- The orchestrator calls:

```csharp
var response = await _httpExecutor.ExecuteAsync(resolvedRequest, ct);
```

File: `ClassifiedAds.Modules.TestExecution/Services/HttpTestExecutor.cs`

- `HttpTestExecutor` uses `IHttpClientFactory` and sends real HTTP requests with `client.SendAsync(...)`.
- It reads the response body, captures headers, and returns `HttpTestResponse`.

So the slow path is not n8n. The slow path is synchronous in-process orchestration plus real HTTP requests, retries/replay, DB reads/writes, and result serialization/persistence.

## Main Performance Findings

### 1. Test run API waits for the entire execution to finish

`StartTestRunCommandHandler` creates the run and then awaits the full orchestrator execution. That means the request duration includes:

- suite/context loading,
- environment resolution,
- endpoint metadata loading,
- every HTTP request,
- retry attempts,
- skipped-case replay scans,
- validation,
- Redis serialization/write,
- DB result persistence.

This is the highest-impact cause if users expect the API call to return quickly after pressing "Run".

Recommended improvement:

- Change `StartTestRunCommandHandler` into a fast command that creates a `Pending` run and enqueues/background-dispatches execution.
- Return `RunId` immediately.
- Let clients poll or subscribe to run status/results.

Expected impact:

- User-facing start latency drops from "full suite duration" to "create run duration".
- Actual execution duration remains, but it no longer blocks the HTTP request.

### 2. Test cases execute sequentially

`TestExecutionOrchestrator.ExecuteAsync` loops over `executionContext.OrderedTestCases` and awaits each case one by one.

This is safe for dependency and variable propagation, but it means total runtime is roughly the sum of all HTTP latencies plus validation/persistence overhead.

Recommended improvement:

- Keep dependency-sensitive cases sequential.
- Build a DAG from dependencies and run independent cases in bounded parallel batches.
- Add an execution policy flag such as:
  - `Sequential`
  - `ParallelIndependentCases`
  - `ParallelReadOnlyCases`
- Protect shared `VariableBag` writes with deterministic merge rules.

Expected impact:

- Large suites with many independent GET/negative tests can run much faster.
- Stateful happy-path chains can remain sequential.

### 3. Skipped-case replay scans the full suite after every case

Inside the main test loop, the orchestrator calls `ReplayEligibleSkippedCasesAsync(...)` after every executed case when `RerunSkippedCases` is enabled.

`ReplayEligibleSkippedCasesAsync` repeatedly scans `orderedCases` to find replayable skipped cases. For large suites, this creates expensive repeated full-suite scans even when no case is replayable.

Recommended improvement:

- Move replay to the end of the initial pass, or
- maintain a dependency-to-dependent lookup and only reconsider cases affected by a newly recovered dependency.

Expected impact:

- Less CPU overhead on large suites.
- Cleaner execution behavior with fewer repeated scans.

### 4. Startup validation performs avoidable DB work

`StartTestRunCommandHandler` does several DB/service calls before execution:

- `GetSuiteAccessContextAsync(...)`
- optional `GetTestCaseIdsBySuiteAsync(...)`
- environment lookup,
- load all current running runs via `ToListAsync(...)`, then count in memory,
- create run in a serializable transaction.

The in-memory count is avoidable:

```csharp
var runningCount = await _runRepository.ToListAsync(...);
var currentRunning = runningCount.Count;
```

Recommended improvement:

- Add/use `CountAsync` for concurrent run count.
- Avoid loading full `TestRun` entities just to count.
- Avoid duplicate suite access loading: `GetExecutionContextAsync(...)` calls `GetSuiteAccessContextAsync(...)` again inside the orchestrator path.
- Consider passing already-resolved suite/environment context into the orchestrator.

Expected impact:

- Lower DB latency before execution starts.
- Lower memory allocations.

### 5. Execution context loading uses multiple separate queries

`TestExecutionReadGatewayService.GetExecutionContextAsync(...)` loads:

- suite access context,
- approved order,
- enabled test cases,
- dependencies for selection expansion,
- requests,
- expectations,
- variables,
- dependencies again for final cases.

These are batched by table, which is good, but there are still multiple round trips. For big suites, this can be a visible part of startup latency.

Recommended improvement:

- Measure each query separately first.
- Avoid dependency double-load when selected cases are supplied.
- Use projections instead of full entities where possible.
- If repository supports it, use no-tracking reads for execution context materialization.

Expected impact:

- Moderate startup improvement, especially on remote DBs.

### 6. HTTP execution reads the entire response body before truncating

`HttpTestExecutor` truncates body after:

```csharp
var body = await response.Content.ReadAsStringAsync(ct);
```

This still loads the complete response into memory before applying the 64 KB cap.

Recommended improvement:

- Use `HttpCompletionOption.ResponseHeadersRead`.
- Stream-read only up to `MaxResponseBodyLength`.
- Mark truncation in the result model.

Expected impact:

- Lower memory and latency for large responses.
- Better protection against unexpectedly large API responses.

### 7. Result collection serializes and persists a large payload synchronously

`TestResultCollector.CollectAsync(...)`:

- builds full result models,
- serializes the entire `TestRunResultModel` to cache,
- writes to distributed cache,
- deletes existing DB case results,
- inserts all persisted case results,
- updates the run,
- saves DB changes.

Each result may include request headers, response headers, response body preview, failure reasons, extracted variables, dependency IDs, and validation flags.

Recommended improvement:

- Persist summary immediately, details asynchronously.
- Batch insert case results if repository/EF path supports it.
- Store large bodies in separate compressed payloads or only on failure.
- Default to smaller response previews for successful cases.
- Use a "details on demand" query path instead of returning all details in the start command response.

Expected impact:

- Faster completion path.
- Less Redis/DB pressure for large runs.

### 8. Real HTTP target latency is probably dominant

Since run test sends actual HTTP requests, performance depends on:

- target API cold start,
- network latency,
- server response time,
- authentication/setup chain,
- request timeout values,
- retry behavior,
- database state of the target API.

Recommended improvement:

- Add per-case timing breakdown:
  - pre-validation ms,
  - variable resolution ms,
  - HTTP latency ms,
  - post-validation ms,
  - extraction ms.
- Add run-level timing:
  - context load ms,
  - metadata load ms,
  - execution ms,
  - collection/cache ms,
  - DB persistence ms.
- Warm up target APIs before suite execution when running against cold hosts.

Expected impact:

- Distinguishes application overhead from target API latency.
- Prevents misdiagnosing n8n or orchestration when the real bottleneck is the tested API.

## Approve Flow Specific Recommendations

Approve is much smaller than run test. If approve is slow, likely causes are DB latency, repository transaction overhead, or frontend waiting for extra follow-up calls.

Recommended changes:

1. Add timing logs around:
   - suite load,
   - proposal load,
   - order JSON deserialize,
   - transaction save,
   - result mapping.
2. Load only required suite/proposal columns if repository supports projection.
3. Avoid deserializing both `UserModifiedOrder` and `ProposedOrder` if a cheap status/length check can determine the final source first.
4. Check frontend network waterfall after approve. The slow part may be refresh/reload of suite/order data after the approve API returns.

Do not optimize this path by removing n8n calls, because no n8n call exists here.

## Test Run Specific Recommendations

Priority order:

1. Make start-run asynchronous: create run, enqueue execution, return `RunId`.
2. Add timing instrumentation before changing behavior.
3. Replace running-run `ToListAsync(...).Count` with a database count.
4. Remove duplicate suite/environment/context loading.
5. Optimize skipped-case replay from repeated full-suite scans to dependency-driven replay.
6. Add bounded parallel execution for independent cases.
7. Stream/truncate response bodies while reading.
8. Persist large result details asynchronously or in batches.

## Suggested Metrics To Add

Add structured logs or OpenTelemetry spans with these names:

- `test_run.start.validate`
- `test_run.start.create_run`
- `test_run.context.load`
- `test_run.environment.resolve`
- `test_run.endpoint_metadata.load`
- `test_run.case.pre_validate`
- `test_run.case.variable_resolve`
- `test_run.case.http`
- `test_run.case.validate`
- `test_run.case.extract_variables`
- `test_run.replay_skipped`
- `test_run.result.cache_write`
- `test_run.result.db_persist`
- `api_test_order.approve.load_suite`
- `api_test_order.approve.load_proposal`
- `api_test_order.approve.save`

Minimum useful fields:

- `RunId`
- `TestSuiteId`
- `TestCaseId`
- `EndpointId`
- `HttpMethod`
- `UrlHost`
- `Status`
- `HttpStatusCode`
- `DurationMs`
- `AttemptNumber`
- `RetryReason`
- `ResponseBodyLength`
- `CaseCount`
- `AttemptCount`

## Conclusion

The current slowness should not be attributed to n8n for approve or run-test execution. The code path shows:

- approve is a DB transaction and JSON/status update path;
- run test is a synchronous full-suite executor that performs real HTTP calls and waits for all result persistence before returning.

The highest-impact fix is to decouple `StartTestRunCommandHandler` from synchronous execution and return immediately after creating/enqueuing the run. After that, optimize actual execution time with instrumentation, dependency-aware parallelism, replay-scan reduction, and lighter result persistence.

## Verification Notes

- This was a documentation/report-only change.
- No EF model, DbContext, migration, module registration, Dockerfile, or compose wiring was changed.
- Migration verification was not required.
- Docker registration verification was not required.
- GitNexus was available, but its index was stale. `npx gitnexus analyze` was attempted and timed out after about two minutes, so final findings rely on direct source inspection.
