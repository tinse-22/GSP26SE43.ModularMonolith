---
name: apidocumentation
description: "Skill for the ApiDocumentation area of GSP26SE43.ModularMonolith. 243 symbols across 54 files."
---

# ApiDocumentation

243 symbols | 54 files | Cohesion: 79%

## When to Use

- Working with code in `ClassifiedAds.UnitTests/`
- Understanding how SpecificationParseResult, SecurityScheme, ParseUploadedSpecificationCommand work
- Modifying apidocumentation-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.UnitTests/ApiDocumentation/CurlParserTests.cs` | Parse_SimpleGetRequest_Should_ExtractMethodAndUrl, Parse_ExplicitGetMethod_Should_SetMethodCorrectly, Parse_PostWithData_Should_AutoDetectPostMethod, Parse_ExplicitPutMethod_Should_OverrideAutoDetection, Parse_WithHeaders_Should_ExtractHeaders (+15) |
| `ClassifiedAds.UnitTests/ApiDocumentation/PostmanSpecificationParserTests.cs` | ParseAsync_NullContent_Should_ReturnFailure, ParseAsync_EmptyContent_Should_ReturnFailure, ParseAsync_InvalidJson_Should_ReturnFailure, ParseAsync_MissingInfoProperty_Should_ReturnFailure, ParseAsync_MissingItemProperty_Should_ReturnFailure (+15) |
| `ClassifiedAds.UnitTests/ApiDocumentation/OpenApiSpecificationParserTests.cs` | ParseAsync_NullContent_Should_ReturnFailure, ParseAsync_EmptyContent_Should_ReturnFailure, ParseAsync_InvalidJson_Should_ReturnFailure, ParseAsync_ValidOpenApi30_Should_ParseEndpoints, ParseAsync_WithSecuritySchemes_Should_ParseSchemes (+14) |
| `ClassifiedAds.UnitTests/ApiDocumentation/ParseUploadedSpecificationCommandHandlerTests.cs` | CreatePendingSpec, SetupSpecFound, SetupFileDownload, SetupParseSuccess, HandleAsync_SpecNotFound_Should_ReturnWithoutProcessing (+12) |
| `ClassifiedAds.UnitTests/ApiDocumentation/AddUpdateEndpointCommandHandlerTests.cs` | SetupValidProjectAndSpec, HandleAsync_CreateEndpoint_Should_AddEndpointSuccessfully, HandleAsync_CreateEndpoint_WithParameters_Should_AddParametersToo, HandleAsync_CreateEndpoint_WithResponses_Should_AddResponsesToo, HandleAsync_CreateEndpoint_WithJsonFields_Should_NormalizeJsonbValuesBeforePersisting (+12) |
| `ClassifiedAds.UnitTests/ApiDocumentation/CreateManualSpecificationCommandHandlerTests.cs` | SetupValidProject, HandleAsync_ValidRequest_Should_CreateSpecWithEndpoints, HandleAsync_WithParameters_Should_CreateParametersForEndpoints, HandleAsync_WithJsonFields_Should_NormalizeJsonbValuesBeforePersisting, HandleAsync_WithMalformedJsonLikeSchema_Should_ThrowValidationExceptionBeforeConsumingLimit (+11) |
| `ClassifiedAds.UnitTests/ApiDocumentation/ImportCurlCommandHandlerTests.cs` | SetupValidProject, HandleAsync_SimpleGetCurl_Should_CreateSpecAndEndpoint, HandleAsync_PostCurlWithBody_Should_CreateBodyParameter, HandleAsync_PostCurlWithPlainTextBody_Should_SerializeBodyForJsonColumn, HandleAsync_CurlWithQueryParams_Should_CreateQueryParameters (+10) |
| `ClassifiedAds.UnitTests/ApiDocumentation/PathParameterTemplateServiceTests.cs` | GenerateMutations_IntegerWithoutFormat_ShouldContainMaxInt64Mutation, GenerateMutations_IntegerInt32_ShouldContainMaxAndOverflowMutations, GenerateMutations_UuidString_ShouldContainUuidSpecificMutations, GenerateMutations_Number_ShouldContainVerySmallMutationWithExpectedStatus, GenerateMutations_Boolean_ShouldContainNumericBooleanMutation (+9) |
| `ClassifiedAds.UnitTests/ApiDocumentation/ProjectQueryHandlerTests.cs` | HandleAsync_Should_ReturnOnlyActiveProjects_WhenStatusNotProvided, HandleAsync_Should_ReturnArchivedProjects_WhenStatusArchivedRequested, SetupProjectQueryable, SetupSpecificationQueryable, HandleAsync_Should_ThrowNotFound_ForArchivedProject_WhenIncludeArchivedIsFalse (+4) |
| `ClassifiedAds.UnitTests/ApiDocumentation/PathParameterQueryHandlerTests.cs` | HandleAsync_Should_ApplyExplicitThenFallbackValues_AndResolveUrl, HandleAsync_Should_LeaveParamUnresolved_WhenExamplesJsonMalformed, HandleAsync_Should_UseSingleJsonStringExampleValue, HandleAsync_Should_ThrowNotFound_WhenProjectOwnerMismatch, SetupRepositories (+4) |

## Entry Points

Start here when exploring this area:

- **`SpecificationParseResult`** (Class) — `ClassifiedAds.Modules.ApiDocumentation/Services/ISpecificationParser.cs:26`
- **`SecurityScheme`** (Class) — `ClassifiedAds.Modules.ApiDocumentation/Entities/SecurityScheme.cs:8`
- **`ParseUploadedSpecificationCommand`** (Class) — `ClassifiedAds.Modules.ApiDocumentation/Commands/ParseUploadedSpecificationCommand.cs:16`
- **`CurlParseResult`** (Class) — `ClassifiedAds.Modules.ApiDocumentation/Services/CurlParser.cs:7`
- **`ImportCurlModel`** (Class) — `ClassifiedAds.Modules.ApiDocumentation/Models/ImportCurlModel.cs:2`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `SpecificationParseResult` | Class | `ClassifiedAds.Modules.ApiDocumentation/Services/ISpecificationParser.cs` | 26 |
| `SecurityScheme` | Class | `ClassifiedAds.Modules.ApiDocumentation/Entities/SecurityScheme.cs` | 8 |
| `ParseUploadedSpecificationCommand` | Class | `ClassifiedAds.Modules.ApiDocumentation/Commands/ParseUploadedSpecificationCommand.cs` | 16 |
| `CurlParseResult` | Class | `ClassifiedAds.Modules.ApiDocumentation/Services/CurlParser.cs` | 7 |
| `ImportCurlModel` | Class | `ClassifiedAds.Modules.ApiDocumentation/Models/ImportCurlModel.cs` | 2 |
| `Project` | Class | `ClassifiedAds.Modules.ApiDocumentation/Entities/Project.cs` | 9 |
| `ImportCurlCommand` | Class | `ClassifiedAds.Modules.ApiDocumentation/Commands/ImportCurlCommand.cs` | 18 |
| `CreateUpdateEndpointModel` | Class | `ClassifiedAds.Modules.ApiDocumentation/Models/CreateUpdateEndpointModel.cs` | 4 |
| `AddUpdateEndpointCommand` | Class | `ClassifiedAds.Modules.ApiDocumentation/Commands/AddUpdateEndpointCommand.cs` | 19 |
| `CreateManualSpecificationModel` | Class | `ClassifiedAds.Modules.ApiDocumentation/Models/CreateManualSpecificationModel.cs` | 4 |
| `CreateManualSpecificationCommand` | Class | `ClassifiedAds.Modules.ApiDocumentation/Commands/CreateManualSpecificationCommand.cs` | 17 |
| `GetProjectsQuery` | Class | `ClassifiedAds.Modules.ApiDocumentation/Queries/GetProjectsQuery.cs` | 12 |
| `GetProjectsQueryHandler` | Class | `ClassifiedAds.Modules.ApiDocumentation/Queries/GetProjectsQuery.cs` | 25 |
| `PaginatedResult` | Class | `ClassifiedAds.Modules.ApiDocumentation/Models/PaginatedResult.cs` | 5 |
| `EndpointSecurityReq` | Class | `ClassifiedAds.Modules.ApiDocumentation/Entities/EndpointSecurityReq.cs` | 8 |
| `EndpointResponse` | Class | `ClassifiedAds.Modules.ApiDocumentation/Entities/EndpointResponse.cs` | 8 |
| `EndpointParameter` | Class | `ClassifiedAds.Modules.ApiDocumentation/Entities/EndpointParameter.cs` | 8 |
| `ApiSpecification` | Class | `ClassifiedAds.Modules.ApiDocumentation/Entities/ApiSpecification.cs` | 9 |
| `ApiEndpoint` | Class | `ClassifiedAds.Modules.ApiDocumentation/Entities/ApiEndpoint.cs` | 9 |
| `GetProjectQueryHandler` | Class | `ClassifiedAds.Modules.ApiDocumentation/Queries/GetProjectQuery.cs` | 24 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `HandleAsync → ValidationException` | cross_community | 4 |
| `HandleAsync → PathParameterInfo` | cross_community | 4 |
| `HandleAsync → IsManualResetEventDisposed` | cross_community | 4 |
| `HandleAsync → DispatchAsync` | cross_community | 4 |
| `HandleAsync → GetCurrentSubscriptionByUserQuery` | cross_community | 4 |
| `HandleAsync → LimitCheckResultDTO` | cross_community | 4 |
| `HandleAsync → GetPlanQuery` | cross_community | 4 |
| `HandleAsync → ResolvedUrlResult` | cross_community | 3 |
| `HandleAsync → TryGetValueIgnoreCase` | cross_community | 3 |
| `HandleAsync → ConsumeLimitAtomicallyCommand` | cross_community | 3 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Services | 30 calls |
| Controllers | 10 calls |
| Queries | 10 calls |
| TestGeneration | 8 calls |
| Commands | 7 calls |
| Subscription | 6 calls |
| Notification | 1 calls |

## How to Explore

1. `gitnexus_context({name: "SpecificationParseResult"})` — see callers and callees
2. `gitnexus_query({query: "apidocumentation"})` — find related execution flows
3. Read key files listed above for implementation details
