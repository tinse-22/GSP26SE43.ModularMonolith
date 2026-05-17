---
name: configurationoptions
description: "Skill for the ConfigurationOptions area of GSP26SE43.ModularMonolith. 36 symbols across 29 files."
---

# ConfigurationOptions

36 symbols | 29 files | Cohesion: 90%

## When to Use

- Working with code in `ClassifiedAds.Modules.Identity/`
- Understanding how TestExecutionModuleOptions, StorageModuleOptions, IdentityModuleOptions work
- Modifying configurationoptions-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs` | AddIdentityModule, AddIdentityModuleCore, AddTokenProviders, AddPasswordValidators, ConfigureOptions |
| `ClassifiedAds.Persistence.PostgreSQL/PostgresConnectionStringNormalizer.cs` | NormalizeForSupabasePooler, IsSupabasePooler |
| `ClassifiedAds.UnitTests/Subscription/PayOsOptionsTests.cs` | Bind_WhenChecksumKeyProvided_ShouldPopulateSecretKey, Bind_WhenLegacySecretKeyProvided_ShouldRemainSupported |
| `ClassifiedAds.Background/ConfigurationOptions/AppSettings.cs` | Validate, Validate |
| `ClassifiedAds.Modules.TestExecution/ServiceCollectionExtensions.cs` | AddTestExecutionModule |
| `ClassifiedAds.Modules.Storage/ServiceCollectionExtensions.cs` | AddStorageModule |
| `ClassifiedAds.Modules.Configuration/ServiceCollectionExtensions.cs` | AddConfigurationModule |
| `ClassifiedAds.Modules.AuditLog/ServiceCollectionExtensions.cs` | AddAuditLogModule |
| `ClassifiedAds.Modules.ApiDocumentation/ServiceCollectionExtensions.cs` | AddApiDocumentationModule |
| `ClassifiedAds.Modules.TestExecution/ConfigurationOptions/TestExecutionModuleOptions.cs` | TestExecutionModuleOptions |

## Entry Points

Start here when exploring this area:

- **`TestExecutionModuleOptions`** (Class) — `ClassifiedAds.Modules.TestExecution/ConfigurationOptions/TestExecutionModuleOptions.cs:2`
- **`StorageModuleOptions`** (Class) — `ClassifiedAds.Modules.Storage/ConfigurationOptions/StorageModuleOptions.cs:4`
- **`IdentityModuleOptions`** (Class) — `ClassifiedAds.Modules.Identity/ConfigurationOptions/IdentityModuleOptions.cs:2`
- **`ConnectionStringsOptions`** (Class) — `ClassifiedAds.Modules.Identity/ConfigurationOptions/ConnectionStringsOptions.cs:2`
- **`ConfigurationModuleOptions`** (Class) — `ClassifiedAds.Modules.Configuration/ConfigurationOptions/ConfigurationModuleOptions.cs:2`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `TestExecutionModuleOptions` | Class | `ClassifiedAds.Modules.TestExecution/ConfigurationOptions/TestExecutionModuleOptions.cs` | 2 |
| `StorageModuleOptions` | Class | `ClassifiedAds.Modules.Storage/ConfigurationOptions/StorageModuleOptions.cs` | 4 |
| `IdentityModuleOptions` | Class | `ClassifiedAds.Modules.Identity/ConfigurationOptions/IdentityModuleOptions.cs` | 2 |
| `ConnectionStringsOptions` | Class | `ClassifiedAds.Modules.Identity/ConfigurationOptions/ConnectionStringsOptions.cs` | 2 |
| `ConfigurationModuleOptions` | Class | `ClassifiedAds.Modules.Configuration/ConfigurationOptions/ConfigurationModuleOptions.cs` | 2 |
| `AuditLogModuleOptions` | Class | `ClassifiedAds.Modules.AuditLog/ConfigurationOptions/AuditLogModuleOptions.cs` | 2 |
| `ApiDocumentationModuleOptions` | Class | `ClassifiedAds.Modules.ApiDocumentation/ConfigurationOptions/ApiDocumentationModuleOptions.cs` | 2 |
| `StorageOptions` | Class | `ClassifiedAds.Infrastructure/Storages/StorageOptions.cs` | 7 |
| `GoogleIdentityProvider` | Class | `ClassifiedAds.Modules.Identity/IdentityProviders/Google/GoogleIdentityProvider.cs` | 5 |
| `Auth0IdentityProvider` | Class | `ClassifiedAds.Modules.Identity/IdentityProviders/Auth0/Auth0IdentityProvider.cs` | 10 |
| `AzureActiveDirectoryB2CIdentityProvider` | Class | `ClassifiedAds.Modules.Identity/IdentityProviders/Azure/AzureActiveDirectoryB2CIdentityProvider.cs` | 10 |
| `SubscriptionModuleOptions` | Class | `ClassifiedAds.Modules.Subscription/ConfigurationOptions/SubscriptionModuleOptions.cs` | 2 |
| `PayOsOptions` | Class | `ClassifiedAds.Modules.Subscription/ConfigurationOptions/PayOsOptions.cs` | 2 |
| `ConnectionStringsOptions` | Class | `ClassifiedAds.Modules.Subscription/ConfigurationOptions/ConnectionStringsOptions.cs` | 2 |
| `LlmAssistantModuleOptions` | Class | `ClassifiedAds.Modules.LlmAssistant/ConfigurationOptions/LlmAssistantModuleOptions.cs` | 2 |
| `FailureExplanationOptions` | Class | `ClassifiedAds.Modules.LlmAssistant/ConfigurationOptions/FailureExplanationOptions.cs` | 2 |
| `IIdentityProvider` | Interface | `ClassifiedAds.Modules.Identity/IdentityProviders/IIdentityProvider.cs` | 5 |
| `NormalizeForSupabasePooler` | Method | `ClassifiedAds.Persistence.PostgreSQL/PostgresConnectionStringNormalizer.cs` | 7 |
| `AddTestExecutionModule` | Method | `ClassifiedAds.Modules.TestExecution/ServiceCollectionExtensions.cs` | 20 |
| `AddStorageModule` | Method | `ClassifiedAds.Modules.Storage/ServiceCollectionExtensions.cs` | 22 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `LoginWithGoogle → IsSupabasePooler` | cross_community | 7 |
| `ChangePassword → IsSupabasePooler` | cross_community | 7 |
| `Login → IsSupabasePooler` | cross_community | 7 |
| `Logout → IsSupabasePooler` | cross_community | 7 |
| `AddIdentityModule → IsSupabasePooler` | intra_community | 3 |
| `AddIdentityModuleCore → IsSupabasePooler` | intra_community | 3 |

## How to Explore

1. `gitnexus_context({name: "TestExecutionModuleOptions"})` — see callers and callees
2. `gitnexus_query({query: "configurationoptions"})` — find related execution flows
3. Read key files listed above for implementation details
