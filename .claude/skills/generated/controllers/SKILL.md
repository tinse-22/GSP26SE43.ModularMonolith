---
name: controllers
description: "Skill for the Controllers area of GSP26SE43.ModularMonolith. 201 symbols across 96 files."
---

# Controllers

201 symbols | 96 files | Cohesion: 83%

## When to Use

- Working with code in `ClassifiedAds.Modules.Identity/`
- Understanding how SrsAnalysisAcceptedResponse, UpsertLlmSuggestionFeedbackCommand, UpdateTestCaseCommand work
- Modifying controllers-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.Identity/Controllers/AuthController.cs` | RegisterCoreAsync, ForgotPassword, ResetPassword, ChangePassword, ResendConfirmationEmail (+13) |
| `ClassifiedAds.Modules.Subscription/Controllers/PaymentsController.cs` | Subscribe, Get, CreatePayOsCheckout, CheckPaymentStatus, SyncPaymentFromPayOs (+5) |
| `ClassifiedAds.Modules.Subscription/Controllers/SubscriptionsController.cs` | Get, Post, Put, GetHistory, GetPayments (+5) |
| `ClassifiedAds.Contracts/Notification/Services/IEmailTemplateService.cs` | WelcomeConfirmEmail, ResendConfirmEmail, ForgotPassword, PasswordChanged, AdminResetPassword (+4) |
| `ClassifiedAds.Modules.Identity/Controllers/UsersController.cs` | Delete, Get, Put, SetPassword, SendPasswordResetEmail (+3) |
| `ClassifiedAds.Modules.ApiDocumentation/Controllers/SpecificationsController.cs` | GetById, Upload, CreateManual, ImportCurl, Activate (+3) |
| `ClassifiedAds.Modules.TestGeneration/Controllers/TestCasesController.cs` | Add, Update, Restore, BulkDelete, BulkRestore (+2) |
| `ClassifiedAds.Modules.TestGeneration/Controllers/SrsDocumentsController.cs` | Create, Analyze, AddRequirement, DeleteRequirement, AnswerClarification (+2) |
| `ClassifiedAds.Modules.ApiDocumentation/Controllers/ProjectsController.cs` | Archive, Unarchive, Delete, Get, Post (+1) |
| `ClassifiedAds.WebAPI/Controllers/AdminApiDocumentationController.cs` | GetProjects, ResolveUserName, ResolveUserName, ResolveUserEmail, ResolveUserEmail |

## Entry Points

Start here when exploring this area:

- **`SrsAnalysisAcceptedResponse`** (Class) — `ClassifiedAds.Modules.TestGeneration/Models/SrsDocumentModel.cs:165`
- **`UpsertLlmSuggestionFeedbackCommand`** (Class) — `ClassifiedAds.Modules.TestGeneration/Commands/UpsertLlmSuggestionFeedbackCommand.cs:14`
- **`UpdateTestCaseCommand`** (Class) — `ClassifiedAds.Modules.TestGeneration/Commands/UpdateTestCaseCommand.cs:14`
- **`TriggerSrsRefinementCommand`** (Class) — `ClassifiedAds.Modules.TestGeneration/Commands/TriggerSrsRefinementCommand.cs:19`
- **`TriggerSrsAnalysisCommand`** (Class) — `ClassifiedAds.Modules.TestGeneration/Commands/TriggerSrsAnalysisCommand.cs:23`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `SrsAnalysisAcceptedResponse` | Class | `ClassifiedAds.Modules.TestGeneration/Models/SrsDocumentModel.cs` | 165 |
| `UpsertLlmSuggestionFeedbackCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/UpsertLlmSuggestionFeedbackCommand.cs` | 14 |
| `UpdateTestCaseCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/UpdateTestCaseCommand.cs` | 14 |
| `TriggerSrsRefinementCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/TriggerSrsRefinementCommand.cs` | 19 |
| `TriggerSrsAnalysisCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/TriggerSrsAnalysisCommand.cs` | 23 |
| `ReviewLlmSuggestionCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/ReviewLlmSuggestionCommand.cs` | 15 |
| `RestoreTestCaseCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/RestoreTestCaseCommand.cs` | 14 |
| `ProcessSrsRefinementCallbackCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/ProcessSrsRefinementCallbackCommand.cs` | 18 |
| `N8nSrsRefinedRequirement` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/ProcessSrsRefinementCallbackCommand.cs` | 102 |
| `DeleteTraceabilityLinkCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/DeleteTraceabilityLinkCommand.cs` | 12 |
| `DeleteSrsRequirementCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/DeleteSrsRequirementCommand.cs` | 12 |
| `DeleteSrsDocumentCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/DeleteSrsDocumentCommand.cs` | 11 |
| `CreateTraceabilityLinkCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/CreateTraceabilityLinkCommand.cs` | 13 |
| `CreateSrsDocumentCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/CreateSrsDocumentCommand.cs` | 12 |
| `BulkRestoreTestCasesCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/BulkRestoreTestCasesCommand.cs` | 14 |
| `BulkRestoreLlmSuggestionsCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/BulkRestoreLlmSuggestionsCommand.cs` | 13 |
| `BulkDeleteTestCasesCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/BulkDeleteTestCasesCommand.cs` | 14 |
| `BulkDeleteLlmSuggestionsCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/BulkDeleteLlmSuggestionsCommand.cs` | 13 |
| `AnswerSrsRequirementClarificationCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/AnswerSrsRequirementClarificationCommand.cs` | 12 |
| `AddTestCaseCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/AddTestCaseCommand.cs` | 14 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `LoginWithGoogle → IsSupabasePooler` | cross_community | 7 |
| `ChangePassword → IsSupabasePooler` | cross_community | 7 |
| `RefreshToken → GetRefreshTokenNames` | cross_community | 7 |
| `Login → IsSupabasePooler` | cross_community | 7 |
| `RefreshToken → IsManualResetEventDisposed` | cross_community | 6 |
| `RefreshToken → SetTokensInMemory` | cross_community | 6 |
| `LoginWithGoogle → SetTokensInMemory` | cross_community | 5 |
| `ChangePassword → GetRefreshTokenNames` | cross_community | 5 |
| `ChangePassword → RemoveTokensInMemory` | cross_community | 5 |
| `RefreshToken → RemoveTokensInMemory` | cross_community | 5 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Services | 19 calls |
| Subscription | 4 calls |
| ApiDocumentation | 4 calls |
| Queries | 3 calls |
| EmailQueue | 3 calls |
| Identity | 2 calls |
| TestGeneration | 2 calls |
| HostedServices | 2 calls |

## How to Explore

1. `gitnexus_context({name: "SrsAnalysisAcceptedResponse"})` — see callers and callees
2. `gitnexus_query({query: "controllers"})` — find related execution flows
3. Read key files listed above for implementation details
