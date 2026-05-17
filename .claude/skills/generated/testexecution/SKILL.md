---
name: testexecution
description: "Skill for the TestExecution area of GSP26SE43.ModularMonolith. 431 symbols across 71 files."
---

# TestExecution

431 symbols | 71 files | Cohesion: 78%

## When to Use

- Working with code in `ClassifiedAds.UnitTests/`
- Understanding how ExecutionTestCaseDto, ExecutionTestCaseRequestDto, ExecutionTestCaseExpectationDto work
- Modifying testexecution-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.UnitTests/TestExecution/VariableResolverTests.cs` | Resolve_Should_ReplacePlaceholdersInUrl, Resolve_Should_ReplacePlaceholdersInHeaders, Resolve_Should_ReplacePlaceholdersInQueryParams, Resolve_Should_ReplacePlaceholdersInBody, Resolve_Should_RewriteSyntheticEmailInHappyPathBody_UsingTestEmail (+43) |
| `ClassifiedAds.UnitTests/TestExecution/RuleBasedValidatorTests.cs` | Validate_TransportError_Should_ShortCircuitWithHttpRequestError, Validate_StatusCodeMismatch_Should_Fail, Validate_StatusCodeMultipleAllowed_Should_PassWhenMatchesAny, Validate_StatusCodeNegativeAllowedSet_Should_PassWhenActual422, Validate_HappyPathPost_Expected200_Actual201_Should_PassWithAdaptiveWarning (+37) |
| `ClassifiedAds.UnitTests/TestExecution/TestExecutionOrchestratorTests.cs` | ExecuteAsync_Should_SkipTestCase_WhenDependencyFailed, ExecuteAsync_Should_NotSkipDependency_WhenOnlyStatusMismatchBut2xx, ExecuteAsync_Should_FetchEndpointMetadataOnce, ExecuteAsync_Should_ResolveEnvironmentOnce, ExecuteAsync_Should_NormalizeBaseUrl_WhenSuiteHasMultipleTopLevelResources (+23) |
| `ClassifiedAds.Modules.TestExecution/Services/VariableResolver.cs` | Resolve, ResolveAuthModeFromHeaders, NormalizeAuthMode, ApplyTokenAliases, ApplyResourceIdAliases (+16) |
| `ClassifiedAds.UnitTests/TestExecution/StartTestRunCommandHandlerTests.cs` | HandleAsync_EmptyTestSuiteId_ShouldThrowValidation, HandleAsync_WrongOwner_ShouldThrowValidation, HandleAsync_SuiteNotReady_ShouldThrowValidation, HandleAsync_SelectedTestCaseNotInSuite_ShouldThrowValidation, HandleAsync_SelectedTestCaseInSuite_ShouldCallOrchestratorWithNormalizedIds (+13) |
| `ClassifiedAds.UnitTests/TestExecution/TestFailureReadGatewayServiceTests.cs` | CreateExecutionTestCase, GetFailureExplanationContextAsync_RunNotFound_ShouldThrowNotFoundException, GetFailureExplanationContextAsync_MissingCachedResults_ShouldThrowRunResultsExpiredConflict, GetFailureExplanationContextAsync_MissingRedisKey_ShouldThrowRunResultsExpiredConflict, GetFailureExplanationContextAsync_ExpiredCachedResults_ShouldThrowRunResultsExpiredConflict (+10) |
| `ClassifiedAds.UnitTests/TestExecution/HttpTestExecutorTests.cs` | ExecuteAsync_SuccessfulGet_ShouldReturnStatusCodeAndBody, ExecuteAsync_PostWithBody_ShouldSendBody, ExecuteAsync_DeleteWithBody_ShouldSendBody, ExecuteAsync_WithQueryParams_ShouldAppendToUrl, ExecuteAsync_WithNullQueryParamValue_ShouldOmitThatParam (+10) |
| `ClassifiedAds.UnitTests/TestExecution/TestResultCollectorTests.cs` | CollectAsync_AllPassed_ShouldSetStatusCompleted, CollectAsync_HasAssertionFailures_ShouldSetStatusCompleted, CollectAsync_HasExecutionFailures_ShouldSetStatusFailed, CollectAsync_WithSkipped_ShouldCountCorrectly, CollectAsync_ShouldTruncateResponseBodyTo65536Chars (+9) |
| `ClassifiedAds.UnitTests/TestExecution/TestRunReportReadGatewayServiceTests.cs` | GetReportContextAsync_RunNotFound_ShouldThrowNotFoundException, GetReportContextAsync_RunNotFinished_ShouldThrowReportRunNotReadyConflict, GetReportContextAsync_MissingCachedResults_ShouldThrowRunResultsExpiredConflict, GetReportContextAsync_ExpiredCachedResults_ShouldThrowRunResultsExpiredConflict, GetReportContextAsync_ValidRun_ShouldReturnDefinitionsResultsAndRecentHistory (+9) |
| `ClassifiedAds.UnitTests/TestExecution/PreExecutionValidatorContractTests.cs` | Validate_Should_Fail_WhenRequiredQueryParamMissing, Validate_Should_Fail_WhenContractRequiresBodyButBodyMissing, Validate_Should_NotFail_WhenLegacyBodyQueryRequirementExists_ButBodyProvided, Validate_Should_Fail_WhenLegacyBodyQueryRequirementExists_AndBodyMissing, Validate_Should_PassContractChecks_WhenRequiredQueryAndBodyProvided (+8) |

## Entry Points

Start here when exploring this area:

- **`ExecutionTestCaseDto`** (Class) — `ClassifiedAds.Contracts/TestGeneration/DTOs/ExecutionTestCaseDto.cs:5`
- **`ExecutionTestCaseRequestDto`** (Class) — `ClassifiedAds.Contracts/TestGeneration/DTOs/ExecutionTestCaseDto.cs:34`
- **`ExecutionTestCaseExpectationDto`** (Class) — `ClassifiedAds.Contracts/TestGeneration/DTOs/ExecutionTestCaseDto.cs:53`
- **`ExecutionVariableRuleDto`** (Class) — `ClassifiedAds.Contracts/TestGeneration/DTOs/ExecutionTestCaseDto.cs:76`
- **`TestRunResultModel`** (Class) — `ClassifiedAds.Modules.TestExecution/Models/TestRunResultModel.cs:5`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `ExecutionTestCaseDto` | Class | `ClassifiedAds.Contracts/TestGeneration/DTOs/ExecutionTestCaseDto.cs` | 5 |
| `ExecutionTestCaseRequestDto` | Class | `ClassifiedAds.Contracts/TestGeneration/DTOs/ExecutionTestCaseDto.cs` | 34 |
| `ExecutionTestCaseExpectationDto` | Class | `ClassifiedAds.Contracts/TestGeneration/DTOs/ExecutionTestCaseDto.cs` | 53 |
| `ExecutionVariableRuleDto` | Class | `ClassifiedAds.Contracts/TestGeneration/DTOs/ExecutionTestCaseDto.cs` | 76 |
| `TestRunResultModel` | Class | `ClassifiedAds.Modules.TestExecution/Models/TestRunResultModel.cs` | 5 |
| `TestCaseValidationResult` | Class | `ClassifiedAds.Modules.TestExecution/Models/TestCaseValidationResult.cs` | 4 |
| `TestCaseRunResultModel` | Class | `ClassifiedAds.Modules.TestExecution/Models/TestCaseRunResultModel.cs` | 5 |
| `ResolvedTestCaseRequest` | Class | `ClassifiedAds.Modules.TestExecution/Models/ResolvedTestCaseRequest.cs` | 5 |
| `ResolvedExecutionEnvironment` | Class | `ClassifiedAds.Modules.TestExecution/Models/ResolvedExecutionEnvironment.cs` | 5 |
| `PreExecutionValidationResult` | Class | `ClassifiedAds.Modules.TestExecution/Models/PreExecutionValidationResult.cs` | 8 |
| `HttpTestResponse` | Class | `ClassifiedAds.Modules.TestExecution/Models/HttpTestResponse.cs` | 4 |
| `TestRunModel` | Class | `ClassifiedAds.Modules.TestExecution/Models/TestRunModel.cs` | 5 |
| `TestRun` | Class | `ClassifiedAds.Modules.TestExecution/Entities/TestRun.cs` | 8 |
| `ExecutionEnvironment` | Class | `ClassifiedAds.Modules.TestExecution/Entities/ExecutionEnvironment.cs` | 8 |
| `StartTestRunCommand` | Class | `ClassifiedAds.Modules.TestExecution/Commands/StartTestRunCommand.cs` | 20 |
| `TestCaseResult` | Class | `ClassifiedAds.Modules.TestExecution/Entities/TestCaseResult.cs` | 9 |
| `ApiEndpointMetadataDto` | Class | `ClassifiedAds.Contracts/ApiDocumentation/DTOs/ApiEndpointMetadataDto.cs` | 5 |
| `ExecutionAuthConfigModel` | Class | `ClassifiedAds.Modules.TestExecution/Models/ExecutionAuthConfigModel.cs` | 4 |
| `GetTestRunResultsQuery` | Class | `ClassifiedAds.Modules.TestExecution/Queries/GetTestRunResultsQuery.cs` | 16 |
| `GetTestRunsQuery` | Class | `ClassifiedAds.Modules.TestExecution/Queries/GetTestRunsQuery.cs` | 15 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `GetCachedAsync → ExecutionTestCaseRequestDto` | cross_community | 5 |
| `GetCachedAsync → ExecutionTestCaseExpectationDto` | cross_community | 5 |
| `ExplainAsync → ExecutionTestCaseRequestDto` | cross_community | 5 |
| `ExplainAsync → ExecutionTestCaseExpectationDto` | cross_community | 5 |
| `HandleAsync → ExecutionAuthConfigModel` | cross_community | 4 |
| `HandleAsync → MaskSecret` | cross_community | 4 |
| `ExecuteSuccessfulPathAsync → ValidationFailureModel` | cross_community | 4 |
| `ExecuteSuccessfulPathAsync → DeserializeDictionary` | cross_community | 4 |
| `HandleAsync → DeserializeAuthConfig` | cross_community | 3 |
| `ExecuteSuccessfulPathAsync → PreExecutionValidationResult` | cross_community | 3 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Services | 67 calls |
| Queries | 26 calls |
| TestGeneration | 15 calls |
| Controllers | 9 calls |
| Commands | 6 calls |
| Models | 5 calls |
| ApiDocumentation | 2 calls |
| TestReporting | 2 calls |

## How to Explore

1. `gitnexus_context({name: "ExecutionTestCaseDto"})` — see callers and callees
2. `gitnexus_query({query: "testexecution"})` — find related execution flows
3. Read key files listed above for implementation details
