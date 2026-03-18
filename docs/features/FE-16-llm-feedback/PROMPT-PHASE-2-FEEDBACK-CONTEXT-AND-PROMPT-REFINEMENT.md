# PHASE 2 PROMPT - Feedback Context And Prompt Refinement For FE-16

Implement only the internal runtime logic that turns feedback rows into prompt-refinement input for future suggestion generation. Do not add controller/HTTP feedback write surface yet.

## Task Classification

Expected classification for this phase:

- `Application code only`

No new migration should be needed in this phase. If you end up changing EF model shape, you must go back through the migration gate from `AGENTS.md`.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.TestGeneration`
- related unit tests in `ClassifiedAds.UnitTests/TestGeneration`

## Goal

Aggregate feedback by suite + endpoint, build a stable feedback fingerprint, and make `LlmScenarioSuggester` consume it in a cache-safe way.

## Preferred Solution For This Phase

- Add `ILlmSuggestionFeedbackContextService` and `LlmSuggestionFeedbackContextService`.
- Build endpoint-scoped feedback context outside the prompt builder.
- Update `LlmScenarioSuggester` so feedback is loaded before cache lookup.
- Extend `N8nBoundaryEndpointPayload` with `FeedbackContext`.
- Keep `ObservationConfirmationPromptBuilder` and `EndpointPromptContext` unchanged unless payload enrichment proves impossible.

This is the preferred path because feedback is user-review context, not OpenAPI-spec evidence.

## Files To Add

- `Services/ILlmSuggestionFeedbackContextService.cs`
- `Services/LlmSuggestionFeedbackContextService.cs`

## Files To Modify

- `Services/LlmScenarioSuggester.cs`
- `Models/N8nBoundaryNegativePayload.cs`

## Required Service Contract

Example direction:

```csharp
Task<LlmSuggestionFeedbackContextResult> BuildAsync(
    Guid testSuiteId,
    IReadOnlyCollection<Guid> endpointIds,
    CancellationToken ct = default);
```

Result should carry:

- endpoint-scoped feedback context text, keyed by `EndpointId`
- a stable feedback fingerprint/hash

The result type can be a small DTO or record, but it should be explicit and deterministic.

## Aggregation Rules

- aggregate only suggestions in the requested suite
- group feedback by `EndpointId`
- ignore suggestions without endpoint id
- ignore `Superseded` suggestions
- include both helpful and not-helpful counts
- include only a bounded number of recent notes per endpoint
- trim, normalize, and truncate notes before building prompt context
- never include raw `UserId` values in the prompt context

Recommended normalization rules:

- trim outer whitespace
- collapse empty-or-whitespace-only notes to null
- truncate note snippets before prompt use
- keep only a small bounded number of note snippets per endpoint, for example 3

## Fingerprint Rules

The feedback fingerprint must be built from canonical aggregated output, not from arbitrary DB row order.

Required properties:

- same suite/spec/endpoints + same aggregated feedback -> same fingerprint
- same suite/spec/endpoints + changed aggregated feedback -> different fingerprint
- reordered DB rows must not change the fingerprint
- whitespace-only note differences after normalization must not change the fingerprint
- raw `UserId`, `RowVersion`, and timestamps must not appear in fingerprint material

Recommended hashing approach:

- sort endpoint groups by `EndpointId`
- within each endpoint, use normalized counts + normalized note snippets
- hash the canonical string or canonical JSON representation

## Suggester Integration Rules

If feedback context is used by `LlmScenarioSuggester`, the cache key must include the feedback fingerprint.

Required behavior:

- load feedback context before cache lookup
- include the fingerprint in cache-key construction
- attach endpoint-specific `FeedbackContext` to each `N8nBoundaryEndpointPayload`
- leave feedback context empty for endpoints with no usable feedback

Do not keep the old cache-key algorithm if it would ignore feedback changes.

## Graceful Degradation

- if feedback aggregation fails, log warning and continue generation with empty feedback context
- if no feedback exists, use empty context and a stable empty fingerprint
- feedback failures must not break the whole FE-06/FE-15 generation flow

## Tests

Add unit tests for:

- helpful/not-helpful counts are aggregated correctly
- notes are truncated and sanitized
- fingerprint is stable for identical normalized inputs
- fingerprint changes when aggregated feedback changes
- `LlmScenarioSuggester` cache key changes when feedback context changes
- empty-feedback path still works
- payload enrichment includes endpoint-specific feedback context

## Done Criteria

- feedback aggregation service exists
- aggregation ignores `Superseded` and endpoint-less suggestions
- feedback fingerprint is deterministic and cache-safe
- `LlmScenarioSuggester` consumes the fingerprint before cache lookup
- `N8nBoundaryEndpointPayload` carries `FeedbackContext`
- prompt-builder files remain unchanged unless a real blocker forced otherwise
- targeted tests for this phase pass

Stop after runtime + tests for this phase are done. Do not add controller/command code yet.
