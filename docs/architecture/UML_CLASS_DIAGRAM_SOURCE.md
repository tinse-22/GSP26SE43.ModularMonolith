# UML Class Diagram Source (Modular Strategy)

## Static Analysis Method
- Scope scanned: all `ClassifiedAds.*` C# projects, with focus on `ClassifiedAds.Modules.*`, `ClassifiedAds.Domain`, `ClassifiedAds.Application`, and `ClassifiedAds.Contracts`.
- Filtering applied:
  - Excluded DTOs, mapper classes, configuration classes, helper/utility classes, constants-only classes, enums, generated files, tests, docs, scripts, and build artifacts (`bin/obj`).
  - Retained core entities, aggregate roots, key value objects, core services, public-facing interfaces, command handlers, and pattern participants.
- Bounded-context inference approach (automatic):
  1. Namespace clustering from `ClassifiedAds.Modules.<Context>`.
  2. Structural dependency extraction from `*.csproj` references.
  3. Cross-context coupling extraction from `using ClassifiedAds.Contracts.*` imports.
  4. Context grouping by namespace affinity + shared contract dependency overlap.

## Inferred Bounded Contexts (Namespace + Structural Coupling)
- `API Spec & Test Design`: `ApiDocumentation`, `TestGeneration`.
- `Identity & Collaboration`: `Identity`, `AuditLog`, `Notification`.
- `Monetization & Assets`: `Subscription`, `Storage`.
- `Execution & Reporting`: `TestExecution`, `TestReporting`.
- `Support Contexts`: `Configuration`, `LlmAssistant`.

---

## STEP 1 - High-Level Architecture Diagram

### Diagram: Layered Modular Monolith and Subsystems
- Scope: Layer-level architecture, dependency direction, and major subsystems.
- Included modules/components:
  - Hosts: `WebAPI`, `Background`, `AppHost`, `Migrator`
  - Core layers: `Application`, `Domain`, `Infrastructure`, `Persistence.PostgreSQL`, `Contracts`, `CrossCuttingConcerns`
  - Subsystems: `ApiDocumentation`, `TestGeneration`, `TestExecution`, `TestReporting`, `Identity`, `AuditLog`, `Notification`, `Subscription`, `Storage`, `Configuration`, `LlmAssistant`

```plantuml
@startuml
left to right direction
skinparam packageStyle rectangle

package "Hosts / Presentation" {
  [WebAPI]
  [Background]
  [AppHost]
  [Migrator]
}

package "Core Layers" {
  [Application]
  [Domain]
  [Infrastructure]
  [Persistence.PostgreSQL]
  [Contracts]
  [CrossCuttingConcerns]
}

package "Subsystem: API & Testing" {
  [ApiDocumentation]
  [TestGeneration]
  [TestExecution]
  [TestReporting]
}

package "Subsystem: Identity & Collaboration" {
  [Identity]
  [AuditLog]
  [Notification]
}

package "Subsystem: Monetization & Assets" {
  [Subscription]
  [Storage]
}

package "Subsystem: Support" {
  [Configuration]
  [LlmAssistant]
}

[WebAPI] --> [ApiDocumentation]
[WebAPI] --> [Identity]
[WebAPI] --> [Subscription]
[Background] --> [Identity]
[Background] --> [Storage]
[Migrator] --> [ApiDocumentation]
[Migrator] --> [Subscription]
[Migrator] --> [TestGeneration]

[ApiDocumentation] --> [Application]
[TestGeneration] --> [Application]
[TestExecution] --> [Application]
[Identity] --> [Application]
[Subscription] --> [Application]
[Storage] --> [Application]

[ApiDocumentation] --> [Domain]
[TestGeneration] --> [Domain]
[TestExecution] --> [Domain]
[TestReporting] --> [Domain]
[Identity] --> [Domain]
[Subscription] --> [Domain]
[Storage] --> [Domain]

[ApiDocumentation] --> [Infrastructure]
[TestGeneration] --> [Infrastructure]
[Identity] --> [Infrastructure]
[Subscription] --> [Infrastructure]
[Storage] --> [Infrastructure]

[ApiDocumentation] --> [Persistence.PostgreSQL]
[TestGeneration] --> [Persistence.PostgreSQL]
[TestExecution] --> [Persistence.PostgreSQL]
[TestReporting] --> [Persistence.PostgreSQL]
[Identity] --> [Persistence.PostgreSQL]
[Subscription] --> [Persistence.PostgreSQL]
[Storage] --> [Persistence.PostgreSQL]

[Infrastructure] --> [Application]
[Infrastructure] --> [Domain]
[Domain] --> [CrossCuttingConcerns]

[ApiDocumentation] ..> [Subscription] : via Contracts.Subscription
[ApiDocumentation] ..> [Storage] : via Contracts.Storage
[TestGeneration] ..> [ApiDocumentation] : via Contracts.ApiDocumentation
[Subscription] ..> [Identity] : via Contracts.Identity
[Storage] ..> [Identity] : via Contracts.Identity
[AuditLog] ..> [Identity] : via Contracts.Identity
[Identity] ..> [Notification] : via Contracts.Notification
@enduml
```

---

## STEP 2 - Domain Core Diagram

### Diagram: API Quality Lifecycle Core Domain
- Scope: Core business entities, aggregates, cross-module domain services, and key relationships.
- Included classes (30):
  - `Project`, `ApiSpecification`, `ApiEndpoint`, `EndpointParameter`, `EndpointResponse`, `SecurityScheme`
  - `TestSuite`, `TestCase`, `TestCaseRequest`, `TestCaseExpectation`, `TestCaseVariable`, `TestDataSet`, `TestOrderProposal`, `TestSuiteVersion`
  - `ExecutionEnvironment`, `TestRun`, `TestReport`, `CoverageMetric`
  - `SubscriptionPlan`, `PlanLimit`, `UserSubscription`, `UsageTracking`
  - `IApiEndpointMetadataService`, `ApiEndpointMetadataService`
  - `IApiTestOrderService`, `ApiTestOrderService`
  - `IApiTestOrderGateService`, `ApiTestOrderGateService`
  - `ISubscriptionLimitGatewayService`, `SubscriptionLimitGatewayService`

```plantuml
@startuml
left to right direction
skinparam classAttributeIconSize 0

package "Api Documentation" {
  class Project
  class ApiSpecification
  class ApiEndpoint
  class EndpointParameter
  class EndpointResponse
  class SecurityScheme
  interface IApiEndpointMetadataService
  class ApiEndpointMetadataService
}

package "Test Design and Execution" {
  class TestSuite
  class TestCase
  class TestCaseRequest
  class TestCaseExpectation
  class TestCaseVariable
  class TestDataSet
  class TestOrderProposal
  class TestSuiteVersion
  class ExecutionEnvironment
  class TestRun
  class TestReport
  class CoverageMetric
  interface IApiTestOrderService
  class ApiTestOrderService
  interface IApiTestOrderGateService
  class ApiTestOrderGateService
}

package "Subscription" {
  class SubscriptionPlan
  class PlanLimit
  class UserSubscription
  class UsageTracking
  interface ISubscriptionLimitGatewayService
  class SubscriptionLimitGatewayService
}

Project "1" o-- "0..*" ApiSpecification
Project "0..1" --> ApiSpecification : ActiveSpec
ApiSpecification "1" *-- "0..*" ApiEndpoint
ApiSpecification "1" *-- "0..*" SecurityScheme
ApiEndpoint "1" *-- "0..*" EndpointParameter
ApiEndpoint "1" *-- "0..*" EndpointResponse

TestSuite ..> Project : ProjectId
TestSuite ..> ApiSpecification : ApiSpecId
TestSuite "1" *-- "0..*" TestCase
TestSuite "1" *-- "0..*" TestSuiteVersion
TestSuite "1" *-- "0..*" TestOrderProposal
TestCase "1" *-- "0..1" TestCaseRequest
TestCase "1" *-- "0..1" TestCaseExpectation
TestCase "1" *-- "0..*" TestCaseVariable
TestCase "1" *-- "0..*" TestDataSet
TestCase "0..*" --> "0..1" TestCase : DependsOn

TestRun ..> TestSuite : TestSuiteId
TestRun ..> ExecutionEnvironment : EnvironmentId
TestReport ..> TestRun
CoverageMetric ..> TestRun

SubscriptionPlan "1" o-- "0..*" PlanLimit
UserSubscription ..> SubscriptionPlan : PlanId
UsageTracking ..> UserSubscription : UserId + period

IApiEndpointMetadataService <|.. ApiEndpointMetadataService
IApiTestOrderService <|.. ApiTestOrderService
IApiTestOrderGateService <|.. ApiTestOrderGateService
ISubscriptionLimitGatewayService <|.. SubscriptionLimitGatewayService

ApiTestOrderService ..> IApiEndpointMetadataService
ApiTestOrderGateService ..> IApiTestOrderService
ApiEndpointMetadataService ..> ApiEndpoint
SubscriptionLimitGatewayService ..> UserSubscription
@enduml
```

---

## STEP 3 - Feature-Based Diagrams

### Diagram: Feature - API Documentation Management
- Scope: Project/specification/endpoint lifecycle, metadata extraction, and contract-based integrations.
- Included classes (25):
  - Entities: `Project`, `ApiSpecification`, `ApiEndpoint`, `EndpointParameter`, `EndpointResponse`, `EndpointSecurityReq`, `SecurityScheme`
  - Services/interfaces: `IApiEndpointMetadataService`, `ApiEndpointMetadataService`, `IPathParameterTemplateService`, `PathParameterTemplateService`
  - Commands and handlers: `AddUpdateProjectCommand`, `AddUpdateProjectCommandHandler`, `UploadApiSpecificationCommand`, `UploadApiSpecificationCommandHandler`, `CreateManualSpecificationCommand`, `CreateManualSpecificationCommandHandler`, `AddUpdateEndpointCommand`, `AddUpdateEndpointCommandHandler`, `ActivateSpecificationCommand`, `ActivateSpecificationCommandHandler`, `ImportCurlCommand`, `ImportCurlCommandHandler`
  - External contracts: `ISubscriptionLimitGatewayService`, `IStorageFileGatewayService`

```plantuml
@startuml
left to right direction
skinparam classAttributeIconSize 0

package "Entities" {
  class Project
  class ApiSpecification
  class ApiEndpoint
  class EndpointParameter
  class EndpointResponse
  class EndpointSecurityReq
  class SecurityScheme
}

package "Services" {
  interface IApiEndpointMetadataService
  class ApiEndpointMetadataService
  interface IPathParameterTemplateService
  class PathParameterTemplateService
}

package "Commands" {
  class AddUpdateProjectCommand
  class AddUpdateProjectCommandHandler
  class UploadApiSpecificationCommand
  class UploadApiSpecificationCommandHandler
  class CreateManualSpecificationCommand
  class CreateManualSpecificationCommandHandler
  class AddUpdateEndpointCommand
  class AddUpdateEndpointCommandHandler
  class ActivateSpecificationCommand
  class ActivateSpecificationCommandHandler
  class ImportCurlCommand
  class ImportCurlCommandHandler
}

package "External Contracts" {
  interface ISubscriptionLimitGatewayService
  interface IStorageFileGatewayService
}

Project "1" o-- "0..*" ApiSpecification
ApiSpecification "1" *-- "0..*" ApiEndpoint
ApiSpecification "1" *-- "0..*" SecurityScheme
ApiEndpoint "1" *-- "0..*" EndpointParameter
ApiEndpoint "1" *-- "0..*" EndpointResponse
ApiEndpoint "1" *-- "0..*" EndpointSecurityReq

IApiEndpointMetadataService <|.. ApiEndpointMetadataService
IPathParameterTemplateService <|.. PathParameterTemplateService

AddUpdateProjectCommandHandler ..> AddUpdateProjectCommand : handles
AddUpdateProjectCommandHandler ..> Project
AddUpdateProjectCommandHandler ..> ISubscriptionLimitGatewayService

UploadApiSpecificationCommandHandler ..> UploadApiSpecificationCommand : handles
UploadApiSpecificationCommandHandler ..> Project
UploadApiSpecificationCommandHandler ..> ApiSpecification
UploadApiSpecificationCommandHandler ..> IStorageFileGatewayService
UploadApiSpecificationCommandHandler ..> ISubscriptionLimitGatewayService

CreateManualSpecificationCommandHandler ..> CreateManualSpecificationCommand : handles
CreateManualSpecificationCommandHandler ..> ApiSpecification
CreateManualSpecificationCommandHandler ..> ApiEndpoint
CreateManualSpecificationCommandHandler ..> EndpointParameter
CreateManualSpecificationCommandHandler ..> EndpointResponse
CreateManualSpecificationCommandHandler ..> ISubscriptionLimitGatewayService
CreateManualSpecificationCommandHandler ..> IPathParameterTemplateService

AddUpdateEndpointCommandHandler ..> AddUpdateEndpointCommand : handles
AddUpdateEndpointCommandHandler ..> ApiEndpoint
AddUpdateEndpointCommandHandler ..> EndpointParameter
AddUpdateEndpointCommandHandler ..> EndpointResponse
AddUpdateEndpointCommandHandler ..> ISubscriptionLimitGatewayService
AddUpdateEndpointCommandHandler ..> IPathParameterTemplateService

ActivateSpecificationCommandHandler ..> ActivateSpecificationCommand : handles
ActivateSpecificationCommandHandler ..> Project
ActivateSpecificationCommandHandler ..> ApiSpecification

ImportCurlCommandHandler ..> ImportCurlCommand : handles
ImportCurlCommandHandler ..> ApiSpecification
ImportCurlCommandHandler ..> ApiEndpoint
ImportCurlCommandHandler ..> EndpointParameter
ImportCurlCommandHandler ..> ISubscriptionLimitGatewayService
ImportCurlCommandHandler ..> IPathParameterTemplateService

ApiEndpointMetadataService ..> ApiSpecification
ApiEndpointMetadataService ..> ApiEndpoint
ApiEndpointMetadataService ..> EndpointParameter
ApiEndpointMetadataService ..> EndpointResponse
ApiEndpointMetadataService ..> EndpointSecurityReq
@enduml
```

### Diagram: Feature - Subscription and Monetization
- Scope: Plan/entitlement domain, payment intent lifecycle, and usage consumption flow.
- Included classes (23):
  - Entities: `SubscriptionPlan`, `PlanLimit`, `UserSubscription`, `UsageTracking`, `PaymentIntent`, `PaymentTransaction`, `SubscriptionHistory`, `OutboxMessage`
  - Services/interfaces: `ISubscriptionLimitGatewayService`, `SubscriptionLimitGatewayService`, `IPayOsService`, `PayOsService`
  - Commands and handlers: `ConsumeLimitAtomicallyCommand`, `ConsumeLimitAtomicallyCommandHandler`, `UpsertUsageTrackingCommand`, `UpsertUsageTrackingCommandHandler`, `CreateSubscriptionPaymentCommand`, `CreateSubscriptionPaymentCommandHandler`, `HandlePayOsWebhookCommand`, `HandlePayOsWebhookCommandHandler`, `ReconcilePayOsCheckoutsCommand`, `ReconcilePayOsCheckoutsCommandHandler`
  - Factory: `OutboxMessageFactory`

```plantuml
@startuml
left to right direction
skinparam classAttributeIconSize 0

package "Entities" {
  class SubscriptionPlan
  class PlanLimit
  class UserSubscription
  class UsageTracking
  class PaymentIntent
  class PaymentTransaction
  class SubscriptionHistory
  class OutboxMessage
}

package "Services" {
  interface ISubscriptionLimitGatewayService
  class SubscriptionLimitGatewayService
  interface IPayOsService
  class PayOsService
}

package "Commands" {
  class ConsumeLimitAtomicallyCommand
  class ConsumeLimitAtomicallyCommandHandler
  class UpsertUsageTrackingCommand
  class UpsertUsageTrackingCommandHandler
  class CreateSubscriptionPaymentCommand
  class CreateSubscriptionPaymentCommandHandler
  class HandlePayOsWebhookCommand
  class HandlePayOsWebhookCommandHandler
  class ReconcilePayOsCheckoutsCommand
  class ReconcilePayOsCheckoutsCommandHandler
}

class OutboxMessageFactory

SubscriptionPlan "1" o-- "0..*" PlanLimit
UserSubscription ..> SubscriptionPlan : PlanId
PaymentIntent ..> SubscriptionPlan : PlanId
PaymentIntent ..> UserSubscription : SubscriptionId
PaymentTransaction ..> PaymentIntent
PaymentTransaction ..> UserSubscription : SubscriptionId
SubscriptionHistory ..> UserSubscription : SubscriptionId
SubscriptionHistory ..> SubscriptionPlan : OldPlan/NewPlan

ISubscriptionLimitGatewayService <|.. SubscriptionLimitGatewayService
IPayOsService <|.. PayOsService

ConsumeLimitAtomicallyCommandHandler ..> ConsumeLimitAtomicallyCommand : handles
ConsumeLimitAtomicallyCommandHandler ..> UserSubscription
ConsumeLimitAtomicallyCommandHandler ..> SubscriptionPlan
ConsumeLimitAtomicallyCommandHandler ..> PlanLimit
ConsumeLimitAtomicallyCommandHandler ..> UsageTracking

UpsertUsageTrackingCommandHandler ..> UpsertUsageTrackingCommand : handles
UpsertUsageTrackingCommandHandler ..> UsageTracking

CreateSubscriptionPaymentCommandHandler ..> CreateSubscriptionPaymentCommand : handles
CreateSubscriptionPaymentCommandHandler ..> SubscriptionPlan
CreateSubscriptionPaymentCommandHandler ..> UserSubscription
CreateSubscriptionPaymentCommandHandler ..> SubscriptionHistory
CreateSubscriptionPaymentCommandHandler ..> PaymentIntent

HandlePayOsWebhookCommandHandler ..> HandlePayOsWebhookCommand : handles
HandlePayOsWebhookCommandHandler ..> IPayOsService
HandlePayOsWebhookCommandHandler ..> PaymentIntent
HandlePayOsWebhookCommandHandler ..> PaymentTransaction
HandlePayOsWebhookCommandHandler ..> UserSubscription
HandlePayOsWebhookCommandHandler ..> SubscriptionPlan
HandlePayOsWebhookCommandHandler ..> SubscriptionHistory

ReconcilePayOsCheckoutsCommandHandler ..> ReconcilePayOsCheckoutsCommand : handles
ReconcilePayOsCheckoutsCommandHandler ..> IPayOsService
ReconcilePayOsCheckoutsCommandHandler ..> PaymentIntent
ReconcilePayOsCheckoutsCommandHandler ..> OutboxMessageFactory
ReconcilePayOsCheckoutsCommandHandler ..> OutboxMessage

SubscriptionLimitGatewayService ..> ConsumeLimitAtomicallyCommand
SubscriptionLimitGatewayService ..> UpsertUsageTrackingCommand
@enduml
```

### Diagram: Feature - Identity and Access Management
- Scope: User-role-claim aggregate structure, current-user abstraction, and external identity synchronization.
- Included classes (18):
  - Entities: `User`, `UserProfile`, `Role`, `UserRole`, `UserClaim`, `RoleClaim`, `UserLogin`, `UserToken`
  - Public interfaces/services: `IUserService`, `UserService`, `ICurrentUser`, `CurrentWebUser`, `AnonymousUser`
  - Provider strategy participants: `IIdentityProvider`, `Auth0IdentityProvider`, `AzureActiveDirectoryB2CIdentityProvider`
  - Sync flow: `SyncUsersCommand`, `SyncUsersCommandHandler`

```plantuml
@startuml
left to right direction
skinparam classAttributeIconSize 0

package "Identity Entities" {
  class User
  class UserProfile
  class Role
  class UserRole
  class UserClaim
  class RoleClaim
  class UserLogin
  class UserToken
}

package "Public Services" {
  interface IUserService
  class UserService
  interface ICurrentUser
  class CurrentWebUser
  class AnonymousUser
}

package "Identity Providers" {
  interface IIdentityProvider
  class Auth0IdentityProvider
  class AzureActiveDirectoryB2CIdentityProvider
}

class SyncUsersCommand
class SyncUsersCommandHandler

User "1" *-- "0..1" UserProfile
User "1" *-- "0..*" UserRole
Role "1" *-- "0..*" UserRole
User "1" *-- "0..*" UserClaim
Role "1" *-- "0..*" RoleClaim
User "1" *-- "0..*" UserLogin
User "1" *-- "0..*" UserToken

IUserService <|.. UserService
ICurrentUser <|.. CurrentWebUser
ICurrentUser <|.. AnonymousUser

IIdentityProvider <|.. Auth0IdentityProvider
IIdentityProvider <|.. AzureActiveDirectoryB2CIdentityProvider

SyncUsersCommandHandler ..> SyncUsersCommand : handles
SyncUsersCommandHandler ..> Auth0IdentityProvider
SyncUsersCommandHandler ..> AzureActiveDirectoryB2CIdentityProvider
UserService ..> User
@enduml
```

### Diagram: Feature - Test Lifecycle Orchestration
- Scope: Test-suite lifecycle, proposal/approval workflow, execution environment management, and reporting artifacts.
- Included classes (31):
  - Design entities: `TestSuite`, `TestCase`, `TestCaseRequest`, `TestCaseExpectation`, `TestCaseVariable`, `TestDataSet`, `TestCaseChangeLog`, `TestOrderProposal`, `TestSuiteVersion`
  - Execution/reporting entities: `ExecutionEnvironment`, `TestRun`, `TestReport`, `CoverageMetric`
  - Services/interfaces: `IApiTestOrderAlgorithm`, `ApiTestOrderAlgorithm`, `IApiTestOrderService`, `ApiTestOrderService`, `IApiTestOrderGateService`, `ApiTestOrderGateService`, `ITestSuiteScopeService`, `TestSuiteScopeService`, `IExecutionAuthConfigService`, `ExecutionAuthConfigService`, `IApiEndpointMetadataService`
  - Commands and handlers: `AddUpdateTestSuiteScopeCommand`, `AddUpdateTestSuiteScopeCommandHandler`, `ProposeApiTestOrderCommand`, `ProposeApiTestOrderCommandHandler`, `ApproveApiTestOrderCommand`, `ApproveApiTestOrderCommandHandler`, `RejectApiTestOrderCommand`, `RejectApiTestOrderCommandHandler`, `ReorderApiTestOrderCommand`, `ReorderApiTestOrderCommandHandler`, `AddUpdateExecutionEnvironmentCommand`, `AddUpdateExecutionEnvironmentCommandHandler`

```plantuml
@startuml
left to right direction
skinparam classAttributeIconSize 0

package "Design Entities" {
  class TestSuite
  class TestCase
  class TestCaseRequest
  class TestCaseExpectation
  class TestCaseVariable
  class TestDataSet
  class TestCaseChangeLog
  class TestOrderProposal
  class TestSuiteVersion
}

package "Execution and Reporting" {
  class ExecutionEnvironment
  class TestRun
  class TestReport
  class CoverageMetric
}

package "Services" {
  interface IApiTestOrderAlgorithm
  class ApiTestOrderAlgorithm
  interface IApiTestOrderService
  class ApiTestOrderService
  interface IApiTestOrderGateService
  class ApiTestOrderGateService
  interface ITestSuiteScopeService
  class TestSuiteScopeService
  interface IExecutionAuthConfigService
  class ExecutionAuthConfigService
  interface IApiEndpointMetadataService
}

package "Commands" {
  class AddUpdateTestSuiteScopeCommand
  class AddUpdateTestSuiteScopeCommandHandler
  class ProposeApiTestOrderCommand
  class ProposeApiTestOrderCommandHandler
  class ApproveApiTestOrderCommand
  class ApproveApiTestOrderCommandHandler
  class RejectApiTestOrderCommand
  class RejectApiTestOrderCommandHandler
  class ReorderApiTestOrderCommand
  class ReorderApiTestOrderCommandHandler
  class AddUpdateExecutionEnvironmentCommand
  class AddUpdateExecutionEnvironmentCommandHandler
}

TestSuite "1" *-- "0..*" TestCase
TestSuite "1" *-- "0..*" TestSuiteVersion
TestSuite "1" *-- "0..*" TestOrderProposal
TestCase "1" *-- "0..1" TestCaseRequest
TestCase "1" *-- "0..1" TestCaseExpectation
TestCase "1" *-- "0..*" TestCaseVariable
TestCase "1" *-- "0..*" TestDataSet
TestCase "1" *-- "0..*" TestCaseChangeLog

TestRun ..> TestSuite
TestRun ..> ExecutionEnvironment
TestReport ..> TestRun
CoverageMetric ..> TestRun

IApiTestOrderAlgorithm <|.. ApiTestOrderAlgorithm
IApiTestOrderService <|.. ApiTestOrderService
IApiTestOrderGateService <|.. ApiTestOrderGateService
ITestSuiteScopeService <|.. TestSuiteScopeService
IExecutionAuthConfigService <|.. ExecutionAuthConfigService

ApiTestOrderService ..> IApiEndpointMetadataService
ApiTestOrderService ..> IApiTestOrderAlgorithm
ApiTestOrderGateService ..> IApiTestOrderService

AddUpdateTestSuiteScopeCommandHandler ..> AddUpdateTestSuiteScopeCommand : handles
AddUpdateTestSuiteScopeCommandHandler ..> TestSuite
AddUpdateTestSuiteScopeCommandHandler ..> ITestSuiteScopeService
AddUpdateTestSuiteScopeCommandHandler ..> IApiEndpointMetadataService

ProposeApiTestOrderCommandHandler ..> ProposeApiTestOrderCommand : handles
ProposeApiTestOrderCommandHandler ..> TestSuite
ProposeApiTestOrderCommandHandler ..> TestOrderProposal
ProposeApiTestOrderCommandHandler ..> IApiTestOrderService

ApproveApiTestOrderCommandHandler ..> ApproveApiTestOrderCommand : handles
ApproveApiTestOrderCommandHandler ..> TestSuite
ApproveApiTestOrderCommandHandler ..> TestOrderProposal
ApproveApiTestOrderCommandHandler ..> IApiTestOrderService

RejectApiTestOrderCommandHandler ..> RejectApiTestOrderCommand : handles
RejectApiTestOrderCommandHandler ..> TestSuite
RejectApiTestOrderCommandHandler ..> TestOrderProposal
RejectApiTestOrderCommandHandler ..> IApiTestOrderService

ReorderApiTestOrderCommandHandler ..> ReorderApiTestOrderCommand : handles
ReorderApiTestOrderCommandHandler ..> TestSuite
ReorderApiTestOrderCommandHandler ..> TestOrderProposal
ReorderApiTestOrderCommandHandler ..> IApiTestOrderService

AddUpdateExecutionEnvironmentCommandHandler ..> AddUpdateExecutionEnvironmentCommand : handles
AddUpdateExecutionEnvironmentCommandHandler ..> ExecutionEnvironment
AddUpdateExecutionEnvironmentCommandHandler ..> IExecutionAuthConfigService
@enduml
```

---

## STEP 4 - Pattern Identification

### Diagram: Strategy Pattern - External Identity Providers
- Scope: Provider strategy abstraction and runtime provider selection in user synchronization flow.
- Included classes:
  - `IIdentityProvider`, `Auth0IdentityProvider`, `AzureActiveDirectoryB2CIdentityProvider`, `SyncUsersCommandHandler`

```plantuml
@startuml
left to right direction

interface IIdentityProvider
class Auth0IdentityProvider
class AzureActiveDirectoryB2CIdentityProvider
class SyncUsersCommandHandler

IIdentityProvider <|.. Auth0IdentityProvider
IIdentityProvider <|.. AzureActiveDirectoryB2CIdentityProvider
SyncUsersCommandHandler ..> Auth0IdentityProvider : select/use
SyncUsersCommandHandler ..> AzureActiveDirectoryB2CIdentityProvider : select/use
@enduml
```

### Diagram: Strategy Pattern - API Test Order Algorithm Pipeline
- Scope: Interchangeable ordering strategy and analyzers behind order service.
- Included classes:
  - `IApiTestOrderService`, `ApiTestOrderService`
  - `IApiTestOrderAlgorithm`, `ApiTestOrderAlgorithm`
  - `ISchemaRelationshipAnalyzer`, `SchemaRelationshipAnalyzer`
  - `IDependencyAwareTopologicalSorter`, `DependencyAwareTopologicalSorter`
  - `IApiEndpointMetadataService`

```plantuml
@startuml
left to right direction

interface IApiTestOrderService
class ApiTestOrderService
interface IApiTestOrderAlgorithm
class ApiTestOrderAlgorithm
interface ISchemaRelationshipAnalyzer
class SchemaRelationshipAnalyzer
interface IDependencyAwareTopologicalSorter
class DependencyAwareTopologicalSorter
interface IApiEndpointMetadataService

IApiTestOrderService <|.. ApiTestOrderService
IApiTestOrderAlgorithm <|.. ApiTestOrderAlgorithm
ISchemaRelationshipAnalyzer <|.. SchemaRelationshipAnalyzer
IDependencyAwareTopologicalSorter <|.. DependencyAwareTopologicalSorter

ApiTestOrderService ..> IApiEndpointMetadataService
ApiTestOrderService ..> IApiTestOrderAlgorithm
ApiTestOrderAlgorithm ..> ISchemaRelationshipAnalyzer
ApiTestOrderAlgorithm ..> IDependencyAwareTopologicalSorter
@enduml
```

### Diagram: Factory Pattern - Subscription Outbox Message Creation
- Scope: Centralized outbox message object creation for payment reconciliation events.
- Included classes:
  - `ReconcilePayOsCheckoutsCommandHandler`, `OutboxMessageFactory`, `OutboxMessage`
  - `SubscriptionOutboxEventBase`, `PaymentIntentStatusChangedOutboxEvent`, `PaymentCheckoutLinkCreatedOutboxEvent`

```plantuml
@startuml
left to right direction

class ReconcilePayOsCheckoutsCommandHandler
class OutboxMessageFactory
class OutboxMessage
abstract class SubscriptionOutboxEventBase
class PaymentIntentStatusChangedOutboxEvent
class PaymentCheckoutLinkCreatedOutboxEvent

SubscriptionOutboxEventBase <|-- PaymentIntentStatusChangedOutboxEvent
SubscriptionOutboxEventBase <|-- PaymentCheckoutLinkCreatedOutboxEvent

ReconcilePayOsCheckoutsCommandHandler ..> OutboxMessageFactory : Create(...)
OutboxMessageFactory ..> OutboxMessage : builds
OutboxMessageFactory ..> PaymentIntentStatusChangedOutboxEvent
OutboxMessageFactory ..> PaymentCheckoutLinkCreatedOutboxEvent
@enduml
```

### Diagram: Observer Pattern - Domain Events and Handlers
- Scope: Event publication and subscription via `Dispatcher` and `IDomainEventHandler<T>`.
- Included classes:
  - `IDomainEvent`, `EntityCreatedEvent<T>`, `EntityUpdatedEvent<T>`, `EntityDeletedEvent<T>`
  - `IDomainEventHandler<T>`, `Dispatcher`
  - `ProjectCreatedEventHandler`, `ProjectUpdatedEventHandler`, `ProjectDeletedEventHandler`
  - `SpecCreatedEventHandler`, `SpecUpdatedEventHandler`, `SpecDeletedEventHandler`

```plantuml
@startuml
left to right direction

interface IDomainEvent
interface "IDomainEventHandler<T>" as IDomainEventHandlerT
class "EntityCreatedEvent<T>" as EntityCreatedEventT
class "EntityUpdatedEvent<T>" as EntityUpdatedEventT
class "EntityDeletedEvent<T>" as EntityDeletedEventT
class Dispatcher

class ProjectCreatedEventHandler
class ProjectUpdatedEventHandler
class ProjectDeletedEventHandler
class SpecCreatedEventHandler
class SpecUpdatedEventHandler
class SpecDeletedEventHandler

IDomainEvent <|.. EntityCreatedEventT
IDomainEvent <|.. EntityUpdatedEventT
IDomainEvent <|.. EntityDeletedEventT

IDomainEventHandlerT <|.. ProjectCreatedEventHandler
IDomainEventHandlerT <|.. ProjectUpdatedEventHandler
IDomainEventHandlerT <|.. ProjectDeletedEventHandler
IDomainEventHandlerT <|.. SpecCreatedEventHandler
IDomainEventHandlerT <|.. SpecUpdatedEventHandler
IDomainEventHandlerT <|.. SpecDeletedEventHandler

Dispatcher ..> IDomainEvent : dispatch
Dispatcher ..> IDomainEventHandlerT : resolve/invoke
@enduml
```

### Diagram: Decorator Pattern - Command and Query Handler Pipeline
- Scope: Attribute-driven handler decoration for audit logging and database retry behavior.
- Included classes:
  - `ICommandHandler<T>`, `IQueryHandler<TQuery,TResult>`
  - `AuditLogCommandDecorator<T>`, `AuditLogQueryDecorator<TQuery,TResult>`
  - `DatabaseRetryCommandDecorator<T>`, `DatabaseRetryQueryDecorator<TQuery,TResult>`, `DatabaseRetryDecoratorBase`
  - `HandlerFactory`, `MappingAttribute`

```plantuml
@startuml
left to right direction

interface "ICommandHandler<T>" as ICommandHandlerT
interface "IQueryHandler<TQuery,TResult>" as IQueryHandlerTQTR
class "AuditLogCommandDecorator<T>" as AuditLogCommandDecoratorT
class "AuditLogQueryDecorator<TQuery,TResult>" as AuditLogQueryDecoratorTQTR
class "DatabaseRetryCommandDecorator<T>" as DatabaseRetryCommandDecoratorT
class "DatabaseRetryQueryDecorator<TQuery,TResult>" as DatabaseRetryQueryDecoratorTQTR
abstract class DatabaseRetryDecoratorBase
class HandlerFactory
class MappingAttribute

ICommandHandlerT <|.. AuditLogCommandDecoratorT
IQueryHandlerTQTR <|.. AuditLogQueryDecoratorTQTR
ICommandHandlerT <|.. DatabaseRetryCommandDecoratorT
IQueryHandlerTQTR <|.. DatabaseRetryQueryDecoratorTQTR

DatabaseRetryDecoratorBase <|-- DatabaseRetryCommandDecoratorT
DatabaseRetryDecoratorBase <|-- DatabaseRetryQueryDecoratorTQTR

AuditLogCommandDecoratorT ..> ICommandHandlerT : wraps
AuditLogQueryDecoratorTQTR ..> IQueryHandlerTQTR : wraps
DatabaseRetryCommandDecoratorT ..> ICommandHandlerT : wraps
DatabaseRetryQueryDecoratorTQTR ..> IQueryHandlerTQTR : wraps

HandlerFactory ..> MappingAttribute : reads attributes
HandlerFactory ..> AuditLogCommandDecoratorT : composes pipeline
HandlerFactory ..> DatabaseRetryCommandDecoratorT : composes pipeline
@enduml
```

### Adapter/Facade Note
- No dedicated adapter hierarchy was found as a first-class pattern.
- `Dispatcher` functions as a lightweight facade over command/query/event dispatch and is already represented in the observer/decorator context.
