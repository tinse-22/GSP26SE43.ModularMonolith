# PHASE 3 PROMPT - API Surface And Config For FE-09

Implement only the CQRS/API/config surface for FE-09. Reuse the phase-1 failure gateway and phase-2 runtime services.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.LlmAssistant`
- `ClassifiedAds.WebAPI`
- light unit tests directly related to handlers/controllers if needed

## Goal

Expose FE-09 through GET/POST APIs and wire runtime/config into the existing WebAPI host.

## Files To Add

- `Controllers/FailureExplanationsController.cs`
- `Commands/ExplainTestFailureCommand.cs`
- `Queries/GetFailureExplanationQuery.cs`

## Required Endpoints

1. `GET /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
2. `POST /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`

Both endpoints must:

- require `[Authorize("Permission:GetTestRuns")]`
- use `ICurrentUser`
- validate ownership by comparing `currentUserId` with `CreatedById` from failure-context gateway

## GET Handler Rules

The query handler must:

1. load failure context via `ITestFailureReadGatewayService`
2. validate owner
3. call `ILlmFailureExplainer.GetCachedAsync(...)`
4. return cached model
5. if no cache -> `NotFoundException` message prefixed with `FAILURE_EXPLANATION_NOT_FOUND:`

## POST Handler Rules

The command handler must:

1. load failure context via `ITestFailureReadGatewayService`
2. validate owner
3. load endpoint metadata via `IApiEndpointMetadataService` when both `ApiSpecId` and `EndpointId` are available
4. call `ILlmFailureExplainer.ExplainAsync(...)`
5. return cached or live model transparently

## Config Rules

Add `Modules:LlmAssistant` config in `ClassifiedAds.WebAPI/appsettings.json`.

Suggested structure:

```json
"LlmAssistant": {
  "FailureExplanation": {
    "Provider": "N8n",
    "Model": "gpt-4.1-mini",
    "TimeoutSeconds": 30,
    "CacheTtlHours": 24,
    "BaseUrl": "",
    "ApiKey": "",
    "WebhookPath": "explain-failure"
  }
}
```

If the repo already stores local overrides in `appsettings.Development.json`, add the matching subtree there too.

## Rules

- Do NOT modify `ClassifiedAds.Background` in this phase.
- Do NOT add new permissions or new identity seed work; reuse `Permission:GetTestRuns`.
- Do NOT add request bodies to the POST endpoint.
- Do NOT generate explanation inside GET.

## Minimal Tests

Add only the tests needed to validate handler behavior if not already covered:

- GET handler returns not found on cache miss
- POST handler loads metadata when available and still works when metadata is missing
- owner mismatch throws validation

Stop after this phase. Leave broad verification to phase 4.
