---
name: persistence
description: "Skill for the Persistence area of GSP26SE43.ModularMonolith. 63 symbols across 40 files."
---

# Persistence

63 symbols | 40 files | Cohesion: 96%

## When to Use

- Working with code in `ClassifiedAds.Modules.Identity/`
- Understanding how DbContextUnitOfWork, TestReportingDbContext, TestGenerationDbContext work
- Modifying persistence-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.Storage/Persistence/StorageDbContext.cs` | StorageDbContext, SaveChanges, SaveChangesAsync, SetOutboxActivityId, HandleFileEntriesDeleted |
| `ClassifiedAds.Modules.TestReporting/Persistence/TestReportingDbContext.cs` | TestReportingDbContext, SaveChanges, SaveChangesAsync, SetOutboxActivityId |
| `ClassifiedAds.Modules.TestExecution/Persistence/TestExecutionDbContext.cs` | TestExecutionDbContext, SaveChanges, SaveChangesAsync, SetOutboxActivityId |
| `ClassifiedAds.Modules.Subscription/Persistence/SubscriptionDbContext.cs` | SubscriptionDbContext, SaveChanges, SaveChangesAsync, SetOutboxActivityId |
| `ClassifiedAds.Modules.LlmAssistant/Persistence/LlmAssistantDbContext.cs` | LlmAssistantDbContext, SaveChanges, SaveChangesAsync, SetOutboxActivityId |
| `ClassifiedAds.Modules.ApiDocumentation/Persistence/ApiDocumentationDbContext.cs` | ApiDocumentationDbContext, SaveChanges, SaveChangesAsync, SetOutboxActivityId |
| `ClassifiedAds.Modules.Identity/Persistence/IRoleRepository.cs` | IRoleRepository, RoleQueryOptions, Get |
| `ClassifiedAds.Modules.Identity/Persistence/RoleRepository.cs` | RoleRepository, Get |
| `ClassifiedAds.Modules.Identity/RoleStore.cs` | FindByIdAsync, FindByNameAsync |
| `ClassifiedAds.Persistence.PostgreSQL/DbContextUnitOfWork.cs` | DbContextUnitOfWork |

## Entry Points

Start here when exploring this area:

- **`DbContextUnitOfWork`** (Class) — `ClassifiedAds.Persistence.PostgreSQL/DbContextUnitOfWork.cs:16`
- **`TestReportingDbContext`** (Class) — `ClassifiedAds.Modules.TestReporting/Persistence/TestReportingDbContext.cs:11`
- **`TestGenerationDbContext`** (Class) — `ClassifiedAds.Modules.TestGeneration/Persistence/TestGenerationDbContext.cs:11`
- **`TestExecutionDbContext`** (Class) — `ClassifiedAds.Modules.TestExecution/Persistence/TestExecutionDbContext.cs:11`
- **`SubscriptionDbContext`** (Class) — `ClassifiedAds.Modules.Subscription/Persistence/SubscriptionDbContext.cs:11`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `DbContextUnitOfWork` | Class | `ClassifiedAds.Persistence.PostgreSQL/DbContextUnitOfWork.cs` | 16 |
| `TestReportingDbContext` | Class | `ClassifiedAds.Modules.TestReporting/Persistence/TestReportingDbContext.cs` | 11 |
| `TestGenerationDbContext` | Class | `ClassifiedAds.Modules.TestGeneration/Persistence/TestGenerationDbContext.cs` | 11 |
| `TestExecutionDbContext` | Class | `ClassifiedAds.Modules.TestExecution/Persistence/TestExecutionDbContext.cs` | 11 |
| `SubscriptionDbContext` | Class | `ClassifiedAds.Modules.Subscription/Persistence/SubscriptionDbContext.cs` | 11 |
| `StorageDbContext` | Class | `ClassifiedAds.Modules.Storage/Persistence/StorageDbContext.cs` | 11 |
| `NotificationDbContext` | Class | `ClassifiedAds.Modules.Notification/Persistence/NotificationDbContext.cs` | 6 |
| `LlmAssistantDbContext` | Class | `ClassifiedAds.Modules.LlmAssistant/Persistence/LlmAssistantDbContext.cs` | 11 |
| `ConfigurationDbContext` | Class | `ClassifiedAds.Modules.Configuration/Persistence/ConfigurationDbContext.cs` | 6 |
| `AuditLogDbContext` | Class | `ClassifiedAds.Modules.AuditLog/Persistence/AuditLogDbContext.cs` | 6 |
| `ApiDocumentationDbContext` | Class | `ClassifiedAds.Modules.ApiDocumentation/Persistence/ApiDocumentationDbContext.cs` | 11 |
| `DbContextRepository` | Class | `ClassifiedAds.Persistence.PostgreSQL/DbContextRepository.cs` | 12 |
| `Repository` | Class | `ClassifiedAds.Modules.TestReporting/Persistence/Repository.cs` | 6 |
| `Repository` | Class | `ClassifiedAds.Modules.TestGeneration/Persistence/Repository.cs` | 7 |
| `Repository` | Class | `ClassifiedAds.Modules.TestExecution/Persistence/Repository.cs` | 6 |
| `Repository` | Class | `ClassifiedAds.Modules.Subscription/Persistence/Repository.cs` | 6 |
| `Repository` | Class | `ClassifiedAds.Modules.Storage/Persistence/Repository.cs` | 6 |
| `Repository` | Class | `ClassifiedAds.Modules.Notification/Persistence/Repository.cs` | 6 |
| `Repository` | Class | `ClassifiedAds.Modules.LlmAssistant/Persistence/Repository.cs` | 6 |
| `Repository` | Class | `ClassifiedAds.Modules.Identity/Persistence/Repository.cs` | 6 |

## How to Explore

1. `gitnexus_context({name: "DbContextUnitOfWork"})` — see callers and callees
2. `gitnexus_query({query: "persistence"})` — find related execution flows
3. Read key files listed above for implementation details
