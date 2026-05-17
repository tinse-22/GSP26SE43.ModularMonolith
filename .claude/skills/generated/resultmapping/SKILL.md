---
name: resultmapping
description: "Skill for the ResultMapping area of GSP26SE43.ModularMonolith. 25 symbols across 6 files."
---

# ResultMapping

25 symbols | 6 files | Cohesion: 96%

## When to Use

- Working with code in `ClassifiedAds.UnitTests/`
- Understanding how ToActionResult, ToActionResult, ToActionResult work
- Modifying resultmapping-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs` | ToActionResult, ToActionResult, ToActionResult, ToCreatedResult, CreateErrorResponse (+6) |
| `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | ToActionResult_OnNotFoundError_ShouldReturn404, ToActionResult_ShouldIncludeTraceIdInProblemDetails, ToActionResult_ShouldIncludeInstancePath, ToActionResult_ShouldIncludeTypeUri, ToActionResult_OnError_ShouldSetProblemJsonContentType (+5) |
| `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | NotFound_ShouldCreateFailedResultWithNotFoundError |
| `ClassifiedAds.UnitTests/Domain/ResultPattern/ErrorTests.cs` | NotFound_ShouldCreateNotFoundErrorWithEntityAndId |
| `ClassifiedAds.Domain/Infrastructure/ResultPattern/Result.cs` | NotFound |
| `ClassifiedAds.Domain/Infrastructure/ResultPattern/Error.cs` | NotFound |

## Entry Points

Start here when exploring this area:

- **`ToActionResult`** (Method) — `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs:35`
- **`ToActionResult`** (Method) — `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs:48`
- **`ToActionResult`** (Method) — `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs:65`
- **`ToCreatedResult`** (Method) — `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs:81`
- **`ToActionResult_OnNotFoundError_ShouldReturn404`** (Method) — `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs:39`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `ToActionResult` | Method | `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs` | 35 |
| `ToActionResult` | Method | `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs` | 48 |
| `ToActionResult` | Method | `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs` | 65 |
| `ToCreatedResult` | Method | `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs` | 81 |
| `ToActionResult_OnNotFoundError_ShouldReturn404` | Method | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 39 |
| `ToActionResult_ShouldIncludeTraceIdInProblemDetails` | Method | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 214 |
| `ToActionResult_ShouldIncludeInstancePath` | Method | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 229 |
| `ToActionResult_ShouldIncludeTypeUri` | Method | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 244 |
| `ToActionResult_OnError_ShouldSetProblemJsonContentType` | Method | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 287 |
| `NotFound_ShouldCreateFailedResultWithNotFoundError` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 71 |
| `NotFound_ShouldCreateNotFoundErrorWithEntityAndId` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ErrorTests.cs` | 59 |
| `NotFound` | Method | `ClassifiedAds.Domain/Infrastructure/ResultPattern/Result.cs` | 80 |
| `NotFound` | Method | `ClassifiedAds.Domain/Infrastructure/ResultPattern/Error.cs` | 49 |
| `GenericToActionResult_OnSuccess_ShouldReturnOkWithValue` | Method | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 137 |
| `GenericToActionResult_WithCustomStatusCode_ShouldUseCustomCode` | Method | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 166 |
| `ToCreatedResult_OnSuccess_ShouldReturn201` | Method | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 180 |
| `ToCreatedResult_WithLocation_ShouldReturnCreatedResultWithLocation` | Method | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 195 |
| `TestDto` | Class | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 304 |
| `CreateErrorResponse` | Method | `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs` | 99 |
| `GetStatusCodeFromError` | Method | `ClassifiedAds.Infrastructure/Web/ResultMapping/ResultExtensions.cs` | 125 |

## Connected Areas

| Area | Connections |
|------|-------------|
| ResultPattern | 1 calls |

## How to Explore

1. `gitnexus_context({name: "ToActionResult"})` — see callers and callees
2. `gitnexus_query({query: "resultmapping"})` — find related execution flows
3. Read key files listed above for implementation details
