# N8N Webhook Generate Tests Analysis Report

Date: 2026-04-07
Scope classification: Docs only

## 1. Executive Summary

The file `Triggering n8n webhook generate-tes.txt` is a stack trace showing that the failure happens while WebAPI is still waiting for the outbound HTTP call to n8n to return.

The strongest conclusion is:

- the request fails before BE receives any HTTP response from n8n
- the failure happens inside `N8nIntegrationService.TriggerWebhookAsync(...)`
- the `POST /api/test-suites/{suiteId}/generate-tests` endpoint is not truly asynchronous even though it returns `202 Accepted`

The most likely root cause is a mismatch between the intended callback-based design and the actual webhook behavior/configuration in n8n:

- BE expects n8n to acknowledge quickly, then send generated test cases back later via callback
- the actual webhook being called appears to keep the HTTP request open long enough to hit the configured Polly/HttpClient timeout

There is also a second high-risk issue:

- repo docs/workflow samples point to `dotnet-integration`
- BE runtime currently calls `generate-test-cases-unified`

That means the code, appsettings, and workflow documentation are not aligned around one single webhook path and one single execution model.

## 2. Source Material Reviewed

Primary input:

- `Triggering n8n webhook generate-tes.txt`

Relevant runtime files:

- `ClassifiedAds.Modules.TestGeneration/Services/N8nIntegrationService.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/GenerateTestCasesCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/Controllers/TestOrderController.cs`
- `ClassifiedAds.Modules.TestGeneration/Controllers/TestCasesController.cs`
- `ClassifiedAds.Modules.TestGeneration/ConfigurationOptions/N8nIntegrationOptions.cs`
- `ClassifiedAds.Modules.TestGeneration/Constants/N8nWebhookNames.cs`
- `ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs`
- `ClassifiedAds.WebAPI/appsettings.json`
- `ClassifiedAds.WebAPI/appsettings.Development.json`
- `docs/n8n-workflow-dotnet-integration.json`
- `docs/frontend/FE-05-test-generation-frontend/README.md`

## 3. What The Stack Trace Proves

The tail of the stack trace shows this exact call chain:

1. `TestOrderController.GenerateTests(...)`
2. `Dispatcher.DispatchAsync(...)`
3. `GenerateTestCasesCommandHandler.HandleAsync(...)`
4. `N8nIntegrationService.TriggerWebhookAsync(...)`
5. `HttpClient.SendAsync(...)`
6. Polly resilience pipeline
7. timeout/cancellation path inside `System.Net.Http`

Important implication:

- the failure is not in callback persistence
- the failure is not in DB save logic
- the failure is not in RabbitMQ sender/receiver
- the failure occurs before BE gets an HTTP response from the n8n webhook

This is the key evidence line from code behavior:

- `N8nIntegrationService.TriggerWebhookAsync<TPayload>(...)` awaits `_httpClient.SendAsync(...)`
- the stack trace points to line 136 in that method

So the request is blocked at the network boundary between WebAPI and n8n.

## 4. Current Runtime Flow

The current flow is:

1. Client calls `POST /api/test-suites/{suiteId}/generate-tests`
2. `TestOrderController.GenerateTests(...)` awaits `_dispatcher.DispatchAsync(...)`
3. `GenerateTestCasesCommandHandler` builds a payload containing:
   - ordered endpoints
   - prompts
   - callback URL
   - callback API key
4. The handler calls `_n8nService.TriggerWebhookAsync(WebhookName, payload, cancellationToken)`
5. `N8nIntegrationService` resolves the configured webhook URL and performs `HttpClient.SendAsync(...)`
6. Only after that call returns successfully does the controller reach `return Accepted()`

This means the API advertises asynchronous behavior, but the trigger phase is still synchronous from the caller's perspective.

## 5. Architectural Intent vs Actual Behavior

The repository clearly indicates a callback-based design:

- `GenerateTestCasesCommand` sends `CallbackUrl` and `CallbackApiKey`
- `TestOrderController` exposes `POST /api/test-suites/{suiteId}/test-cases/from-ai`
- frontend docs say the generation flow is callback-based and `202 Accepted`

That design only works well if the n8n webhook acknowledges quickly and performs long-running work after the HTTP trigger has already completed.

However, the stack trace shows the opposite practical behavior:

- the initial outbound request is still open long enough to time out

So one of these must be true:

1. the actual n8n workflow does not respond immediately
2. BE is calling a different workflow/path than the documented one
3. the called workflow performs blocking work before acknowledging
4. the call is stuck before n8n can answer at all

## 6. Strong Findings

### Finding A: `202 Accepted` is semantically misleading in the current implementation

`TestOrderController.GenerateTests(...)` awaits the dispatcher before returning `Accepted()`.

That means:

- the endpoint does not return immediately
- n8n trigger latency directly impacts user-facing API latency
- if the outbound HTTP call to n8n times out, the client does not receive `202`; it receives an error

This is the biggest design mismatch in the current implementation.

### Finding B: the "fire-and-forget" method is not fire-and-forget

`N8nIntegrationService.TriggerWebhookAsync<TPayload>(...)` logs `(fire-and-forget)`, but still does:

- `await _httpClient.SendAsync(...)`

That means it is only "do not parse response body", not "return immediately and continue in background".

### Finding C: timeout budget is finite and environment-dependent

`ServiceCollectionExtensions.cs` configures a typed client with:

- `AttemptTimeout = TimeoutSeconds`
- `TotalRequestTimeout = TimeoutSeconds + 60`
- reduced retries

Observed config drift:

- `appsettings.json` sets `TimeoutSeconds: 30`
- `appsettings.Development.json` sets `TimeoutSeconds: 60`

So depending on environment, the same flow may fail faster or slower.

### Finding D: runtime naming is inconsistent with the documented workflow

Code uses:

- logical webhook name `generate-test-cases-unified`

Appsettings map:

- `generate-test-cases-unified -> generate-test-cases-unified`
- `DotnetIntegration -> dotnet-integration`

But the repository workflow JSON reviewed is:

- `docs/n8n-workflow-dotnet-integration.json`
- webhook path `dotnet-integration`
- `responseMode: onReceived`

This is a major mismatch.

If the live n8n flow still follows the documented `dotnet-integration` path while BE calls `generate-test-cases-unified`, then BE may be hitting:

- a different workflow
- an outdated workflow
- a workflow with different response mode
- a misconfigured path alias

### Finding E: docs mention a callback flow, but the legacy route still triggers blocking behavior

Frontend docs explicitly say:

- the legacy route `POST /api/test-suites/{suiteId}/generate-tests` still exists
- it returns `202 Accepted`
- frontend should prefer `POST /api/test-suites/{suiteId}/test-cases/generate-happy-path`

But both generation paths eventually rely on the same synchronous outbound trigger step when callback mode is enabled.

So the issue is deeper than one controller route.

## 7. Most Likely Root Causes

## Root Cause 1: the live n8n webhook does not acknowledge immediately

Confidence: High

Why:

- stack trace dies inside outbound `SendAsync`
- long-running LLM workflows commonly exceed 30-60s
- callback design only helps if the initial webhook returns quickly

If the live workflow uses:

- `responseMode: responseNode`
- or a final response after OpenAI finishes
- or blocking logic before webhook acknowledgement

then the initial BE request will sit open until timeout.

## Root Cause 2: BE is calling the wrong webhook path/workflow

Confidence: High

Why:

- repo workflow sample uses `dotnet-integration`
- runtime webhook name used by `GenerateTestCasesCommand` is `generate-test-cases-unified`
- appsettings contain both names

This creates ambiguity and makes it very plausible that BE is calling a different n8n flow from the one the team believes is active.

## Root Cause 3: configuration drift between environments

Confidence: Medium to High

Observed drift:

- different `BaseUrl`
- different `TimeoutSeconds`
- different `BeBaseUrl`
- likely different live webhook instances

This matters because the same code path can behave differently between:

- local development
- cloud dev
- staging
- production

## Root Cause 4: callback URL may not be reachable from n8n cloud

Confidence: Medium

Why it matters:

- `GenerateTestCasesCommand` builds callback URL from `BeBaseUrl`
- sample values include localhost-style addresses
- if n8n cloud cannot reach that BE URL, the workflow may fail downstream

Important nuance:

- this does not explain the initial timeout by itself if the webhook truly responds `onReceived`
- but it does explain why a workflow configured to wait for downstream success could hang or fail before returning

## 8. Secondary Technical Weaknesses

### Weakness 1: timeout exceptions are not translated into domain-friendly errors

`N8nIntegrationService` handles:

- non-success status codes
- empty body
- invalid JSON

But it does not explicitly catch:

- `TaskCanceledException`
- `OperationCanceledException`
- Polly timeout exceptions

So the user gets a low-level failure path instead of a controlled "n8n trigger timed out" error.

### Weakness 2: logs are useful but not sufficient for outage triage

The service logs:

- webhook name
- URL
- success/failure response details

But for timeout paths, the stack trace alone is noisy and does not show:

- actual timeout budget used
- environment name
- resolved `BaseUrl`
- resolved callback URL
- whether this was `generate-test-cases-unified` or `dotnet-integration`

### Weakness 3: there is no local queue boundary before leaving the request thread

If test generation is conceptually asynchronous, the controller should ideally:

- persist a generation request
- publish a background job/message
- return `202` immediately

Right now the API still depends on the n8n trigger succeeding inline during the request.

## 9. Recommended Solution

## Recommended Direction: keep callback-based result delivery, but make the trigger genuinely asynchronous

The clean solution has 3 parts.

### Part 1: unify webhook naming and path

Pick one canonical webhook name and use it everywhere:

- code constant
- appsettings
- n8n workflow path
- docs

Recommended choice:

- use one logical name only, preferably `DotnetIntegration` or `GenerateTestCasesUnified`
- delete or deprecate the other alias

Do not keep both active unless they intentionally point to distinct workflows with documented purposes.

### Part 2: ensure the actual live n8n workflow acknowledges immediately

For the real workflow BE is calling:

- confirm webhook path
- confirm response mode
- confirm it returns immediately on receive

If the workflow is intended for callback processing, it should:

1. accept the payload
2. return success immediately
3. continue heavy OpenAI/LLM work in workflow execution
4. POST final result back to `/api/test-suites/{suiteId}/test-cases/from-ai`

The documented sample `docs/n8n-workflow-dotnet-integration.json` already shows the right idea with:

- `responseMode: onReceived`

But the runtime path being called must match that actual deployed workflow.

### Part 3: stop coupling `202 Accepted` to inline trigger success

Best long-term fix:

- create an internal generation job record or command
- hand off execution to background infrastructure
- return `202` immediately from WebAPI

Possible implementations:

- internal DB-backed job table + hosted worker
- RabbitMQ message + background consumer
- outbox-driven background processing

This repo already has messaging infrastructure, so a background trigger flow would fit the architecture better than a blocking controller call.

## 10. Solution Options By Priority

### Option A: Fastest operational hotfix

Goal: unblock generation without major code refactor.

Actions:

1. Inspect the actual live n8n workflow being hit by BE.
2. Verify the path matches the appsettings mapping.
3. Set the live webhook to acknowledge immediately.
4. Keep final result delivery via callback endpoint.

Pros:

- fast
- minimal code change
- likely resolves the immediate timeout

Cons:

- still leaves misleading controller behavior
- still keeps external trigger in the request path

### Option B: Correct the naming/configuration mismatch

Goal: eliminate ambiguity.

Actions:

1. Decide whether the canonical path is `dotnet-integration` or `generate-test-cases-unified`.
2. Update:
   - `N8nWebhookNames`
   - `appsettings*.json`
   - n8n workflow path
   - docs
3. Remove the unused alias.

Pros:

- reduces human error
- prevents future "wrong workflow" outages

Cons:

- does not alone fix blocking behavior if the live workflow still responds late

### Option C: Real asynchronous architecture

Goal: make `202 Accepted` truthful.

Actions:

1. WebAPI stores a generation request or publishes an internal message.
2. Background worker triggers n8n outside the user request.
3. Callback still persists test cases to BE.
4. Frontend polls status or listens for completion.

Pros:

- best reliability
- best UX
- external latency no longer breaks initial API call

Cons:

- requires implementation work
- needs status tracking

## 11. Concrete Fix Plan I Recommend

If the team wants the most pragmatic path, do this in order:

1. Verify which exact webhook URL is being called in the failing environment.
2. Open the live n8n workflow bound to that URL.
3. Confirm whether it responds immediately or waits for LLM completion.
4. Align the live workflow path with the one configured in BE.
5. Re-test until `POST /generate-tests` returns quickly.
6. Then plan a second pass to move the trigger fully off the request thread.

That gives a practical two-stage strategy:

- Stage 1: fix runtime mismatch and timeout behavior
- Stage 2: harden architecture

## 12. Verification Checklist After Fix

After applying the fix, verify all of the following:

1. `POST /api/test-suites/{suiteId}/generate-tests` returns `202 Accepted` in a few seconds or less.
2. WebAPI logs show the exact webhook URL and no timeout exception.
3. n8n execution continues after the initial trigger response.
4. n8n successfully calls:
   - `POST /api/test-suites/{suiteId}/test-cases/from-ai`
5. callback returns `204 NoContent`
6. generated test cases are persisted
7. suite status becomes `Ready`
8. no path alias confusion remains between:
   - `generate-test-cases-unified`
   - `dotnet-integration`

## 13. Suggested Code Hardening

Even if the operational fix is done in n8n first, BE should still be hardened.

Recommended code improvements:

1. Catch timeout/cancellation exceptions in `N8nIntegrationService` and translate them into a clearer application error.
2. Log:
   - resolved webhook name
   - resolved URL
   - timeout setting
   - callback URL
   - environment
3. Consider returning a domain-specific status model for trigger initiation.
4. Consider separating:
   - synchronous request/response webhook calls
   - callback-based trigger-only webhook calls

Right now both patterns share the same service abstraction, which makes intent less explicit.

## 14. Bottom Line

This is not primarily a RabbitMQ problem, DB problem, or test-case persistence problem.

It is primarily a webhook trigger contract problem:

- the BE request is waiting inline
- the live n8n behavior appears not to acknowledge fast enough
- code/config/docs are not aligned on one webhook identity and one response pattern

The shortest path to recovery is:

1. align the real webhook path
2. make the live workflow acknowledge immediately
3. keep callback for final result delivery

The best long-term path is:

1. move the n8n trigger off the request thread
2. keep `202 Accepted` as a true async contract
3. add status tracking and better timeout diagnostics

## 15. Notes About This Report

- This task was treated as `Docs only`.
- No DB model, migration, Docker, or compose files were changed.
- GitNexus MCP resources were not available in this session, so analysis was done by direct file inspection.
