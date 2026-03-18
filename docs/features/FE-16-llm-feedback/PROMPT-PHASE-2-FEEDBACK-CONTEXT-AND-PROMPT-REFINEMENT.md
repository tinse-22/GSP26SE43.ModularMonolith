# PHASE 2 PROMPT - Feedback Context And Prompt Refinement For FE-16

Implement only the internal runtime logic that turns feedback rows into prompt-refinement input for future suggestion generation. Do not add controller/HTTP feedback write surface yet.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.TestGeneration`
- related unit tests in `ClassifiedAds.UnitTests/TestGeneration`

## Goal

Aggregate feedback by suite + endpoint, build a stable feedback fingerprint, and make `LlmScenarioSuggester` consume it in a cache-safe way.

## Files To Add

- `Services/ILlmSuggestionFeedbackContextService.cs`
- `Services/LlmSuggestionFeedbackContextService.cs`

## Files To Modify

- `Services/LlmScenarioSuggester.cs`
- `Models/N8nBoundaryNegativePayload.cs`
- optionally prompt-context files if you choose repo-owned prompt enrichment

## Required Service Contract

Example direction:

```csharp
Task<LlmSuggestionFeedbackContextResult> BuildAsync(
    Guid testSuiteId,
    IReadOnlyCollection<Guid> endpointIds,
    CancellationToken ct = default);
```

Result should carry:

- endpoint-scoped feedback context text
- a stable feedback fingerprint/hash

## Aggregation Rules

- aggregate only suggestions in the requested suite
- group feedback by `EndpointId`
- ignore suggestions without endpoint id
- ignore `Superseded` suggestions
- include both helpful and not-helpful counts
- include only a bounded number of recent notes
- trim and truncate notes before building prompt context
- never include raw `UserId` values in the prompt context

## Cache-Key Rule

If feedback context is used by `LlmScenarioSuggester`, the cache key must include a feedback fingerprint.

That means:

- same suite/spec/endpoints + same feedback fingerprint -> cache key stable
- same suite/spec/endpoints + changed feedback fingerprint -> cache key changes

Do not keep the old cache key algorithm if it would ignore feedback changes.

## Prompt / Payload Rule

One of these is acceptable:

1. add `FeedbackContext` to `N8nBoundaryEndpointPayload`
2. append feedback context into in-process prompt text via `EndpointPromptContext` / prompt builder

Choose one clean path. Do not do both unless clearly necessary.

## Graceful Degradation

- if feedback aggregation fails, log warning and continue generation with empty feedback context
- if no feedback exists, use empty context and a stable empty fingerprint

## Tests

Add unit tests for:

- helpful/not-helpful counts are aggregated correctly
- notes are truncated/sanitized
- fingerprint is stable for identical inputs
- fingerprint changes when feedback changes
- `LlmScenarioSuggester` cache key changes when feedback context changes
- empty-feedback path still works

Stop after runtime + tests for this phase are done. Do not add controller/command code yet.
