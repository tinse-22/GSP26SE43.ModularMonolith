# PHASE 2 PROMPT - LLM Runtime And Cache For FE-09

Implement only the reusable LlmAssistant runtime services for FE-09. Do not add controller/command/query API surface yet.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.LlmAssistant`
- related unit tests in `ClassifiedAds.UnitTests/LlmAssistant`

## Goal

Build the FE-09 runtime pipeline inside `LlmAssistant`: sanitize deterministic failure context, compute cache keys, build prompts, call provider, save audit, and cache explanations.

## Files To Add

- `Models/FailureExplanationModel.cs`
- `Models/FailureExplanationPrompt.cs`
- `Models/FailureExplanationProviderResponse.cs`
- `ConfigurationOptions/FailureExplanationOptions.cs`
- `Services/ILlmFailureExplainer.cs`
- `Services/LlmFailureExplainer.cs`
- `Services/IFailureExplanationFingerprintBuilder.cs`
- `Services/FailureExplanationFingerprintBuilder.cs`
- `Services/IFailureExplanationSanitizer.cs`
- `Services/FailureExplanationSanitizer.cs`
- `Services/IFailureExplanationPromptBuilder.cs`
- `Services/FailureExplanationPromptBuilder.cs`
- `Services/ILlmFailureExplanationClient.cs`
- `Services/N8nFailureExplanationClient.cs`

## Files To Modify

- `Entities/LlmSuggestionCache.cs`
  - add `SuggestionType.FailureExplanation = 4`
- `ConfigurationOptions/LlmAssistantModuleOptions.cs`
  - add nested `FailureExplanation` options
- `ServiceCollectionExtensions.cs`
  - register new FE-09 services and named `HttpClient`

## Runtime Pipeline

Implement this sequence in `LlmFailureExplainer`:

1. sanitize incoming context
2. build deterministic fingerprint
3. try cache lookup first
4. if cache hit:
   - return `FailureExplanationModel` with `Source = "cache"`
   - do not call provider
5. if cache miss:
   - build prompt
   - call provider client
   - parse structured response
   - try save `LlmInteraction`
   - try cache explanation payload
   - return `FailureExplanationModel` with `Source = "live"`

## Cache Rules

- reuse `LlmSuggestionCache`
- reuse `CacheKey`
- TTL = 24h
- cache payload must include:
  - `summaryVi`
  - `possibleCauses`
  - `suggestedNextActions`
  - `confidence`
  - `provider`
  - `model`
  - `tokensUsed`
  - `generatedAt`
  - `failureCodes`

## Audit Rules

- reuse `LlmInteraction`
- `InteractionType = FailureExplanation`
- `InputContext` must be sanitized serialized prompt/provider input
- `LlmResponse` must be serialized provider response
- if audit save fails:
  - log warning
  - do not fail the main explanation flow

## Sanitization Rules

Mask values when key contains:

- `authorization`
- `cookie`
- `set-cookie`
- `token`
- `secret`
- `password`
- `apikey`
- `api-key`

Also protect body preview strings that obviously contain bearer tokens or secret-like pairs.

## Prompt Rules

Prompt must include:

- deterministic failure reason codes/messages
- original request definition
- actual response status/body preview/headers
- expectation details
- explicit instruction:
  - do not decide pass/fail
  - explain likely causes only
  - respond as strict JSON only

Required provider response JSON:

```json
{
  "summaryVi": "string",
  "possibleCauses": ["string"],
  "suggestedNextActions": ["string"],
  "confidence": "Low|Medium|High",
  "model": "string",
  "tokensUsed": 0
}
```

## Graceful Failure Rules

- cache write failure -> log warning, still return live explanation
- audit write failure -> log warning, still return live explanation
- provider invalid JSON -> throw controlled error, do not cache
- provider HTTP failure -> throw controlled error, do not cache

## Tests

Add unit tests for:

- deterministic fingerprint generation
- sanitizer masks secret-bearing headers and variables
- cache hit skips provider call
- cache miss calls provider and returns live explanation
- audit failure is graceful
- cache save failure is graceful
- invalid provider payload throws controlled error

Stop after this phase. Do not add controller/command/query work yet.
