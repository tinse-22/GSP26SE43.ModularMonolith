---
name: resultpattern
description: "Skill for the ResultPattern area of GSP26SE43.ModularMonolith. 55 symbols across 5 files."
---

# ResultPattern

55 symbols | 5 files | Cohesion: 78%

## When to Use

- Working with code in `ClassifiedAds.UnitTests/`
- Understanding how Result, Fail_WithSingleError_ShouldCreateFailedResult, HasErrorCode_ShouldReturnTrueWhenErrorExists work
- Modifying resultpattern-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | Fail_WithSingleError_ShouldCreateFailedResult, HasErrorCode_ShouldReturnTrueWhenErrorExists, HasErrorCodePrefix_ShouldMatchByPrefix, ImplicitConversion_FromError_ShouldCreateFailedResult, GenericFail_ShouldCreateFailedResult (+14) |
| `ClassifiedAds.UnitTests/Domain/ResultPattern/ErrorTests.cs` | Create_ShouldCreateErrorWithCodeAndMessage, Create_WithMetadata_ShouldIncludeMetadata, ToString_ShouldReturnFormattedString, Create_WithNullCode_ShouldThrowArgumentNullException, Create_WithNullMessage_ShouldThrowArgumentNullException (+6) |
| `ClassifiedAds.Domain/Infrastructure/ResultPattern/Result.cs` | Fail, Fail, Result, Fail, GetErrorMessages (+6) |
| `ClassifiedAds.Domain/Infrastructure/ResultPattern/Error.cs` | Create, ToString, Conflict, Forbidden, Internal (+2) |
| `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | ToActionResult_OnConflictError_ShouldReturn409, ToActionResult_OnForbiddenError_ShouldReturn403, ToActionResult_OnInternalError_ShouldReturn500, ToActionResult_OnValidationError_ShouldReturn400WithValidationProblemDetails, ToActionResult_MultipleValidationErrors_ShouldGroupByField (+2) |

## Entry Points

Start here when exploring this area:

- **`Result`** (Class) — `ClassifiedAds.Domain/Infrastructure/ResultPattern/Result.cs:11`
- **`Fail_WithSingleError_ShouldCreateFailedResult`** (Method) — `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs:25`
- **`HasErrorCode_ShouldReturnTrueWhenErrorExists`** (Method) — `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs:110`
- **`HasErrorCodePrefix_ShouldMatchByPrefix`** (Method) — `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs:121`
- **`ImplicitConversion_FromError_ShouldCreateFailedResult`** (Method) — `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs:149`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `Result` | Class | `ClassifiedAds.Domain/Infrastructure/ResultPattern/Result.cs` | 11 |
| `Fail_WithSingleError_ShouldCreateFailedResult` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 25 |
| `HasErrorCode_ShouldReturnTrueWhenErrorExists` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 110 |
| `HasErrorCodePrefix_ShouldMatchByPrefix` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 121 |
| `ImplicitConversion_FromError_ShouldCreateFailedResult` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 149 |
| `GenericFail_ShouldCreateFailedResult` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 181 |
| `Value_OnFailedResult_ShouldThrowInvalidOperationException` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 195 |
| `Map_OnFailure_ShouldPreserveErrors` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 248 |
| `OnSuccess_WhenFailed_ShouldNotExecuteAction` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 277 |
| `OnFailure_WhenFailed_ShouldExecuteAction` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 291 |
| `GetValueOrDefault_OnFailure_ShouldReturnDefault` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 332 |
| `GenericImplicitConversion_FromError_ShouldCreateFailedResult` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ResultTests.cs` | 356 |
| `Create_ShouldCreateErrorWithCodeAndMessage` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ErrorTests.cs` | 10 |
| `Create_WithMetadata_ShouldIncludeMetadata` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ErrorTests.cs` | 26 |
| `ToString_ShouldReturnFormattedString` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ErrorTests.cs` | 140 |
| `Create_WithNullCode_ShouldThrowArgumentNullException` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ErrorTests.cs` | 153 |
| `Create_WithNullMessage_ShouldThrowArgumentNullException` | Method | `ClassifiedAds.UnitTests/Domain/ResultPattern/ErrorTests.cs` | 163 |
| `Create` | Method | `ClassifiedAds.Domain/Infrastructure/ResultPattern/Error.cs` | 37 |
| `ToString` | Method | `ClassifiedAds.Domain/Infrastructure/ResultPattern/Error.cs` | 91 |
| `ToActionResult_OnConflictError_ShouldReturn409` | Method | `ClassifiedAds.UnitTests/Infrastructure/ResultMapping/ResultExtensionsTests.cs` | 77 |

## Connected Areas

| Area | Connections |
|------|-------------|
| ResultMapping | 1 calls |

## How to Explore

1. `gitnexus_context({name: "Result"})` — see callers and callees
2. `gitnexus_query({query: "resultpattern"})` — find related execution flows
3. Read key files listed above for implementation details
