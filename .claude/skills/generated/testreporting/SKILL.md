---
name: testreporting
description: "Skill for the TestReporting area of GSP26SE43.ModularMonolith. 34 symbols across 20 files."
---

# TestReporting

34 symbols | 20 files | Cohesion: 71%

## When to Use

- Working with code in `ClassifiedAds.Modules.TestReporting/`
- Understanding how TestReportGenerator, ReportDataSanitizer, CoverageCalculator work
- Modifying testreporting-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.TestReporting/Commands/GenerateTestReportCommand.cs` | NormalizeRecentHistoryLimit, ResolveMaxHistoryLimit, GenerateTestReportCommand, GenerateTestReportCommandHandler |
| `ClassifiedAds.UnitTests/TestReporting/GenerateTestReportCommandHandlerTests.cs` | HandleAsync_WhenCurrentUserIsNotOwner_ShouldThrowValidationException, HandleAsync_WhenRunIsNotReady_ShouldPropagateConflictAndNotInvokeGenerator, HandleAsync_WhenCommandIsValid_ShouldDelegateToGeneratorWithNormalizedValues, CreateHandler |
| `ClassifiedAds.UnitTests/TestReporting/TestReportGeneratorTests.cs` | GenerateAsync_ShouldSelectRendererUploadUpsertCoverageAndReturnMetadata, GenerateAsync_ShouldSelectRequestedRendererAndAddCoverageWhenMissing, GenerateAsync_WhenUploadFails_ShouldNotPersistMetadata |
| `ClassifiedAds.UnitTests/TestReporting/ReportTestData.cs` | CreateContext, CreateMetadata, CreateCoverageModel |
| `ClassifiedAds.Contracts/TestExecution/DTOs/TestRunReportContextDto.cs` | TestRunHistoryItemDto, TestRunReportContextDto, TestRunReportRunDto |
| `ClassifiedAds.Modules.TestReporting/Services/ReportDataSanitizer.cs` | ReportDataSanitizer, CloneRecentRun |
| `ClassifiedAds.Modules.TestExecution/Services/TestRunReportReadGatewayService.cs` | MapHistoryItem, MapRun |
| `ClassifiedAds.Modules.TestReporting/ServiceCollectionExtensions.cs` | AddTestReportingModule |
| `ClassifiedAds.UnitTests/TestReporting/ReportDataSanitizerTests.cs` | Sanitize_ShouldMaskSecretBearingHeadersVariablesAndBodyPreview |
| `ClassifiedAds.UnitTests/TestReporting/CoverageCalculatorTests.cs` | Calculate_ShouldUseScopedEndpointsAndMetadataDeterministically |

## Entry Points

Start here when exploring this area:

- **`TestReportGenerator`** (Class) — `ClassifiedAds.Modules.TestReporting/Services/TestReportGenerator.cs:20`
- **`ReportDataSanitizer`** (Class) — `ClassifiedAds.Modules.TestReporting/Services/ReportDataSanitizer.cs:11`
- **`CoverageCalculator`** (Class) — `ClassifiedAds.Modules.TestReporting/Services/CoverageCalculator.cs:10`
- **`TestReportingModuleOptions`** (Class) — `ClassifiedAds.Modules.TestReporting/ConfigurationOptions/TestReportingModuleOptions.cs:2`
- **`ReportGenerationOptions`** (Class) — `ClassifiedAds.Modules.TestReporting/ConfigurationOptions/ReportGenerationOptions.cs:2`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `TestReportGenerator` | Class | `ClassifiedAds.Modules.TestReporting/Services/TestReportGenerator.cs` | 20 |
| `ReportDataSanitizer` | Class | `ClassifiedAds.Modules.TestReporting/Services/ReportDataSanitizer.cs` | 11 |
| `CoverageCalculator` | Class | `ClassifiedAds.Modules.TestReporting/Services/CoverageCalculator.cs` | 10 |
| `TestReportingModuleOptions` | Class | `ClassifiedAds.Modules.TestReporting/ConfigurationOptions/TestReportingModuleOptions.cs` | 2 |
| `ReportGenerationOptions` | Class | `ClassifiedAds.Modules.TestReporting/ConfigurationOptions/ReportGenerationOptions.cs` | 2 |
| `ConnectionStringsOptions` | Class | `ClassifiedAds.Modules.TestReporting/ConfigurationOptions/ConnectionStringsOptions.cs` | 2 |
| `TestRunHistoryItemDto` | Class | `ClassifiedAds.Contracts/TestExecution/DTOs/TestRunReportContextDto.cs` | 64 |
| `StorageUploadedFileDTO` | Class | `ClassifiedAds.Contracts/Storage/DTOs/StorageUploadedFileDTO.cs` | 4 |
| `GenerateTestReportCommand` | Class | `ClassifiedAds.Modules.TestReporting/Commands/GenerateTestReportCommand.cs` | 14 |
| `GenerateTestReportCommandHandler` | Class | `ClassifiedAds.Modules.TestReporting/Commands/GenerateTestReportCommand.cs` | 31 |
| `TestRunReportContextDto` | Class | `ClassifiedAds.Contracts/TestExecution/DTOs/TestRunReportContextDto.cs` | 6 |
| `TestRunReportRunDto` | Class | `ClassifiedAds.Contracts/TestExecution/DTOs/TestRunReportContextDto.cs` | 33 |
| `ITestReportGenerator` | Interface | `ClassifiedAds.Modules.TestReporting/Services/ITestReportGenerator.cs` | 9 |
| `IReportDataSanitizer` | Interface | `ClassifiedAds.Modules.TestReporting/Services/IReportDataSanitizer.cs` | 4 |
| `ICoverageCalculator` | Interface | `ClassifiedAds.Modules.TestReporting/Services/ICoverageCalculator.cs` | 7 |
| `AddTestReportingModule` | Method | `ClassifiedAds.Modules.TestReporting/ServiceCollectionExtensions.cs` | 16 |
| `GenerateAsync_ShouldSelectRendererUploadUpsertCoverageAndReturnMetadata` | Method | `ClassifiedAds.UnitTests/TestReporting/TestReportGeneratorTests.cs` | 17 |
| `GenerateAsync_ShouldSelectRequestedRendererAndAddCoverageWhenMissing` | Method | `ClassifiedAds.UnitTests/TestReporting/TestReportGeneratorTests.cs` | 136 |
| `GenerateAsync_WhenUploadFails_ShouldNotPersistMetadata` | Method | `ClassifiedAds.UnitTests/TestReporting/TestReportGeneratorTests.cs` | 234 |
| `CreateContext` | Method | `ClassifiedAds.UnitTests/TestReporting/ReportTestData.cs` | 24 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `GenerateAsync → TestRunReportRunDto` | cross_community | 4 |
| `GenerateAsync → TestRunReportContextDto` | cross_community | 3 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Services | 14 calls |
| Commands | 4 calls |
| TestExecution | 3 calls |
| ConfigurationOptions | 1 calls |
| Controllers | 1 calls |
| Models | 1 calls |
| Queries | 1 calls |

## How to Explore

1. `gitnexus_context({name: "TestReportGenerator"})` — see callers and callees
2. `gitnexus_query({query: "testreporting"})` — find related execution flows
3. Read key files listed above for implementation details
