# PHASE 2 PROMPT - Execution Engine For FE-07/08

Implement only the runtime execution pipeline after the read gateway already exists.

## Scope

Project allowed:

- `ClassifiedAds.Modules.TestExecution`
- related unit tests in `ClassifiedAds.UnitTests/TestExecution`

## Goal

Build the services that turn execution DTOs into real HTTP calls with dependency chaining and variable flow.

## Files To Add

- `Models/ResolvedExecutionEnvironment.cs`
- `Models/ResolvedTestCaseRequest.cs`
- `Models/HttpTestResponse.cs`
- `Models/TestCaseExecutionResult.cs`
- `Services/IExecutionEnvironmentRuntimeResolver.cs`
- `Services/ExecutionEnvironmentRuntimeResolver.cs`
- `Services/IVariableResolver.cs`
- `Services/VariableResolver.cs`
- `Services/IHttpTestExecutor.cs`
- `Services/HttpTestExecutor.cs`
- `Services/IVariableExtractor.cs`
- `Services/VariableExtractor.cs`
- `Services/ITestExecutionOrchestrator.cs`
- `Services/TestExecutionOrchestrator.cs`

## Runtime Environment Resolver

Responsibilities:

- deserialize `ExecutionEnvironment.AuthConfig`
- materialize runtime auth one lan per run
- return `ResolvedExecutionEnvironment`

Support:

- `None`
- `BearerToken`
- `Basic`
- `ApiKey`
- `OAuth2ClientCredentials`

Rules:

- use `IHttpClientFactory`
- for OAuth2 client credentials, request token one lan then reuse for all requests in that run
- explicit request-level auth header/query param must win over environment auth injection

## Variable Resolver

Resolve:

- `{{var}}` placeholders in URL/query/header/body/path params
- `{pathParam}` placeholders in URL after path param values are materialized

Rules:

- precedence: extracted vars > environment vars > literal values
- unresolved placeholder means current case is failed with `UNRESOLVED_VARIABLE`
- timeout clamp: `1000..60000`

## HTTP Executor

Rules:

- create requests through `IHttpClientFactory`
- relative URL + `BaseUrl` -> absolute URL
- absolute URL -> keep as-is
- merge headers/query params deterministically
- capture status/body/headers/latency/transport error
- no retry policy in FE-07/08 v1

## Variable Extractor

Support:

- body extraction by simple JSONPath subset
- header extraction by header name
- status extraction

If extraction fails:

- use `DefaultValue` if available
- else do not add variable

## Orchestrator

Dependencies:

- `ITestExecutionReadGatewayService`
- `IApiEndpointMetadataService`
- `IExecutionEnvironmentRuntimeResolver`
- `IVariableResolver`
- `IHttpTestExecutor`
- `IVariableExtractor`
- `IRuleBasedValidator`
- `ITestResultCollector`
- `IRepository<TestRun, Guid>`

Flow:

1. load execution context from gateway
2. load endpoint metadata one lan for all endpoint ids
3. resolve runtime environment one lan
4. mark run `Running`
5. loop test cases sequentially
6. skip on failed dependency
7. resolve request
8. execute HTTP
9. extract vars
10. validate
11. accumulate case result
12. delegate final persistence to collector

## Tests

Add unit tests for:

- OAuth2 token requested once per run
- request-level auth override respected
- placeholder resolution across all request surfaces
- unresolved variable -> failed case
- dependency failed -> dependent skipped
- endpoint metadata fetched once for all endpoints

Do not implement controller/command/query/read models in this phase.
