# TASK: Implement FE-06 — Boundary & Negative Test Case Generation

## CONTEXT

You are implementing feature FE-06 in a .NET 10 Modular Monolith codebase (ASP.NET Core Web API, EF Core, PostgreSQL, CQRS via Dispatcher). Read `AGENTS.md` in workspace root before starting. All code lives in `ClassifiedAds.Modules.TestGeneration`. No new modules, no new entities, no new migrations.

## HARD CONSTRAINTS

- MUST pass FE-05A gate (approved/applied API order) before generation. Gate fail → HTTP 409 `ORDER_CONFIRMATION_REQUIRED`.
- MUST reuse all entities from FE-04/FE-05: `TestCase`, `TestCaseRequest`, `TestCaseExpectation`, `TestCaseVariable`, `TestCaseDependency`, `TestCaseChangeLog`, `TestSuiteVersion`, `TestSuite`. Only difference: `TestType = Boundary | Negative`.
- MUST NOT create new entities, tables, or migrations. Schema `testgen` already exists.
- MUST NOT access DbContext/Repository of other modules (ApiDocumentation, LlmAssistant, Subscription). Communicate only via contract interfaces.
- MUST NOT create new cross-module interfaces — all already exist (see CROSS-MODULE INTERFACES section).
- MUST follow existing FE-05B code patterns for builders, naming, transactions, DI registration.
- Error messages displayed to user MUST be in Vietnamese, following existing codebase pattern.

## ARCHITECTURE

FE-06 has 3 independent pipelines. User can toggle each via boolean flags:

```
PIPE-01: Path Parameter Mutations   (rule-based)  → IncludePathMutations
PIPE-02: Body Mutations             (rule-based)  → IncludeBodyMutations
PIPE-03: LLM Scenario Suggestions   (llm-assisted) → IncludeLlmSuggestions
```

## API CONTRACT

```
POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative
Authorization: [Authorize] + [Authorize(Permissions.GenerateBoundaryNegativeTestCases)]
Success: 201 Created
```

### Request: `GenerateBoundaryNegativeTestCasesRequest`

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| specificationId | Guid | yes | — | API specification ID, must match spec used in approved proposal |
| forceRegenerate | bool | no | false | If true, delete existing boundary/negative test cases before regeneration |
| includePathMutations | bool | no | true | Enable PIPE-01 |
| includeBodyMutations | bool | no | true | Enable PIPE-02 |
| includeLlmSuggestions | bool | no | true | Enable PIPE-03 |

### Response: `GenerateBoundaryNegativeResultModel`

| Field | Type | Description |
|-------|------|-------------|
| testSuiteId | Guid | Test suite ID |
| totalGenerated | int | Total test cases created (path + body + llm) |
| pathMutationCount | int | Count from PIPE-01 |
| bodyMutationCount | int | Count from PIPE-02 |
| llmSuggestionCount | int | Count from PIPE-03 |
| endpointsCovered | int | Distinct EndpointId count |
| llmModel | string? | LLM model used (null if LLM not used) |
| llmTokensUsed | int? | Tokens consumed (null if LLM not used) |
| generatedAt | DateTimeOffset | Completion timestamp |
| testCases | List\<GeneratedTestCaseSummary\> | Summary of created test cases |

### `GeneratedTestCaseSummary` fields: testCaseId (Guid), endpointId (Guid?), name (string), httpMethod (string?), path (string?), orderIndex (int), variableCount (int).

## VALIDATION AND ERROR MAP

| HTTP | ReasonCode | ExceptionType | Condition |
|------|-----------|---------------|-----------|
| 400 | INVALID_INPUT | ValidationException | TestSuiteId or SpecificationId is Guid.Empty |
| 400 | NO_PIPELINE_ENABLED | ValidationException | All 3 pipeline flags are false |
| 400 | OWNERSHIP_DENIED | ValidationException | CreatedById != CurrentUserId |
| 400 | SUITE_ARCHIVED | ValidationException | Suite.Status == Archived |
| 400 | EXISTING_CASES_FOUND | ValidationException | Existing boundary/negative cases exist AND ForceRegenerate=false |
| 400 | SUBSCRIPTION_LIMIT_EXCEEDED | ValidationException | Exceeds MaxTestCasesPerSuite |
| 400 | LLM_CALL_LIMIT_EXCEEDED | ValidationException | Exceeds MaxLlmCallsPerMonth (only when IncludeLlmSuggestions=true) |
| 404 | TEST_SUITE_NOT_FOUND | NotFoundException | Suite not found |
| 409 | ORDER_CONFIRMATION_REQUIRED | ConflictException | FE-05A gate fail |

## IMPLEMENTATION SEQUENCE (MANDATORY ORDER)

### STEP 1: Models

Create these files in `ClassifiedAds.Modules.TestGeneration/Models/`:

**1a. `BodyMutation.cs`**
```
Fields:
- MutationType: string (emptyBody|malformedJson|missingRequired|typeMismatch|overflow|invalidEnum)
- Label: string (human-readable mutation description)
- MutatedBody: string? (null for emptyBody null variant)
- TargetFieldName: string? (null for whole-body mutations)
- ExpectedStatusCode: int (default: 400)
- Description: string
- SuggestedTestType: TestType (Boundary|Negative)
```

**1b. `GenerateBoundaryNegativeResultModel.cs`** — API response model with per-pipeline counts and test case summaries.

**1c. `N8nBoundaryNegativePayload.cs`** — n8n webhook request payload.
```
N8nBoundaryNegativePayload:
- TestSuiteId: Guid
- TestSuiteName: string
- GlobalBusinessRules: string?
- Endpoints: List<N8nBoundaryEndpointPayload>

N8nBoundaryEndpointPayload:
- EndpointId: Guid
- HttpMethod: string
- Path: string
- OperationId: string?
- OrderIndex: int
- BusinessContext: string?
- Prompt: N8nPromptPayload? (SystemPrompt, CombinedPrompt, ObservationPrompt, ConfirmationPromptTemplate)
- ParameterSchemaPayloads: List<string>
- ResponseSchemaPayloads: List<string>
- ParameterDetails: List<N8nParameterDetail>

N8nParameterDetail:
- Name: string
- Location: string
- DataType: string
- Format: string?
- IsRequired: bool
- DefaultValue: string?
```

**1d. `N8nBoundaryNegativeResponse.cs`** — n8n webhook response.
```
N8nBoundaryNegativeResponse:
- Scenarios: List<N8nSuggestedScenario>
- Model: string?
- TokensUsed: int?

N8nSuggestedScenario:
- EndpointId: Guid
- ScenarioName: string
- Description: string
- TestType: string ("Boundary"|"Negative")
- Priority: string? ("critical"|"high"|"medium"|"low")
- Tags: List<string>
- Request: N8nTestCaseRequest (reuse from FE-05B)
- Expectation: N8nTestCaseExpectation (reuse from FE-05B)
- Variables: List<N8nTestCaseVariable> (reuse from FE-05B)
```

**1e. `Requests/GenerateBoundaryNegativeTestCasesRequest.cs`** — API request model (see Request table above).

### STEP 2: Interfaces

**2a. `Services/IBodyMutationEngine.cs`**
```csharp
public interface IBodyMutationEngine
{
    IReadOnlyList<BodyMutation> GenerateMutations(BodyMutationContext context);
}

public class BodyMutationContext
{
    public Guid EndpointId { get; set; }
    public string HttpMethod { get; set; }
    public string Path { get; set; }
    public IReadOnlyList<ParameterDetailDto> BodyParameters { get; set; }
    public string? RequestBodySchema { get; set; }
}
```

**2b. `Services/ILlmScenarioSuggester.cs`**
```csharp
public interface ILlmScenarioSuggester
{
    Task<LlmScenarioSuggestionResult> SuggestScenariosAsync(LlmScenarioSuggestionContext context, CancellationToken ct);
}

public class LlmScenarioSuggestionContext
{
    public Guid TestSuiteId { get; set; }
    public Guid UserId { get; set; }
    public TestSuite Suite { get; set; }
    public IReadOnlyList<ApiEndpointMetadataDto> EndpointMetadata { get; set; }
    public IReadOnlyList<ApiOrderItemModel> OrderedEndpoints { get; set; }
    public Guid SpecificationId { get; set; }
}

public class LlmScenarioSuggestionResult
{
    public IReadOnlyList<LlmSuggestedScenario> Scenarios { get; set; }
    public string? LlmModel { get; set; }
    public int? TokensUsed { get; set; }
    public int? LatencyMs { get; set; }
    public bool FromCache { get; set; }
}

public class LlmSuggestedScenario
{
    public Guid EndpointId { get; set; }
    public string ScenarioName { get; set; }       // max 200 chars after sanitize
    public string Description { get; set; }
    public TestType SuggestedTestType { get; set; } // default: Negative
    public string? SuggestedBody { get; set; }
    public Dictionary<string, string>? SuggestedPathParams { get; set; }
    public Dictionary<string, string>? SuggestedQueryParams { get; set; }
    public Dictionary<string, string>? SuggestedHeaders { get; set; }
    public int ExpectedStatusCode { get; set; }     // default: 400
    public string? ExpectedBehavior { get; set; }
    public string? Priority { get; set; }           // critical|high|medium|low
    public List<string> Tags { get; set; }
}
```

**2c. `Services/IBoundaryNegativeTestCaseGenerator.cs`**
```csharp
public interface IBoundaryNegativeTestCaseGenerator
{
    Task<BoundaryNegativeGenerationResult> GenerateAsync(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        Guid specificationId,
        BoundaryNegativeOptions options,
        CancellationToken ct);
}

public class BoundaryNegativeOptions
{
    public bool IncludePathMutations { get; set; } = true;
    public bool IncludeBodyMutations { get; set; } = true;
    public bool IncludeLlmSuggestions { get; set; } = true;
    public Guid UserId { get; set; }
}

public class BoundaryNegativeGenerationResult
{
    public IReadOnlyList<TestCase> TestCases { get; set; }
    public int PathMutationCount { get; set; }
    public int BodyMutationCount { get; set; }
    public int LlmSuggestionCount { get; set; }
    public string? LlmModel { get; set; }
    public int? LlmTokensUsed { get; set; }
    public int EndpointsCovered { get; set; }
}
```

### STEP 3: BodyMutationEngine

File: `Services/BodyMutationEngine.cs`
- Stateless, no external dependencies.
- Method: `IReadOnlyList<BodyMutation> GenerateMutations(BodyMutationContext context)`
- Only applies to POST/PUT/PATCH. For GET/DELETE/HEAD/OPTIONS → return empty list.

**6 mutation types:**

| Type | Category | TestType | Variants |
|------|----------|----------|----------|
| emptyBody | whole-body | Negative | 3: body=null, body="", body="{}" |
| malformedJson | whole-body | Negative | 3: missing closing brace, truncated value, plain text |
| missingRequired | per-field | Negative | N (one per required field) — build base body, remove one required field each |
| typeMismatch | per-field | Negative | M (one per field) — replace value with wrong type |
| overflow | per-field | Boundary | ≤M (numeric/string fields only) |
| invalidEnum | per-field | Negative | ≤K (fields with enum schema only) |

**Wrong type mapping for typeMismatch:**
```
integer|int|long|number|float|double|decimal → "not_a_number"
boolean|bool → "not_a_boolean"
string → 12345
array → "not_an_array"
object → "not_an_object"
default → 12345
```

**Overflow value mapping:**
```
integer|int (format: int32) → 2147483648 (Int32.MaxValue + 1)
integer|int|long (no format or format != int32) → 9223372036854775807 (Int64.MaxValue)
number|float|double|decimal → 999999999999.999
string → new string('a', 10000)
boolean|array|object → SKIP (no overflow mutation)
```

**Schema-based mutations:**
- Trigger: `context.RequestBodySchema != null` and valid JSON.
- Parse JSON schema, extract `properties` object and `required` array.
- Find properties in `required` but NOT in `BodyParameters` (by name, case-insensitive).
- Create `missingRequired` mutation for each, with `MutatedBody = "{}"`.
- Catch `JsonException` → skip schema-based mutations.

**BuildBaseBody method:**
- Build base body object from all body parameters.
- Default value strategy: param.DefaultValue → param.Examples → fallback by DataType:
  - integer/int/long → 1
  - number/float/double/decimal → 1.0
  - boolean/bool → true
  - array → []
  - object → {}
  - string/default → "sample_value"
- Use `TryParseJsonValue(string value, string dataType)` to parse DefaultValue/Examples as JSON.

### STEP 4: LlmScenarioSuggester

File: `Services/LlmScenarioSuggester.cs`
Dependencies: `IObservationConfirmationPromptBuilder`, `IN8nIntegrationService`, `ILlmAssistantGatewayService`, `ILogger<LlmScenarioSuggester>`

**6-step pipeline in `SuggestScenariosAsync`:**

```
Step 1: Build cache key
  - Input string: "{testSuiteId}:{specificationId}:{endpointId1},{endpointId2},..." (endpointIds sorted ASC)
  - SHA256 hash → take first 16 hex characters
  - Deterministic: same input → same cache key

Step 2: Check cache (all-or-nothing)
  - For each endpointId: call ILlmAssistantGatewayService.GetCachedSuggestionsAsync(endpointId, suggestionType=1, cacheKey)
  - If ALL endpoints have HasCache=true → deserialize JSON, return LlmScenarioSuggestionResult { FromCache=true }
  - If ANY endpoint has HasCache=false → return null (full cache miss, proceed to call LLM)
  - On JsonException during deserialization → log warning, return null (treat as cache miss)

Step 3: Build prompts
  - Map metadata to EndpointPromptContext via EndpointPromptContextMapper.Map(orderedMetadata, suite)
  - Build prompts via IObservationConfirmationPromptBuilder.BuildForSequence(promptContexts)

Step 4: Build payload and call n8n
  - Build N8nBoundaryNegativePayload with per-endpoint data
  - Map prompt[i] to endpoint[i]; if i >= prompts.Count → prompt = null
  - Call IN8nIntegrationService.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
      webhookName = "generate-boundary-negative-scenarios", payload)
  - Measure latency via Stopwatch

Step 5: Save audit (GRACEFUL — must not fail main flow)
  - try {
      await ILlmAssistantGatewayService.SaveInteractionAsync({
        UserId, InteractionType=0 (ScenarioSuggestion),
        InputContext=serialized-payload, LlmResponse=serialized-response,
        ModelUsed=response.Model, TokensUsed=response.TokensUsed, LatencyMs
      });
    } catch (Exception ex) {
      _logger.LogWarning(ex, "Failed to save LLM interaction for audit");
      // DO NOT THROW — continue
    }

Step 6: Parse and cache
  - Parse N8nBoundaryNegativeResponse.Scenarios → List<LlmSuggestedScenario>
  - ParseTestType: "boundary" → Boundary, null/empty/anything else → Negative (case-insensitive)
  - ExpectedStatusCode: s.Expectation?.ExpectedStatus?.FirstOrDefault() ?? 400
  - ExpectedBehavior: s.Expectation?.BodyContains?.FirstOrDefault()
  - Cache per-endpoint: group by EndpointId, serialize, call CacheSuggestionsAsync(endpointId, 1, cacheKey, json, TTL=24h)
  - Cache save is GRACEFUL: catch Exception → log warning, do not throw
```

**JSON serialization config:**
```csharp
new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = false }
```

### STEP 5: BoundaryNegativeTestCaseGenerator (Orchestrator)

File: `Services/BoundaryNegativeTestCaseGenerator.cs`
Dependencies: `IApiEndpointMetadataService`, `IApiEndpointParameterDetailService`, `IPathParameterMutationGatewayService`, `IBodyMutationEngine`, `ILlmScenarioSuggester`, `ITestCaseRequestBuilder`, `ITestCaseExpectationBuilder`, `ILogger`

**Pipeline in `GenerateAsync`:**

```
Step 1: Fetch endpoint metadata
  - IApiEndpointMetadataService.GetEndpointMetadataAsync(specificationId, endpointIds)
  - Result: Dictionary<Guid, ApiEndpointMetadataDto>

Step 2: Fetch parameter details (if IncludePathMutations OR IncludeBodyMutations)
  - IApiEndpointParameterDetailService.GetParameterDetailsAsync(specificationId, endpointIds)
  - Result: Dictionary<Guid, EndpointParameterDetailDto>

Step 3: Iterate ordered endpoints, per endpoint:
  3a. Path Mutation Pipeline (if IncludePathMutations AND endpoint has path parameters):
    - Filter parameters with Location="Path" (case-insensitive)
    - For each path parameter: call IPathParameterMutationGatewayService.GenerateMutations(name, dataType, format, defaultValue)
    - Build TestCase per mutation via BuildPathMutationTestCase

  3b. Body Mutation Pipeline (if IncludeBodyMutations):
    - Build BodyMutationContext from body parameters (Location="Body") and RequestBodySchema
    - Call IBodyMutationEngine.GenerateMutations(context)
    - Build TestCase per mutation via BuildBodyMutationTestCase

Step 4: LLM Suggestion Pipeline (if IncludeLlmSuggestions)
  - Build LlmScenarioSuggestionContext
  - Call ILlmScenarioSuggester.SuggestScenariosAsync(context)
  - Build TestCase per scenario via BuildLlmSuggestionTestCase

Step 5: Assign sequential OrderIndex (0..N-1) to all test cases
  - Count endpointsCovered = distinct EndpointId count
  - Return BoundaryNegativeGenerationResult
```

**3 Builder methods:**

```
BuildPathMutationTestCase:
  - Name: "{HttpMethod} {Path} - {mutation.Label}"
  - TestType: ClassifyPathMutationType(mutation.Type)
  - Tags: [testType, "auto-generated", "rule-based", "path-mutation"]
  - Request: HttpMethod, Url=path, PathParams={paramName: mutation.Value}, BodyType=None, Timeout=30000ms
  - Expectation: ExpectedStatus=[mutation.ExpectedStatusCode]
  - Priority: High

BuildBodyMutationTestCase:
  - Name: "{HttpMethod} {Path} - {mutation.Label}"
  - TestType: mutation.SuggestedTestType
  - Tags: [testType, "auto-generated", "rule-based", "body-mutation"]
  - Request: HttpMethod, Url=path, BodyType=JSON, Body=mutation.MutatedBody, Timeout=30000ms
  - Expectation: ExpectedStatus=[mutation.ExpectedStatusCode]
  - Priority: High

BuildLlmSuggestionTestCase:
  - Name: SanitizeName(scenario.ScenarioName, orderItem) — max 200 chars, fallback: "{HttpMethod} {Path} - Boundary/Negative"
  - TestType: scenario.SuggestedTestType
  - Tags: [testType, "auto-generated", "llm-suggested", ...scenario.Tags]
  - Request: via ITestCaseRequestBuilder.Build (reuse FE-05B builder)
  - Expectation: via ITestCaseExpectationBuilder.Build (reuse FE-05B builder)
  - Priority: ParsePriority(scenario.Priority) — default Medium
```

**Helper methods:**

```
ClassifyPathMutationType(string mutationType):
  - If mutationType contains "boundary" OR "zero" OR "max" OR "overflow" → TestType.Boundary
  - Else → TestType.Negative
  - Null/empty → TestType.Negative

SanitizeName(string scenarioName, ApiOrderItemModel orderItem):
  - Truncate to 200 chars
  - If empty/null → "{HttpMethod} {Path} - Boundary/Negative"

ParsePriority(string priority):
  - "critical" → Critical, "high" → High, "medium" → Medium, "low" → Low
  - Default: Medium

ParseHttpMethod(string method):
  - Parse to Entities.HttpMethod enum. Default: GET

SerializeTags(TestType testType, string source, params string[] extra):
  - Auto-add: testType.ToString().ToLower(), "auto-generated", source
  - Serialize to JSON array string
```

### STEP 6: Command + Handler

File: `Commands/GenerateBoundaryNegativeTestCasesCommand.cs`

```csharp
public class GenerateBoundaryNegativeTestCasesCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
    public Guid SpecificationId { get; set; }
    public bool ForceRegenerate { get; set; }
    public bool IncludePathMutations { get; set; } = true;
    public bool IncludeBodyMutations { get; set; } = true;
    public bool IncludeLlmSuggestions { get; set; } = true;
    public GenerateBoundaryNegativeResultModel Result { get; set; }
}
```

**Handler 10-step pipeline:**

```
Step 1: Validate inputs
  - TestSuiteId == Guid.Empty → throw ValidationException("TestSuiteId la bat buoc.")
  - SpecificationId == Guid.Empty → throw ValidationException("SpecificationId la bat buoc.")
  - All 3 pipelines false → throw ValidationException("It nhat mot nguon tao test case phai duoc bat.")

Step 2: Load & validate suite
  - Load TestSuite by ID → NotFoundException("Khong tim thay test suite voi ma '{id}'.")
  - CreatedById != CurrentUserId → ValidationException("Ban khong co quyen thao tac test suite nay.")
  - Status == Archived → ValidationException("Khong the generate test cases cho test suite da archived.")

Step 3: FE-05A gate
  - IApiTestOrderGateService.RequireApprovedOrderAsync(testSuiteId)
  - Throws ConflictException(409, "ORDER_CONFIRMATION_REQUIRED") if gate fails
  - Returns ordered endpoints list on success

Step 4: Check existing cases
  - Query TestCase where TestSuiteId=id AND TestType IN [Boundary, Negative]
  - If count > 0 AND ForceRegenerate=false → ValidationException("Test suite da co {count} boundary/negative test case(s). Su dung ForceRegenerate=true de tao lai.")

Step 5: Subscription limits
  - ISubscriptionLimitGatewayService.CheckLimitAsync(userId, MaxTestCasesPerSuite, endpointCount)
  - If IncludeLlmSuggestions=true → also CheckLimitAsync(userId, MaxLlmCallsPerMonth, 1)

Step 6: Delete old cases (if ForceRegenerate=true AND existing cases exist)
  - Delete all existing boundary/negative test cases for this suite
  - SaveChanges (separate from main transaction)

Step 7: Generate
  - Call IBoundaryNegativeTestCaseGenerator.GenerateAsync(suite, orderedEndpoints, specificationId, options)

Step 8: Empty result check
  - If generationResult.TestCases is empty → build empty result model, set on command.Result, return

Step 9: Persist in transaction (ExecuteInTransactionAsync)
  - For each TestCase:
    - AddAsync(testCase)
    - AddAsync(testCase.Request)
    - AddAsync(testCase.Expectation)
    - AddAsync(each variable)
    - AddAsync(each dependency)
    - AddAsync(TestCaseChangeLog { ChangeType=Created })
  - AddAsync(TestSuiteVersion { VersionNumber=suite.Version+1, ChangeType=TestCasesModified, ChangeDescription=summary })
  - UpdateAsync(TestSuite { Version++, Status=Ready, LastModifiedById, RowVersion=new })
  - SaveChangesAsync (1 time at end of transaction)

Step 10: Post-transaction
  - IncrementUsageAsync(MaxTestCasesPerSuite, totalGenerated)
  - If LLM used and llmSuggestionCount > 0 → IncrementUsageAsync(MaxLlmCallsPerMonth, 1)
  - Build GenerateBoundaryNegativeResultModel, set on command.Result
```

### STEP 7: Authorization, Controller, Constants, DI

**7a. `Authorization/Permissions.cs`** — Add:
```csharp
public const string GenerateBoundaryNegativeTestCases = "Permission:GenerateBoundaryNegativeTestCases";
```

**7b. `Controllers/TestCasesController.cs`** — Add endpoint:
```csharp
[HttpPost("generate-boundary-negative")]
[Authorize(Permissions.GenerateBoundaryNegativeTestCases)]
public async Task<ActionResult<GenerateBoundaryNegativeResultModel>> GenerateBoundaryNegative(
    Guid suiteId,
    [FromBody] GenerateBoundaryNegativeTestCasesRequest request)
{
    // Dispatch GenerateBoundaryNegativeTestCasesCommand
    // Return CreatedAtAction(201) with result
}
```

**7c. `Constants/N8nWebhookNames.cs`** — Add:
```csharp
public const string GenerateBoundaryNegative = "generate-boundary-negative-scenarios";
```

**7d. `ServiceCollectionExtensions.cs`** — Register:
```csharp
services.AddScoped<IBodyMutationEngine, BodyMutationEngine>();
services.AddScoped<ILlmScenarioSuggester, LlmScenarioSuggester>();
services.AddScoped<IBoundaryNegativeTestCaseGenerator, BoundaryNegativeTestCaseGenerator>();
```

## CROSS-MODULE INTERFACES (ALL PRE-EXISTING — DO NOT CREATE)

| Interface | Module | Key Methods |
|-----------|--------|-------------|
| IApiEndpointMetadataService | ApiDocumentation | GetEndpointMetadataAsync(specId, endpointIds) |
| IApiEndpointParameterDetailService | ApiDocumentation | GetParameterDetailsAsync(specId, endpointIds) |
| IPathParameterMutationGatewayService | ApiDocumentation | GenerateMutations(name, dataType, format, defaultValue) |
| ILlmAssistantGatewayService | LlmAssistant | SaveInteractionAsync, GetCachedSuggestionsAsync, CacheSuggestionsAsync |
| ISubscriptionLimitGatewayService | Subscription | CheckLimitAsync, IncrementUsageAsync |
| IApiTestOrderGateService | TestGeneration (shared FE-05) | RequireApprovedOrderAsync |
| ITestCaseRequestBuilder | TestGeneration (shared FE-05B) | Build |
| ITestCaseExpectationBuilder | TestGeneration (shared FE-05B) | Build |
| IObservationConfirmationPromptBuilder | TestGeneration (shared FE-05B) | BuildForSequence |
| IN8nIntegrationService | TestGeneration (shared FE-05B) | TriggerWebhookAsync |
| EndpointPromptContextMapper | TestGeneration (shared FE-05B) | Map |

## UNIT TESTS (MANDATORY)

Create 4 test files in `ClassifiedAds.UnitTests/TestGeneration/`:

**File 1: `BodyMutationEngineTests.cs`**
- Test all 6 mutation types with various data types
- Test schema-based mutations when JSON schema is present
- Test POST/PUT/PATCH endpoints produce emptyBody + malformedJson mutations
- Test GET/DELETE/HEAD/OPTIONS endpoints return empty list
- Test BuildBaseBody default value strategy
- Test overflow skips boolean/array/object fields
- Test invalidEnum extraction from JSON schema

**File 2: `BoundaryNegativeTestCaseGeneratorTests.cs`**
- Test toggle each pipeline independently (path only, body only, LLM only, all, none)
- Test OrderIndex assigned sequentially (0..N-1)
- Test endpointsCovered counts distinct EndpointIds correctly
- Test ClassifyPathMutationType classification logic
- Test SanitizeName truncation and fallback
- Test metadata fetch is skipped when both path+body disabled

**File 3: `LlmScenarioSuggesterTests.cs`**
- Test cache hit returns cached result immediately without calling n8n
- Test cache miss calls n8n webhook and caches result
- Test partial cache miss (1 endpoint missing) triggers full LLM call
- Test n8n response parsing (valid scenarios)
- Test null/empty response returns empty scenario list
- Test audit log failure does NOT throw (graceful degradation)
- Test cache save failure does NOT throw
- Test cache key determinism (same input → same key)

**File 4: `GenerateBoundaryNegativeTestCasesCommandHandlerTests.cs`**
- Test gate fail returns 409 ConflictException
- Test existing cases + ForceRegenerate=false returns 400
- Test ForceRegenerate=true deletes old cases before generating
- Test subscription limit exceeded returns 400
- Test LLM call limit exceeded returns 400 (when IncludeLlmSuggestions=true)
- Test empty generation result skips transaction
- Test all 3 pipelines disabled returns 400
- Test suite not found returns 404
- Test ownership check fails returns 400
- Test archived suite returns 400
- Test successful generation persists in transaction and increments usage

## QUALITY GATES (VERIFY BEFORE DONE)

```bash
# 1. Build entire solution — must succeed with 0 errors
dotnet build

# 2. Run unit tests — all must pass
dotnet test

# 3. Verify no pending model changes (should be none since no entity changes)
dotnet ef migrations has-pending-model-changes --context TestGenerationDbContext --project ClassifiedAds.Migrator --startup-project ClassifiedAds.Migrator
```

## TRANSACTION RULES

- Main persist: ALL test cases + change logs + suite version + suite update in ONE `ExecuteInTransactionAsync` call with ONE `SaveChangesAsync` at the end.
- Subscription usage increment: AFTER main transaction (not inside).
- Delete old cases: AFTER subscription check, BEFORE main transaction.
- LLM audit logging: BEFORE main transaction (in orchestrator), graceful failure.
- LLM cache save: BEFORE main transaction (in orchestrator), graceful failure.

## GRACEFUL DEGRADATION RULES

These failures MUST NOT crash the main flow:
1. LLM interaction audit save → catch Exception, log warning, continue
2. LLM cache save → catch Exception, log warning, continue
3. LLM cache deserialization → catch JsonException, log warning, return null (treat as cache miss)

These failures ARE critical (propagate to client):
1. n8n webhook call failure → Exception propagates → HTTP 500
2. Database transaction failure → Exception propagates → HTTP 500
