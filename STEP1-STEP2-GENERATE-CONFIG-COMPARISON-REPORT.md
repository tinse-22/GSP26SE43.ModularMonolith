# Step1-Step2 Generate Config Comparison Report

Date: 2026-04-20
Scope: FE Step 1 (Configure) -> Step 2 (AI Review generate preview)

## 1) Objective
Check whether generate result path is different between:
- Case A: Step 1 has config (GlobalBusinessRules and/or EndpointBusinessContexts)
- Case B: Step 1 has no config

## 2) Flow Checked (Code Trace)

### FE: Step 1 save and trigger Step 2 generate
- FE saves suite config from Step 1 with:
  - selectedEndpointIds
  - endpointBusinessContexts
  - globalBusinessRules
- FE then approves order and calls generate suggestion preview.

Evidence:
- FE save payload to update suite:
  - FE/llm-api-test-generator/src/pages/TestSuiteDetailPage.tsx:581
  - FE/llm-api-test-generator/src/pages/TestSuiteDetailPage.tsx:590
  - FE/llm-api-test-generator/src/pages/TestSuiteDetailPage.tsx:591
- FE calls Step 2 generate after approve:
  - FE/llm-api-test-generator/src/pages/TestSuiteDetailPage.tsx:633
  - FE/llm-api-test-generator/src/pages/TestSuiteDetailPage.tsx:682

### BE: Receive and persist config
- Controller maps request fields to command.
- Command handler sanitizes and persists config.

Evidence:
- BE map request -> command:
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Controllers/TestSuitesController.cs:123
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Controllers/TestSuitesController.cs:124
- BE persist and sanitize:
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Commands/AddUpdateTestSuiteScopeCommand.cs:206
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Commands/AddUpdateTestSuiteScopeCommand.cs:207
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Commands/AddUpdateTestSuiteScopeCommand.cs:231

### BE: Step 2 generate uses suite config
- Generate preview command passes suite object into LLM context.
- LlmScenarioSuggester reads endpoint/global config and injects into payload/prompt.

Evidence:
- Suite passed into LLM context:
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs:138
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs:146
- Config used in suggester:
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs:317
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs:354
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs:404
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs:411
- Mapper behavior when both empty => null context:
  - BE/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Services/EndpointPromptContextMapper.cs:235

## 3) Executed Verification Tests
Command executed:

dotnet test ClassifiedAds.UnitTests\\ClassifiedAds.UnitTests.csproj --filter "FullyQualifiedName~AddUpdateTestSuiteScopeCommandHandlerTests.HandleAsync_Create_Should_PersistGlobalBusinessRules|FullyQualifiedName~EndpointPromptContextMapperTests.Map_Should_IncludeGlobalBusinessRules|FullyQualifiedName~EndpointPromptContextMapperTests.Map_Should_IncludeEndpointSpecificBusinessContext|FullyQualifiedName~EndpointPromptContextMapperTests.Map_Should_CombineGlobalAndEndpointRules|FullyQualifiedName~EndpointPromptContextMapperTests.Map_Should_HandleNullBusinessContextGracefully|FullyQualifiedName~LlmScenarioSuggesterTests.SuggestScenariosAsync_Should_CallN8n_WhenCacheMiss" --logger "console;verbosity=minimal"

Result: Passed 6 / Failed 0.

Passed tests:
- AddUpdateTestSuiteScopeCommandHandlerTests.HandleAsync_Create_Should_PersistGlobalBusinessRules
- EndpointPromptContextMapperTests.Map_Should_IncludeGlobalBusinessRules
- EndpointPromptContextMapperTests.Map_Should_IncludeEndpointSpecificBusinessContext
- EndpointPromptContextMapperTests.Map_Should_CombineGlobalAndEndpointRules
- EndpointPromptContextMapperTests.Map_Should_HandleNullBusinessContextGracefully
- LlmScenarioSuggesterTests.SuggestScenariosAsync_Should_CallN8n_WhenCacheMiss

## 4) Comparison: With Config vs Without Config

| Aspect | With Config | Without Config |
|---|---|---|
| Step 1 save payload | endpointBusinessContexts/globalBusinessRules populated | empty string/map or null-equivalent |
| BE persisted suite fields | persisted after sanitize/trim | persisted empty/null |
| Step 2 LLM context | includes suite global/endpoint rules | no business rule text in context |
| Prompt/payload sections | has "Global Business Rules" and/or "Endpoint Business Context" when non-empty | those sections are not added |
| Generate path execution | still generates suggestions normally | still generates suggestions normally |

## 5) Final Conclusion
Yes, there is a real difference.
- Generate pipeline (Step 1 -> Step 2) executes in both cases.
- But input context to LLM is different:
  - With config: business rules are injected and can influence generated suggestions.
  - Without config: that rule context is absent.

So behavior difference is not "generate runs vs does not run".
Difference is in generated content quality/constraint alignment, because the prompt context is different.

## 6) Practical Note
Config only takes effect after Step 1 save/approve flow is executed (Approve Order or Save & Approve Order), because that is when suite config is persisted and Step 2 generate is triggered.
