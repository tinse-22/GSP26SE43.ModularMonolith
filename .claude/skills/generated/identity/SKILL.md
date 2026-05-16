---
name: identity
description: "Skill for the Identity area of GSP26SE43.ModularMonolith. 49 symbols across 16 files."
---

# Identity

49 symbols | 16 files | Cohesion: 85%

## When to Use

- Working with code in `ClassifiedAds.UnitTests/`
- Understanding how AssignRoleModel, LoginModel, RegisterModel work
- Modifying identity-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | RegisterModel_Should_BeValid_WithCorrectData, RegisterModel_Should_RequireEmail, RegisterModel_Should_ValidateEmailFormat, RegisterModel_Should_RequirePassword, RegisterModel_Should_RequirePasswordMinLength (+10) |
| `ClassifiedAds.UnitTests/Identity/RateLimiterPolicyTests.cs` | DefaultPolicy_AuthenticatedUser_Should_UseUserIdPartition, DefaultPolicy_AnonymousUser_Should_UseIpPartition, DefaultPolicy_Should_UseXForwardedFor_WhenPresent, PasswordPolicy_Should_HaveStricterLimits, CreateMockHttpContext (+2) |
| `ClassifiedAds.UnitTests/Identity/IdentityTokenNormalizerTests.cs` | Normalize_Should_KeepRawTokenUnchanged, Normalize_Should_DecodeUrlEncodedToken, Normalize_Should_DecodeDoubleEncodedToken, Normalize_Should_ConvertSpacesToPlus, Normalize_Should_TrimToken |
| `ClassifiedAds.Modules.Identity/Models/AuthModels.cs` | LoginModel, RegisterModel, UpdateProfileModel |
| `ClassifiedAds.UnitTests/Identity/JwtTokenServiceTests.cs` | JwtOptions_Should_HaveDefaultValues, JwtOptions_Should_AllowCustomValues, IdentityModuleOptions_Should_ContainJwtOptions |
| `ClassifiedAds.Modules.Identity/Models/UserModel.cs` | AssignRoleModel, CreateUserModel |
| `ClassifiedAds.Modules.Identity/RateLimiterPolicies/PasswordRateLimiterPolicy.cs` | PasswordRateLimiterPolicy, GetPartition |
| `ClassifiedAds.Modules.Identity/RateLimiterPolicies/DefaultRateLimiterPolicy.cs` | DefaultRateLimiterPolicy, GetPartition |
| `ClassifiedAds.UnitTests/Identity/UsersControllerTests.cs` | Post_Should_KeepRequestedRolesAndAutoConfirmEmail_WhenRequestContainsOnlyUserRole, Post_Should_AssignDefaultUserRole_AndKeepEmailConfirmed_WhenRolesAreEmpty |
| `ClassifiedAds.Modules.Identity/RateLimiterPolicies/AuthRateLimiterPolicy.cs` | AuthRateLimiterPolicy, GetPartition |

## Entry Points

Start here when exploring this area:

- **`AssignRoleModel`** (Class) — `ClassifiedAds.Modules.Identity/Models/UserModel.cs:60`
- **`LoginModel`** (Class) — `ClassifiedAds.Modules.Identity/Models/AuthModels.cs:7`
- **`RegisterModel`** (Class) — `ClassifiedAds.Modules.Identity/Models/AuthModels.cs:78`
- **`PasswordRateLimiterPolicy`** (Class) — `ClassifiedAds.Modules.Identity/RateLimiterPolicies/PasswordRateLimiterPolicy.cs:14`
- **`DefaultRateLimiterPolicy`** (Class) — `ClassifiedAds.Modules.Identity/RateLimiterPolicies/DefaultRateLimiterPolicy.cs:16`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `AssignRoleModel` | Class | `ClassifiedAds.Modules.Identity/Models/UserModel.cs` | 60 |
| `LoginModel` | Class | `ClassifiedAds.Modules.Identity/Models/AuthModels.cs` | 7 |
| `RegisterModel` | Class | `ClassifiedAds.Modules.Identity/Models/AuthModels.cs` | 78 |
| `PasswordRateLimiterPolicy` | Class | `ClassifiedAds.Modules.Identity/RateLimiterPolicies/PasswordRateLimiterPolicy.cs` | 14 |
| `DefaultRateLimiterPolicy` | Class | `ClassifiedAds.Modules.Identity/RateLimiterPolicies/DefaultRateLimiterPolicy.cs` | 16 |
| `CreateUserModel` | Class | `ClassifiedAds.Modules.Identity/Models/UserModel.cs` | 39 |
| `Role` | Class | `ClassifiedAds.Modules.Identity/Entities/Role.cs` | 6 |
| `UpdateProfileModel` | Class | `ClassifiedAds.Modules.Identity/Models/AuthModels.cs` | 221 |
| `AuthRateLimiterPolicy` | Class | `ClassifiedAds.Modules.Identity/RateLimiterPolicies/AuthRateLimiterPolicy.cs` | 16 |
| `JwtOptions` | Class | `ClassifiedAds.Modules.Identity/ConfigurationOptions/JwtOptions.cs` | 6 |
| `RegisterModel_Should_BeValid_WithCorrectData` | Method | `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | 14 |
| `RegisterModel_Should_RequireEmail` | Method | `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | 32 |
| `RegisterModel_Should_ValidateEmailFormat` | Method | `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | 50 |
| `RegisterModel_Should_RequirePassword` | Method | `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | 68 |
| `RegisterModel_Should_RequirePasswordMinLength` | Method | `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | 86 |
| `RegisterModel_Should_RequireMatchingPasswords` | Method | `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | 104 |
| `LoginModel_Should_RequireEmail` | Method | `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | 204 |
| `LoginModel_Should_RequirePassword` | Method | `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | 221 |
| `AssignRoleModel_Should_RequireRoleId` | Method | `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | 261 |
| `AssignRoleModel_Should_AcceptValidRoleId` | Method | `ClassifiedAds.UnitTests/Identity/AuthModelsTests.cs` | 278 |

## Connected Areas

| Area | Connections |
|------|-------------|
| ConfigurationOptions | 1 calls |
| HostedServices | 1 calls |

## How to Explore

1. `gitnexus_context({name: "AssignRoleModel"})` — see callers and callees
2. `gitnexus_query({query: "identity"})` — find related execution flows
3. Read key files listed above for implementation details
