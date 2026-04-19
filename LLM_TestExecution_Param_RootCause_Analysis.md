# LLM Test Execution Param Root Cause Analysis

## 1. Executive Summary

This issue is a backend workflow bug in the LLM preview -> approve -> execute pipeline. It is not primarily a frontend binding problem, and it is not primarily a low-level HTTP client problem.

The primary root cause is that `GenerateLlmSuggestionPreviewCommandHandler` creates `LlmScenarioSuggestionContext` without loading endpoint metadata and without loading structured parameter details. In the failing preview flow, the LLM is therefore asked to generate requests with only coarse endpoint information, not the full route/query/body contract.

The second primary root cause is that `LlmSuggestionReviewService.ApproveManyAsync(...)` materializes approved suggestions directly into `TestCase` / `TestCaseRequest` rows without running `GeneratedTestCaseDependencyEnricher.Enrich(...)`. That skips the repo's existing backfill logic for route placeholders such as `{"id":"{{productId}}"}` and skips persistence of dependency links needed for chaining.

There is also a secondary but real auth/body-variable bug: the prompt instructs LLM to create extraction rules with `extractFrom: "RequestBody"`, but the persisted enum/parsers in TestGeneration only support `ResponseBody`, `ResponseHeader`, and `Status`. Execution supports `RequestBody`, but the generation/materialization layer strips or remaps it before runtime. This likely explains why `Valid Login` fails even though `/api/auth/login` has no route param.

Conclusion:

- Path/query/body params are parsed correctly from Swagger/OpenAPI and stored correctly.
- Param intent is first lost when preview input is built for LLM.
- Missing route-param wiring becomes permanent when approved suggestions are materialized without enrichment.
- Execution mostly exposes the upstream defect:
  - `DELETE` and `PUT/PATCH` route-param cases fail pre-validation.
  - body/query problems can still reach the target API and return `400`.

GitNexus CLI corroboration:

- `gitnexus impact --repo GSP26SE43.ModularMonolith GenerateLlmSuggestionPreviewCommandHandler --direction upstream` reported `HIGH`.
- `gitnexus impact --repo GSP26SE43.ModularMonolith LlmSuggestionReviewService --direction upstream` reported `CRITICAL`.

## 2. End-to-End Param Flow

Current failing branch from the logs:

`Swagger/OpenAPI -> persisted endpoint metadata -> preview generation -> LLM suggestion JSON -> bulk approve/materialize -> persisted TestCaseRequest -> StartTestRun -> PreExecutionValidator -> VariableResolver -> HttpTestExecutor`

| Stage | Input | Output | Param state | Main code | Verdict |
| --- | --- | --- | --- | --- | --- |
| 1. Swagger/OpenAPI parse | Raw OpenAPI JSON/YAML | `ParsedEndpoint`, `ParsedParameter` | Path params, query params, and request body are parsed correctly | `ClassifiedAds.Modules.ApiDocumentation/Services/OpenApiSpecificationParser.cs` -> `ParseAsync`, `ParseOperation`, `MapParameter`, `MapRequestBody` | Not the loss point |
| 2. Metadata persistence | `ParsedEndpoint.Parameters` | `EndpointParameter` rows | `Name`, `Location`, `DataType`, `Format`, `IsRequired`, `Schema`, `Examples` are stored | `ClassifiedAds.Modules.ApiDocumentation/Commands/ParseUploadedSpecificationCommand.cs` | Not the loss point |
| 3. Structured parameter retrieval | `EndpointParameter` rows | `EndpointParameterDetailDto` | Full location-aware contract exists here | `ClassifiedAds.Modules.ApiDocumentation/Services/ApiEndpointParameterDetailService.cs` -> `GetParameterDetailsAsync` | Good source of truth |
| 4. Endpoint metadata retrieval | `ApiEndpoint`, `EndpointParameter`, `EndpointResponse` | `ApiEndpointMetadataDto` | Schema payloads are present, but `ParameterNames` property exists and is never populated | `ClassifiedAds.Modules.ApiDocumentation/Services/ApiEndpointMetadataService.cs` | Secondary metadata weakness |
| 5. Normal LLM generation path | Approved order + spec ID | `LlmScenarioSuggestionContext` with metadata/details | Good branch passes metadata + parameter details | `ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs` | Healthy comparison path |
| 6. Preview LLM generation path | Suite + approved order + spec ID | `LlmScenarioSuggestionContext` without metadata/details | First real loss point in the failing flow | `ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs` -> `HandleAsync` | Primary root cause |
| 7. Prompt payload build | `LlmScenarioSuggestionContext` | `N8nBoundaryNegativePayload` | If preview context has no metadata/details, payload only carries method/path and weak prompt context | `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs` -> `BuildN8nPayload` | Param contract already degraded |
| 8. Prompt context mapping | `ApiEndpointMetadataDto` | `EndpointPromptContext` | Even when metadata exists, params are flattened into synthetic `param_n` with `In = "body"` | `ClassifiedAds.Modules.TestGeneration/Services/EndpointPromptContextMapper.cs` | Secondary design flaw |
| 9. LLM output parsing | n8n JSON response | `LlmSuggestedScenario` | Missing `pathParams`, `queryParams`, and `body` are accepted as-is | `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs` -> `ParseScenarios` | Missing completeness gate |
| 10. Suggestion persistence | `LlmSuggestedScenario` | `LlmSuggestion.SuggestedRequest` JSON | Missing params are serialized into suggestion storage | `GenerateLlmSuggestionPreviewCommandHandler` | Loss preserved |
| 11. Suggestion approval/materialization | `LlmSuggestion` | `TestCase`, `TestCaseRequest`, `TestCaseVariable` | Route/query/body stored separately, but no route placeholder/dependency enrichment runs here | `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionReviewService.cs` + `LlmSuggestionMaterializer.cs` + `TestCaseRequestBuilder.cs` | Second primary loss point |
| 12. Dependency repair in standard generators | Generated test cases | Updated requests + dependencies + producer variables | Existing repo logic can backfill missing route params and link consumers to producers | `ClassifiedAds.Modules.TestGeneration/Services/GeneratedTestCaseDependencyEnricher.cs` | Good logic exists but preview-approve flow skips it |
| 13. Start test run | Suite ID + `EnvironmentId` + optional selected IDs | `StartTestRunCommand` | Start API does not accept route/query/body param overrides | `ClassifiedAds.Modules.TestExecution/Controllers/TestRunsController.cs` + `Models/Requests/StartTestRunRequest.cs` | Not a param source |
| 14. Execution environment | Saved env config | `ResolvedExecutionEnvironment` | Supplies base URL, headers, auth, generic variables; not per-endpoint IDs/body | `ClassifiedAds.Modules.TestExecution/Entities/ExecutionEnvironment.cs` | Supportive only |
| 15. Pre-execution validation | `ExecutionTestCaseDto` + env/variables | `PreExecutionValidationResult` | Missing path params hard-fail; body/query completeness is weaker | `ClassifiedAds.Modules.TestExecution/Services/PreExecutionValidator.cs` | Surfaces the defect |
| 16. Request resolution | Persisted request + variables | `ResolvedTestCaseRequest` | Replaces `{id}`, resolves query/body/header placeholders correctly if source data exists | `ClassifiedAds.Modules.TestExecution/Services/VariableResolver.cs` | Works if inputs are present |
| 17. Final HTTP execution | `ResolvedTestCaseRequest` | Actual HTTP request | Query string append and request body serialization are implemented correctly for supported methods | `ClassifiedAds.Modules.TestExecution/Services/HttpTestExecutor.cs` | Not the primary bug |

### Direct answers to the required tracing questions

1. When parsing Swagger/OpenAPI:

- Yes, path params such as `{id}` are read in `OpenApiSpecificationParser.MapParameter(...)`.
- Yes, query params are read in the same mapper via `Location = Query`.
- Yes, request body schema is read in `MapRequestBody(...)`.
- These params are stored in `EndpointParameter` and surfaced via `EndpointParameterDetailDto` / `ParameterDetailDto`.

2. When building LLM input:

- In the failing preview flow, LLM does not receive the full route/query/body contract because `GenerateLlmSuggestionPreviewCommandHandler` does not populate `EndpointMetadata` or `EndpointParameterDetails`.
- In the healthier generation flow (`BoundaryNegativeTestCaseGenerator`), both are passed correctly.
- The prompt does not strictly require required param surfaces to be filled; response examples even allow `pathParams: null` and `queryParams: null`.

3. After LLM returns:

- Output is parsed into `LlmSuggestedScenario`.
- `SuggestedPathParams`, `SuggestedQueryParams`, `SuggestedHeaders`, and `SuggestedBody` are mapped directly from the response.
- There is no completeness validation before persistence, so missing fields remain null/empty.

4. When approving/materializing test cases:

- Params are stored in `TestCaseRequest.PathParams`, `QueryParams`, and `Body`.
- Route/query/body are stored separately.
- The storage code does not treat `DELETE`, `PUT`, and `PATCH` differently at persistence time.
- The real difference is that these methods depend more heavily on route/body completeness, so missing data becomes visible there first.

5. Before execution:

- `PreExecutionValidator` runs.
- `Pre-execution validation failed` occurs on missing route params, unresolved placeholders, missing body under certain body-type conditions, or other unresolved variable issues.
- For route-token endpoints, the most important hard failure is `MISSING_PATH_PARAM`.

6. When building the real HTTP request:

- `VariableResolver.Resolve(...)` does replace `{id}` with the resolved path param value if `request.pathParams` exists.
- `HttpTestExecutor.BuildUrlWithQueryParams(...)` appends query params correctly.
- `HttpTestExecutor.ExecuteAsync(...)` serializes body for `POST`, `PUT`, and `PATCH`.
- There is no evidence that `DELETE`, `PUT`, or `PATCH` are being dropped by the request builder itself.

7. Exact loss points:

- Primary loss point 1: preview LLM input building.
- Primary loss point 2: approval/materialization skipping dependency enrichment.
- Secondary loss point: `RequestBody` extraction contract mismatch.
- Downstream side effect: pre-validation catches missing route params; server returns `400` for malformed body/query cases that slip through.

## 3. Affected API Patterns

### Patterns directly observed in the provided logs and sample `swagger.json`

1. `DELETE` with route param only

- `DELETE /api/categories/{id}`
- `DELETE /api/products/{id}`
- Strongest failure signature: pre-validation fails with a single missing path-param error before HTTP execution.

2. `PUT` with route param plus body

- `PUT /api/categories/{id}`
- `PUT /api/products/{id}`
- Fails in two ways:
  - route param missing -> pre-validation fail
  - body missing/incomplete -> HTTP request can still be sent and return `400`

3. `POST` with body and variable chaining

- `POST /api/auth/login`
- No route param here, so failure points to unresolved placeholders or missing/incomplete body.
- The `RequestBody` extraction mismatch makes auth chaining especially fragile.

### Patterns structurally affected by the same code path

4. `PATCH` with route param plus body

- No direct example in the current sample swagger, but same `VariableResolver` + `HttpTestExecutor` + preview/approve logic applies.
- Expected failure mode is the same as `PUT`.

5. `GET` with required query params

- Current sample swagger does not contain such endpoints.
- Code scan shows preview generation and pre-validation are weak for required query params, so the same pipeline would also mis-handle them.

### Pattern failing the most

The most affected pattern is a consumer endpoint that needs an ID produced by an earlier create/setup step:

- `DELETE /resource/{id}` is the most brittle because it depends almost entirely on route-param completeness.
- `PUT /resource/{id}` and `PATCH /resource/{id}` are next because they need both route param and body.
- `POST /api/auth/login` is also affected, but for a different reason: request-body variable chaining is broken by the `RequestBody` extraction mismatch.

## 4. Code Trace

| File | Class | Method | Responsibility | Data in | Data out | Param impact |
| --- | --- | --- | --- | --- | --- | --- |
| `ClassifiedAds.Modules.ApiDocumentation/Services/OpenApiSpecificationParser.cs` | `OpenApiSpecificationParser` | `ParseAsync`, `ParseOperation`, `MapParameter`, `MapRequestBody` | Parse OAS into normalized endpoint models | Raw spec JSON | `ParsedEndpoint`, `ParsedParameter` | Correctly reads path/query/body params |
| `ClassifiedAds.Modules.ApiDocumentation/Commands/ParseUploadedSpecificationCommand.cs` | `ParseUploadedSpecificationCommandHandler` | `HandleAsync` | Persist parsed endpoints and parameters | `ParsedEndpoint` | `ApiEndpoint`, `EndpointParameter`, `EndpointResponse` | Correctly stores route/query/body metadata |
| `ClassifiedAds.Modules.ApiDocumentation/Services/ApiEndpointParameterDetailService.cs` | `ApiEndpointParameterDetailService` | `GetParameterDetailsAsync` | Build full parameter detail DTO | `EndpointParameter` rows | `EndpointParameterDetailDto` | Best structured source for prompt/execution completeness |
| `ClassifiedAds.Modules.ApiDocumentation/Services/ApiEndpointMetadataService.cs` | `ApiEndpointMetadataService` | `GetEndpointMetadataAsync` | Build cross-endpoint metadata | Endpoints + parameters + responses | `ApiEndpointMetadataDto` | Schema payloads available, but `ParameterNames` never populated |
| `ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs` | `GenerateLlmSuggestionPreviewCommandHandler` | `HandleAsync` | Build preview LLM context and persist suggestions | Suite + approved order + spec ID | `LlmScenarioSuggestionContext`, `LlmSuggestion` | Omits `EndpointMetadata` and `EndpointParameterDetails` |
| `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs` | `LlmScenarioSuggester` | `SuggestScenariosAsync`, `BuildN8nPayload`, `ParseScenarios`, `CreateFallbackScenario` | Build prompt payload and parse scenarios | `LlmScenarioSuggestionContext` | `LlmSuggestedScenario` list | Accepts null params and can create fallback scenarios with empty param dictionaries |
| `ClassifiedAds.Modules.TestGeneration/Services/EndpointPromptContextMapper.cs` | `EndpointPromptContextMapper` | `Map`, `MapParameters` | Convert endpoint metadata into prompt context | `ApiEndpointMetadataDto` | `EndpointPromptContext` | Destroys real param semantics by renaming to `param_n` and forcing `In = "body"` |
| `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionMaterializer.cs` | `LlmSuggestionMaterializer` | `MaterializeFromScenario`, `MaterializeFromSuggestion`, `ParseExtractFrom` | Convert LLM suggestion into `TestCase` graph | `LlmSuggestedScenario` / `LlmSuggestion` | `TestCase`, `TestCaseVariable`, `N8nTestCaseRequest` | Persists missing params unchanged; does not support `RequestBody` extraction value |
| `ClassifiedAds.Modules.TestGeneration/Services/TestCaseRequestBuilder.cs` | `TestCaseRequestBuilder` | `Build`, `SerializeDict` | Persist request surfaces | `N8nTestCaseRequest` | `TestCaseRequest` | Stores route/query/body separately, but null/empty dicts collapse to null |
| `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionReviewService.cs` | `LlmSuggestionReviewService` | `ApproveManyAsync` | Bulk-approve and materialize suggestions | `LlmSuggestion` rows | persisted `TestCase` graph | Skips `GeneratedTestCaseDependencyEnricher` and does not persist dependencies |
| `ClassifiedAds.Modules.TestGeneration/Services/GeneratedTestCaseDependencyEnricher.cs` | `GeneratedTestCaseDependencyEnricher` | `Enrich`, `FillMissingRouteParams` | Backfill route placeholders and dependency links | Generated cases + order + producer context | updated test cases + dependencies + variables | Existing repair logic that preview-approve path does not use |
| `ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs` | `BoundaryNegativeTestCaseGenerator` | `GenerateAsync` | Reference implementation for good LLM context assembly | Suite + ordered endpoints + spec ID | generated test cases | Loads metadata and parameter details correctly |
| `ClassifiedAds.Modules.TestGeneration/Services/TestGenerationPayloadBuilder.cs` | `TestGenerationPayloadBuilder` | `BuildPayloadAsync` | Unified n8n generation payload for another flow | Suite + proposal | `N8nGenerateTestsPayload` | Not on the current failing path, but shares the same `RequestBody` prompt contract wording |
| `ClassifiedAds.Modules.TestGeneration/Services/TestExecutionReadGatewayService.cs` | `TestExecutionReadGatewayService` | `GetExecutionContextAsync`, `MapToExecutionTestCaseDto` | Map persisted test cases into execution DTOs | DB entities | `ExecutionTestCaseDto` | No evidence of param loss here |
| `ClassifiedAds.Modules.TestExecution/Services/PreExecutionValidator.cs` | `PreExecutionValidator` | `ValidatePathParams`, `ValidateBody`, `ValidateUnresolvedPlaceholders` | Validate request before HTTP call | `ExecutionTestCaseDto` + env + variables | `PreExecutionValidationResult` | Hard-fails route-token problems; body/query checks are weaker |
| `ClassifiedAds.Modules.TestExecution/Services/VariableResolver.cs` | `VariableResolver` | `Resolve` | Replace placeholders and apply path/query/body vars | Persisted request + variable bag + env | `ResolvedTestCaseRequest` | Replaces `{id}` correctly when `PathParams` exists |
| `ClassifiedAds.Modules.TestExecution/Services/HttpTestExecutor.cs` | `HttpTestExecutor` | `ExecuteAsync`, `BuildUrlWithQueryParams`, `HasBody` | Build final URL/body and send HTTP request | `ResolvedTestCaseRequest` | actual HTTP request/response | Query/body handling is implemented; not the upstream loss point |
| `ClassifiedAds.Modules.TestExecution/Services/VariableExtractor.cs` | `VariableExtractor` | `Extract` | Extract runtime variables from response or request body | response + variable rules + requestBody | extracted variable dictionary | Supports `RequestBody` at runtime, proving the mismatch is upstream |

## 5. Root Cause Analysis

### Primary root cause 1: preview generation omits endpoint contract

`GenerateLlmSuggestionPreviewCommandHandler` builds `LlmScenarioSuggestionContext` with:

- `TestSuiteId`
- `UserId`
- `Suite`
- `OrderedEndpoints`
- `SpecificationId`
- `AlgorithmProfile`

but it does not load:

- `IApiEndpointMetadataService.GetEndpointMetadataAsync(...)`
- `IApiEndpointParameterDetailService.GetParameterDetailsAsync(...)`

This is the first real point where param intent is lost. The LLM therefore sees endpoint path and method, but not a reliable, location-aware contract for:

- required path params
- required query params
- request body schema
- examples/defaults

There is even a dead variable in the handler:

- `var endpointMetadataService = _gateService;`

which strongly suggests the handler was left incomplete.

### Primary root cause 2: approved suggestions skip dependency/route enrichment

`LlmSuggestionReviewService.ApproveManyAsync(...)` directly persists materialized test cases. It does not call `GeneratedTestCaseDependencyEnricher.Enrich(...)`.

That matters because `GeneratedTestCaseDependencyEnricher` is exactly where the repo already knows how to:

- inspect route tokens from `orderItem.Path`
- choose a producer test case
- create a variable if needed
- inject `request.pathParams[token] = "{{variableName}}"`
- add `TestCaseDependency`

Standard generators already use this repair logic:

- `GenerateHappyPathTestCasesCommand`
- `GenerateBoundaryNegativeTestCasesCommand`

The preview-approve path does not. So even when an approved suggestion should have become:

- `PUT /api/products/{id}` with `pathParams.id = "{{productId}}"`
- `DELETE /api/categories/{id}` with `pathParams.id = "{{categoryId}}"`

it is persisted without the route placeholder and without the dependency graph.

### Secondary root cause 1: `RequestBody` extraction contract mismatch

The prompt explicitly tells LLM to generate:

- `variableName: "registeredEmail", extractFrom: "RequestBody"`
- `variableName: "registeredPassword", extractFrom: "RequestBody"`

But the persisted enum and parsers in TestGeneration do not support that value:

- `ClassifiedAds.Modules.TestGeneration/Entities/TestCaseVariable.cs` enum has only `ResponseBody`, `ResponseHeader`, `Status`
- `LlmSuggestionMaterializer.ParseExtractFrom(...)` remaps unknown values to `ResponseBody`
- `HappyPathTestCaseGenerator.ParseExtractFrom(...)` does the same
- `SaveAiGeneratedTestCasesCommand.ParseExtractFrom(...)` does the same

Execution-side extractor does support `RequestBody`:

- `ClassifiedAds.Modules.TestExecution/Services/VariableExtractor.cs`

So the mismatch is not in runtime extraction. It is in generation/materialization before runtime.

This is the most plausible root cause for the `Valid Login` failure:

- registration values that should have been captured from request body are not persisted with the correct extraction source
- later login request tries to use `{{registeredEmail}}` / `{{registeredPassword}}`
- those variables are unresolved or missing

### Secondary root cause 2: prompt contract and parser are too permissive

`LlmScenarioSuggester` currently:

- allows `pathParams: null`
- allows `queryParams: null`
- only says `body` may be JSON string or null
- parses missing request surfaces without completeness checks
- can even produce fallback scenarios with empty `PathParams` / `QueryParams`

So once the preview handler starves the LLM of metadata, the backend still accepts the incomplete response and persists it.

### Secondary root cause 3: prompt mapper destroys param semantics even when metadata exists

`EndpointPromptContextMapper.MapParameters(...)` rewrites every parameter schema into:

- `Name = "param_n"`
- `In = "body"`
- `Required = true`

This is not the primary cause of the current failing run, because preview generation did not pass metadata at all. But it is a real design flaw that reduces prompt quality in other generation paths.

### Secondary root cause 4: validation is asymmetric

`PreExecutionValidator` is strict for path params but weak for body/query completeness:

- `ValidatePathParams(...)` hard-fails missing `{id}` values
- `ValidateBody(...)` only hard-fails if `BodyType` is set and `Body` is empty
- there is no endpoint-contract-driven validation for required query params

So route-token cases fail early, but malformed write requests can still reach the API and return `400`.

### Why the bug is most visible on DELETE / PUT / PATCH

1. These endpoints usually target an existing resource.
2. They often need an ID from an earlier producer call.
3. The preview-approve path skips the only existing enrichment logic that can wire producer output into consumer route params.
4. `DELETE` has no body fallback, so missing `{id}` fails immediately.
5. `PUT` and `PATCH` are doubly exposed because they need both route param and body completeness.

### Why LLM-generated test cases fail to send the correct params

Because the backend does not do all three required things on the preview path:

1. give LLM a location-aware endpoint contract
2. reject/regenerate incomplete request surfaces returned by LLM
3. enrich approved test cases with dependencies and route placeholders before execution

## 6. Evidence from Backend Logs

### Review/approval logs identify the exact failing workflow

Observed logs:

- `Approved 33 LLM suggestion(s)...`
- `Bulk reviewed LLM suggestions... MaterializedCount=33`

Why this matters:

- These logs prove the executed test cases came from `LlmSuggestionReviewService.ApproveManyAsync(...)`.
- That means the failing run used the path that skips `GeneratedTestCaseDependencyEnricher`.
- It was not the normal happy-path or boundary-negative generator path.

### Execution environment logs show environment creation succeeded, not param hydration

Observed logs:

- `Created execution environment...`

Why this matters:

- `ExecutionEnvironment` stores `BaseUrl`, `Variables`, `Headers`, and `AuthConfig`.
- It does not generate route params or request body values for each endpoint.
- So environment creation success does not imply correct request-param hydration.

### Start/run logs show orchestration is working

Observed logs:

- `Created test run. RunId=32330dde-257f-4838-a3cd-34a50d0096b6...`
- `Test run started. RunId=32330dde-257f-4838-a3cd-34a50d0096b6, TotalTests=33`

Why this matters:

- Controller dispatch, command handling, and run orchestration are functioning.
- Failure happens after run creation, inside per-test validation and execution.

### Pre-validation failures map directly to missing route params

Observed logs:

- `Pre-execution validation failed for TestCase=Update Category ... Errors=1`
- `Pre-execution validation failed for TestCase=Delete Category ... Errors=1`
- `Pre-execution validation failed for TestCase=Update Product ... Errors=1`
- `Pre-execution validation failed for TestCase=Delete Product ... Errors=1`

Why this maps directly to code:

- In the sample `swagger.json`, these endpoints each contain exactly one required path token: `{id}`.
- `PreExecutionValidator.ValidatePathParams(...)` emits one hard error per missing route token.
- `DELETE` has no request body in the spec, so one error is most consistent with `MISSING_PATH_PARAM`.
- For `PUT`, a missing body may only become a warning if `BodyType` is missing/`None`, so one hard error still strongly points to missing route param as the blocker.

### `Valid Login` failure points to variable/body chaining, not route params

Observed log:

- `Pre-execution validation failed for TestCase=Valid Login ... Errors=2`

Why this matters:

- `/api/auth/login` has no route token in the sample `swagger.json`.
- So its errors are not caused by missing path params.
- The most plausible explanation from code is unresolved placeholders or malformed missing body related to `registeredEmail` / `registeredPassword`.
- That aligns with the `RequestBody` extraction mismatch found in the codebase.

### `Polly` `400` logs prove malformed requests still reached the API

Observed logs:

- Multiple `Polly` execution attempts returned `400`

Why this maps directly to code:

- `Polly` logs only appear after `HttpTestExecutor.ExecuteAsync(...)` sends the request.
- So these requests passed pre-validation and reached the target API.
- That is consistent with:
  - incomplete request body not being promoted to a hard validation error
  - missing required query params not being validated against endpoint contract
  - semantically invalid bodies generated by LLM

## 7. FE/BE Contract Clarification

### What FE is responsible for

FE is responsible for:

- choosing the suite
- choosing the spec
- triggering preview generation
- reviewing/modifying/approving suggestions
- choosing execution environment
- starting the run

### What FE is not responsible for

FE is not the source of per-request route/query/body hydration at execution time for auto-generated cases.

Evidence:

- `StartTestRunRequest` contains only:
  - `EnvironmentId`
  - `SelectedTestCaseIds`
  - `StrictValidation`
- It does not carry `PathParams`, `QueryParams`, or `Body`.

### What BE must already have before `StartTestRun`

Before execution begins, backend must already have persisted a complete `TestCaseRequest` for each executable case:

1. Route params

- Any URL containing `{id}`, `{projectId}`, `{itemId}`, etc. must have `Request.PathParams` populated.
- Each token must have either a literal value or a resolvable placeholder such as `{{productId}}`.

2. Query params

- Any required query param from the endpoint contract must already be present in `Request.QueryParams`.

3. Body

- Any endpoint with required request body must already have:
  - `Request.BodyType`
  - `Request.Body`

4. Dependencies and variables

- If a consumer endpoint needs output from a producer endpoint, backend must already have:
  - extraction rules on the producer case
  - dependency links on the consumer case
  - placeholders in route/query/body fields that reference those variables

### Execution environment's actual role

`ExecutionEnvironment` can provide:

- `BaseUrl`
- shared headers
- auth config
- shared variables

It is not the primary place to invent missing per-endpoint resource IDs or request bodies.

### Required vs nullable fields for FE to understand

Required in practice:

- `Request.HttpMethod`
- `Request.Url`
- `Request.PathParams` when URL contains route tokens
- `Request.QueryParams` when endpoint has required query params
- `Request.BodyType` and `Request.Body` when endpoint requires body

Nullable only when the endpoint contract allows it:

- `Headers`
- `Body`
- `PathParams`
- `QueryParams`

### Practical FE takeaway

For the current bug, FE should not be told to "send the ID again when starting the run". The missing values should have been generated, enriched, and persisted by backend before execution began.

## 8. Fix Recommendations

### Priority 1: fix preview generation context

Update `ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs`:

- inject `IApiEndpointMetadataService`
- inject `IApiEndpointParameterDetailService`
- load metadata for the approved endpoint order
- load parameter details for the approved endpoint order
- populate `llmContext.EndpointMetadata`
- populate `llmContext.EndpointParameterDetails`
- remove the dead `endpointMetadataService` local

This is the highest-value immediate fix because it repairs the first loss point.

### Priority 2: run dependency enrichment on approved suggestions

Update `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionReviewService.cs`:

- materialize approved suggestions into memory first
- load existing producer test cases / variables as the standard generators do
- call `GeneratedTestCaseDependencyEnricher.Enrich(...)`
- persist:
  - updated `Request.PathParams`
  - new `TestCaseDependency` rows
  - any producer variables created by the enricher

This is the highest-value fix for `DELETE` / `PUT` / `PATCH` route-param failures.

### Priority 3: align `RequestBody` extraction contract end-to-end

Update all of these so the generation layer matches runtime behavior:

- `ClassifiedAds.Modules.TestGeneration/Entities/TestCaseVariable.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionMaterializer.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/HappyPathTestCaseGenerator.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/SaveAiGeneratedTestCasesCommand.cs`

Recommended direction:

- add `RequestBody` to the `ExtractFrom` enum
- map `"requestbody"` / `"request_body"` / `"body"` explicitly where appropriate
- keep naming consistent with `VariableExtractor`

Alternative fallback:

- stop asking LLM for `RequestBody` extraction and use a supported value instead

but this is weaker than fixing the contract properly.

### Priority 4: make the LLM response contract param-complete

Update `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs`:

- do not show `pathParams: null` for endpoints with route tokens
- do not show `queryParams: null` when required query params exist
- require `bodyType` + `body` for endpoints with required request body
- add post-parse validation that rejects or flags incomplete scenarios before persistence

### Priority 5: preserve real parameter semantics in prompt context

Update `ClassifiedAds.Modules.TestGeneration/Services/EndpointPromptContextMapper.cs`:

- preserve the real parameter name
- preserve the real location (`path`, `query`, `header`, `body`)
- preserve required flag
- preserve schema/examples

Also consider updating `ApiEndpointMetadataService` so `ApiEndpointMetadataDto.ParameterNames` is actually populated if other semantic-matching logic depends on it.

### Priority 6: strengthen pre-execution validation and logging

Update `ClassifiedAds.Modules.TestExecution/Services/PreExecutionValidator.cs`:

- validate required query params against endpoint metadata
- validate required body using endpoint contract, not only `BodyType`
- report exact missing param names in logs/results

Update execution logging so failures clearly print:

- endpoint path/method
- missing route/query/body field names
- unresolved variable names
- suggestion/testcase ID

### Priority 7: add targeted tests

Add unit/integration coverage for:

- preview handler populating metadata and parameter details
- review service running dependency enrichment
- approved `PUT`/`DELETE` cases persisting non-empty `PathParams`
- `RequestBody` extraction persisting and resolving correctly
- required query/body validation firing before HTTP execution

## 9. Verification Checklist

- [ ] Re-generate preview for a suite containing `PUT /api/products/{id}` and `DELETE /api/products/{id}`.
- [ ] Confirm preview payload sent to n8n contains location-aware parameter details for each endpoint.
- [ ] Confirm preview payload includes body schema for body-required endpoints.
- [ ] Confirm LLM output contains non-empty `request.pathParams` for route-token endpoints.
- [ ] Confirm LLM output contains `request.bodyType` and non-empty `request.body` for body-required endpoints.
- [ ] Approve suggestions and inspect persisted `TestCaseRequest` rows.
- [ ] Confirm `PathParams`, `QueryParams`, and `Body` are stored separately and correctly.
- [ ] Confirm `TestCaseDependency` rows are created for consumer endpoints.
- [ ] Confirm route placeholders such as `{{productId}}` or `{{categoryId}}` are backfilled where needed.
- [ ] Confirm auth/login chaining persists `RequestBody` extraction rules correctly.
- [ ] Start a run and confirm `PreExecutionValidator` no longer emits `MISSING_PATH_PARAM` for approved `PUT`/`DELETE` cases.
- [ ] Confirm `VariableResolver` replaces `{id}` with the extracted/available value.
- [ ] Confirm `HttpTestExecutor` sends final URLs without unresolved route tokens.
- [ ] Confirm `PUT` / `PATCH` / `POST` body-required requests no longer hit avoidable `400` due to empty/malformed body caused by generation gaps.
- [ ] If required query-param endpoints exist in the target spec, confirm they fail fast when missing and execute correctly when present.

## 10. Uncertainties

1. The workspace does not include the persisted request payload for the exact failing run `32330dde-257f-4838-a3cd-34a50d0096b6`.

- Because of that, the exact two validation failures inside `Valid Login` cannot be proven from run-state data alone.
- Based on code and the absence of route tokens on `/api/auth/login`, unresolved request-body variables are the strongest explanation.

2. The current sample `swagger.json` does not contain direct `PATCH` endpoints or required-query examples.

- So the strongest direct evidence is for route-param and body-chaining failures.
- `PATCH` and required-query impact is inferred from the shared code path.

3. There is a secondary parser timing risk during the fast upload parse.

- `UploadApiSpecificationCommandHandler.ParseOpenApiJsonEndpoints(...)` is weaker than the normalized parser and does not merge path-item shared parameters the same way.
- This is not the strongest explanation for the current failing run because the deeper preview/approval bugs align much more directly with the observed logs.
- It is still worth checking if generation can run before normalized parsing completes.

## Final Conclusion

The request builder is not the main culprit. The real bug is upstream in the backend LLM-generated test workflow used by the logged run:

1. preview generation does not provide full parameter metadata/details to the LLM
2. LLM response parsing accepts missing request surfaces
3. approved suggestions are materialized without route/dependency enrichment
4. auth/body-variable chaining is additionally broken by the `RequestBody` extraction mismatch
5. execution then exposes the defect, especially on `DELETE`, `PUT`, and any other endpoint that needs a prior resource ID or a required body

If the team only patches `HttpTestExecutor` or the controller layer, the bug will remain. The high-value fix is to repair the preview/approve pipeline so that approved test cases already contain correct route/query/body data, correct variable extraction semantics, and correct dependency links before execution begins.
