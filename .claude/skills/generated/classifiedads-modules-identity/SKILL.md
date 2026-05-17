---
name: classifiedads-modules-identity
description: "Skill for the ClassifiedAds.Modules.Identity area of GSP26SE43.ModularMonolith. 39 symbols across 13 files."
---

# ClassifiedAds.Modules.Identity

39 symbols | 13 files | Cohesion: 82%

## When to Use

- Working with code in `ClassifiedAds.Modules.Identity/`
- Understanding how UserRole, UserClaim, UserQueryOptions work
- Modifying classifiedads.modules.identity-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.Identity/UserStore.cs` | CreateAsync, DeleteAsync, UpdateAsync, AddToRoleAsync, RemoveFromRoleAsync (+16) |
| `ClassifiedAds.Modules.Identity/RoleStore.cs` | RoleStore, CreateAsync, DeleteAsync, UpdateAsync, PersistRoleAsync (+1) |
| `ClassifiedAds.UnitTests/Identity/CustomIdentityStoreTests.cs` | UserStore_CreateAsync_ShouldAddUserAndPersist_WhenIdIsPreAssigned, RoleStore_CreateAsync_ShouldAddRoleAndPersist_WhenIdIsPreAssigned |
| `ClassifiedAds.Modules.Identity/DbConfigurations/UserRoleConfiguration.cs` | Configure |
| `ClassifiedAds.Modules.Identity/Entities/UserRole.cs` | UserRole |
| `ClassifiedAds.Modules.Identity/Entities/UserClaim.cs` | UserClaim |
| `ClassifiedAds.Modules.Identity/Queries/GetUsersQuery.cs` | HandleAsync |
| `ClassifiedAds.Modules.Identity/Queries/GetUserQuery.cs` | HandleAsync |
| `ClassifiedAds.Modules.Identity/Persistence/UserRepository.cs` | Get |
| `ClassifiedAds.Modules.Identity/Persistence/IUserRepository.cs` | Get |

## Entry Points

Start here when exploring this area:

- **`UserRole`** (Class) — `ClassifiedAds.Modules.Identity/Entities/UserRole.cs:5`
- **`UserClaim`** (Class) — `ClassifiedAds.Modules.Identity/Entities/UserClaim.cs:5`
- **`UserQueryOptions`** (Class) — `ClassifiedAds.Contracts/Identity/DTOs/UserQueryOptions.cs:2`
- **`UserToken`** (Class) — `ClassifiedAds.Modules.Identity/Entities/UserToken.cs:5`
- **`UserStore`** (Class) — `ClassifiedAds.Modules.Identity/UserStore.cs:15`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `UserRole` | Class | `ClassifiedAds.Modules.Identity/Entities/UserRole.cs` | 5 |
| `UserClaim` | Class | `ClassifiedAds.Modules.Identity/Entities/UserClaim.cs` | 5 |
| `UserQueryOptions` | Class | `ClassifiedAds.Contracts/Identity/DTOs/UserQueryOptions.cs` | 2 |
| `UserToken` | Class | `ClassifiedAds.Modules.Identity/Entities/UserToken.cs` | 5 |
| `UserStore` | Class | `ClassifiedAds.Modules.Identity/UserStore.cs` | 15 |
| `RoleStore` | Class | `ClassifiedAds.Modules.Identity/RoleStore.cs` | 13 |
| `IdentityDbContext` | Class | `ClassifiedAds.Modules.Identity/Persistence/IdentityDbContext.cs` | 8 |
| `CreateAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 43 |
| `DeleteAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 57 |
| `UpdateAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 243 |
| `AddToRoleAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 349 |
| `RemoveFromRoleAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 370 |
| `AddClaimsAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 460 |
| `ReplaceClaimAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 481 |
| `RemoveClaimsAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 501 |
| `Configure` | Method | `ClassifiedAds.Modules.Identity/DbConfigurations/UserRoleConfiguration.cs` | 9 |
| `FindByEmailAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 67 |
| `FindByIdAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 73 |
| `FindByNameAsync` | Method | `ClassifiedAds.Modules.Identity/UserStore.cs` | 78 |
| `HandleAsync` | Method | `ClassifiedAds.Modules.Identity/Queries/GetUsersQuery.cs` | 27 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `AddToRoleAsync → SaveChangesAsync` | cross_community | 3 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Services | 1 calls |
| HostedServices | 1 calls |
| Identity | 1 calls |

## How to Explore

1. `gitnexus_context({name: "UserRole"})` — see callers and callees
2. `gitnexus_query({query: "classifiedads.modules.identity"})` — find related execution flows
3. Read key files listed above for implementation details
