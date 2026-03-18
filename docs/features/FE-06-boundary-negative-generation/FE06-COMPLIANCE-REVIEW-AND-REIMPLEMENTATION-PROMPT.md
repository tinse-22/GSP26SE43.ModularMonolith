# FE-06 Compliance Review and Re-Implementation Prompt

Date: 2026-03-09
Scope: `ClassifiedAds.Modules.TestGeneration` and `ClassifiedAds.UnitTests/TestGeneration`
Verdict: FE-06 is partially implemented, but the current code is not fully compliant with the prompt.

## Review Summary

The module already contains the FE-06 surface area:

- request/response models
- controller endpoint
- authorization constant
- DI registrations
- `BodyMutationEngine`
- `LlmScenarioSuggester`
- `BoundaryNegativeTestCaseGenerator`
- `GenerateBoundaryNegativeTestCasesCommand`
- FE-06 unit test files

However, several required behaviors are still missing or only partially implemented.

## Confirmed Gaps In Current Code

### 1. `N8nBoundaryEndpointPayload.ParameterDetails` is never populated

The prompt requires each endpoint payload sent to n8n to include `ParameterDetails`.

Evidence:

- Prompt requires `ParameterDetails` in payload: `PROMPT-IMPLEMENT-FE06.md:104-123`
- Payload model contains the property: `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs:20-55`
- `BuildN8nPayload` never assigns `ParameterDetails`: `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs:196-207`

Impact:

- n8n does not receive per-parameter metadata for boundary/negative suggestion quality.
- The code cannot satisfy the payload contract described in the prompt.

Required fix:

- Extend the FE-06 orchestration flow so `LlmScenarioSuggester` receives endpoint parameter details.
- Populate `N8nBoundaryEndpointPayload.ParameterDetails` for every endpoint.

### 2. LLM variables are dropped end-to-end

The prompt requires `N8nSuggestedScenario.Variables` to be reused from FE-05B and persisted as `TestCaseVariable`.

Evidence:

- Prompt requires `Variables` in n8n response: `PROMPT-IMPLEMENT-FE06.md:132-142`
- Response model includes `Variables`: `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativeResponse.cs:18-39`
- `LlmSuggestedScenario` has no variables property: `ClassifiedAds.Modules.TestGeneration/Services/ILlmScenarioSuggester.cs:50-75`
- `ParseScenarios` discards `s.Variables`: `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs:226-240`
- `BuildLlmSuggestionTestCase` builds request and expectation only, never `TestCaseVariable`: `ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs:297-341`
- FE-05B already has the reference mapping pattern for variables: `ClassifiedAds.Modules.TestGeneration/Services/HappyPathTestCaseGenerator.cs:221-238`

Impact:

- Any extraction variables returned by n8n are lost before persistence.
- The command handler can persist variables, but FE-06 never creates them for LLM scenarios.

Required fix:

- Add variables to the FE-06 in-memory suggestion model.
- Parse `N8nSuggestedScenario.Variables` in `LlmScenarioSuggester`.
- Convert them to `TestCaseVariable` in `BoundaryNegativeTestCaseGenerator`, reusing the same extraction mapping rules as FE-05B.

### 3. `BodyMutationEngine.TryParseJsonValue` does not materialize array/object JSON defaults/examples

The prompt explicitly requires `TryParseJsonValue` to parse `DefaultValue` and `Examples` as JSON for `BuildBaseBody`.

Evidence:

- Prompt requirement: `PROMPT-IMPLEMENT-FE06.md:284-293`
- Current parser returns the raw input string for arrays and objects because only number/bool/string are handled explicitly: `ClassifiedAds.Modules.TestGeneration/Services/BodyMutationEngine.cs:382-406`

Impact:

- A default like `{"mode":"strict"}` or `[1,2,3]` is inserted into the base body as a string instead of a JSON object/array.
- Mutated bodies can be structurally wrong even when the source metadata provides valid JSON defaults/examples.

Required fix:

- Implement recursive JSON conversion for object and array values.
- Update tests to cover object and array defaults/examples.

### 4. Existing FE-06 report in repo should not be treated as source of truth

`docs/features/FE-06-boundary-negative-generation/IMPLEMENTATION-REPORT.md` currently states FE-06 is complete and quality gates passed, but this does not match the code gaps above.

Action:

- Do not use the existing report as acceptance evidence.
- Use this file as the current source of truth for rework.

## Prompt Issues That Should Be Corrected Before Re-Implementation

The prompt itself contains two internal inconsistencies that can mislead the next agent.

### A. Prompt requires `ParameterDetails`, but the suggester context does not carry them

Evidence:

- Payload contract requires `ParameterDetails`: `PROMPT-IMPLEMENT-FE06.md:104-123`
- `LlmScenarioSuggestionContext` in the prompt does not include endpoint parameter details: `PROMPT-IMPLEMENT-FE06.md:172-205`

Correction:

- Add endpoint parameter details to `LlmScenarioSuggestionContext`, or
- explicitly instruct `BoundaryNegativeTestCaseGenerator` to pass the already-fetched parameter detail map into `LlmScenarioSuggester`.

Recommended correction:

```csharp
public IReadOnlyList<EndpointParameterDetailDto> EndpointParameterDetails { get; set; }
```

### B. The unit-test bullet says metadata fetch is skipped when both path/body are disabled

Evidence:

- Prompt test bullet: `PROMPT-IMPLEMENT-FE06.md:573-580`

Why this is incorrect:

- LLM-only generation still needs endpoint metadata for prompts and payload construction.
- The thing that should be skipped is parameter-detail fetching, not metadata fetching.

Correction:

- Replace "metadata fetch is skipped when both path+body disabled"
- With "parameter-detail fetch is skipped when both path+body disabled"

### C. Controller return style conflicts with existing FE-05B pattern

Evidence:

- Prompt says `CreatedAtAction`: `PROMPT-IMPLEMENT-FE06.md:519-529`
- Existing FE-05B controller pattern uses `Created(...)`: `ClassifiedAds.Modules.TestGeneration/Controllers/TestCasesController.cs:55-75`

Correction:

- Prefer matching the existing FE-05B pattern unless both endpoints are intentionally changed together.

## Re-Implementation Prompt For AI Agent

Use this instead of the current optimistic report.

### Objective

Bring FE-06 to prompt-complete status without introducing new entities, tables, migrations, or cross-module interfaces.

### Non-Negotiable Constraints

- Keep all changes inside `ClassifiedAds.Modules.TestGeneration` and `ClassifiedAds.UnitTests/TestGeneration`.
- Do not add new EF entities or migrations.
- Do not access other modules' DbContexts or repositories directly.
- Reuse existing contracts only.
- Follow FE-05B code patterns where applicable.
- Keep user-facing validation messages in Vietnamese.

### Implementation Tasks

#### 1. Fix prompt contract drift for LLM payload parameter details

- Extend `LlmScenarioSuggestionContext` so it can carry endpoint parameter details already fetched by `BoundaryNegativeTestCaseGenerator`.
- In `BoundaryNegativeTestCaseGenerator.GenerateAsync`, pass the parameter-detail collection/map into `LlmScenarioSuggestionContext`.
- In `LlmScenarioSuggester.BuildN8nPayload`, populate `N8nBoundaryEndpointPayload.ParameterDetails` with:
  - `Name`
  - `Location`
  - `DataType`
  - `Format`
  - `IsRequired`
  - `DefaultValue`

#### 2. Preserve LLM variables and persist them as `TestCaseVariable`

- Add a variables collection to `LlmSuggestedScenario`.
- In `LlmScenarioSuggester.ParseScenarios`, map `N8nSuggestedScenario.Variables` into that collection.
- In `BoundaryNegativeTestCaseGenerator.BuildLlmSuggestionTestCase`, create `TestCaseVariable` entries from the parsed variables.
- Reuse the same extract-from parsing rules already used in happy path generation.

#### 3. Fix `BuildBaseBody` JSON default/example parsing

- Update `TryParseJsonValue` so it converts:
  - numbers to numeric CLR values
  - booleans to `bool`
  - strings to `string`
  - arrays to real `List<object>` or equivalent JSON-serializable structure
  - objects to real `Dictionary<string, object>` or equivalent JSON-serializable structure
- Ensure `BuildBaseBody` uses parsed JSON values for both `DefaultValue` and `Examples`.

#### 4. Align the prompt wording for the next pass

- Treat `parameter-detail fetch is skipped` as the correct expectation for LLM-only mode.
- Keep controller response style consistent with FE-05B unless the team explicitly wants `CreatedAtAction`.

## Mandatory Test Additions Or Updates

Add or update tests so the following are explicitly covered:

- `BodyMutationEngine` uses JSON object defaults correctly.
- `BodyMutationEngine` uses JSON array examples/defaults correctly.
- `LlmScenarioSuggester` includes `ParameterDetails` in the webhook payload.
- `LlmScenarioSuggester` preserves variables returned by n8n.
- `BoundaryNegativeTestCaseGenerator` converts preserved variables into `TestCaseVariable`.
- LLM-only mode skips parameter-detail fetching only when appropriate.

## Suggested Acceptance Checklist

- FE-06 payload sent to n8n contains `ParameterDetails`.
- LLM-returned variables survive parse -> generator -> transaction persistence.
- Base-body generation respects JSON defaults/examples for primitive and complex values.
- No new migrations are created.
- Existing FE-05B behavior remains unchanged.

## Verification Notes From This Review

I attempted local verification, but full quality-gate execution was blocked by the sandbox/tooling environment:

- `dotnet build` at solution level hit restore/signature issues around external package resolution.
- `dotnet test` was also not fully verifiable in this sandbox.

Because of that, the conclusions in this file are based on direct code inspection plus prompt-to-code comparison, not on a clean end-to-end execution of the full solution.
