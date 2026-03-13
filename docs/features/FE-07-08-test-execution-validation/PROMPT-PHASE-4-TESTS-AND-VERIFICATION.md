# PHASE 4 PROMPT - Tests And Verification For FE-07/08

The implementation should already exist. This phase is only for hardening, regression coverage, and verification.

## Scope

Projects allowed:

- `ClassifiedAds.UnitTests`
- `ClassifiedAds.IntegrationTests`
- test-only support code if required

## Minimum Unit Test Files

Create or extend these files:

- `ClassifiedAds.UnitTests/TestGeneration/TestExecutionReadGatewayServiceTests.cs`
- `ClassifiedAds.UnitTests/TestExecution/ExecutionEnvironmentRuntimeResolverTests.cs`
- `ClassifiedAds.UnitTests/TestExecution/VariableResolverTests.cs`
- `ClassifiedAds.UnitTests/TestExecution/HttpTestExecutorTests.cs`
- `ClassifiedAds.UnitTests/TestExecution/VariableExtractorTests.cs`
- `ClassifiedAds.UnitTests/TestExecution/RuleBasedValidatorTests.cs`
- `ClassifiedAds.UnitTests/TestExecution/TestResultCollectorTests.cs`
- `ClassifiedAds.UnitTests/TestExecution/StartTestRunCommandHandlerTests.cs`

## Minimum Integration Test Files

Create or extend:

- `ClassifiedAds.IntegrationTests/TestExecution/TestRunExecutionIntegrationTests.cs`
- `ClassifiedAds.IntegrationTests/TestExecution/TestRunResultsQueryIntegrationTests.cs`

## Mandatory Scenarios

### Unit

- gateway rejects subset missing dependency
- environment default fallback works
- request-level auth override wins over env auth
- OAuth2 token fetched once per run
- unresolved variable fails case deterministically
- dependency fail skips dependent cases
- each validator rule emits expected failure code
- result collector writes cache payload and updates counters

### Integration

- POST start run returns `201` and summary counters match detailed results
- login/token extraction can feed a later protected request in one run
- failed dependency causes skipped dependent case in returned payload
- GET run history is paged and does not need cache
- GET run results after expiry returns `RUN_RESULTS_EXPIRED`

## Verification Commands

Run what is feasible:

```bash
dotnet build
dotnet test
```

## Final Verification Checklist

- no migration files added
- no direct `TestGenerationDbContext` usage inside `ClassifiedAds.Modules.TestExecution`
- no N+1 query pattern in gateway
- detailed results only in cache, summary only in `TestRuns`
- permissions wired on controller endpoints

If any command cannot run because of sandbox/network/package restore constraints, state it explicitly in the final report.
