# UML Class Diagram — Mermaid Source (for draw.io Import)

> Each diagram below is a standalone Mermaid `classDiagram` block.
> To import into draw.io: Copy the content inside each `mermaid` code fence (without the triple backticks) → Extras → Edit Diagram → paste.

---

## A) DOMAIN CORE CLASS DIAGRAMS

### A.1 — API Documentation Bounded Context

```mermaid
classDiagram
    class Project {
        <<aggregate root>>
        +Id : Guid
        +OwnerId : Guid
        +ActiveSpecId : Guid
        +Name : string
        +Description : string
        +BaseUrl : string
        +Status : ProjectStatus
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class ApiSpecification {
        <<entity>>
        +Id : Guid
        +ProjectId : Guid
        +OriginalFileId : Guid
        +Name : string
        +SourceType : SourceType
        +Version : string
        +IsActive : bool
        +ParsedAt : DateTimeOffset
        +ParseStatus : ParseStatus
        +ParseErrors : string
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class ApiEndpoint {
        <<entity>>
        +Id : Guid
        +ApiSpecId : Guid
        +HttpMethod : HttpMethod
        +Path : string
        +OperationId : string
        +Summary : string
        +Description : string
        +Tags : string
        +IsDeprecated : bool
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class EndpointParameter {
        <<entity>>
        +Id : Guid
        +EndpointId : Guid
        +Name : string
        +Location : ParameterLocation
        +DataType : string
        +Format : string
        +IsRequired : bool
        +DefaultValue : string
        +Schema : string
        +Examples : string
    }

    class EndpointResponse {
        <<entity>>
        +Id : Guid
        +EndpointId : Guid
        +StatusCode : int
        +Description : string
        +Schema : string
        +Examples : string
        +Headers : string
    }

    class SecurityScheme {
        <<entity>>
        +Id : Guid
        +ApiSpecId : Guid
        +Name : string
        +Type : SchemeType
        +Scheme : string
        +BearerFormat : string
        +In : ApiKeyLocation
        +ParameterName : string
        +Configuration : string
    }

    class EndpointSecurityReq {
        <<entity>>
        +Id : Guid
        +EndpointId : Guid
        +SecurityType : SecurityType
        +SchemeName : string
        +Scopes : string
    }

    Project "1" o-- "0..*" ApiSpecification : ApiSpecifications
    Project "0..1" --> ApiSpecification : ActiveSpec
    ApiSpecification "1" *-- "0..*" ApiEndpoint : Endpoints
    ApiSpecification "1" *-- "0..*" SecurityScheme : SecuritySchemes
    ApiEndpoint "1" *-- "0..*" EndpointParameter : Parameters
    ApiEndpoint "1" *-- "0..*" EndpointResponse : Responses
    ApiEndpoint "1" *-- "0..*" EndpointSecurityReq : SecurityRequirements
```

---

### A.2 — Test Generation Bounded Context

```mermaid
classDiagram
    class TestSuite {
        <<aggregate root>>
        +Id : Guid
        +ProjectId : Guid
        +ApiSpecId : Guid
        +SelectedEndpointIds : List~Guid~
        +Name : string
        +Description : string
        +GenerationType : GenerationType
        +Status : TestSuiteStatus
        +CreatedById : Guid
        +ApprovalStatus : ApprovalStatus
        +ApprovedById : Guid
        +ApprovedAt : DateTimeOffset
        +Version : int
        +LastModifiedById : Guid
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class TestCase {
        <<entity>>
        +Id : Guid
        +TestSuiteId : Guid
        +EndpointId : Guid
        +Name : string
        +Description : string
        +TestType : TestType
        +Priority : TestPriority
        +IsEnabled : bool
        +DependsOnId : Guid
        +OrderIndex : int
        +CustomOrderIndex : int
        +IsOrderCustomized : bool
        +Tags : string
        +LastModifiedById : Guid
        +Version : int
    }

    class TestCaseRequest {
        <<entity>>
        +Id : Guid
        +TestCaseId : Guid
        +HttpMethod : HttpMethod
        +Url : string
        +Headers : string
        +PathParams : string
        +QueryParams : string
        +BodyType : BodyType
        +Body : string
        +Timeout : int
    }

    class TestCaseExpectation {
        <<entity>>
        +Id : Guid
        +TestCaseId : Guid
        +ExpectedStatus : string
        +ResponseSchema : string
        +HeaderChecks : string
        +BodyContains : string
        +BodyNotContains : string
        +JsonPathChecks : string
        +MaxResponseTime : int
    }

    class TestCaseVariable {
        <<entity>>
        +Id : Guid
        +TestCaseId : Guid
        +VariableName : string
        +ExtractFrom : ExtractFrom
        +JsonPath : string
        +HeaderName : string
        +Regex : string
        +DefaultValue : string
    }

    class TestDataSet {
        <<entity>>
        +Id : Guid
        +TestCaseId : Guid
        +Name : string
        +Data : string
        +IsEnabled : bool
    }

    class TestCaseChangeLog {
        <<entity>>
        +Id : Guid
        +TestCaseId : Guid
        +ChangedById : Guid
        +ChangeType : TestCaseChangeType
        +FieldName : string
        +OldValue : string
        +NewValue : string
        +ChangeReason : string
        +VersionAfterChange : int
        +IpAddress : string
        +UserAgent : string
    }

    class TestOrderProposal {
        <<entity>>
        +Id : Guid
        +TestSuiteId : Guid
        +ProposalNumber : int
        +Source : ProposalSource
        +Status : ProposalStatus
        +ProposedOrder : string
        +AiReasoning : string
        +ConsideredFactors : string
        +ReviewedById : Guid
        +ReviewedAt : DateTimeOffset
        +ReviewNotes : string
        +UserModifiedOrder : string
        +AppliedOrder : string
        +AppliedAt : DateTimeOffset
        +LlmModel : string
        +TokensUsed : int
    }

    class TestSuiteVersion {
        <<entity>>
        +Id : Guid
        +TestSuiteId : Guid
        +VersionNumber : int
        +ChangedById : Guid
        +ChangeType : VersionChangeType
        +ChangeDescription : string
        +TestCaseOrderSnapshot : string
        +ApprovalStatusSnapshot : ApprovalStatus
        +PreviousState : string
        +NewState : string
    }

    TestSuite "1" *-- "0..*" TestCase : TestCases
    TestSuite "1" *-- "0..*" TestSuiteVersion : Versions
    TestSuite "1" *-- "0..*" TestOrderProposal : OrderProposals
    TestCase "1" *-- "0..1" TestCaseRequest : Request
    TestCase "1" *-- "0..1" TestCaseExpectation : Expectation
    TestCase "1" *-- "0..*" TestCaseVariable : Variables
    TestCase "1" *-- "0..*" TestDataSet : DataSets
    TestCase "1" *-- "0..*" TestCaseChangeLog : ChangeLogs
    TestCase "0..*" --> "0..1" TestCase : DependsOn
```

---

### A.3 — Test Execution & Reporting Bounded Context

```mermaid
classDiagram
    %% TestExecution Module
    class ExecutionEnvironment {
        <<aggregate root>>
        +Id : Guid
        +ProjectId : Guid
        +Name : string
        +BaseUrl : string
        +Variables : string
        +Headers : string
        +AuthConfig : string
        +IsDefault : bool
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class TestRun {
        <<aggregate root>>
        +Id : Guid
        +TestSuiteId : Guid
        +EnvironmentId : Guid
        +TriggeredById : Guid
        +RunNumber : int
        +Status : TestRunStatus
        +StartedAt : DateTimeOffset
        +CompletedAt : DateTimeOffset
        +TotalTests : int
        +PassedCount : int
        +FailedCount : int
        +SkippedCount : int
        +DurationMs : long
        +RedisKey : string
        +ResultsExpireAt : DateTimeOffset
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    %% TestReporting Module
    class TestReport {
        <<aggregate root>>
        +Id : Guid
        +TestRunId : Guid
        +GeneratedById : Guid
        +FileId : Guid
        +ReportType : ReportType
        +Format : ReportFormat
        +GeneratedAt : DateTimeOffset
        +ExpiresAt : DateTimeOffset
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class CoverageMetric {
        <<aggregate root>>
        +Id : Guid
        +TestRunId : Guid
        +TotalEndpoints : int
        +TestedEndpoints : int
        +CoveragePercent : decimal
        +ByMethod : string
        +ByTag : string
        +UncoveredPaths : string
        +CalculatedAt : DateTimeOffset
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    note "All cross-module references\n(TestSuiteId, EnvironmentId, TestRunId, FileId)\nare Guid-only FKs — no navigation properties.\nModule boundaries enforced by design."
```

---

### A.4 — Subscription & Monetization Bounded Context

```mermaid
classDiagram
    class SubscriptionPlan {
        <<aggregate root>>
        +Id : Guid
        +Name : string
        +DisplayName : string
        +Description : string
        +PriceMonthly : decimal
        +PriceYearly : decimal
        +Currency : string
        +IsActive : bool
        +SortOrder : int
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class PlanLimit {
        <<entity>>
        +Id : Guid
        +PlanId : Guid
        +LimitType : LimitType
        +LimitValue : int
        +IsUnlimited : bool
    }

    class UserSubscription {
        <<aggregate root>>
        +Id : Guid
        +UserId : Guid
        +PlanId : Guid
        +Status : SubscriptionStatus
        +BillingCycle : BillingCycle
        +StartDate : DateOnly
        +EndDate : DateOnly
        +NextBillingDate : DateOnly
        +TrialEndsAt : DateTimeOffset
        +CancelledAt : DateTimeOffset
        +AutoRenew : bool
        +ExternalSubId : string
        +ExternalCustId : string
        +SnapshotPriceMonthly : decimal
        +SnapshotPriceYearly : decimal
        +SnapshotCurrency : string
        +SnapshotPlanName : string
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class UsageTracking {
        <<aggregate root>>
        +Id : Guid
        +UserId : Guid
        +PeriodStart : DateOnly
        +PeriodEnd : DateOnly
        +ProjectCount : int
        +EndpointCount : int
        +TestSuiteCount : int
        +TestCaseCount : int
        +TestRunCount : int
        +LlmCallCount : int
        +StorageUsedMB : decimal
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class PaymentIntent {
        <<aggregate root>>
        +Id : Guid
        +UserId : Guid
        +Amount : decimal
        +Currency : string
        +Purpose : PaymentPurpose
        +PlanId : Guid
        +BillingCycle : BillingCycle
        +SubscriptionId : Guid
        +Status : PaymentIntentStatus
        +CheckoutUrl : string
        +ExpiresAt : DateTimeOffset
        +OrderCode : long
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class PaymentTransaction {
        <<aggregate root>>
        +Id : Guid
        +UserId : Guid
        +SubscriptionId : Guid
        +PaymentIntentId : Guid
        +Amount : decimal
        +Currency : string
        +Status : PaymentStatus
        +PaymentMethod : string
        +Provider : string
        +ProviderRef : string
        +ExternalTxnId : string
        +InvoiceUrl : string
        +FailureReason : string
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class SubscriptionHistory {
        <<aggregate root>>
        +Id : Guid
        +SubscriptionId : Guid
        +OldPlanId : Guid
        +NewPlanId : Guid
        +ChangeType : ChangeType
        +ChangeReason : string
        +EffectiveDate : DateOnly
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class OutboxMessage {
        <<aggregate root>>
        +Id : Guid
        +EventType : string
        +TriggeredById : Guid
        +ObjectId : string
        +Payload : string
        +Published : bool
        +ActivityId : string
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    PlanLimit "0..*" --> "1" SubscriptionPlan : Plan
    UserSubscription "0..*" --> "1" SubscriptionPlan : Plan
    PaymentIntent "0..*" --> "1" SubscriptionPlan : Plan
    PaymentIntent "0..*" --> "0..1" UserSubscription : Subscription
    PaymentTransaction "0..*" --> "1" UserSubscription : Subscription
    PaymentTransaction "0..*" --> "0..1" PaymentIntent : PaymentIntent
    SubscriptionHistory "0..*" --> "1" UserSubscription : Subscription
    SubscriptionHistory "0..*" --> "0..1" SubscriptionPlan : OldPlan
    SubscriptionHistory "0..*" --> "1" SubscriptionPlan : NewPlan
```

---

### A.5 — Identity Bounded Context

```mermaid
classDiagram
    class User {
        <<aggregate root>>
        +Id : Guid
        +UserName : string
        +NormalizedUserName : string
        +Email : string
        +NormalizedEmail : string
        +EmailConfirmed : bool
        +PasswordHash : string
        +PhoneNumber : string
        +PhoneNumberConfirmed : bool
        +TwoFactorEnabled : bool
        +ConcurrencyStamp : string
        +SecurityStamp : string
        +LockoutEnabled : bool
        +LockoutEnd : DateTimeOffset
        +AccessFailedCount : int
        +Auth0UserId : string
        +AzureAdB2CUserId : string
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class UserProfile {
        <<entity>>
        +Id : Guid
        +UserId : Guid
        +DisplayName : string
        +AvatarUrl : string
        +Timezone : string
    }

    class Role {
        <<aggregate root>>
        +Id : Guid
        +Name : string
        +NormalizedName : string
        +ConcurrencyStamp : string
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class UserRole {
        <<entity>>
        +Id : Guid
        +UserId : Guid
        +RoleId : Guid
    }

    class UserClaim {
        <<entity>>
        +Id : Guid
        +UserId : Guid
        +Type : string
        +Value : string
    }

    class RoleClaim {
        <<entity>>
        +Id : Guid
        +RoleId : Guid
        +Type : string
        +Value : string
    }

    class UserLogin {
        <<entity>>
        +Id : Guid
        +UserId : Guid
        +LoginProvider : string
        +ProviderKey : string
        +ProviderDisplayName : string
    }

    class UserToken {
        <<entity>>
        +Id : Guid
        +UserId : Guid
        +LoginProvider : string
        +TokenName : string
        +TokenValue : string
    }

    User "1" *-- "0..1" UserProfile : Profile
    User "1" *-- "0..*" UserRole : UserRoles
    User "1" *-- "0..*" UserClaim : Claims
    User "1" *-- "0..*" UserLogin : UserLogins
    User "1" *-- "0..*" UserToken : Tokens
    Role "1" *-- "0..*" UserRole : UserRoles
    Role "1" *-- "0..*" RoleClaim : Claims
```

---

### A.6 — LLM Assistant Bounded Context

```mermaid
classDiagram
    class LlmInteraction {
        <<aggregate root>>
        +Id : Guid
        +UserId : Guid
        +InteractionType : InteractionType
        +InputContext : string
        +LlmResponse : string
        +ModelUsed : string
        +TokensUsed : int
        +LatencyMs : int
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class LlmSuggestionCache {
        <<aggregate root>>
        +Id : Guid
        +EndpointId : Guid
        +SuggestionType : SuggestionType
        +CacheKey : string
        +Suggestions : string
        +ExpiresAt : DateTimeOffset
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class InteractionType {
        <<enumeration>>
        ScenarioSuggestion
        FailureExplanation
        DocumentationParsing
    }

    class SuggestionType {
        <<enumeration>>
        BoundaryCase
        NegativeCase
        HappyPath
        SecurityCase
    }

    LlmInteraction ..> InteractionType
    LlmSuggestionCache ..> SuggestionType

    note "Cross-module references:\nUserId -> Identity module\nEndpointId -> ApiDocumentation module\nGuid-only FKs, no navigation properties."
```

---

### A.7 — Notification Bounded Context

```mermaid
classDiagram
    class EmailMessageBase {
        <<abstract>>
        +Id : Guid
        +From : string
        +Tos : string
        +CCs : string
        +BCCs : string
        +Subject : string
        +Body : string
        +AttemptCount : int
        +MaxAttemptCount : int
        +NextAttemptDateTime : DateTimeOffset
        +ExpiredDateTime : DateTimeOffset
        +Log : string
        +SentDateTime : DateTimeOffset
        +CopyFromId : Guid
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class EmailMessage {
        <<aggregate root>>
    }

    class ArchivedEmailMessage {
        <<aggregate root>>
    }

    class EmailMessageAttachment {
        <<entity>>
        +Id : Guid
        +EmailMessageId : Guid
        +FileEntryId : Guid
        +Name : string
    }

    class SmsMessageBase {
        <<abstract>>
        +Id : Guid
        +Message : string
        +PhoneNumber : string
        +AttemptCount : int
        +MaxAttemptCount : int
        +NextAttemptDateTime : DateTimeOffset
        +ExpiredDateTime : DateTimeOffset
        +Log : string
        +SentDateTime : DateTimeOffset
        +CopyFromId : Guid
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class SmsMessage {
        <<aggregate root>>
    }

    class ArchivedSmsMessage {
        <<aggregate root>>
    }

    EmailMessageBase <|-- EmailMessage
    EmailMessageBase <|-- ArchivedEmailMessage
    SmsMessageBase <|-- SmsMessage
    SmsMessageBase <|-- ArchivedSmsMessage
    EmailMessage "1" *-- "0..*" EmailMessageAttachment : EmailMessageAttachments
```

---

### A.8 — Storage Bounded Context

```mermaid
classDiagram
    class FileEntry {
        <<aggregate root>>
        +Id : Guid
        +Name : string
        +Description : string
        +Size : long
        +UploadedTime : DateTimeOffset
        +FileName : string
        +FileLocation : string
        +Encrypted : bool
        +EncryptionKey : string
        +EncryptionIV : string
        +Archived : bool
        +ArchivedDate : DateTimeOffset
        +Deleted : bool
        +DeletedDate : DateTimeOffset
        +OwnerId : Guid
        +ContentType : string
        +FileCategory : FileCategory
        +ExpiresAt : DateTimeOffset
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class DeletedFileEntry {
        <<aggregate root>>
        +Id : Guid
        +FileEntryId : Guid
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class FileCategory {
        <<enumeration>>
        ApiSpec
        Report
        Export
        Attachment
    }

    FileEntry ..> FileCategory

    note "Computed aliases in code:\nFileSize -> Size\nStoragePath -> FileLocation\nIsDeleted -> Deleted\nDeletedAt -> DeletedDate"
```

---

### A.9 — Configuration & AuditLog Bounded Context

```mermaid
classDiagram
    %% Configuration Module
    class ConfigurationEntry {
        <<aggregate root>>
        +Id : Guid
        +Key : string
        +Value : string
        +Description : string
        +IsSensitive : bool
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class LocalizationEntry {
        <<aggregate root>>
        +Id : Guid
        +Name : string
        +Value : string
        +Culture : string
        +Description : string
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    %% AuditLog Module
    class AuditLogEntry {
        <<aggregate root>>
        +Id : Guid
        +UserId : Guid
        +Action : string
        +ObjectId : string
        +Log : string
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class IdempotentRequest {
        <<aggregate root>>
        +Id : Guid
        +RequestType : string
        +RequestId : string
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    note "Each module also has its own local\nAuditLogEntry entity with identical structure.\nThis is the shared AuditLog module version."
```

---

## B) APPLICATION / FEATURE CLASS DIAGRAMS

### B.1 — Feature: API Documentation Management

```mermaid
classDiagram
    %% Commands
    class AddUpdateProjectCommand {
        <<command>>
    }
    class UploadApiSpecificationCommand {
        <<command>>
    }
    class CreateManualSpecificationCommand {
        <<command>>
    }
    class AddUpdateEndpointCommand {
        <<command>>
    }
    class ActivateSpecificationCommand {
        <<command>>
    }
    class ImportCurlCommand {
        <<command>>
    }
    class ArchiveProjectCommand {
        <<command>>
    }
    class DeleteProjectCommand {
        <<command>>
    }
    class DeleteSpecificationCommand {
        <<command>>
    }
    class DeleteEndpointCommand {
        <<command>>
    }

    %% Handlers
    class AddUpdateProjectCommandHandler {
        <<handler>>
    }
    class UploadApiSpecificationCommandHandler {
        <<handler>>
    }
    class CreateManualSpecificationCommandHandler {
        <<handler>>
    }
    class AddUpdateEndpointCommandHandler {
        <<handler>>
    }
    class ActivateSpecificationCommandHandler {
        <<handler>>
    }
    class ImportCurlCommandHandler {
        <<handler>>
    }
    class ArchiveProjectCommandHandler {
        <<handler>>
    }
    class DeleteProjectCommandHandler {
        <<handler>>
    }
    class DeleteSpecificationCommandHandler {
        <<handler>>
    }
    class DeleteEndpointCommandHandler {
        <<handler>>
    }

    %% Domain Services
    class IApiEndpointMetadataService {
        <<interface>>
    }
    class ApiEndpointMetadataService {
        <<domain service>>
    }
    class IPathParameterTemplateService {
        <<interface>>
    }
    class PathParameterTemplateService {
        <<domain service>>
    }

    %% Cross-Module Contracts
    class ISubscriptionLimitGatewayService {
        <<interface>>
    }
    class IStorageFileGatewayService {
        <<interface>>
    }

    IApiEndpointMetadataService <|.. ApiEndpointMetadataService
    IPathParameterTemplateService <|.. PathParameterTemplateService

    AddUpdateProjectCommandHandler ..> AddUpdateProjectCommand : handles
    AddUpdateProjectCommandHandler ..> ISubscriptionLimitGatewayService

    UploadApiSpecificationCommandHandler ..> UploadApiSpecificationCommand : handles
    UploadApiSpecificationCommandHandler ..> IStorageFileGatewayService
    UploadApiSpecificationCommandHandler ..> ISubscriptionLimitGatewayService

    CreateManualSpecificationCommandHandler ..> CreateManualSpecificationCommand : handles
    CreateManualSpecificationCommandHandler ..> ISubscriptionLimitGatewayService
    CreateManualSpecificationCommandHandler ..> IPathParameterTemplateService

    AddUpdateEndpointCommandHandler ..> AddUpdateEndpointCommand : handles
    AddUpdateEndpointCommandHandler ..> ISubscriptionLimitGatewayService
    AddUpdateEndpointCommandHandler ..> IPathParameterTemplateService

    ActivateSpecificationCommandHandler ..> ActivateSpecificationCommand : handles

    ImportCurlCommandHandler ..> ImportCurlCommand : handles
    ImportCurlCommandHandler ..> ISubscriptionLimitGatewayService
    ImportCurlCommandHandler ..> IPathParameterTemplateService

    ArchiveProjectCommandHandler ..> ArchiveProjectCommand : handles
    DeleteProjectCommandHandler ..> DeleteProjectCommand : handles
    DeleteSpecificationCommandHandler ..> DeleteSpecificationCommand : handles
    DeleteEndpointCommandHandler ..> DeleteEndpointCommand : handles
```

---

### B.2 — Feature: Test Lifecycle Orchestration

```mermaid
classDiagram
    %% Commands
    class AddUpdateTestSuiteScopeCommand {
        <<command>>
    }
    class ProposeApiTestOrderCommand {
        <<command>>
    }
    class ApproveApiTestOrderCommand {
        <<command>>
    }
    class RejectApiTestOrderCommand {
        <<command>>
    }
    class ReorderApiTestOrderCommand {
        <<command>>
    }
    class AddUpdateExecutionEnvironmentCommand {
        <<command>>
    }
    class ArchiveTestSuiteScopeCommand {
        <<command>>
    }
    class DeleteExecutionEnvironmentCommand {
        <<command>>
    }

    %% Handlers
    class AddUpdateTestSuiteScopeCommandHandler {
        <<handler>>
    }
    class ProposeApiTestOrderCommandHandler {
        <<handler>>
    }
    class ApproveApiTestOrderCommandHandler {
        <<handler>>
    }
    class RejectApiTestOrderCommandHandler {
        <<handler>>
    }
    class ReorderApiTestOrderCommandHandler {
        <<handler>>
    }
    class AddUpdateExecutionEnvironmentCommandHandler {
        <<handler>>
    }
    class ArchiveTestSuiteScopeCommandHandler {
        <<handler>>
    }
    class DeleteExecutionEnvironmentCommandHandler {
        <<handler>>
    }

    %% Domain Services
    class IApiTestOrderService {
        <<interface>>
    }
    class ApiTestOrderService {
        <<domain service>>
    }
    class IApiTestOrderGateService {
        <<interface>>
    }
    class ApiTestOrderGateService {
        <<domain service>>
    }
    class ITestSuiteScopeService {
        <<interface>>
    }
    class TestSuiteScopeService {
        <<domain service>>
    }
    class IExecutionAuthConfigService {
        <<interface>>
    }
    class ExecutionAuthConfigService {
        <<domain service>>
    }
    class IApiEndpointMetadataService {
        <<interface>>
    }
    class IApiTestOrderAlgorithm {
        <<interface>>
    }
    class ApiTestOrderAlgorithm {
        <<domain service>>
    }

    class ApiTestOrderModelMapper {
        <<application service>>
        +ToModel(proposal, orderService)$ ApiTestOrderProposalModel
        +ParseRowVersion(base64)$ byte[]
    }

    IApiTestOrderService <|.. ApiTestOrderService
    IApiTestOrderGateService <|.. ApiTestOrderGateService
    ITestSuiteScopeService <|.. TestSuiteScopeService
    IExecutionAuthConfigService <|.. ExecutionAuthConfigService
    IApiTestOrderAlgorithm <|.. ApiTestOrderAlgorithm

    ApiTestOrderService ..> IApiEndpointMetadataService
    ApiTestOrderService ..> IApiTestOrderAlgorithm
    ApiTestOrderGateService ..> IApiTestOrderService

    AddUpdateTestSuiteScopeCommandHandler ..> AddUpdateTestSuiteScopeCommand : handles
    AddUpdateTestSuiteScopeCommandHandler ..> ITestSuiteScopeService
    AddUpdateTestSuiteScopeCommandHandler ..> IApiEndpointMetadataService

    ProposeApiTestOrderCommandHandler ..> ProposeApiTestOrderCommand : handles
    ProposeApiTestOrderCommandHandler ..> IApiTestOrderService
    ProposeApiTestOrderCommandHandler ..> ApiTestOrderModelMapper

    ApproveApiTestOrderCommandHandler ..> ApproveApiTestOrderCommand : handles
    ApproveApiTestOrderCommandHandler ..> IApiTestOrderService
    ApproveApiTestOrderCommandHandler ..> ApiTestOrderModelMapper

    RejectApiTestOrderCommandHandler ..> RejectApiTestOrderCommand : handles
    RejectApiTestOrderCommandHandler ..> IApiTestOrderService
    RejectApiTestOrderCommandHandler ..> ApiTestOrderModelMapper

    ReorderApiTestOrderCommandHandler ..> ReorderApiTestOrderCommand : handles
    ReorderApiTestOrderCommandHandler ..> IApiTestOrderService
    ReorderApiTestOrderCommandHandler ..> ApiTestOrderModelMapper

    AddUpdateExecutionEnvironmentCommandHandler ..> AddUpdateExecutionEnvironmentCommand : handles
    AddUpdateExecutionEnvironmentCommandHandler ..> IExecutionAuthConfigService

    ArchiveTestSuiteScopeCommandHandler ..> ArchiveTestSuiteScopeCommand : handles
    DeleteExecutionEnvironmentCommandHandler ..> DeleteExecutionEnvironmentCommand : handles
```

---

### B.3 — Feature: Subscription & Payment Management

```mermaid
classDiagram
    %% Commands
    class ConsumeLimitAtomicallyCommand {
        <<command>>
    }
    class UpsertUsageTrackingCommand {
        <<command>>
    }
    class CreateSubscriptionPaymentCommand {
        <<command>>
    }
    class HandlePayOsWebhookCommand {
        <<command>>
    }
    class ReconcilePayOsCheckoutsCommand {
        <<command>>
    }
    class AddUpdatePlanCommand {
        <<command>>
    }
    class DeletePlanCommand {
        <<command>>
    }
    class AddUpdateSubscriptionCommand {
        <<command>>
    }
    class CancelSubscriptionCommand {
        <<command>>
    }
    class AddPaymentTransactionCommand {
        <<command>>
    }
    class CreatePayOsCheckoutCommand {
        <<command>>
    }
    class SyncPaymentFromPayOsCommand {
        <<command>>
    }
    class PublishEventsCommand {
        <<command>>
    }

    %% Handlers
    class ConsumeLimitAtomicallyCommandHandler {
        <<handler>>
    }
    class UpsertUsageTrackingCommandHandler {
        <<handler>>
    }
    class CreateSubscriptionPaymentCommandHandler {
        <<handler>>
    }
    class HandlePayOsWebhookCommandHandler {
        <<handler>>
    }
    class ReconcilePayOsCheckoutsCommandHandler {
        <<handler>>
    }
    class AddUpdatePlanCommandHandler {
        <<handler>>
    }
    class DeletePlanCommandHandler {
        <<handler>>
    }
    class AddUpdateSubscriptionCommandHandler {
        <<handler>>
    }
    class CancelSubscriptionCommandHandler {
        <<handler>>
    }
    class AddPaymentTransactionCommandHandler {
        <<handler>>
    }
    class CreatePayOsCheckoutCommandHandler {
        <<handler>>
    }
    class SyncPaymentFromPayOsCommandHandler {
        <<handler>>
    }
    class PublishEventsCommandHandler {
        <<handler>>
    }

    %% Services
    class ISubscriptionLimitGatewayService {
        <<interface>>
    }
    class SubscriptionLimitGatewayService {
        <<application service>>
    }
    class IPayOsService {
        <<interface>>
    }
    class PayOsService {
        <<application service>>
    }

    class OutboxMessageFactory {
        <<factory>>
        +Create(eventType, triggeredById, objectId, payload)$ OutboxMessage
    }

    ISubscriptionLimitGatewayService <|.. SubscriptionLimitGatewayService
    IPayOsService <|.. PayOsService

    SubscriptionLimitGatewayService ..> ConsumeLimitAtomicallyCommand : dispatches
    SubscriptionLimitGatewayService ..> UpsertUsageTrackingCommand : dispatches

    ConsumeLimitAtomicallyCommandHandler ..> ConsumeLimitAtomicallyCommand : handles
    UpsertUsageTrackingCommandHandler ..> UpsertUsageTrackingCommand : handles
    CreateSubscriptionPaymentCommandHandler ..> CreateSubscriptionPaymentCommand : handles
    HandlePayOsWebhookCommandHandler ..> HandlePayOsWebhookCommand : handles
    HandlePayOsWebhookCommandHandler ..> IPayOsService

    ReconcilePayOsCheckoutsCommandHandler ..> ReconcilePayOsCheckoutsCommand : handles
    ReconcilePayOsCheckoutsCommandHandler ..> IPayOsService
    ReconcilePayOsCheckoutsCommandHandler ..> OutboxMessageFactory

    AddUpdatePlanCommandHandler ..> AddUpdatePlanCommand : handles
    DeletePlanCommandHandler ..> DeletePlanCommand : handles
    AddUpdateSubscriptionCommandHandler ..> AddUpdateSubscriptionCommand : handles
    CancelSubscriptionCommandHandler ..> CancelSubscriptionCommand : handles
    AddPaymentTransactionCommandHandler ..> AddPaymentTransactionCommand : handles
    CreatePayOsCheckoutCommandHandler ..> CreatePayOsCheckoutCommand : handles
    CreatePayOsCheckoutCommandHandler ..> IPayOsService
    SyncPaymentFromPayOsCommandHandler ..> SyncPaymentFromPayOsCommand : handles
    SyncPaymentFromPayOsCommandHandler ..> IPayOsService
    PublishEventsCommandHandler ..> PublishEventsCommand : handles
```

---

### B.4 — Feature: Identity & Access Management

```mermaid
classDiagram
    %% Public Service Contracts
    class IUserService {
        <<interface>>
    }
    class UserService {
        <<application service>>
    }
    class ICurrentUser {
        <<interface>>
    }
    class CurrentWebUser {
        <<application service>>
    }
    class AnonymousUser {
        <<application service>>
    }

    %% Identity Providers
    class IIdentityProvider {
        <<interface>>
    }
    class Auth0IdentityProvider {
        <<application service>>
    }
    class AzureActiveDirectoryB2CIdentityProvider {
        <<application service>>
    }

    class SyncUsersCommand {
        <<command>>
    }
    class SyncUsersCommandHandler {
        <<handler>>
    }

    IUserService <|.. UserService
    ICurrentUser <|.. CurrentWebUser
    ICurrentUser <|.. AnonymousUser

    IIdentityProvider <|.. Auth0IdentityProvider
    IIdentityProvider <|.. AzureActiveDirectoryB2CIdentityProvider

    SyncUsersCommandHandler ..> SyncUsersCommand : handles
    SyncUsersCommandHandler ..> IIdentityProvider
```

---

## C) PATTERN CLASS DIAGRAMS

### C.1 — Strategy Pattern: External Identity Providers

```mermaid
classDiagram
    class IIdentityProvider {
        <<interface>>
        +GetUsersAsync() Task~IList~IUser~~
        +GetUserById(userId) Task~IUser~
        +GetUserByUsernameAsync(username) Task~IUser~
        +CreateUserAsync(user) Task
        +UpdateUserAsync(userId, user) Task
        +DeleteUserAsync(userId) Task
    }

    class Auth0IdentityProvider {
        <<application service>>
    }
    class AzureActiveDirectoryB2CIdentityProvider {
        <<application service>>
    }
    class SyncUsersCommandHandler {
        <<handler>>
    }

    IIdentityProvider <|.. Auth0IdentityProvider
    IIdentityProvider <|.. AzureActiveDirectoryB2CIdentityProvider
    SyncUsersCommandHandler ..> IIdentityProvider : uses injected
```

---

### C.2 — Strategy Pattern: API Test Order Algorithm Pipeline

```mermaid
classDiagram
    class IApiTestOrderService {
        <<interface>>
        +BuildProposalOrderAsync() Task~IReadOnlyList~ApiOrderItemModel~~
        +ValidateReorderedEndpointSet() IReadOnlyList~Guid~
        +DeserializeOrderJson(json) IReadOnlyList~ApiOrderItemModel~
        +SerializeOrderJson(items) string
    }

    class ApiTestOrderService {
        <<domain service>>
    }

    class IApiTestOrderAlgorithm {
        <<interface>>
        +BuildProposalOrder(endpoints) IReadOnlyList~ApiOrderItemModel~
    }

    class ApiTestOrderAlgorithm {
        <<domain service>>
    }

    class ISchemaRelationshipAnalyzer {
        <<interface>>
        +BuildSchemaReferenceGraph(schemas) IReadOnlyDictionary
        +ComputeTransitiveClosure(refs) IReadOnlyDictionary
        +FindTransitiveSchemaDependencies() IReadOnlyCollection~DependencyEdge~
        +FindFuzzySchemaNameDependencies() IReadOnlyCollection~DependencyEdge~
    }

    class SchemaRelationshipAnalyzer {
        <<domain service>>
    }

    class IDependencyAwareTopologicalSorter {
        <<interface>>
        +Sort(operations, edges) IReadOnlyList~SortedOperationResult~
    }

    class DependencyAwareTopologicalSorter {
        <<domain service>>
    }

    class ISemanticTokenMatcher {
        <<interface>>
        +FindMatches(source, target, minScore) IReadOnlyCollection~TokenMatchResult~
        +Match(source, target) TokenMatchResult
        +NormalizeToken(token) string
    }

    class SemanticTokenMatcher {
        <<domain service>>
    }

    class IObservationConfirmationPromptBuilder {
        <<interface>>
        +BuildForEndpoint(context) ObservationConfirmationPrompt
        +BuildForSequence(orderedEndpoints) IReadOnlyList~ObservationConfirmationPrompt~
    }

    class ObservationConfirmationPromptBuilder {
        <<domain service>>
    }

    class IApiEndpointMetadataService {
        <<interface>>
    }

    IApiTestOrderService <|.. ApiTestOrderService
    IApiTestOrderAlgorithm <|.. ApiTestOrderAlgorithm
    ISchemaRelationshipAnalyzer <|.. SchemaRelationshipAnalyzer
    IDependencyAwareTopologicalSorter <|.. DependencyAwareTopologicalSorter
    ISemanticTokenMatcher <|.. SemanticTokenMatcher
    IObservationConfirmationPromptBuilder <|.. ObservationConfirmationPromptBuilder

    ApiTestOrderService --> IApiTestOrderAlgorithm : algorithm
    ApiTestOrderService ..> IApiEndpointMetadataService : fetches metadata
    ApiTestOrderAlgorithm --> ISchemaRelationshipAnalyzer : schemaAnalyzer
    ApiTestOrderAlgorithm --> IDependencyAwareTopologicalSorter : topologicalSorter
    ApiTestOrderAlgorithm --> ISemanticTokenMatcher : tokenMatcher
```

---

### C.3 — Factory Pattern: Subscription Outbox Message Creation

```mermaid
classDiagram
    class OutboxMessageFactory {
        <<factory>>
        +Create(eventType, triggeredById, objectId, payload)$ OutboxMessage
    }

    class OutboxMessage {
        <<aggregate root>>
        +Id : Guid
        +EventType : string
        +TriggeredById : Guid
        +ObjectId : string
        +Payload : string
        +Published : bool
        +ActivityId : string
    }

    class SubscriptionOutboxEventBase {
        <<abstract>>
    }

    class PaymentIntentStatusChangedOutboxEvent {
    }

    class PaymentCheckoutLinkCreatedOutboxEvent {
    }

    class ReconcilePayOsCheckoutsCommandHandler {
        <<handler>>
    }

    SubscriptionOutboxEventBase <|-- PaymentIntentStatusChangedOutboxEvent
    SubscriptionOutboxEventBase <|-- PaymentCheckoutLinkCreatedOutboxEvent

    ReconcilePayOsCheckoutsCommandHandler ..> OutboxMessageFactory : Create
    OutboxMessageFactory ..> OutboxMessage : creates
    OutboxMessageFactory ..> SubscriptionOutboxEventBase : serializes payload
```

---

### C.4 — Observer Pattern: Domain Events and Handlers

```mermaid
classDiagram
    class IDomainEvent {
        <<interface>>
    }

    class EntityCreatedEvent~T~ {
        <<event>>
    }
    class EntityUpdatedEvent~T~ {
        <<event>>
    }
    class EntityDeletedEvent~T~ {
        <<event>>
    }

    class IDomainEventHandler~T~ {
        <<interface>>
    }

    class Dispatcher {
        +DispatchAsync(event) Task
    }

    class ProjectCreatedEventHandler {
        <<handler>>
    }
    class ProjectUpdatedEventHandler {
        <<handler>>
    }
    class ProjectDeletedEventHandler {
        <<handler>>
    }
    class SpecCreatedEventHandler {
        <<handler>>
    }
    class SpecUpdatedEventHandler {
        <<handler>>
    }
    class SpecDeletedEventHandler {
        <<handler>>
    }

    IDomainEvent <|.. EntityCreatedEvent~T~
    IDomainEvent <|.. EntityUpdatedEvent~T~
    IDomainEvent <|.. EntityDeletedEvent~T~

    IDomainEventHandler~T~ <|.. ProjectCreatedEventHandler
    IDomainEventHandler~T~ <|.. ProjectUpdatedEventHandler
    IDomainEventHandler~T~ <|.. ProjectDeletedEventHandler
    IDomainEventHandler~T~ <|.. SpecCreatedEventHandler
    IDomainEventHandler~T~ <|.. SpecUpdatedEventHandler
    IDomainEventHandler~T~ <|.. SpecDeletedEventHandler

    Dispatcher ..> IDomainEvent : dispatches
    Dispatcher ..> IDomainEventHandler~T~ : resolves and invokes
```

---

### C.5 — Decorator Pattern: Command and Query Handler Pipeline

```mermaid
classDiagram
    class ICommandHandler~T~ {
        <<interface>>
        +HandleAsync(command) Task
    }

    class IQueryHandler~TQuery_TResult~ {
        <<interface>>
        +HandleAsync(query) Task~TResult~
    }

    class DatabaseRetryDecoratorBase {
        <<abstract>>
    }

    class AuditLogCommandDecorator~T~ {
        <<application service>>
        -inner : ICommandHandler~T~
    }

    class DatabaseRetryCommandDecorator~T~ {
        <<application service>>
        -inner : ICommandHandler~T~
    }

    class AuditLogQueryDecorator~TQuery_TResult~ {
        <<application service>>
        -inner : IQueryHandler~TQuery_TResult~
    }

    class DatabaseRetryQueryDecorator~TQuery_TResult~ {
        <<application service>>
        -inner : IQueryHandler~TQuery_TResult~
    }

    class HandlerFactory {
        +CreateHandler~T~() ICommandHandler~T~
    }

    class MappingAttribute {
    }

    ICommandHandler~T~ <|.. AuditLogCommandDecorator~T~
    ICommandHandler~T~ <|.. DatabaseRetryCommandDecorator~T~
    IQueryHandler~TQuery_TResult~ <|.. AuditLogQueryDecorator~TQuery_TResult~
    IQueryHandler~TQuery_TResult~ <|.. DatabaseRetryQueryDecorator~TQuery_TResult~

    DatabaseRetryDecoratorBase <|-- DatabaseRetryCommandDecorator~T~
    DatabaseRetryDecoratorBase <|-- DatabaseRetryQueryDecorator~TQuery_TResult~

    AuditLogCommandDecorator~T~ --> ICommandHandler~T~ : inner wraps
    DatabaseRetryCommandDecorator~T~ --> ICommandHandler~T~ : inner wraps
    AuditLogQueryDecorator~TQuery_TResult~ --> IQueryHandler~TQuery_TResult~ : inner wraps
    DatabaseRetryQueryDecorator~TQuery_TResult~ --> IQueryHandler~TQuery_TResult~ : inner wraps

    HandlerFactory ..> MappingAttribute : reads
    HandlerFactory ..> AuditLogCommandDecorator~T~ : composes
    HandlerFactory ..> DatabaseRetryCommandDecorator~T~ : composes
```

---

## D) PLANNED FEATURE CLASS DIAGRAMS

### D.1 — Feature: Happy-Path Test Case Generation (FE-05B)

```mermaid
classDiagram
    %% Commands
    class GenerateHappyPathTestCasesCommand {
        <<planned command>>
        +TestSuiteId : Guid
        +UserId : Guid
    }

    %% Handlers
    class GenerateHappyPathTestCasesCommandHandler {
        <<planned handler>>
    }

    %% Domain Services Existing
    class IApiTestOrderGateService {
        <<interface>>
    }
    class IApiEndpointMetadataService {
        <<interface>>
    }

    %% Domain Services Planned
    class IHappyPathTestCaseGenerator {
        <<interface>>
        +GenerateAsync(suiteId, orderedEndpoints, metadata, ct) Task~IReadOnlyList~GeneratedTestCase~~
    }
    class HappyPathTestCaseGenerator {
        <<planned domain service>>
    }

    class ITestCaseRequestBuilder {
        <<interface>>
        +Build(endpoint, metadata) TestCaseRequestModel
    }
    class TestCaseRequestBuilder {
        <<planned domain service>>
    }

    class ITestCaseExpectationBuilder {
        <<interface>>
        +Build(endpoint, metadata) TestCaseExpectationModel
    }
    class TestCaseExpectationBuilder {
        <<planned domain service>>
    }

    %% Cross-Module Contracts
    class ISubscriptionLimitGatewayService {
        <<interface>>
    }

    IHappyPathTestCaseGenerator <|.. HappyPathTestCaseGenerator
    ITestCaseRequestBuilder <|.. TestCaseRequestBuilder
    ITestCaseExpectationBuilder <|.. TestCaseExpectationBuilder

    GenerateHappyPathTestCasesCommandHandler ..> GenerateHappyPathTestCasesCommand : handles
    GenerateHappyPathTestCasesCommandHandler ..> IApiTestOrderGateService : requires approved order
    GenerateHappyPathTestCasesCommandHandler ..> IHappyPathTestCaseGenerator
    GenerateHappyPathTestCasesCommandHandler ..> ISubscriptionLimitGatewayService : checks limits

    HappyPathTestCaseGenerator ..> IApiEndpointMetadataService : fetches endpoint details
    HappyPathTestCaseGenerator --> ITestCaseRequestBuilder : requestBuilder
    HappyPathTestCaseGenerator --> ITestCaseExpectationBuilder : expectationBuilder
```

---

### D.2 — Feature: Boundary & Negative Test Case Generation (FE-06)

```mermaid
classDiagram
    %% Commands
    class GenerateBoundaryNegativeTestCasesCommand {
        <<planned command>>
        +TestSuiteId : Guid
        +UserId : Guid
        +IncludePathMutations : bool
        +IncludeBodyMutations : bool
        +IncludeLlmSuggestions : bool
    }

    %% Handlers
    class GenerateBoundaryNegativeTestCasesCommandHandler {
        <<planned handler>>
    }

    %% Domain Services Existing
    class IPathParameterTemplateService {
        <<interface>>
    }
    class IObservationConfirmationPromptBuilder {
        <<interface>>
    }
    class IApiTestOrderGateService {
        <<interface>>
    }

    %% Domain Services Planned
    class IBodyMutationEngine {
        <<interface>>
        +GenerateBodyMutations(schema, body) IReadOnlyList~BodyMutation~
    }
    class BodyMutationEngine {
        <<planned domain service>>
    }

    class ILlmScenarioSuggester {
        <<interface>>
        +SuggestScenariosAsync(prompt, ct) Task~IReadOnlyList~LlmScenarioSuggestion~~
    }
    class LlmScenarioSuggester {
        <<planned domain service>>
    }

    class IBoundaryNegativeTestCaseGenerator {
        <<interface>>
        +GenerateAsync(suiteId, happyPathCases, metadata, options, ct) Task~IReadOnlyList~GeneratedTestCase~~
    }
    class BoundaryNegativeTestCaseGenerator {
        <<planned domain service>>
    }

    IBodyMutationEngine <|.. BodyMutationEngine
    ILlmScenarioSuggester <|.. LlmScenarioSuggester
    IBoundaryNegativeTestCaseGenerator <|.. BoundaryNegativeTestCaseGenerator

    GenerateBoundaryNegativeTestCasesCommandHandler ..> GenerateBoundaryNegativeTestCasesCommand : handles
    GenerateBoundaryNegativeTestCasesCommandHandler ..> IApiTestOrderGateService : requires approved order
    GenerateBoundaryNegativeTestCasesCommandHandler ..> IBoundaryNegativeTestCaseGenerator

    BoundaryNegativeTestCaseGenerator --> IBodyMutationEngine : bodyMutator
    BoundaryNegativeTestCaseGenerator --> ILlmScenarioSuggester : llmSuggester
    BoundaryNegativeTestCaseGenerator ..> IPathParameterTemplateService : path mutations
    BoundaryNegativeTestCaseGenerator ..> IObservationConfirmationPromptBuilder : builds prompts
```

---

### D.3 — Feature: Test Execution Engine (FE-07 + FE-08)

```mermaid
classDiagram
    %% Commands
    class StartTestRunCommand {
        <<planned command>>
        +TestSuiteId : Guid
        +EnvironmentId : Guid
        +TriggeredById : Guid
    }

    %% Handlers
    class StartTestRunCommandHandler {
        <<planned handler>>
    }

    %% Services Planned
    class ITestExecutionOrchestrator {
        <<interface>>
        +ExecuteAsync(testRunId, ct) Task~TestRunResult~
    }
    class TestExecutionOrchestrator {
        <<planned domain service>>
    }

    class IHttpTestExecutor {
        <<interface>>
        +ExecuteAsync(request, environment, variables, ct) Task~HttpTestResponse~
    }
    class HttpTestExecutor {
        <<planned domain service>>
    }

    class IVariableExtractor {
        <<interface>>
        +Extract(response, variables) IReadOnlyDictionary~string_string~
    }
    class VariableExtractor {
        <<planned domain service>>
    }

    class IVariableResolver {
        <<interface>>
        +Resolve(request, currentVariables) ResolvedTestCaseRequest
    }
    class VariableResolver {
        <<planned domain service>>
    }

    class IRuleBasedValidator {
        <<interface>>
        +Validate(response, expectation) TestCaseValidationResult
    }
    class RuleBasedValidator {
        <<planned domain service>>
    }

    class ITestResultCollector {
        <<interface>>
        +CollectAsync(testRunId, results, ct) Task
    }
    class TestResultCollector {
        <<planned domain service>>
    }

    %% Services Existing
    class IApiTestOrderGateService {
        <<interface>>
    }
    class IExecutionAuthConfigService {
        <<interface>>
    }

    ITestExecutionOrchestrator <|.. TestExecutionOrchestrator
    IHttpTestExecutor <|.. HttpTestExecutor
    IVariableExtractor <|.. VariableExtractor
    IVariableResolver <|.. VariableResolver
    IRuleBasedValidator <|.. RuleBasedValidator
    ITestResultCollector <|.. TestResultCollector

    StartTestRunCommandHandler ..> StartTestRunCommand : handles
    StartTestRunCommandHandler ..> ITestExecutionOrchestrator

    TestExecutionOrchestrator ..> IApiTestOrderGateService : loads approved order
    TestExecutionOrchestrator --> IHttpTestExecutor : executor
    TestExecutionOrchestrator --> IVariableExtractor : extractor
    TestExecutionOrchestrator --> IVariableResolver : resolver
    TestExecutionOrchestrator --> IRuleBasedValidator : validator
    TestExecutionOrchestrator --> ITestResultCollector : collector
    TestExecutionOrchestrator ..> IExecutionAuthConfigService : resolves auth
```

---

### D.4 — Feature: LLM Failure Explanations (FE-09)

```mermaid
classDiagram
    %% Commands
    class ExplainTestFailureCommand {
        <<planned command>>
        +TestRunId : Guid
        +TestCaseId : Guid
        +UserId : Guid
    }

    %% Handlers
    class ExplainTestFailureCommandHandler {
        <<planned handler>>
    }

    %% Services Planned
    class ILlmFailureExplainer {
        <<interface>>
        +ExplainAsync(failedResult, endpointContext, ct) Task~FailureExplanation~
    }
    class LlmFailureExplainer {
        <<planned domain service>>
    }

    class ILlmClient {
        <<interface>>
        +CompleteAsync(prompt, systemPrompt, model, ct) Task~LlmCompletionResult~
    }
    class OpenAiLlmClient {
        <<planned application service>>
    }

    %% Domain Entities Existing
    class LlmInteraction {
        <<aggregate root>>
    }
    class LlmSuggestionCache {
        <<aggregate root>>
    }

    ILlmFailureExplainer <|.. LlmFailureExplainer
    ILlmClient <|.. OpenAiLlmClient

    ExplainTestFailureCommandHandler ..> ExplainTestFailureCommand : handles
    ExplainTestFailureCommandHandler ..> ILlmFailureExplainer

    LlmFailureExplainer --> ILlmClient : llmClient
    LlmFailureExplainer ..> LlmInteraction : creates interaction record
    LlmFailureExplainer ..> LlmSuggestionCache : checks and stores cache
```

---

### D.5 — Feature: Test Reporting & Export (FE-10)

```mermaid
classDiagram
    %% Commands
    class GenerateTestReportCommand {
        <<planned command>>
        +TestRunId : Guid
        +ReportType : ReportType
        +Format : ReportFormat
        +GeneratedById : Guid
    }

    %% Handlers
    class GenerateTestReportCommandHandler {
        <<planned handler>>
    }

    %% Services Planned
    class ITestReportGenerator {
        <<interface>>
        +GenerateAsync(testRunId, reportType, format, ct) Task~Guid~
    }
    class TestReportGenerator {
        <<planned domain service>>
    }

    class ICoverageCalculator {
        <<interface>>
        +CalculateAsync(testRunId, ct) Task~CoverageMetricResult~
    }
    class CoverageCalculator {
        <<planned domain service>>
    }

    class IReportRenderer {
        <<interface>>
        +RenderAsync(data, format, ct) Task~Stream~
    }
    class PdfReportRenderer {
        <<planned application service>>
    }
    class CsvReportRenderer {
        <<planned application service>>
    }
    class JsonReportRenderer {
        <<planned application service>>
    }
    class HtmlReportRenderer {
        <<planned application service>>
    }

    %% Cross-Module Contracts
    class IStorageFileGatewayService {
        <<interface>>
    }

    ITestReportGenerator <|.. TestReportGenerator
    ICoverageCalculator <|.. CoverageCalculator
    IReportRenderer <|.. PdfReportRenderer
    IReportRenderer <|.. CsvReportRenderer
    IReportRenderer <|.. JsonReportRenderer
    IReportRenderer <|.. HtmlReportRenderer

    GenerateTestReportCommandHandler ..> GenerateTestReportCommand : handles
    GenerateTestReportCommandHandler ..> ITestReportGenerator

    TestReportGenerator --> ICoverageCalculator : coverageCalc
    TestReportGenerator --> IReportRenderer : renderer strategy
    TestReportGenerator ..> IStorageFileGatewayService : uploads report file
```

---

### D.6 — Feature: LLM Suggestion Review Pipeline (FE-15 / FE-16 / FE-17)

```mermaid
classDiagram
    %% Planned Entities
    class LlmSuggestion {
        <<planned entity>>
        +Id : Guid
        +TestSuiteId : Guid
        +EndpointId : Guid
        +SuggestionType : SuggestionType
        +TestType : TestType
        +SuggestedName : string
        +SuggestedDescription : string
        +SuggestedRequest : string
        +SuggestedExpectation : string
        +Confidence : double
        +ReviewStatus : ReviewStatus
        +ReviewedById : Guid
        +ReviewedAt : DateTimeOffset
        +ReviewNotes : string
        +ModifiedContent : string
        +LlmInteractionId : Guid
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class UserFeedback {
        <<planned entity>>
        +Id : Guid
        +SuggestionId : Guid
        +UserId : Guid
        +Rating : int
        +Comment : string
        +CreatedDateTime : DateTimeOffset
    }

    %% Commands Planned
    class ReviewLlmSuggestionCommand {
        <<planned command>>
        +SuggestionId : Guid
        +Action : ReviewAction
        +ModifiedContent : string
        +ReviewNotes : string
        +UserId : Guid
    }

    class BulkReviewLlmSuggestionsCommand {
        <<planned command>>
        +TestSuiteId : Guid
        +Action : ReviewAction
        +FilterByType : SuggestionType
        +FilterByConfidence : double
        +UserId : Guid
    }

    class SubmitSuggestionFeedbackCommand {
        <<planned command>>
        +SuggestionId : Guid
        +UserId : Guid
        +Rating : int
        +Comment : string
    }

    %% Handlers Planned
    class ReviewLlmSuggestionCommandHandler {
        <<planned handler>>
    }
    class BulkReviewLlmSuggestionsCommandHandler {
        <<planned handler>>
    }
    class SubmitSuggestionFeedbackCommandHandler {
        <<planned handler>>
    }

    %% Services Planned
    class ILlmSuggestionReviewService {
        <<interface>>
        +ApproveAsync(suggestion, userId, ct) Task
        +RejectAsync(suggestion, userId, notes, ct) Task
        +ModifyAndApproveAsync(suggestion, modified, userId, ct) Task
        +MaterializeApprovedAsync(suiteId, ct) Task~int~
    }
    class LlmSuggestionReviewService {
        <<planned domain service>>
    }

    class IUserFeedbackService {
        <<interface>>
        +SubmitFeedbackAsync(suggestionId, userId, rating, comment, ct) Task
    }
    class UserFeedbackService {
        <<planned domain service>>
    }

    ILlmSuggestionReviewService <|.. LlmSuggestionReviewService
    IUserFeedbackService <|.. UserFeedbackService

    ReviewLlmSuggestionCommandHandler ..> ReviewLlmSuggestionCommand : handles
    ReviewLlmSuggestionCommandHandler ..> ILlmSuggestionReviewService

    BulkReviewLlmSuggestionsCommandHandler ..> BulkReviewLlmSuggestionsCommand : handles
    BulkReviewLlmSuggestionsCommandHandler ..> ILlmSuggestionReviewService

    SubmitSuggestionFeedbackCommandHandler ..> SubmitSuggestionFeedbackCommand : handles
    SubmitSuggestionFeedbackCommandHandler ..> IUserFeedbackService

    LlmSuggestion "1" *-- "0..*" UserFeedback : Feedbacks

    note "ReviewStatus enum planned:\nPending, Approved, Rejected,\nModifiedAndApproved, Expired"
```

---

### D.7 — Feature: Data-Set Driven Parameterized Execution (FE-05B Extension)

```mermaid
classDiagram
    %% Commands
    class ManageTestDataSetsCommand {
        <<planned command>>
        +TestCaseId : Guid
        +DataSets : List~TestDataSetDto~
        +UserId : Guid
    }

    %% Handlers
    class ManageTestDataSetsCommandHandler {
        <<planned handler>>
    }

    %% Services Planned
    class ITestDataSetResolver {
        <<interface>>
        +ExpandAsync(testCase, dataSets, ct) Task~IReadOnlyList~ExpandedTestCaseRequest~~
    }
    class TestDataSetResolver {
        <<planned domain service>>
    }

    class IDataSetPlaceholderEngine {
        <<interface>>
        +Resolve(template, dataRow) string
        +FindPlaceholders(template) IReadOnlyList~string~
        +ValidateDataSetSchema(placeholders, dataSet) ValidationResult
    }
    class DataSetPlaceholderEngine {
        <<planned domain service>>
    }

    ITestDataSetResolver <|.. TestDataSetResolver
    IDataSetPlaceholderEngine <|.. DataSetPlaceholderEngine

    ManageTestDataSetsCommandHandler ..> ManageTestDataSetsCommand : handles
    TestDataSetResolver --> IDataSetPlaceholderEngine : placeholderEngine
```

---

### D.8 — Feature: Real-Time Test Execution Monitoring (FE-07 Extension)

```mermaid
classDiagram
    %% SignalR Hub Planned
    class ITestExecutionHubClient {
        <<interface>>
        +ReceiveTestProgress(message) Task
        +ReceiveTestRunCompleted(summary) Task
    }
    class TestExecutionHub {
        <<planned application service>>
    }

    %% Services Planned
    class ITestProgressPublisher {
        <<interface>>
        +PublishProgressAsync(testRunId, testCaseId, status, durationMs, error, ct) Task
        +PublishCompletedAsync(testRunId, summary, ct) Task
    }
    class SignalRTestProgressPublisher {
        <<planned application service>>
    }

    class ITestRunSubscriptionManager {
        <<interface>>
        +SubscribeAsync(connectionId, testRunId, ct) Task
        +UnsubscribeAsync(connectionId, testRunId, ct) Task
    }
    class RedisTestRunSubscriptionManager {
        <<planned application service>>
    }

    %% DTOs Planned
    class TestProgressMessage {
        <<planned dto>>
        +TestRunId : Guid
        +TestCaseId : Guid
        +TestCaseName : string
        +Status : TestCaseExecutionStatus
        +DurationMs : long
        +ErrorSummary : string
        +ProgressPercent : double
        +CompletedCount : int
        +TotalCount : int
    }

    class TestRunSummaryMessage {
        <<planned dto>>
        +TestRunId : Guid
        +FinalStatus : TestRunStatus
        +TotalDurationMs : long
        +PassedCount : int
        +FailedCount : int
        +SkippedCount : int
    }

    ITestProgressPublisher <|.. SignalRTestProgressPublisher
    ITestRunSubscriptionManager <|.. RedisTestRunSubscriptionManager

    TestExecutionHub ..> ITestRunSubscriptionManager
    SignalRTestProgressPublisher ..> ITestExecutionHubClient : pushes to clients

    note "Integration with D.3:\nTestExecutionOrchestrator calls\nITestProgressPublisher after each\ntest case execution completes."
```

---

### D.9 — Feature: CI/CD Webhook Integration (Post-FE-10)

```mermaid
classDiagram
    %% Planned Entities
    class WebhookRegistration {
        <<planned aggregate root>>
        +Id : Guid
        +ProjectId : Guid
        +Name : string
        +CallbackUrl : string
        +Secret : string
        +EventFilter : string
        +IsActive : bool
        +LastTriggeredAt : DateTimeOffset
        +CreatedById : Guid
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class WebhookDeliveryLog {
        <<planned entity>>
        +Id : Guid
        +WebhookRegistrationId : Guid
        +EventType : string
        +Payload : string
        +ResponseStatusCode : int
        +ResponseBody : string
        +DeliveredAt : DateTimeOffset
        +DurationMs : int
        +Success : bool
        +RetryCount : int
    }

    %% Commands Planned
    class RegisterWebhookCommand {
        <<planned command>>
        +ProjectId : Guid
        +Name : string
        +CallbackUrl : string
        +EventFilter : string
        +UserId : Guid
    }

    class TriggerTestRunViaWebhookCommand {
        <<planned command>>
        +ProjectId : Guid
        +TestSuiteId : Guid
        +EnvironmentId : Guid
        +WebhookPayload : string
        +Signature : string
    }

    %% Handlers Planned
    class RegisterWebhookCommandHandler {
        <<planned handler>>
    }
    class TriggerTestRunViaWebhookCommandHandler {
        <<planned handler>>
    }

    %% Services Planned
    class IWebhookTriggerService {
        <<interface>>
        +ValidateSignatureAsync(payload, signature, secret, ct) Task~bool~
        +TriggerAsync(projectId, suiteId, environmentId, ct) Task~Guid~
    }
    class WebhookTriggerService {
        <<planned domain service>>
    }

    class IWebhookResultNotifier {
        <<interface>>
        +NotifyAsync(webhookRegistration, testRunResult, ct) Task~WebhookDeliveryLog~
    }
    class WebhookResultNotifier {
        <<planned domain service>>
    }

    IWebhookTriggerService <|.. WebhookTriggerService
    IWebhookResultNotifier <|.. WebhookResultNotifier

    RegisterWebhookCommandHandler ..> RegisterWebhookCommand : handles
    TriggerTestRunViaWebhookCommandHandler ..> TriggerTestRunViaWebhookCommand : handles
    TriggerTestRunViaWebhookCommandHandler ..> IWebhookTriggerService

    WebhookRegistration "1" *-- "0..*" WebhookDeliveryLog : DeliveryLogs
    WebhookResultNotifier ..> WebhookDeliveryLog : creates delivery record
```

---

### D.10 — Feature: Test Suite Comparison & Regression Detection (Post-FE-10)

```mermaid
classDiagram
    %% Planned Entities
    class ComparisonReport {
        <<planned aggregate root>>
        +Id : Guid
        +TestSuiteId : Guid
        +BaselineRunId : Guid
        +ComparedRunId : Guid
        +NewlyFailing : string
        +NewlyPassing : string
        +ConsistentlyFailing : string
        +FlakyTestCandidates : string
        +PerformanceDelta : string
        +Summary : string
        +GeneratedById : Guid
        +CreatedDateTime : DateTimeOffset
        +UpdatedDateTime : DateTimeOffset
    }

    class RegressionAlert {
        <<planned entity>>
        +Id : Guid
        +ComparisonReportId : Guid
        +TestCaseId : Guid
        +AlertType : RegressionAlertType
        +Severity : AlertSeverity
        +Description : string
        +PreviousStatus : string
        +CurrentStatus : string
        +PerformanceDeltaMs : long
        +AcknowledgedById : Guid
        +AcknowledgedAt : DateTimeOffset
    }

    %% Commands Planned
    class CompareTestRunsCommand {
        <<planned command>>
        +TestSuiteId : Guid
        +BaselineRunId : Guid
        +ComparedRunId : Guid
        +GeneratedById : Guid
    }

    class DetectRegressionsCommand {
        <<planned command>>
        +TestSuiteId : Guid
        +RecentRunCount : int
        +UserId : Guid
    }

    %% Handlers Planned
    class CompareTestRunsCommandHandler {
        <<planned handler>>
    }
    class DetectRegressionsCommandHandler {
        <<planned handler>>
    }

    %% Services Planned
    class ITestRunComparator {
        <<interface>>
        +CompareAsync(baselineRunId, comparedRunId, ct) Task~ComparisonResult~
    }
    class TestRunComparator {
        <<planned domain service>>
    }

    class IRegressionDetector {
        <<interface>>
        +DetectAsync(suiteId, recentRunCount, ct) Task~RegressionAnalysisResult~
        +IdentifyFlakyTests(suiteId, runCount, failureThreshold, ct) Task~IReadOnlyList~FlakyTestResult~~
    }
    class RegressionDetector {
        <<planned domain service>>
    }

    class IPerformanceTrendAnalyzer {
        <<interface>>
        +AnalyzeTrendsAsync(suiteId, runCount, ct) Task~IReadOnlyList~PerformanceTrendResult~~
        +DetectDegradationAsync(suiteId, threshold, ct) Task~IReadOnlyList~DegradationAlert~~
    }
    class PerformanceTrendAnalyzer {
        <<planned domain service>>
    }

    ITestRunComparator <|.. TestRunComparator
    IRegressionDetector <|.. RegressionDetector
    IPerformanceTrendAnalyzer <|.. PerformanceTrendAnalyzer

    CompareTestRunsCommandHandler ..> CompareTestRunsCommand : handles
    CompareTestRunsCommandHandler ..> ITestRunComparator

    DetectRegressionsCommandHandler ..> DetectRegressionsCommand : handles
    DetectRegressionsCommandHandler ..> IRegressionDetector
    DetectRegressionsCommandHandler ..> IPerformanceTrendAnalyzer

    ComparisonReport "1" *-- "0..*" RegressionAlert : Alerts
```
