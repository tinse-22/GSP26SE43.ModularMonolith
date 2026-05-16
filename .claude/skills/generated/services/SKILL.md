---
name: services
description: "Skill for the Services area of GSP26SE43.ModularMonolith. 1108 symbols across 201 files."
---

# Services

1108 symbols | 201 files | Cohesion: 74%

## When to Use

- Working with code in `ClassifiedAds.Modules.TestGeneration/`
- Understanding how GeneratedTestCaseEnrichmentResult, ResolvedExpectation, PdfReportRenderer work
- Modifying services-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs` | BuildEndpointBatches, BuildSuggestionTaskInstruction, FilterSrsRequirementsForEndpoint, EnsureAdaptiveCoverage, ShouldDiscardReadOnlySuccessOnlyScenario (+87) |
| `ClassifiedAds.Modules.TestExecution/Services/VariableResolver.cs` | ResolvePlaceholders, TryResolveDuplicateIdentifierAliasValue, IsSafeDuplicateIdentifierAliasValue, IsHex32String, NormalizePathParams (+70) |
| `ClassifiedAds.Modules.TestGeneration/Services/ContractAwareRequestSynthesizer.cs` | ContractAwareRequestContext, ContractAwareRequestData, BuildRequestData, BuildHeaders, InferBodyType (+57) |
| `ClassifiedAds.Modules.TestGeneration/Services/GeneratedTestCaseDependencyEnricher.cs` | Enrich, BuildProducerCandidates, AddDeclaredDependencies, EnsureSameEndpointPostHappyPathDependency, FillMissingAuthBindings (+42) |
| `ClassifiedAds.Modules.TestGeneration/Services/ExpectationResolver.cs` | Resolve, MergeSrsWithLlmAndSwagger, EnrichFromSrsAndSwagger, ConstrainToOpenApi, EnrichSrsRequiredResponseFieldAssertions (+42) |
| `ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs` | ValidateStatusCode, ValidateHeaders, HeaderValuesEqual, ExtractMediaType, IsBodyContainsSoftMode (+42) |
| `ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs` | ExecuteAndTrackAsync, RetryCaseAsync, ReplayEligibleSkippedCasesAsync, AnalyzeDependencies, ShouldRetryCase (+32) |
| `ClassifiedAds.Modules.TestExecution/Services/RequestBodyAutoHydrator.cs` | BuildNodeFromSchema, TryBuildCompositeNode, BuildObjectNode, BuildArrayNode, ShouldIncludeOptionalProperty (+25) |
| `ClassifiedAds.Modules.TestExecution/Services/PreExecutionValidator.cs` | ValidateEnvironment, ValidateVariableChaining, ValidateBody, IsMeaninglessRequiredBody, IsLikelyErrorCase (+15) |
| `ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs` | GenerateTokensAsync, ValidateRefreshTokenAsync, ValidateAndRotateRefreshTokenAsync, ValidateRefreshTokenInternalAsync, RevokeRefreshTokenAsync (+14) |

## Entry Points

Start here when exploring this area:

- **`GeneratedTestCaseEnrichmentResult`** (Class) — `ClassifiedAds.Modules.TestGeneration/Services/GeneratedTestCaseDependencyEnricher.cs:1537`
- **`ResolvedExpectation`** (Class) — `ClassifiedAds.Modules.TestGeneration/Models/ExpectationResolutionModels.cs:15`
- **`PdfReportRenderer`** (Class) — `ClassifiedAds.Modules.TestReporting/Services/PdfReportRenderer.cs:11`
- **`JsonReportRenderer`** (Class) — `ClassifiedAds.Modules.TestReporting/Services/JsonReportRenderer.cs:11`
- **`HtmlReportRenderer`** (Class) — `ClassifiedAds.Modules.TestReporting/Services/HtmlReportRenderer.cs:10`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `GeneratedTestCaseEnrichmentResult` | Class | `ClassifiedAds.Modules.TestGeneration/Services/GeneratedTestCaseDependencyEnricher.cs` | 1537 |
| `ResolvedExpectation` | Class | `ClassifiedAds.Modules.TestGeneration/Models/ExpectationResolutionModels.cs` | 15 |
| `PdfReportRenderer` | Class | `ClassifiedAds.Modules.TestReporting/Services/PdfReportRenderer.cs` | 11 |
| `JsonReportRenderer` | Class | `ClassifiedAds.Modules.TestReporting/Services/JsonReportRenderer.cs` | 11 |
| `HtmlReportRenderer` | Class | `ClassifiedAds.Modules.TestReporting/Services/HtmlReportRenderer.cs` | 10 |
| `ExcelReportRenderer` | Class | `ClassifiedAds.Modules.TestReporting/Services/ExcelReportRenderer.cs` | 15 |
| `CsvReportRenderer` | Class | `ClassifiedAds.Modules.TestReporting/Services/CsvReportRenderer.cs` | 16 |
| `RenderedReportFile` | Class | `ClassifiedAds.Modules.TestReporting/Models/RenderedReportFile.cs` | 4 |
| `N8nGenerateTestsPayload` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/GenerateTestCasesCommand.cs` | 166 |
| `N8nSrsRequirement` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/GenerateTestCasesCommand.cs` | 192 |
| `N8nOrderedEndpoint` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/GenerateTestCasesCommand.cs` | 233 |
| `N8nUnifiedPromptConfig` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/GenerateTestCasesCommand.cs` | 250 |
| `N8nErrorResponseDescriptor` | Class | `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs` | 59 |
| `N8nBoundaryNegativePayload` | Class | `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs` | 69 |
| `N8nSuggestionPromptConfig` | Class | `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs` | 90 |
| `N8nBoundaryEndpointPayload` | Class | `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs` | 101 |
| `N8nRequirementMatchBrief` | Class | `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs` | 140 |
| `N8nParameterDetail` | Class | `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs` | 151 |
| `ValidationFailureModel` | Class | `ClassifiedAds.Modules.TestExecution/Models/ValidationFailureModel.cs` | 2 |
| `ValidationException` | Class | `ClassifiedAds.CrossCuttingConcerns/Exceptions/ValidationException.cs` | 6 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `LoginWithGoogle → IsSupabasePooler` | cross_community | 7 |
| `ChangePassword → IsSupabasePooler` | cross_community | 7 |
| `RefreshToken → GetRefreshTokenNames` | cross_community | 7 |
| `Login → IsSupabasePooler` | cross_community | 7 |
| `Logout → IsSupabasePooler` | cross_community | 7 |
| `RefreshToken → IsManualResetEventDisposed` | cross_community | 6 |
| `RefreshToken → SetTokensInMemory` | cross_community | 6 |
| `HandleAsync → BuildFailureSummary` | cross_community | 6 |
| `LoginWithGoogle → SetTokensInMemory` | cross_community | 5 |
| `ChangePassword → GetRefreshTokenNames` | cross_community | 5 |

## Connected Areas

| Area | Connections |
|------|-------------|
| TestGeneration | 65 calls |
| TestExecution | 24 calls |
| Controllers | 18 calls |
| Queries | 12 calls |
| Models | 9 calls |
| Subscription | 9 calls |
| Commands | 8 calls |
| LlmAssistant | 5 calls |

## How to Explore

1. `gitnexus_context({name: "GeneratedTestCaseEnrichmentResult"})` — see callers and callees
2. `gitnexus_query({query: "services"})` — find related execution flows
3. Read key files listed above for implementation details
