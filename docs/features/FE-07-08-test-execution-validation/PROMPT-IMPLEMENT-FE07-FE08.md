# TASK: Implement FE-07 + FE-08 - Test Execution And Deterministic Validation

## CONTEXT

You are implementing FE-07 + FE-08 in a .NET 10 Modular Monolith codebase (ASP.NET Core Web API, EF Core, PostgreSQL, CQRS via Dispatcher). Read `AGENTS.md` at repository root before touching code.

Primary module: `ClassifiedAds.Modules.TestExecution`
Supporting module: `ClassifiedAds.Modules.TestGeneration`
Contract project: `ClassifiedAds.Contracts`

Read these spec files first:

- `docs/features/FE-07-08-test-execution-validation/requirement.json`
- `docs/features/FE-07-08-test-execution-validation/FE-07/requirement.json`
- `docs/features/FE-07-08-test-execution-validation/FE-08/requirement.json`
- `docs/features/FE-07-08-test-execution-validation/workflow.json`
- `docs/features/FE-07-08-test-execution-validation/contracts.json`
- `docs/features/FE-07-08-test-execution-validation/implementation-map.json`

## HARD CONSTRAINTS

- MUST keep `ClassifiedAds.Modules.TestExecution` as the primary runtime module.
- MUST NOT add new entities, tables, or EF migrations.
- MUST reuse existing `ExecutionEnvironment` and `TestRun`.
- MUST NOT let `ClassifiedAds.Modules.TestExecution` access `TestGenerationDbContext` or `IRepository<TestCase,...>` directly.
- MUST add a cross-module read gateway in `ClassifiedAds.Contracts/TestGeneration`, implemented inside `ClassifiedAds.Modules.TestGeneration`.
- MUST keep detailed test results in distributed cache keyed by `TestRun.RedisKey`; PostgreSQL stores summary only.
- MUST keep pass/fail fully rule-based. LLM is forbidden in FE-07/08 runtime decisions.
- MUST avoid N+1 queries when loading execution graph.
- User-facing validation/error messages MUST stay Vietnamese.
- Default to ASCII in new files.

## REQUIRED API SURFACE

Implement these endpoints in `TestRunsController`:

1. `POST /api/test-suites/{suiteId}/test-runs`
   - Permission: `Permission:StartTestRun`
   - Request: `StartTestRunRequest`
   - Response: `201 Created` + `TestRunResultModel`

2. `GET /api/test-suites/{suiteId}/test-runs`
   - Permission: `Permission:GetTestRuns`
   - Query: `pageNumber`, `pageSize`, `status?`
   - Response: `200 OK` + paged `TestRunModel`

3. `GET /api/test-suites/{suiteId}/test-runs/{runId}`
   - Permission: `Permission:GetTestRuns`
   - Response: `200 OK` + `TestRunModel`

4. `GET /api/test-suites/{suiteId}/test-runs/{runId}/results`
   - Permission: `Permission:GetTestRuns`
   - Response: `200 OK` + `TestRunResultModel`
   - If cache expired: controlled `ConflictException("RUN_RESULTS_EXPIRED", ...)`

## ARCHITECTURE TO IMPLEMENT

### 1. New cross-module read gateway

Create new contract DTOs/interfaces under `ClassifiedAds.Contracts/TestGeneration/`:

- `DTOs/TestSuiteAccessContextDto.cs`
- `DTOs/TestSuiteExecutionContextDto.cs`
- `DTOs/ExecutionTestCaseDto.cs`
- `Services/ITestExecutionReadGatewayService.cs`

Implement the gateway in `ClassifiedAds.Modules.TestGeneration/Services/TestExecutionReadGatewayService.cs`.

The gateway must:

- load suite access context (owner, project, api spec, status)
- enforce FE-05A gate via existing `IApiTestOrderGateService`
- load enabled test cases + request + expectation + variables + dependencies in batch
- validate `selectedTestCaseIds`
- reject missing dependency closure
- return deterministic ordered DTOs

Do NOT return entities. Return DTOs only.

### 2. TestExecution runtime services

Add these services:

- `ITestExecutionOrchestrator` / `TestExecutionOrchestrator`
- `IExecutionEnvironmentRuntimeResolver` / `ExecutionEnvironmentRuntimeResolver`
- `IVariableResolver` / `VariableResolver`
- `IHttpTestExecutor` / `HttpTestExecutor`
- `IVariableExtractor` / `VariableExtractor`
- `IRuleBasedValidator` / `RuleBasedValidator`
- `ITestResultCollector` / `TestResultCollector`

Rationale:

- keep runtime auth separate from config validation
- keep placeholder resolution separate from HTTP transport
- keep validation separate from transport
- keep result persistence separate from execution

### 3. Result storage pattern

- PostgreSQL: `testexecution.TestRuns` only
- Cache: detailed result payload under `testrun:{testRunId}:results`
- `ResultsExpireAt` = `UtcNow + retentionDays`
- response body stored in result payload must be truncated to `65536` chars max

## IMPLEMENTATION ORDER

1. Contracts/TestGeneration gateway DTOs + interface
2. Gateway implementation in TestGeneration + DI registration
3. TestExecution request/response/internal models
4. Permissions + controller + command/query surface
5. Runtime environment resolver
6. Variable resolver
7. HTTP executor
8. Variable extractor
9. Rule-based validator
10. Result collector
11. StartTestRun command handler + query handlers
12. Unit + integration tests

## DETAILED REQUIREMENTS

### A. StartTestRun preflight

Command handler pipeline:

1. Validate `TestSuiteId != Guid.Empty`
2. Normalize `selectedTestCaseIds`
3. Call `GetSuiteAccessContextAsync(testSuiteId)`
4. Validate ownership: `CreatedById == CurrentUserId`
5. Validate `Status == Ready`
6. Resolve environment:
   - if `EnvironmentId != null`: load by id + project
   - else: load `IsDefault=true` by project
7. Count user runs with `Status in (Pending, Running)` and check `MaxConcurrentRuns`
8. Reserve monthly quota via `TryConsumeLimitAsync(userId, MaxTestRunsPerMonth, 1)`
9. Allocate `RunNumber` in `Serializable` transaction and insert `TestRun(Status=Pending)`
10. Call orchestrator

### B. Runtime auth/environment resolver

Implement one-run auth materialization:

- `None`: no-op
- `BearerToken`: add `Authorization: Bearer <token>` unless request already sets same header
- `Basic`: add `Authorization: Basic <base64(username:password)>` unless request already overrides
- `ApiKey`:
  - header location -> default header only if request has not set same key
  - query location -> default query param only if request has not set same key
- `OAuth2ClientCredentials`:
  - request access token one lan via `IHttpClientFactory`
  - content type: `application/x-www-form-urlencoded`
  - fields: `grant_type=client_credentials`, `client_id`, `client_secret`, `scope`
  - parse `access_token`
  - use token as Bearer header

Do not put OAuth network logic into `ExecutionAuthConfigService`.

### C. Variable resolution

Variable precedence:

1. extracted run variables
2. execution environment variables
3. literal values already in request

Resolve these surfaces:

- `Request.Url`
- `Request.PathParams`
- `Request.QueryParams`
- `Request.Headers`
- `Request.Body`

Rules:

- support `{{variableName}}` placeholders
- after resolving `PathParams`, replace `{pathParam}` tokens in URL and URI-encode values
- if any placeholder remains unresolved -> fail current case with `UNRESOLVED_VARIABLE`
- timeout clamp: `1000..60000` ms

### D. HTTP executor

`HttpTestExecutor` must:

- use `IHttpClientFactory`
- combine relative URL with `ResolvedExecutionEnvironment.BaseUrl`
- keep absolute URL as-is
- merge headers with precedence:
  - environment default headers
  - auth-generated headers only when missing
  - request headers last
- merge query params similarly
- capture:
  - status code
  - headers
  - body
  - latency ms
  - transport error message if request throws

Transport error means case result is `Failed`, not crash-the-run.

### E. Variable extractor

Support current `TestCaseVariable` shape only:

- `ResponseBody` + `JsonPath`
- `ResponseHeader` + `HeaderName`
- `Status`

If extraction fails:

- use `DefaultValue` when non-empty
- else do not add variable

Mask variable values in API result when variable name contains:

- `token`
- `secret`
- `password`
- `apikey`

### F. Rule-based validator

Validation sources:

- primary: `ExecutionTestCaseDto.Expectation`
- schema fallback: `ApiEndpointMetadataDto.ResponseSchemaPayloads` when expectation schema is empty

Checks:

1. status code
2. response schema
3. header exact match
4. body contains
5. body not contains
6. JSONPath equality
7. max response time

Rules:

- parse `ExpectedStatus` from JSON array
- header names compare case-insensitive
- header values compare exact after trim
- body contains/not contains uses deterministic substring matching
- JSONPath support minimum subset:
  - `$`
  - `$.prop`
  - `$.prop.nested`
  - `$.items[0]`
- do NOT bring in `Newtonsoft.Json` just for JSONPath
- for schema validation, a single dedicated package in `ClassifiedAds.Modules.TestExecution.csproj` is allowed and preferred over hand-writing full JSON Schema

Failure codes to use:

- `STATUS_CODE_MISMATCH`
- `RESPONSE_SCHEMA_MISMATCH`
- `HEADER_MISMATCH`
- `BODY_CONTAINS_MISSING`
- `BODY_NOT_CONTAINS_PRESENT`
- `JSONPATH_ASSERTION_FAILED`
- `RESPONSE_TIME_EXCEEDED`
- `RESPONSE_NOT_JSON`
- `HTTP_REQUEST_ERROR`
- `UNRESOLVED_VARIABLE`
- `DEPENDENCY_FAILED`

### G. Execution loop behavior

Execution is sequential only.

For each test case:

1. If any dependency status != Passed -> mark current case `Skipped`
2. Else resolve request
3. Execute HTTP request
4. Extract variables
5. Validate response
6. Build case result

Final run status:

- `Completed` if `FailedCount == 0`
- `Failed` if `FailedCount > 0`

### H. Result collector

Collector responsibilities:

- convert internal results -> `TestRunResultModel`
- serialize detail payload into cache
- set `ResultsExpireAt`
- update `TestRun` counters and final status
- save DB update

If cache write fails:

- log critical
- still update `TestRun.Status = Failed`
- propagate exception only after summary is persisted

## QUERY OPTIMIZATION RULES

- Gateway must issue bounded batch queries, not per-test-case queries.
- `GetTestRuns` must project directly from `IQueryable<TestRun>` with paging.
- Endpoint metadata must be fetched one time per run for all endpoint ids.
- OAuth2 token must be resolved once per run.

## FILES TO ADD OR MODIFY

Mandatory additions/modifications are defined in:

- `docs/features/FE-07-08-test-execution-validation/contracts.json`
- `docs/features/FE-07-08-test-execution-validation/implementation-map.json`

Follow those file paths exactly unless local code proves a naming conflict.

## TESTS (MANDATORY)

Add/update tests covering:

- gateway selection/dependency/order validation
- runtime auth resolution for all auth types
- variable resolution across URL/query/header/body
- variable extraction with default fallback
- schema/header/body/jsonpath/latency validation
- result collector cache expiry and summary counters
- command handler preflight paths
- end-to-end run start + result retrieval

## QUALITY GATES

Run what is feasible locally:

```bash
dotnet build
dotnet test
```

If package restore or network blocks full execution, state it explicitly and still ensure all new JSON/docs/code paths are internally consistent.
