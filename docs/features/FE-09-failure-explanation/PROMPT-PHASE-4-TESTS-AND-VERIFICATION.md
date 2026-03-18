# PHASE 4 PROMPT - Tests And Verification For FE-09

Implement and run the missing tests for FE-09. Do not redesign the runtime architecture in this phase unless a test reveals a real defect.

## Scope

Projects allowed:

- `ClassifiedAds.UnitTests`
- minimal production-code fixes only when required by failing tests

## Goal

Finish FE-09 with targeted unit coverage and verification commands that prove the feature is wired correctly.

## Required Test Areas

1. `TestFailureReadGatewayServiceTests`
   - expired results -> `RUN_RESULTS_EXPIRED`
   - non-failed case -> `TEST_CASE_NOT_FAILED`
   - successful mapping returns both definition and actual failed result

2. `FailureExplanationFingerprintBuilderTests`
   - same deterministic input -> same key
   - different failure payload -> different key

3. `FailureExplanationSanitizerTests`
   - Authorization/Cookie/token/password/apiKey values are masked

4. `FailureExplanationPromptBuilderTests`
   - prompt contains deterministic sections
   - prompt does not contain raw secrets

5. `LlmFailureExplainerTests`
   - cache hit skips provider
   - cache miss calls provider
   - audit failure is graceful
   - cache save failure is graceful
   - invalid provider payload throws controlled error

6. `ExplainTestFailureCommandHandlerTests`
   - owner validation
   - metadata optional path
   - service invocation

7. `GetFailureExplanationQueryHandlerTests`
   - cache hit returns model
   - cache miss returns not found

## Verification Commands

Run targeted commands and report exactly what happened:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.LlmAssistant'
```

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.TestExecution'
```

If the test namespaces differ after implementation, use the closest filter and state the exact command you actually ran.

## Review Checklist

- no migrations were added
- no Background worker or RabbitMQ consumer was added
- no direct TestExecution repository/cache access from LlmAssistant
- GET explanation stays cache-only
- POST explanation stays cache-first
- secrets are masked before prompt/audit/cache
- FE-08 verdict remains unchanged

Stop after tests pass or after you identify the exact remaining blocker with evidence.
