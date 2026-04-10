# Test Run Failure Analysis Report

## Scope

- Analysis target: `https://localhost:44312/api/test-suites/820c6d68-1501-42e5-85dd-7f77cd490019/test-runs`
- Source data used:
  - `D:\GSP26SE43.ModularMonolith\New Text Document.txt`
  - repo source code in `ClassifiedAds.Modules.TestExecution`, `ClassifiedAds.Modules.TestGeneration`, `ClassifiedAds.Modules.ApiDocumentation`
  - live verification requests to `https://test-llm-api-testing.onrender.com`
- Analysis date: `2026-04-10`

## Important limitation

`https://localhost:44312` was not reachable from the current shell at analysis time, so this report is based on:

1. the cached run payload already captured in `New Text Document.txt`
2. direct verification against the real upstream API under test: `https://test-llm-api-testing.onrender.com`

That is still enough to explain the failures because the payload contains each test case status, HTTP code, response preview, and failure reason.

## Executive summary

Run `#2` failed with:

- `14` total tests
- `2` passed
- `12` failed

The `12` failures are not one single problem. They split into `4` distinct root causes:

1. `7` cases fail because the request reached a protected endpoint without Bearer token, so the API returned `401 Unauthorized`.
2. `3` cases never reached the API at all because path parameter `{id}` was not resolved before execution.
3. `1` case fails because the suite expected `400`, but the real API accepts an invalid query param and still returns `200`.
4. `1` case fails because malformed JSON returns `500 Internal Server Error`, not `400 Bad Request`.

So the suite is failing partly because of bad runtime configuration, partly because of bad test-data generation, and partly because some expected statuses do not match the real target API behavior.

## Passed cases

These are the only two passing cases:

- `Empty query params`
- `Happy Path: GET /api/products (3)`

Both are simple `GET /api/products` requests and both hit an endpoint that currently works without auth.

## Failure breakdown

### Group A: Missing auth at runtime (`7` cases)

Affected cases:

- `Missing required field`
- `Happy Path: POST /api/products (3)`
- `Negative Validation: POST /api/products (4)`
- `Invalid product ID` for `PUT /api/products/invalidId`
- `Empty update body`
- `Non-existent product ID` for `DELETE /api/products/nonExistentId`
- `Invalid product ID` for `DELETE /api/products/invalidId`

Observed evidence from the cached run:

- `requestHeaders` is `{}` on those cases
- response body is `{"success":false,"message":"Unauthorized: Bearer token is required"}`
- actual status is `401`

Why this is the root cause:

- The runtime resolver injects `Authorization` automatically only when the execution environment has `AuthConfig` or default headers configured.
- In code, `ExecutionEnvironmentRuntimeResolver.ResolveAsync(...)` loads environment headers and auth config, then `ResolveAuth(...)` adds `Authorization: Bearer <token>` for `BearerToken` auth.
- Relevant code:
  - `ClassifiedAds.Modules.TestExecution/Services/ExecutionEnvironmentRuntimeResolver.cs:35-56`
  - `ClassifiedAds.Modules.TestExecution/Services/ExecutionEnvironmentRuntimeResolver.cs:66-72`
  - `ClassifiedAds.Modules.TestExecution/Services/ExecutionAuthConfigService.cs:18-34`

Conclusion:

- Environment `dddd` was executed without usable auth at runtime.
- This is not just a display issue in the report, because the stored `requestHeaders` are empty and the API explicitly says Bearer token is required.

Live verification against the upstream API:

- `POST /api/products` with no auth and no body returns `401`
- `POST /api/products` with no auth and `{}` also returns `401`
- `PUT /api/products/invalidId` with no auth returns `401`
- `DELETE /api/products/nonExistentId` with no auth returns `401`

Impact on analysis:

- These cases did not reach business validation logic.
- That means expected statuses like `201`, `400`, or `404` were never actually evaluated by the target API.

### Group B: Unresolved path variable `{id}` before execution (`3` cases)

Affected cases:

- `Happy Path: PUT /api/products/{id} (3)`
- `Negative Validation: PUT /api/products/{id} (4)`
- `Happy Path: DELETE /api/products/{id} (3)`

Observed evidence from the cached run:

- `httpStatusCode = null`
- `durationMs = 0`
- `resolvedUrl = null`
- failure code = `UNRESOLVED_VARIABLE`
- message = `Path parameter '{id}' chưa được giải quyết trong URL.`

Why this is the root cause:

- `VariableResolver` replaces route tokens only from `request.PathParams`.
- If `{id}` is still present after resolution, it throws `UnresolvedVariableException` and execution fails before any HTTP request is sent.
- Relevant code:
  - `ClassifiedAds.Modules.TestExecution/Services/VariableResolver.cs:54-60`
  - `ClassifiedAds.Modules.TestExecution/Services/VariableResolver.cs:104-106`
  - `ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs:165-189`
  - `ClassifiedAds.UnitTests/TestExecution/VariableResolverTests.cs:126-139`

Why this likely happened in the generated suite:

- `TestCaseRequestBuilder` only serializes the path params that n8n/LLM already generated. It does not auto-invent missing values.
- Relevant code:
  - `ClassifiedAds.Modules.TestGeneration/Services/TestCaseRequestBuilder.cs:50-62`

Important extra signal:

- These failed cases show `DependencyIds: []`.
- The orchestrator only skips cases when dependencies are present and a dependency failed.
- Relevant code:
  - `ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs:138-163`

Conclusion:

- The suite contains path-based cases that need a concrete product id, but the generated test case data did not provide one.
- They also were not linked strongly enough to a producer case that could create or expose an id first.
- These 3 cases did not "run and fail"; they never ran.

### Group C: Invalid query param expectation does not match real API (`1` case)

Affected case:

- `Invalid query param`

Observed evidence from the cached run:

- expected status: `400`
- actual status: `200`
- same success payload as the normal `GET /api/products`

Direct verification:

- `GET https://test-llm-api-testing.onrender.com/api/products?invalidParam=%40%40%40` returns `200 OK`

Conclusion:

- The real API currently ignores unknown query parameters instead of rejecting them.
- So this test case is failing because the expected result is wrong for the current API behavior.
- This is a test expectation problem, not a runner problem.

### Group D: Invalid JSON body expectation does not match real API behavior (`1` case)

Affected case:

- `Invalid JSON body`

Observed evidence from the cached run:

- expected status: `400`
- actual status: `500`
- response body: `{"success":false,"message":"Internal server error"}`

Direct verification:

- `POST https://test-llm-api-testing.onrender.com/api/products` with malformed JSON also returns `500 Internal Server Error`

Conclusion:

- The real API does not gracefully reject malformed JSON with `400`.
- It is currently crashing into a `500`.
- So this failure is real and reproducible on the upstream API.

This case is not a fake failure from the test runner.

## Why some test cases "should run" but still failed

There are `3` different meanings of "failed" in this run:

1. **Executed and got a different status than expected**
   - Example: `Invalid query param` expected `400`, actual `200`
   - Example: `Invalid JSON body` expected `400`, actual `500`

2. **Executed but was blocked by auth before reaching business logic**
   - Example: POST/PUT/DELETE cases returning `401`

3. **Never executed because test data was incomplete**
   - Example: any case still containing unresolved `{id}`

So not every failed case means "API logic is broken".

## Most likely root causes in priority order

### Priority 1: Execution environment is missing auth

Strongest evidence:

- all auth-sensitive failures have empty request headers
- API explicitly returns `Bearer token is required`
- codebase already supports injecting Bearer token if environment auth is configured

Expected fix direction:

- open execution environment `dddd`
- configure `AuthConfig` with `BearerToken`, or add default `Authorization` header
- if token must be obtained dynamically, add/login bootstrap flow and store extracted token for later cases

### Priority 2: Path-param test cases were generated without usable `id`

Strongest evidence:

- unresolved route token failures happen before HTTP execution
- those cases have no resolved URL and no duration
- dependency list is empty even though a product-id producer should exist earlier in the flow

Expected fix direction:

- make sure POST happy-path case extracts product id from response body
- make sure PUT/DELETE cases use `pathParams` like `{"id":"{{productId}}"}` or another extracted variable
- make sure dependency chain is persisted so consumer cases skip or wait correctly instead of hard-failing

### Priority 3: Some expected statuses are simply wrong for the target API

Cases:

- invalid query param: should currently expect `200`, not `400`
- invalid JSON body: should currently expect `500` if you are documenting actual behavior, or keep `400` only if the goal is to detect and report the API defect

## Recommended next actions

1. Fix environment auth first, then rerun the suite.
2. Inspect the generated request data for the POST happy-path case and confirm whether it extracts a created product id.
3. Fix the generated PUT/DELETE cases so `pathParams.id` is populated from an extracted variable.
4. Decide whether the suite should validate actual API behavior or ideal contract behavior:
   - if validating actual behavior, update expectations for `Invalid query param` and possibly `Invalid JSON body`
   - if validating ideal contract behavior, keep them failing but label them as confirmed API defects

## Final assessment

The main reason this run looks "bad" is not that all APIs are broken.

The real picture is:

- `GET /api/products` is working
- auth-protected write endpoints are being hit without auth in this run
- several generated path-param cases are incomplete and never reach execution
- one negative query test has the wrong expectation
- one malformed-JSON test is exposing a real upstream `500`

So before judging the target API quality, the suite itself needs two corrections first:

1. execution environment auth must be configured correctly
2. dependent path-param test cases must receive a real `{id}` value
