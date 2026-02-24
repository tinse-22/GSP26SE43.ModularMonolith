# ERD Analysis - API Testing Automation System

> **Đối chiếu chuẩn theo codebase** — Tài liệu này được sinh 100% từ DbContext model snapshots, entity configurations, và seed data trong code.
>
> - **Target DB**: `ClassifiedAds` (từ `ConnectionStrings__Default` trong `.env`)
> - **Schemas đã kiểm tra**: `identity`, `apidoc`, `testgen`, `testexecution`, `testreporting`, `subscription`, `storage`, `notification`, `configuration`, `auditlog`, `llmassistant`
> - **Latest migration IDs** (theo code, không xác nhận runtime):
>   | Module | Latest Migration |
>   |--------|-----------------|
>   | Identity | `20260216012247_IdentitySeedHashSync` |
>   | ApiDocumentation | `20260214004925_InitialApiDocumentation` |
>   | TestGeneration | `20260219061423_AddTestSuiteSelectedEndpointIds` |
>   | TestExecution | `20260201104234_InitialTestExecution` |
>   | TestReporting | `20260201104246_InitialTestReporting` |
>   | Subscription | `20260217103000_ReseedAdminEnterpriseSubscriptionForDev` |
>   | Storage | `20260201101114_InitialStorage` |
>   | Notification | `20260201104436_InitialNotification` |
>   | Configuration | `20260201104426_InitialConfiguration` |
>   | AuditLog | `20260201102457_InitialAuditLog` |
>   | LlmAssistant | *(chưa có migration folder — chỉ có DbContext + entity config)* |
> - **Preflight SQL** (`current_database`, `current_schema`, `__EFMigrationsHistory`): không thực hiện — tài liệu chỉ audit tĩnh từ source code.

---

## 1. Overview

### 1.1 Module Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           MODULE ARCHITECTURE                               │
│                    11 modules — 11 PostgreSQL schemas                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌────────────┐  ┌─────────────────┐  ┌─────────────────┐  ┌────────────┐  │
│  │  Identity  │  │ ApiDocumentation│  │  TestGeneration │  │  Storage   │  │
│  │ (identity) │  │    (apidoc)     │  │    (testgen)    │  │ (storage)  │  │
│  └────────────┘  └─────────────────┘  └─────────────────┘  └────────────┘  │
│                                                                              │
│  ┌────────────────┐  ┌─────────────────┐  ┌─────────────────┐              │
│  │ TestExecution  │  │  TestReporting  │  │   Subscription  │              │
│  │(testexecution) │  │ (testreporting) │  │  (subscription) │              │
│  └────────────────┘  └─────────────────┘  └─────────────────┘              │
│                                                                              │
│  ┌────────────────┐  ┌─────────────────┐  ┌─────────────────┐              │
│  │  Notification  │  │  Configuration  │  │    AuditLog     │              │
│  │ (notification) │  │ (configuration) │  │   (auditlog)    │              │
│  └────────────────┘  └─────────────────┘  └─────────────────┘              │
│                                                                              │
│  ┌────────────────┐                                                         │
│  │  LlmAssistant  │                                                         │
│  │ (llmassistant) │  ← chưa có migration                                   │
│  └────────────────┘                                                         │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Storage Strategy

| Data Type | Storage | Retention | Lý do |
|-----------|---------|-----------|-------|
| **User, Project, ApiSpec** | PostgreSQL | Permanent | Core business data |
| **TestCase, TestSuite** | PostgreSQL | Permanent | Reusable test definitions |
| **TestRun Results** | Redis → PostgreSQL | 5-10 days in Redis | Hot data for real-time access |
| **TestExecution Logs** | Redis | 5-10 days | Temporary, high-frequency writes |
| **Reports (generated)** | File Storage + PostgreSQL metadata | Permanent | User-requested exports |

### 1.3 Cross-Module Reference Pattern

> **Quan trọng**: Các module KHÔNG có FK liên module ở DB level. Thay vào đó, dùng **Guid column + index** (ví dụ `ProjectId`, `ApiSpecId`, `EndpointId` trong `testgen` schema chỉ là Guid column có index, không có FK constraint sang `apidoc` schema). Đây là pattern chuẩn của Modular Monolith — module boundary được enforce ở application layer, không ở DB layer.

---

## 2. Detailed ERD by Module

### 2.1 Identity Module — Schema: `identity`

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    IDENTITY MODULE — Schema: identity                       │
│                    (Custom Identity, NOT default AspNet* tables)            │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│              Users                       │  ← Custom, NOT AspNetUsers
├─────────────────────────────────────────┤
│ PK  Id                    : UUID        │  default gen_random_uuid()
│     UserName              : text        │
│     NormalizedUserName    : text        │
│     Email                 : text        │
│     NormalizedEmail       : text        │
│     EmailConfirmed        : boolean     │
│     PasswordHash          : text        │
│     SecurityStamp         : text        │
│     ConcurrencyStamp      : text        │
│     PhoneNumber           : text        │
│     PhoneNumberConfirmed  : boolean     │
│     TwoFactorEnabled      : boolean     │
│     LockoutEnd            : timestamptz │
│     LockoutEnabled        : boolean     │
│     AccessFailedCount     : integer     │
│     Auth0UserId           : text        │
│     AzureAdB2CUserId      : text        │
│     CreatedDateTime       : timestamptz │
│     UpdatedDateTime       : timestamptz │
│     RowVersion            : bytea       │  ConcurrencyToken
└─────────────────────────────────────────┘
    Seed: 2 rows — tinvtse@gmail.com (admin), user@example.com (user)
           │
           │ 1:N
           ▼
┌─────────────────────────────────────────┐
│             UserRoles                    │
├─────────────────────────────────────────┤
│ PK  Id                : UUID            │  default gen_random_uuid()
│ FK  UserId            : UUID → Users    │  Cascade
│ FK  RoleId            : UUID → Roles    │  Cascade
│     CreatedDateTime   : timestamptz     │
│     UpdatedDateTime   : timestamptz     │
│     RowVersion        : bytea           │
└─────────────────────────────────────────┘
    Index: RoleId, UserId
    Seed: 2 rows — Admin↔User1, User↔User2
           │
           │ N:1
           ▼
┌─────────────────────────────────────────┐
│              Roles                       │
├─────────────────────────────────────────┤
│ PK  Id                    : UUID        │  default gen_random_uuid()
│     Name                  : text        │
│     NormalizedName        : text        │
│     ConcurrencyStamp      : text        │
│     CreatedDateTime       : timestamptz │
│     UpdatedDateTime       : timestamptz │
│     RowVersion            : bytea       │
└─────────────────────────────────────────┘
    Seed: 2 rows — Admin, User

┌─────────────────────────────────────────┐
│            RoleClaims                    │
├─────────────────────────────────────────┤
│ PK  Id                : UUID            │  default gen_random_uuid()
│ FK  RoleId            : UUID → Roles    │  Cascade
│     Type              : text            │
│     Value             : text            │
│     CreatedDateTime   : timestamptz     │
│     UpdatedDateTime   : timestamptz     │
│     RowVersion        : bytea           │
└─────────────────────────────────────────┘
    Index: RoleId
    Seed: 17 rows — Permission claims for Admin role

┌─────────────────────────────────────────┐
│            UserClaims                    │
├─────────────────────────────────────────┤
│ PK  Id                : UUID            │  default gen_random_uuid()
│ FK  UserId            : UUID → Users    │  Cascade
│     Type              : text            │
│     Value             : text            │
│     CreatedDateTime   : timestamptz     │
│     UpdatedDateTime   : timestamptz     │
│     RowVersion        : bytea           │
└─────────────────────────────────────────┘
    Index: UserId

┌─────────────────────────────────────────┐
│            UserLogins                    │
├─────────────────────────────────────────┤
│ PK  Id                    : UUID        │  default gen_random_uuid()
│ FK  UserId                : UUID → Users│  Cascade
│     LoginProvider         : text        │
│     ProviderKey           : text        │
│     ProviderDisplayName   : text        │
│     CreatedDateTime       : timestamptz │
│     UpdatedDateTime       : timestamptz │
│     RowVersion            : bytea       │
└─────────────────────────────────────────┘
    Index: UserId

┌─────────────────────────────────────────┐
│            UserTokens                    │
├─────────────────────────────────────────┤
│ PK  Id                : UUID            │  default gen_random_uuid()
│ FK  UserId            : UUID → Users    │  Cascade
│     LoginProvider     : text            │
│     TokenName         : text            │
│     TokenValue        : text            │
│     CreatedDateTime   : timestamptz     │
│     UpdatedDateTime   : timestamptz     │
│     RowVersion        : bytea           │
└─────────────────────────────────────────┘
    Index: UserId

┌─────────────────────────────────────────┐
│           UserProfiles                   │  ← 1:1 with Users
├─────────────────────────────────────────┤
│ PK  Id                : UUID            │  default gen_random_uuid()
│ FK  UserId            : UUID → Users    │  Cascade, UNIQUE
│     DisplayName       : varchar(200)    │
│     AvatarUrl         : varchar(500)    │
│     Timezone          : varchar(50)     │
│     CreatedDateTime   : timestamptz     │
│     UpdatedDateTime   : timestamptz     │
│     RowVersion        : bytea           │
└─────────────────────────────────────────┘
    Index: UserId (UNIQUE)
    Seed: 1 row — "System Administrator", "Asia/Ho_Chi_Minh"

┌─────────────────────────────────────────┐
│        DataProtectionKeys                │
├─────────────────────────────────────────┤
│ PK  Id              : integer           │  auto-increment
│     FriendlyName    : text              │
│     Xml             : text              │
└─────────────────────────────────────────┘
```

**9 bảng**: Users, Roles, UserRoles, RoleClaims, UserClaims, UserLogins, UserTokens, UserProfiles, DataProtectionKeys

---

### 2.2 ApiDocumentation Module — Schema: `apidoc`

```
┌─────────────────────────────────────────┐
│             Projects                     │
├─────────────────────────────────────────┤
│ PK  Id              : UUID              │  default gen_random_uuid()
│ FK  ActiveSpecId    : UUID → ApiSpecs   │  SetNull
│     OwnerId         : UUID              │  (cross-module ref, no FK)
│     Name            : varchar(200)      │  Required
│     Description     : text              │
│     BaseUrl         : varchar(500)      │
│     Status          : varchar(20)       │  Required
│     CreatedDateTime : timestamptz       │
│     UpdatedDateTime : timestamptz       │
│     RowVersion      : bytea             │
└─────────────────────────────────────────┘
    Index: ActiveSpecId, OwnerId, Status
           │
           │ 1:N
           ▼
┌─────────────────────────────────────────┐
│         ApiSpecifications                │
├─────────────────────────────────────────┤
│ PK  Id              : UUID              │  default gen_random_uuid()
│ FK  ProjectId       : UUID → Projects   │  Cascade
│     OriginalFileId  : UUID              │  (cross-module ref, no FK)
│     Name            : varchar(200)      │  Required
│     SourceType      : varchar(20)       │  Required
│     Version         : varchar(50)       │
│     IsActive        : boolean           │
│     ParsedAt        : timestamptz       │
│     ParseStatus     : varchar(20)       │  Required
│     ParseErrors     : jsonb             │
│     CreatedDateTime : timestamptz       │
│     UpdatedDateTime : timestamptz       │
│     RowVersion      : bytea             │
└─────────────────────────────────────────┘
    Index: IsActive, ProjectId
           │
           │ 1:N
           ├──────────────────────────────────┐
           ▼                                  ▼
┌──────────────────────────────┐  ┌──────────────────────────────┐
│       ApiEndpoints           │  │      SecuritySchemes          │
├──────────────────────────────┤  ├──────────────────────────────┤
│ PK Id          : UUID        │  │ PK Id          : UUID        │
│ FK ApiSpecId   : UUID        │  │ FK ApiSpecId   : UUID        │
│    HttpMethod  : varchar(10) │  │    Name        : varchar(100)│
│    Path        : varchar(500)│  │    Type        : varchar(20) │
│    OperationId : varchar(200)│  │    Scheme      : varchar(50) │
│    Summary     : varchar(500)│  │    BearerFormat: varchar(50) │
│    Description : text        │  │    In          : varchar(20) │
│    Tags        : jsonb       │  │    ParameterName: varchar(100)│
│    IsDeprecated: boolean     │  │    Configuration: jsonb      │
│    CreatedDateTime: ts       │  │    CreatedDateTime: ts       │
│    UpdatedDateTime: ts       │  │    UpdatedDateTime: ts       │
│    RowVersion  : bytea       │  │    RowVersion  : bytea       │
└──────────────────────────────┘  └──────────────────────────────┘
    Index: ApiSpecId,                Index: ApiSpecId
           (ApiSpecId, HttpMethod, Path)
           │
           │ 1:N (3 child tables)
           ├───────────────────────────────┬──────────────────────────┐
           ▼                               ▼                          ▼
┌────────────────────────┐  ┌────────────────────────┐  ┌────────────────────────┐
│  EndpointParameters    │  │   EndpointResponses    │  │ EndpointSecurityReqs   │
├────────────────────────┤  ├────────────────────────┤  ├────────────────────────┤
│ PK Id       : UUID     │  │ PK Id       : UUID     │  │ PK Id       : UUID     │
│ FK EndpointId: UUID    │  │ FK EndpointId: UUID    │  │ FK EndpointId: UUID    │
│    Name    : v(100)    │  │    StatusCode: integer │  │    SecurityType: v(20) │
│    Location: v(20)     │  │    Description: text   │  │    SchemeName: v(100)  │
│    DataType: v(50)     │  │    Schema   : jsonb    │  │    Scopes   : jsonb    │
│    Format  : v(50)     │  │    Examples : jsonb    │  │    CreatedDateTime: ts │
│    IsRequired: bool    │  │    Headers  : jsonb    │  │    UpdatedDateTime: ts │
│    DefaultValue: text  │  │    CreatedDateTime: ts │  │    RowVersion: bytea   │
│    Schema  : jsonb     │  │    UpdatedDateTime: ts │  └────────────────────────┘
│    Examples: jsonb     │  │    RowVersion: bytea   │      Index: EndpointId
│    CreatedDateTime: ts │  └────────────────────────┘
│    UpdatedDateTime: ts │      Index: EndpointId,
│    RowVersion: bytea   │             (EndpointId, StatusCode)
└────────────────────────┘
    Index: EndpointId

┌──────────────────────────┐  ┌──────────────────────────┐
│  AuditLogEntries (apidoc)│  │  OutboxMessages (apidoc) │
├──────────────────────────┤  ├──────────────────────────┤
│ PK Id      : UUID        │  │ PK Id         : UUID     │
│    UserId  : UUID        │  │    EventType  : text     │
│    Action  : text        │  │    TriggeredById: UUID   │
│    ObjectId: text        │  │    ObjectId   : text     │
│    Log     : text        │  │    Payload    : text     │
│    CreatedDateTime: ts   │  │    Published  : boolean  │
│    UpdatedDateTime: ts   │  │    ActivityId : text     │
│    RowVersion: bytea     │  │    CreatedDateTime: ts   │
└──────────────────────────┘  │    UpdatedDateTime: ts   │
                              │    RowVersion: bytea     │
                              └──────────────────────────┘
                                  Index: CreatedDateTime,
                                         (Published, CreatedDateTime)

┌────────────────────────────────┐
│ ArchivedOutboxMessages (apidoc)│  ← same columns, no gen_random_uuid()
├────────────────────────────────┤
│    Index: CreatedDateTime      │
└────────────────────────────────┘
```

**10 bảng**: Projects, ApiSpecifications, ApiEndpoints, EndpointParameters, EndpointResponses, EndpointSecurityReqs, SecuritySchemes, AuditLogEntries, OutboxMessages, ArchivedOutboxMessages

---

### 2.3 TestGeneration Module — Schema: `testgen`

```
┌──────────────────────────────────────────────┐
│                TestSuites                     │
├──────────────────────────────────────────────┤
│ PK  Id                : UUID                 │  default gen_random_uuid()
│     ProjectId         : UUID                 │  (cross-module, no FK)
│     ApiSpecId         : UUID (nullable)      │  (cross-module, no FK)
│     Name              : varchar(200)         │  Required
│     Description       : text                 │
│     GenerationType    : varchar(20)          │  Required
│     Status            : varchar(20)          │  Required
│     ApprovalStatus    : varchar(30)          │  Required
│     ApprovedAt        : timestamptz          │
│     ApprovedById      : UUID (nullable)      │
│     CreatedById       : UUID                 │  (cross-module, no FK)
│     LastModifiedById  : UUID (nullable)      │
│     SelectedEndpointIds : jsonb              │  PrimitiveCollection
│     Version           : integer              │  Default 1
│     CreatedDateTime   : timestamptz          │
│     UpdatedDateTime   : timestamptz          │
│     RowVersion        : bytea                │
└──────────────────────────────────────────────┘
    Index: ApiSpecId, ApprovalStatus, ApprovedById,
           CreatedById, LastModifiedById, ProjectId, Status
           │
           │ 1:N
           ├──────────────────────┬──────────────────┐
           ▼                      ▼                  ▼
┌────────────────────┐ ┌────────────────────┐ ┌────────────────────┐
│    TestCases       │ │ TestOrderProposals │ │ TestSuiteVersions  │
└────────────────────┘ └────────────────────┘ └────────────────────┘

┌──────────────────────────────────────────────┐
│                TestCases                      │
├──────────────────────────────────────────────┤
│ PK  Id                : UUID                 │  default gen_random_uuid()
│ FK  TestSuiteId       : UUID → TestSuites    │  Cascade
│ FK  DependsOnId       : UUID → TestCases     │  SetNull (self-ref)
│     EndpointId        : UUID (nullable)      │  (cross-module, no FK)
│     Name              : varchar(200)         │  Required
│     Description       : text                 │
│     TestType          : varchar(20)          │  Required
│     Priority          : varchar(20)          │  Required
│     IsEnabled         : boolean              │
│     OrderIndex        : integer              │
│     IsOrderCustomized : boolean              │
│     CustomOrderIndex  : integer (nullable)   │
│     LastModifiedById  : UUID (nullable)      │
│     Tags              : jsonb                │
│     Version           : integer              │  Default 1
│     CreatedDateTime   : timestamptz          │
│     UpdatedDateTime   : timestamptz          │
│     RowVersion        : bytea                │
└──────────────────────────────────────────────┘
    Index: DependsOnId, EndpointId, LastModifiedById,
           TestSuiteId, (TestSuiteId, CustomOrderIndex),
           (TestSuiteId, OrderIndex)
           │
           │ 1:1 and 1:N children
           ├──────────────┬──────────────┬──────────────┬──────────────┐
           ▼              ▼              ▼              ▼              ▼
  TestCaseRequests  TestCaseExpect.  TestCaseVars  TestDataSets  TestCaseChangeLogs
       (1:1)           (1:1)          (1:N)         (1:N)           (1:N)

┌──────────────────────────────────────────────┐
│           TestCaseRequests                    │  ← 1:1 with TestCases
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│ FK  TestCaseId      : UUID → TestCases       │  Cascade, UNIQUE
│     HttpMethod      : varchar(10)            │  Required
│     Url             : varchar(1000)          │  Required
│     Headers         : jsonb                  │
│     PathParams      : jsonb                  │
│     QueryParams     : jsonb                  │
│     BodyType        : varchar(20)            │  Required
│     Body            : text                   │
│     Timeout         : integer                │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: TestCaseId (UNIQUE)

┌──────────────────────────────────────────────┐
│         TestCaseExpectations                  │  ← 1:1 with TestCases
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│ FK  TestCaseId      : UUID → TestCases       │  Cascade, UNIQUE
│     ExpectedStatus  : jsonb                  │
│     ResponseSchema  : jsonb                  │
│     HeaderChecks    : jsonb                  │
│     BodyContains    : jsonb                  │
│     BodyNotContains : jsonb                  │
│     JsonPathChecks  : jsonb                  │
│     MaxResponseTime : integer (nullable)     │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: TestCaseId (UNIQUE)

┌──────────────────────────────────────────────┐
│           TestCaseVariables                   │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│ FK  TestCaseId      : UUID → TestCases       │  Cascade
│     VariableName    : varchar(100)           │  Required
│     ExtractFrom     : varchar(20)            │  Required
│     JsonPath        : varchar(500)           │
│     HeaderName      : varchar(100)           │
│     Regex           : varchar(500)           │
│     DefaultValue    : text                   │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: TestCaseId

┌──────────────────────────────────────────────┐
│              TestDataSets                     │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│ FK  TestCaseId      : UUID → TestCases       │  Cascade
│     Name            : varchar(100)           │  Required
│     Data            : jsonb                  │  Required
│     IsEnabled       : boolean                │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: TestCaseId

┌──────────────────────────────────────────────┐
│          TestCaseChangeLogs                   │
├──────────────────────────────────────────────┤
│ PK  Id                  : UUID               │
│ FK  TestCaseId          : UUID → TestCases   │  Cascade
│     ChangedById         : UUID               │  (cross-module, no FK)
│     ChangeType          : varchar(30)        │  Required
│     FieldName           : varchar(100)       │
│     OldValue            : text               │
│     NewValue            : text               │
│     ChangeReason        : text               │
│     VersionAfterChange  : integer            │
│     IpAddress           : varchar(45)        │
│     UserAgent           : varchar(500)       │
│     CreatedDateTime     : timestamptz        │
│     UpdatedDateTime     : timestamptz        │
│     RowVersion          : bytea              │
└──────────────────────────────────────────────┘
    Index: ChangeType, ChangedById, CreatedDateTime, TestCaseId

┌──────────────────────────────────────────────┐
│          TestOrderProposals                   │
├──────────────────────────────────────────────┤
│ PK  Id                : UUID                 │
│ FK  TestSuiteId       : UUID → TestSuites    │  Cascade
│     ProposalNumber    : integer              │
│     Source            : varchar(20)          │  Required
│     Status            : varchar(30)          │  Required
│     ProposedOrder     : jsonb                │  Required
│     AppliedOrder      : jsonb                │
│     UserModifiedOrder : jsonb                │
│     AiReasoning       : text                 │
│     ConsideredFactors : jsonb                │
│     LlmModel          : varchar(100)        │
│     TokensUsed        : integer (nullable)   │
│     ReviewedById      : UUID (nullable)      │
│     ReviewedAt        : timestamptz          │
│     ReviewNotes       : text                 │
│     AppliedAt         : timestamptz          │
│     CreatedDateTime   : timestamptz          │
│     UpdatedDateTime   : timestamptz          │
│     RowVersion        : bytea                │
└──────────────────────────────────────────────┘
    Index: ReviewedById, Source, Status, TestSuiteId,
           (TestSuiteId, ProposalNumber)

┌──────────────────────────────────────────────┐
│          TestSuiteVersions                    │
├──────────────────────────────────────────────┤
│ PK  Id                      : UUID           │
│ FK  TestSuiteId             : UUID → Suites  │  Cascade
│     VersionNumber           : integer        │
│     ChangeType              : varchar(30)    │  Required
│     ChangeDescription       : text           │
│     ChangedById             : UUID           │  (cross-module, no FK)
│     PreviousState           : jsonb          │
│     NewState                : jsonb          │
│     TestCaseOrderSnapshot   : jsonb          │
│     ApprovalStatusSnapshot  : varchar(30)    │  Required
│     CreatedDateTime         : timestamptz    │
│     UpdatedDateTime         : timestamptz    │
│     RowVersion              : bytea          │
└──────────────────────────────────────────────┘
    Index: ChangeType, ChangedById, TestSuiteId,
           (TestSuiteId, VersionNumber)

┌─────────────────────────────┐  ┌─────────────────────────────┐
│ AuditLogEntries (testgen)   │  │ OutboxMessages (testgen)    │
├─────────────────────────────┤  ├─────────────────────────────┤
│ Standard audit table        │  │ Standard outbox table       │
└─────────────────────────────┘  └─────────────────────────────┘
```

**11 bảng**: TestSuites, TestCases, TestCaseRequests, TestCaseExpectations, TestCaseVariables, TestDataSets, TestCaseChangeLogs, TestOrderProposals, TestSuiteVersions, AuditLogEntries, OutboxMessages

---

### 2.4 TestExecution Module — Schema: `testexecution`

```
┌──────────────────────────────────────────────┐
│          ExecutionEnvironments                │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     ProjectId       : UUID                   │  (cross-module, no FK)
│     Name            : varchar(100)           │  Required
│     BaseUrl         : varchar(500)           │  Required
│     Variables       : jsonb                  │
│     Headers         : jsonb                  │
│     AuthConfig      : jsonb                  │
│     IsDefault       : boolean                │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: ProjectId, (ProjectId, IsDefault)

┌──────────────────────────────────────────────┐
│               TestRuns                        │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     TestSuiteId     : UUID                   │  (cross-module, no FK)
│     EnvironmentId   : UUID                   │  (cross-module, no FK)
│     TriggeredById   : UUID                   │  (cross-module, no FK)
│     RunNumber       : integer                │
│     Status          : integer                │
│     StartedAt       : timestamptz            │
│     CompletedAt     : timestamptz            │
│     TotalTests      : integer                │
│     PassedCount     : integer                │
│     FailedCount     : integer                │
│     SkippedCount    : integer                │
│     DurationMs      : bigint                 │
│     RedisKey        : varchar(200)           │
│     ResultsExpireAt : timestamptz            │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: EnvironmentId, Status, TestSuiteId, TriggeredById,
           (TestSuiteId, RunNumber) UNIQUE

┌─────────────────────────────────────┐  ┌─────────────────────────────────────────┐
│ AuditLogEntries (testexecution)     │  │ OutboxMessages (testexecution)          │
├─────────────────────────────────────┤  ├─────────────────────────────────────────┤
│ Standard audit table                │  │ Standard outbox                         │
└─────────────────────────────────────┘  │ Index: CreatedDateTime,                 │
                                         │        (Published, CreatedDateTime)      │
┌─────────────────────────────────────┐  └─────────────────────────────────────────┘
│ ArchivedOutboxMessages (testexec.)  │
├─────────────────────────────────────┤
│ Index: CreatedDateTime              │
└─────────────────────────────────────┘
```

**5 bảng**: ExecutionEnvironments, TestRuns, AuditLogEntries, OutboxMessages, ArchivedOutboxMessages

---

### 2.5 TestReporting Module — Schema: `testreporting`

```
┌──────────────────────────────────────────────┐
│            CoverageMetrics                    │
├──────────────────────────────────────────────┤
│ PK  Id                : UUID                 │
│     TestRunId         : UUID                 │  (cross-module, no FK)
│     TotalEndpoints    : integer              │
│     TestedEndpoints   : integer              │
│     CoveragePercent   : numeric(5,2)         │
│     ByMethod          : jsonb                │
│     ByTag             : jsonb                │
│     UncoveredPaths    : jsonb                │
│     CalculatedAt      : timestamptz          │
│     CreatedDateTime   : timestamptz          │
│     UpdatedDateTime   : timestamptz          │
│     RowVersion        : bytea                │
└──────────────────────────────────────────────┘
    Index: TestRunId

┌──────────────────────────────────────────────┐
│              TestReports                      │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     TestRunId       : UUID                   │  (cross-module, no FK)
│     GeneratedById   : UUID                   │  (cross-module, no FK)
│     FileId          : UUID                   │  (cross-module, no FK)
│     ReportType      : integer                │
│     Format          : integer                │
│     GeneratedAt     : timestamptz            │
│     ExpiresAt       : timestamptz            │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: FileId, GeneratedById, TestRunId

┌─────────────────────────────────────┐  ┌─────────────────────────────────────────┐
│ AuditLogEntries (testreporting)     │  │ OutboxMessages (testreporting)          │
├─────────────────────────────────────┤  ├─────────────────────────────────────────┤
│ Standard audit table                │  │ Index: CreatedDateTime,                 │
└─────────────────────────────────────┘  │        (Published, CreatedDateTime)      │
                                         └─────────────────────────────────────────┘
┌──────────────────────────────────────────┐
│ ArchivedOutboxMessages (testreporting)   │
├──────────────────────────────────────────┤
│ Index: CreatedDateTime                   │
└──────────────────────────────────────────┘
```

**5 bảng**: CoverageMetrics, TestReports, AuditLogEntries, OutboxMessages, ArchivedOutboxMessages

---

### 2.6 Subscription Module — Schema: `subscription`

```
┌──────────────────────────────────────────────┐
│           SubscriptionPlans                   │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     Name            : varchar(100)           │  Required, UNIQUE index
│     DisplayName     : varchar(200)           │
│     Description     : text                   │
│     PriceMonthly    : numeric(10,2)          │
│     PriceYearly     : numeric(10,2)          │
│     Currency        : varchar(3)             │
│     IsActive        : boolean                │
│     SortOrder       : integer                │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: Name (UNIQUE)
    Seed: 3 rows → Free (0 VND), Pro (299000/mo), Enterprise (999000/mo)
           │
           │ 1:N
           ▼
┌──────────────────────────────────────────────┐
│              PlanLimits                       │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│ FK  PlanId          : UUID → SubPlans        │  Cascade
│     LimitType       : integer                │  enum (0-7)
│     LimitValue      : integer (nullable)     │
│     IsUnlimited     : boolean                │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: PlanId, (PlanId, LimitType) UNIQUE
    Seed: 24 rows → 8 limits × 3 plans
      LimitType: 0=Projects, 1=ApiSpecs, 2=TestSuites, 3=TestCases,
                 4=Environments, 5=ReportRetentionDays, 6=TestRuns, 7=TestCaseVariables
      Free:       1, 10, 20, 50, 1, 7, 10, 100
      Pro:       10, 50, 100, 500, 3, 30, 100, 1000
      Enterprise: ∞, ∞, ∞, ∞, 10, 365, ∞, ∞

┌──────────────────────────────────────────────┐
│          UserSubscriptions                    │
├──────────────────────────────────────────────┤
│ PK  Id                : UUID                 │
│ FK  PlanId            : UUID → SubPlans      │  Restrict
│     UserId            : UUID                 │  (cross-module, no FK)
│     Status            : integer              │
│     BillingCycle      : integer (nullable)   │
│     StartDate         : date                 │
│     EndDate           : date (nullable)      │
│     NextBillingDate   : date (nullable)      │
│     TrialEndsAt       : timestamptz          │
│     CancelledAt       : timestamptz          │
│     AutoRenew         : boolean              │
│     ExternalSubId     : varchar(200)         │
│     ExternalCustId    : varchar(200)         │
│     SnapshotPlanName  : varchar(200)         │
│     SnapshotPriceMonthly : numeric(10,2)     │
│     SnapshotPriceYearly  : numeric(10,2)     │
│     SnapshotCurrency  : varchar(3)           │
│     CreatedDateTime   : timestamptz          │
│     UpdatedDateTime   : timestamptz          │
│     RowVersion        : bytea                │
└──────────────────────────────────────────────┘
    Index: PlanId, Status, UserId

┌──────────────────────────────────────────────┐
│           PaymentIntents                      │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│ FK  PlanId          : UUID → SubPlans        │  Restrict
│ FK  SubscriptionId  : UUID → UserSubs        │  SetNull (nullable)
│     UserId          : UUID                   │  (cross-module, no FK)
│     Amount          : numeric(18,2)          │
│     Currency        : varchar(3)             │  Required
│     BillingCycle    : varchar(10)            │  Required
│     Purpose         : varchar(30)            │  Required
│     Status          : varchar(20)            │  Required
│     OrderCode       : bigint (nullable)      │
│     CheckoutUrl     : varchar(500)           │
│     ExpiresAt       : timestamptz            │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: OrderCode (UNIQUE, filtered NOT NULL),
           PlanId, Status, SubscriptionId, UserId,
           (Status, CreatedDateTime), (Status, Purpose)

┌──────────────────────────────────────────────┐
│         PaymentTransactions                   │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│ FK  PaymentIntentId : UUID → PayIntents      │  SetNull (nullable)
│ FK  SubscriptionId  : UUID → UserSubs        │  Restrict
│     UserId          : UUID                   │  (cross-module, no FK)
│     Amount          : numeric(18,2)          │
│     Currency        : varchar(3)             │
│     Status          : integer                │
│     PaymentMethod   : varchar(50)            │
│     Provider        : varchar(20)            │
│     ProviderRef     : varchar(200)           │
│     ExternalTxnId   : varchar(200)           │
│     InvoiceUrl      : varchar(500)           │
│     FailureReason   : text                   │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: PaymentIntentId, Status, SubscriptionId, UserId,
           (Provider, ProviderRef) UNIQUE filtered NOT NULL

┌──────────────────────────────────────────────┐
│        SubscriptionHistories                  │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│ FK  SubscriptionId  : UUID → UserSubs        │  Cascade
│ FK  NewPlanId       : UUID → SubPlans        │  Restrict
│ FK  OldPlanId       : UUID → SubPlans        │  Restrict (nullable)
│     ChangeType      : integer                │
│     ChangeReason    : text                   │
│     EffectiveDate   : date                   │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: NewPlanId, OldPlanId, SubscriptionId

┌──────────────────────────────────────────────┐
│           UsageTrackings                      │  ← LƯU Ý: plural "Trackings"
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     UserId          : UUID                   │  (cross-module, no FK)
│     PeriodStart     : date                   │
│     PeriodEnd       : date                   │
│     ProjectCount    : integer                │
│     EndpointCount   : integer                │
│     TestSuiteCount  : integer                │
│     TestCaseCount   : integer                │
│     TestRunCount    : integer                │
│     LlmCallCount    : integer                │
│     StorageUsedMB   : numeric(10,2)          │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: UserId, (UserId, PeriodStart, PeriodEnd) UNIQUE

┌─────────────────────────────────────┐  ┌─────────────────────────────────────────┐
│ AuditLogEntries (subscription)      │  │ OutboxMessages (subscription)           │
├─────────────────────────────────────┤  ├─────────────────────────────────────────┤
│ Standard audit table                │  │ Standard outbox                         │
└─────────────────────────────────────┘  └─────────────────────────────────────────┘

┌──────────────────────────────────────────┐
│ ArchivedOutboxMessages (subscription)    │
└──────────────────────────────────────────┘
```

**10 bảng**: SubscriptionPlans, PlanLimits, UserSubscriptions, PaymentIntents, PaymentTransactions, SubscriptionHistories, UsageTrackings, AuditLogEntries, OutboxMessages, ArchivedOutboxMessages

---

### 2.7 Storage Module — Schema: `storage`

```
┌──────────────────────────────────────────────┐
│             FileEntries                       │  ← KHÔNG phải StorageFiles
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     OwnerId         : UUID (nullable)        │  (cross-module, no FK)
│     FileName        : text                   │
│     Name            : text                   │
│     ContentType     : varchar(100)           │
│     Size            : bigint                 │
│     FileLocation    : text                   │
│     Description     : text                   │
│     FileCategory    : integer                │  Default 3
│     Deleted         : boolean                │
│     DeletedDate     : timestamptz            │
│     Archived        : boolean                │
│     ArchivedDate    : timestamptz            │
│     Encrypted       : boolean                │
│     EncryptionKey   : text                   │
│     EncryptionIV    : text                   │
│     ExpiresAt       : timestamptz            │
│     UploadedTime    : timestamptz            │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: Deleted, FileCategory, OwnerId

┌──────────────────────────────────────────────┐
│          DeletedFileEntries                   │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     FileEntryId     : UUID                   │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘

┌─────────────────────────────────────┐  ┌──────────────────────────────────┐
│ AuditLogEntries (storage)           │  │ OutboxMessages (storage)         │
├─────────────────────────────────────┤  ├──────────────────────────────────┤
│ Standard audit table                │  │ Standard outbox                  │
└─────────────────────────────────────┘  └──────────────────────────────────┘

┌──────────────────────────────────────────┐
│ ArchivedOutboxMessages (storage)         │
└──────────────────────────────────────────┘
```

**5 bảng**: FileEntries, DeletedFileEntries, AuditLogEntries, OutboxMessages, ArchivedOutboxMessages

---

### 2.8 Notification Module — Schema: `notification`

```
┌──────────────────────────────────────────────┐
│            EmailMessages                      │
├──────────────────────────────────────────────┤
│ PK  Id                  : UUID               │
│     From                : text               │
│     Tos                 : text               │
│     CCs                 : text               │
│     BCCs                : text               │
│     Subject             : text               │
│     Body                : text               │
│     AttemptCount        : integer            │
│     MaxAttemptCount     : integer            │
│     SentDateTime        : timestamptz        │
│     ExpiredDateTime     : timestamptz        │
│     NextAttemptDateTime : timestamptz        │
│     CopyFromId          : UUID (nullable)    │
│     Log                 : text               │
│     CreatedDateTime     : timestamptz        │
│     UpdatedDateTime     : timestamptz        │
│     RowVersion          : bytea              │
└──────────────────────────────────────────────┘
    Index: CreatedDateTime,
           SentDateTime (INCLUDE: ExpiredDateTime, AttemptCount,
                         MaxAttemptCount, NextAttemptDateTime)
           │
           │ 1:N
           ▼
┌──────────────────────────────────────────────┐
│       EmailMessageAttachments                 │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│ FK  EmailMessageId  : UUID → EmailMessages   │  Cascade
│     FileEntryId     : UUID                   │  (cross-module ref)
│     Name            : text                   │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: EmailMessageId

┌──────────────────────────────────────────────┐
│             SmsMessages                       │
├──────────────────────────────────────────────┤
│ PK  Id                  : UUID               │
│     PhoneNumber         : text               │
│     Message             : text               │
│     AttemptCount        : integer            │
│     MaxAttemptCount     : integer            │
│     SentDateTime        : timestamptz        │
│     ExpiredDateTime     : timestamptz        │
│     NextAttemptDateTime : timestamptz        │
│     CopyFromId          : UUID (nullable)    │
│     Log                 : text               │
│     CreatedDateTime     : timestamptz        │
│     UpdatedDateTime     : timestamptz        │
│     RowVersion          : bytea              │
└──────────────────────────────────────────────┘
    Index: CreatedDateTime,
           SentDateTime (INCLUDE: ExpiredDateTime, AttemptCount,
                         MaxAttemptCount, NextAttemptDateTime)

┌──────────────────────────────────────────────┐
│        ArchivedEmailMessages                  │  ← same columns, no defaults
├──────────────────────────────────────────────┤
│    Index: CreatedDateTime                    │
└──────────────────────────────────────────────┘

┌──────────────────────────────────────────────┐
│         ArchivedSmsMessages                   │  ← same columns, no defaults
├──────────────────────────────────────────────┤
│    Index: CreatedDateTime                    │
└──────────────────────────────────────────────┘
```

**5 bảng**: EmailMessages, EmailMessageAttachments, SmsMessages, ArchivedEmailMessages, ArchivedSmsMessages

---

### 2.9 Configuration Module — Schema: `configuration`

```
┌──────────────────────────────────────────────┐
│         ConfigurationEntries                  │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     Key             : text                   │
│     Value           : text                   │
│     Description     : text                   │
│     IsSensitive     : boolean                │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Seed: 1 row → "SecurityHeaders:Test-Read-From-SqlServer"

┌──────────────────────────────────────────────┐
│         LocalizationEntries                   │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     Name            : text                   │
│     Value           : text                   │
│     Culture         : text                   │
│     Description     : text                   │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Seed: 2 rows → "Test"/en-US, "Test"/vi-VN
```

**2 bảng**: ConfigurationEntries, LocalizationEntries

---

### 2.10 AuditLog Module — Schema: `auditlog`

```
┌──────────────────────────────────────────────┐
│       AuditLogEntries (auditlog)              │  ← central audit log
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     UserId          : UUID                   │
│     Action          : text                   │
│     ObjectId        : text                   │
│     Log             : text                   │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘

┌──────────────────────────────────────────────┐
│         IdempotentRequests                    │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │
│     RequestType     : text                   │  Required
│     RequestId       : text                   │  Required
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: (RequestType, RequestId) UNIQUE
```

**2 bảng**: AuditLogEntries, IdempotentRequests

---

### 2.11 LlmAssistant Module — Schema: `llmassistant`

> **Lưu ý**: Module này có DbContext riêng (`LlmAssistantDbContext`) với schema `llmassistant`, nhưng **chưa có migration folder** trong `ClassifiedAds.Migrator`. Cấu trúc bảng dưới đây lấy từ entity configurations.

```
┌──────────────────────────────────────────────┐
│           LlmInteractions                     │
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │  default gen_random_uuid()
│     UserId          : UUID                   │  (cross-module, no FK)
│     InteractionType : integer                │  enum: 0=ScenarioSuggestion,
│                                               │        1=FailureExplanation,
│                                               │        2=DocumentationParsing
│     InputContext    : text                   │
│     LlmResponse    : text                   │
│     ModelUsed       : varchar(100)           │
│     TokensUsed      : integer               │
│     LatencyMs       : integer                │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: UserId, InteractionType, CreatedDateTime

┌──────────────────────────────────────────────┐
│         LlmSuggestionCaches                   │  ← LƯU Ý: plural "Caches"
├──────────────────────────────────────────────┤
│ PK  Id              : UUID                   │  default gen_random_uuid()
│     EndpointId      : UUID                   │  (cross-module, no FK)
│     SuggestionType  : integer                │  enum: 0=BoundaryCase,
│                                               │        1=NegativeCase,
│                                               │        2=HappyPath,
│                                               │        3=SecurityCase
│     CacheKey        : varchar(500)           │
│     Suggestions     : jsonb                  │
│     ExpiresAt       : timestamptz            │
│     CreatedDateTime : timestamptz            │
│     UpdatedDateTime : timestamptz            │
│     RowVersion      : bytea                  │
└──────────────────────────────────────────────┘
    Index: EndpointId, CacheKey, ExpiresAt

┌─────────────────────────────────────┐  ┌──────────────────────────────────┐
│ AuditLogEntries (llmassistant)      │  │ OutboxMessages (llmassistant)    │
├─────────────────────────────────────┤  ├──────────────────────────────────┤
│ Standard audit table                │  │ Index: CreatedDateTime,          │
└─────────────────────────────────────┘  │        (Published, CreatedDateTime)│
                                         └──────────────────────────────────┘
┌──────────────────────────────────────────┐
│ ArchivedOutboxMessages (llmassistant)    │
├──────────────────────────────────────────┤
│ Index: CreatedDateTime                   │
└──────────────────────────────────────────┘
```

**5 bảng**: LlmInteractions, LlmSuggestionCaches, AuditLogEntries, OutboxMessages, ArchivedOutboxMessages

---

## 3. Infrastructure Tables Pattern

Hầu hết các module đều có bộ infrastructure tables giống nhau:

| Table | Có trong modules | Mục đích |
|-------|-----------------|----------|
| `AuditLogEntries` | apidoc, testgen, testexecution, testreporting, subscription, storage, llmassistant | Per-module audit log |
| `OutboxMessages` | apidoc, testgen, testexecution, testreporting, subscription, storage, llmassistant | Transactional outbox pattern |
| `ArchivedOutboxMessages` | apidoc, testexecution, testreporting, subscription, storage, llmassistant | Archived outbox messages |

> Mỗi schema có bảng riêng — KHÔNG share AuditLogEntries/OutboxMessages giữa các schema. Module `auditlog` (schema `auditlog`) là central audit log cho cross-module queries.

---

## 4. Complete Mermaid ERD Diagram

### 4.1 Full ERD with All Tables

```mermaid
erDiagram
    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 1: IDENTITY — Schema: identity
    %% 9 tables (custom Identity, NOT AspNet* prefix)
    %% ═══════════════════════════════════════════════════════════════════════

    identity_Users {
        UUID Id PK "gen_random_uuid()"
        text UserName
        text NormalizedUserName
        text Email
        text NormalizedEmail
        boolean EmailConfirmed
        text PasswordHash
        text SecurityStamp
        text ConcurrencyStamp
        text PhoneNumber
        boolean PhoneNumberConfirmed
        boolean TwoFactorEnabled
        timestamptz LockoutEnd
        boolean LockoutEnabled
        integer AccessFailedCount
        text Auth0UserId
        text AzureAdB2CUserId
        timestamptz CreatedDateTime
        timestamptz UpdatedDateTime
        bytea RowVersion "ConcurrencyToken"
    }

    identity_Roles {
        UUID Id PK "gen_random_uuid()"
        text Name
        text NormalizedName
        text ConcurrencyStamp
        timestamptz CreatedDateTime
        timestamptz UpdatedDateTime
        bytea RowVersion
    }

    identity_UserRoles {
        UUID Id PK "gen_random_uuid()"
        UUID UserId FK "Users"
        UUID RoleId FK "Roles"
        timestamptz CreatedDateTime
        timestamptz UpdatedDateTime
        bytea RowVersion
    }

    identity_RoleClaims {
        UUID Id PK "gen_random_uuid()"
        UUID RoleId FK "Roles"
        text Type
        text Value
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    identity_UserClaims {
        UUID Id PK "gen_random_uuid()"
        UUID UserId FK "Users"
        text Type
        text Value
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    identity_UserLogins {
        UUID Id PK "gen_random_uuid()"
        UUID UserId FK "Users"
        text LoginProvider
        text ProviderKey
        text ProviderDisplayName
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    identity_UserTokens {
        UUID Id PK "gen_random_uuid()"
        UUID UserId FK "Users"
        text LoginProvider
        text TokenName
        text TokenValue
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    identity_UserProfiles {
        UUID Id PK "gen_random_uuid()"
        UUID UserId FK_UK "Users 1:1 UNIQUE"
        varchar_200 DisplayName
        varchar_500 AvatarUrl
        varchar_50 Timezone
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    identity_DataProtectionKeys {
        integer Id PK "auto-increment"
        text FriendlyName
        text Xml
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 2: API DOCUMENTATION — Schema: apidoc
    %% 10 tables
    %% ═══════════════════════════════════════════════════════════════════════

    apidoc_Projects {
        UUID Id PK "gen_random_uuid()"
        UUID ActiveSpecId FK "ApiSpecifications SetNull"
        UUID OwnerId "cross-module Guid"
        varchar_200 Name "Required"
        text Description
        varchar_500 BaseUrl
        varchar_20 Status "Required"
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    apidoc_ApiSpecifications {
        UUID Id PK "gen_random_uuid()"
        UUID ProjectId FK "Projects Cascade"
        UUID OriginalFileId "cross-module Guid"
        varchar_200 Name "Required"
        varchar_20 SourceType "Required"
        varchar_50 Version
        boolean IsActive
        timestamptz ParsedAt
        varchar_20 ParseStatus "Required"
        jsonb ParseErrors
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    apidoc_ApiEndpoints {
        UUID Id PK "gen_random_uuid()"
        UUID ApiSpecId FK "ApiSpecifications Cascade"
        varchar_10 HttpMethod "Required"
        varchar_500 Path "Required"
        varchar_200 OperationId
        varchar_500 Summary
        text Description
        jsonb Tags
        boolean IsDeprecated
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    apidoc_EndpointParameters {
        UUID Id PK "gen_random_uuid()"
        UUID EndpointId FK "ApiEndpoints Cascade"
        varchar_100 Name "Required"
        varchar_20 Location "Required"
        varchar_50 DataType
        varchar_50 Format
        boolean IsRequired
        text DefaultValue
        jsonb Schema
        jsonb Examples
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    apidoc_EndpointResponses {
        UUID Id PK "gen_random_uuid()"
        UUID EndpointId FK "ApiEndpoints Cascade"
        integer StatusCode
        text Description
        jsonb Schema
        jsonb Examples
        jsonb Headers
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    apidoc_EndpointSecurityReqs {
        UUID Id PK "gen_random_uuid()"
        UUID EndpointId FK "ApiEndpoints Cascade"
        varchar_20 SecurityType "Required"
        varchar_100 SchemeName
        jsonb Scopes
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    apidoc_SecuritySchemes {
        UUID Id PK "gen_random_uuid()"
        UUID ApiSpecId FK "ApiSpecifications Cascade"
        varchar_100 Name "Required"
        varchar_20 Type "Required"
        varchar_50 Scheme
        varchar_50 BearerFormat
        varchar_20 In
        varchar_100 ParameterName
        jsonb Configuration
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 3: TEST GENERATION — Schema: testgen
    %% 11 tables
    %% ═══════════════════════════════════════════════════════════════════════

    testgen_TestSuites {
        UUID Id PK "gen_random_uuid()"
        UUID ProjectId "cross-module Guid"
        UUID ApiSpecId "cross-module Guid nullable"
        varchar_200 Name "Required"
        text Description
        varchar_20 GenerationType "Required"
        varchar_20 Status "Required"
        varchar_30 ApprovalStatus "Required"
        timestamptz ApprovedAt
        UUID ApprovedById "nullable"
        UUID CreatedById "cross-module Guid"
        UUID LastModifiedById "nullable"
        jsonb SelectedEndpointIds "PrimitiveCollection"
        integer Version "Default 1"
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    testgen_TestCases {
        UUID Id PK "gen_random_uuid()"
        UUID TestSuiteId FK "TestSuites Cascade"
        UUID DependsOnId FK "TestCases SetNull self-ref"
        UUID EndpointId "cross-module Guid nullable"
        varchar_200 Name "Required"
        text Description
        varchar_20 TestType "Required"
        varchar_20 Priority "Required"
        boolean IsEnabled
        integer OrderIndex
        boolean IsOrderCustomized
        integer CustomOrderIndex "nullable"
        UUID LastModifiedById "nullable"
        jsonb Tags
        integer Version "Default 1"
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    testgen_TestCaseRequests {
        UUID Id PK "gen_random_uuid()"
        UUID TestCaseId FK_UK "TestCases 1:1 UNIQUE"
        varchar_10 HttpMethod "Required"
        varchar_1000 Url "Required"
        jsonb Headers
        jsonb PathParams
        jsonb QueryParams
        varchar_20 BodyType "Required"
        text Body
        integer Timeout
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    testgen_TestCaseExpectations {
        UUID Id PK "gen_random_uuid()"
        UUID TestCaseId FK_UK "TestCases 1:1 UNIQUE"
        jsonb ExpectedStatus
        jsonb ResponseSchema
        jsonb HeaderChecks
        jsonb BodyContains
        jsonb BodyNotContains
        jsonb JsonPathChecks
        integer MaxResponseTime "nullable"
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    testgen_TestCaseVariables {
        UUID Id PK "gen_random_uuid()"
        UUID TestCaseId FK "TestCases Cascade"
        varchar_100 VariableName "Required"
        varchar_20 ExtractFrom "Required"
        varchar_500 JsonPath
        varchar_100 HeaderName
        varchar_500 Regex
        text DefaultValue
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    testgen_TestDataSets {
        UUID Id PK "gen_random_uuid()"
        UUID TestCaseId FK "TestCases Cascade"
        varchar_100 Name "Required"
        jsonb Data "Required"
        boolean IsEnabled
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    testgen_TestCaseChangeLogs {
        UUID Id PK "gen_random_uuid()"
        UUID TestCaseId FK "TestCases Cascade"
        UUID ChangedById "cross-module Guid"
        varchar_30 ChangeType "Required"
        varchar_100 FieldName
        text OldValue
        text NewValue
        text ChangeReason
        integer VersionAfterChange
        varchar_45 IpAddress
        varchar_500 UserAgent
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    testgen_TestOrderProposals {
        UUID Id PK "gen_random_uuid()"
        UUID TestSuiteId FK "TestSuites Cascade"
        integer ProposalNumber
        varchar_20 Source "Required"
        varchar_30 Status "Required"
        jsonb ProposedOrder "Required"
        jsonb AppliedOrder
        jsonb UserModifiedOrder
        text AiReasoning
        jsonb ConsideredFactors
        varchar_100 LlmModel
        integer TokensUsed "nullable"
        UUID ReviewedById "nullable"
        timestamptz ReviewedAt
        text ReviewNotes
        timestamptz AppliedAt
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    testgen_TestSuiteVersions {
        UUID Id PK "gen_random_uuid()"
        UUID TestSuiteId FK "TestSuites Cascade"
        integer VersionNumber
        varchar_30 ChangeType "Required"
        text ChangeDescription
        UUID ChangedById "cross-module Guid"
        jsonb PreviousState
        jsonb NewState
        jsonb TestCaseOrderSnapshot
        varchar_30 ApprovalStatusSnapshot "Required"
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 4: TEST EXECUTION — Schema: testexecution
    %% 5 tables
    %% ═══════════════════════════════════════════════════════════════════════

    testexec_ExecutionEnvironments {
        UUID Id PK "gen_random_uuid()"
        UUID ProjectId "cross-module Guid"
        varchar_100 Name "Required"
        varchar_500 BaseUrl "Required"
        jsonb Variables
        jsonb Headers
        jsonb AuthConfig
        boolean IsDefault
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    testexec_TestRuns {
        UUID Id PK "gen_random_uuid()"
        UUID TestSuiteId "cross-module Guid"
        UUID EnvironmentId "cross-module Guid"
        UUID TriggeredById "cross-module Guid"
        integer RunNumber
        integer Status
        timestamptz StartedAt
        timestamptz CompletedAt
        integer TotalTests
        integer PassedCount
        integer FailedCount
        integer SkippedCount
        bigint DurationMs
        varchar_200 RedisKey
        timestamptz ResultsExpireAt
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 5: TEST REPORTING — Schema: testreporting
    %% 5 tables
    %% ═══════════════════════════════════════════════════════════════════════

    testreport_CoverageMetrics {
        UUID Id PK "gen_random_uuid()"
        UUID TestRunId "cross-module Guid"
        integer TotalEndpoints
        integer TestedEndpoints
        numeric_5_2 CoveragePercent
        jsonb ByMethod
        jsonb ByTag
        jsonb UncoveredPaths
        timestamptz CalculatedAt
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    testreport_TestReports {
        UUID Id PK "gen_random_uuid()"
        UUID TestRunId "cross-module Guid"
        UUID GeneratedById "cross-module Guid"
        UUID FileId "cross-module Guid"
        integer ReportType
        integer Format
        timestamptz GeneratedAt
        timestamptz ExpiresAt
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 6: SUBSCRIPTION — Schema: subscription
    %% 10 tables
    %% ═══════════════════════════════════════════════════════════════════════

    sub_SubscriptionPlans {
        UUID Id PK "gen_random_uuid()"
        varchar_100 Name "Required UNIQUE"
        varchar_200 DisplayName
        text Description
        numeric_10_2 PriceMonthly
        numeric_10_2 PriceYearly
        varchar_3 Currency
        boolean IsActive
        integer SortOrder
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    sub_PlanLimits {
        UUID Id PK "gen_random_uuid()"
        UUID PlanId FK "SubscriptionPlans Cascade"
        integer LimitType "enum 0-7"
        integer LimitValue "nullable"
        boolean IsUnlimited
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    sub_UserSubscriptions {
        UUID Id PK "gen_random_uuid()"
        UUID PlanId FK "SubscriptionPlans Restrict"
        UUID UserId "cross-module Guid"
        integer Status
        integer BillingCycle "nullable"
        date StartDate
        date EndDate "nullable"
        date NextBillingDate "nullable"
        timestamptz TrialEndsAt
        timestamptz CancelledAt
        boolean AutoRenew
        varchar_200 ExternalSubId
        varchar_200 ExternalCustId
        varchar_200 SnapshotPlanName
        numeric_10_2 SnapshotPriceMonthly
        numeric_10_2 SnapshotPriceYearly
        varchar_3 SnapshotCurrency
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    sub_PaymentIntents {
        UUID Id PK "gen_random_uuid()"
        UUID PlanId FK "SubscriptionPlans Restrict"
        UUID SubscriptionId FK "UserSubscriptions SetNull"
        UUID UserId "cross-module Guid"
        numeric_18_2 Amount
        varchar_3 Currency "Required"
        varchar_10 BillingCycle "Required"
        varchar_30 Purpose "Required"
        varchar_20 Status "Required"
        bigint OrderCode "UNIQUE filtered"
        varchar_500 CheckoutUrl
        timestamptz ExpiresAt
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    sub_PaymentTransactions {
        UUID Id PK "gen_random_uuid()"
        UUID PaymentIntentId FK "PaymentIntents SetNull"
        UUID SubscriptionId FK "UserSubscriptions Restrict"
        UUID UserId "cross-module Guid"
        numeric_18_2 Amount
        varchar_3 Currency
        integer Status
        varchar_50 PaymentMethod
        varchar_20 Provider
        varchar_200 ProviderRef
        varchar_200 ExternalTxnId
        varchar_500 InvoiceUrl
        text FailureReason
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    sub_SubscriptionHistories {
        UUID Id PK "gen_random_uuid()"
        UUID SubscriptionId FK "UserSubscriptions Cascade"
        UUID NewPlanId FK "SubscriptionPlans Restrict"
        UUID OldPlanId FK "SubscriptionPlans Restrict nullable"
        integer ChangeType
        text ChangeReason
        date EffectiveDate
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    sub_UsageTrackings {
        UUID Id PK "gen_random_uuid()"
        UUID UserId "cross-module Guid"
        date PeriodStart
        date PeriodEnd
        integer ProjectCount
        integer EndpointCount
        integer TestSuiteCount
        integer TestCaseCount
        integer TestRunCount
        integer LlmCallCount
        numeric_10_2 StorageUsedMB
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 7: STORAGE — Schema: storage
    %% 5 tables (FileEntries NOT StorageFiles)
    %% ═══════════════════════════════════════════════════════════════════════

    storage_FileEntries {
        UUID Id PK "gen_random_uuid()"
        UUID OwnerId "cross-module Guid nullable"
        text FileName
        text Name
        varchar_100 ContentType
        bigint Size
        text FileLocation
        text Description
        integer FileCategory "Default 3"
        boolean Deleted
        timestamptz DeletedDate
        boolean Archived
        timestamptz ArchivedDate
        boolean Encrypted
        text EncryptionKey
        text EncryptionIV
        timestamptz ExpiresAt
        timestamptz UploadedTime
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    storage_DeletedFileEntries {
        UUID Id PK "gen_random_uuid()"
        UUID FileEntryId
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 8: NOTIFICATION — Schema: notification
    %% 5 tables
    %% ═══════════════════════════════════════════════════════════════════════

    notif_EmailMessages {
        UUID Id PK "gen_random_uuid()"
        text From
        text Tos
        text CCs
        text BCCs
        text Subject
        text Body
        integer AttemptCount
        integer MaxAttemptCount
        timestamptz SentDateTime
        timestamptz ExpiredDateTime
        timestamptz NextAttemptDateTime
        UUID CopyFromId "nullable"
        text Log
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    notif_EmailMessageAttachments {
        UUID Id PK "gen_random_uuid()"
        UUID EmailMessageId FK "EmailMessages Cascade"
        UUID FileEntryId "cross-module Guid"
        text Name
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    notif_SmsMessages {
        UUID Id PK "gen_random_uuid()"
        text PhoneNumber
        text Message
        integer AttemptCount
        integer MaxAttemptCount
        timestamptz SentDateTime
        timestamptz ExpiredDateTime
        timestamptz NextAttemptDateTime
        UUID CopyFromId "nullable"
        text Log
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 9: CONFIGURATION — Schema: configuration
    %% 2 tables
    %% ═══════════════════════════════════════════════════════════════════════

    config_ConfigurationEntries {
        UUID Id PK "gen_random_uuid()"
        text Key
        text Value
        text Description
        boolean IsSensitive
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    config_LocalizationEntries {
        UUID Id PK "gen_random_uuid()"
        text Name
        text Value
        text Culture
        text Description
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 10: AUDIT LOG — Schema: auditlog
    %% 2 tables
    %% ═══════════════════════════════════════════════════════════════════════

    auditlog_AuditLogEntries {
        UUID Id PK "gen_random_uuid()"
        UUID UserId
        text Action
        text ObjectId
        text Log
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    auditlog_IdempotentRequests {
        UUID Id PK "gen_random_uuid()"
        text RequestType "Required"
        text RequestId "Required"
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 11: LLM ASSISTANT — Schema: llmassistant
    %% 5 tables (chưa có migration)
    %% ═══════════════════════════════════════════════════════════════════════

    llm_LlmInteractions {
        UUID Id PK "gen_random_uuid()"
        UUID UserId "cross-module Guid"
        integer InteractionType "enum 0-2"
        text InputContext
        text LlmResponse
        varchar_100 ModelUsed
        integer TokensUsed
        integer LatencyMs
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    llm_LlmSuggestionCaches {
        UUID Id PK "gen_random_uuid()"
        UUID EndpointId "cross-module Guid"
        integer SuggestionType "enum 0-3"
        varchar_500 CacheKey
        jsonb Suggestions
        timestamptz ExpiresAt
        timestamptz CreatedDateTime
        bytea RowVersion
    }

    %% ═══════════════════════════════════════════════════════════════════════
    %% INTRA-MODULE RELATIONSHIPS (actual FK constraints in DB)
    %% Cross-module references are Guid columns with indexes, NO FK
    %% ═══════════════════════════════════════════════════════════════════════

    %% Identity (intra-module FK)
    identity_Users ||--o{ identity_UserRoles : "has"
    identity_Roles ||--o{ identity_UserRoles : "assigned_to"
    identity_Users ||--o{ identity_UserClaims : "has"
    identity_Users ||--o{ identity_UserLogins : "has"
    identity_Users ||--o{ identity_UserTokens : "has"
    identity_Roles ||--o{ identity_RoleClaims : "has"
    identity_Users ||--o| identity_UserProfiles : "has 1:1"

    %% ApiDocumentation (intra-module FK)
    apidoc_Projects ||--o{ apidoc_ApiSpecifications : "contains"
    apidoc_Projects ||--o| apidoc_ApiSpecifications : "active_spec"
    apidoc_ApiSpecifications ||--o{ apidoc_ApiEndpoints : "defines"
    apidoc_ApiSpecifications ||--o{ apidoc_SecuritySchemes : "includes"
    apidoc_ApiEndpoints ||--o{ apidoc_EndpointParameters : "has"
    apidoc_ApiEndpoints ||--o{ apidoc_EndpointResponses : "returns"
    apidoc_ApiEndpoints ||--o{ apidoc_EndpointSecurityReqs : "requires"

    %% TestGeneration (intra-module FK)
    testgen_TestSuites ||--o{ testgen_TestCases : "contains"
    testgen_TestSuites ||--o{ testgen_TestOrderProposals : "has"
    testgen_TestSuites ||--o{ testgen_TestSuiteVersions : "versions"
    testgen_TestCases ||--o| testgen_TestCases : "depends_on self"
    testgen_TestCases ||--|| testgen_TestCaseRequests : "request 1:1"
    testgen_TestCases ||--|| testgen_TestCaseExpectations : "expectations 1:1"
    testgen_TestCases ||--o{ testgen_TestCaseVariables : "extracts"
    testgen_TestCases ||--o{ testgen_TestDataSets : "uses"
    testgen_TestCases ||--o{ testgen_TestCaseChangeLogs : "tracks"

    %% Subscription (intra-module FK)
    sub_SubscriptionPlans ||--o{ sub_PlanLimits : "defines"
    sub_SubscriptionPlans ||--o{ sub_UserSubscriptions : "subscribed_to"
    sub_UserSubscriptions ||--o{ sub_SubscriptionHistories : "tracks"
    sub_SubscriptionPlans ||--o{ sub_SubscriptionHistories : "new_plan"
    sub_SubscriptionPlans ||--o{ sub_SubscriptionHistories : "old_plan"
    sub_SubscriptionPlans ||--o{ sub_PaymentIntents : "for_plan"
    sub_UserSubscriptions ||--o{ sub_PaymentIntents : "for_sub"
    sub_PaymentIntents ||--o{ sub_PaymentTransactions : "fulfills"
    sub_UserSubscriptions ||--o{ sub_PaymentTransactions : "for_sub"

    %% Notification (intra-module FK)
    notif_EmailMessages ||--o{ notif_EmailMessageAttachments : "has"
```

### 4.2 Table Naming Convention

| Convention | Example | Rule |
|------------|---------|------|
| **Identity Tables** | `Users`, `Roles`, `UserRoles` | PascalCase, Plural — **KHÔNG dùng prefix AspNet** |
| **Business Tables** | `Projects`, `TestSuites` | PascalCase, Plural |
| **1:1 Detail Tables** | `TestCaseRequests`, `TestCaseExpectations` | Parent + Detail name |
| **History/Audit Tables** | `SubscriptionHistories`, `TestCaseChangeLogs` | Suffix Histories/ChangeLogs |
| **Tracking Tables** | `UsageTrackings`, `CoverageMetrics` | Descriptive name, plural |
| **Infrastructure** | `OutboxMessages`, `AuditLogEntries` | Shared pattern per module |
| **Archived** | `ArchivedOutboxMessages`, `ArchivedEmailMessages` | Prefix `Archived` |

### 4.3 Table Count Summary (chính xác theo snapshot + config)

| # | Module | Schema | Business Tables | Infra Tables | Total |
|---|--------|--------|----------------|--------------|-------|
| 1 | Identity | `identity` | 9 | 0 | **9** |
| 2 | ApiDocumentation | `apidoc` | 7 | 3 | **10** |
| 3 | TestGeneration | `testgen` | 9 | 2 | **11** |
| 4 | TestExecution | `testexecution` | 2 | 3 | **5** |
| 5 | TestReporting | `testreporting` | 2 | 3 | **5** |
| 6 | Subscription | `subscription` | 7 | 3 | **10** |
| 7 | Storage | `storage` | 2 | 3 | **5** |
| 8 | Notification | `notification` | 5 | 0 | **5** |
| 9 | Configuration | `configuration` | 2 | 0 | **2** |
| 10 | AuditLog | `auditlog` | 2 | 0 | **2** |
| 11 | LlmAssistant | `llmassistant` | 2 | 3 | **5** |
| | **TOTAL** | **11 schemas** | **49** | **20** | **69** |

> **Ghi chú**: Infra tables (AuditLogEntries, OutboxMessages, ArchivedOutboxMessages) được tính riêng vì mỗi schema có bản copy riêng. Con số 69 là tổng bảng thực tế nếu tính cả infra tables lặp lại. Nếu tính unique business entities: **49 bảng**.

---

## 5. Redis Schema cho Test Results (Hot Storage)

### 5.1 Key Patterns

| Key Pattern | Data Type | TTL | Purpose |
|-------------|-----------|-----|---------|
| `testrun:{id}:results` | HASH | 5-10 days | Run summary & individual results |
| `testrun:{id}:execution:{testId}` | HASH | 5-10 days | Individual test execution detail |
| `testrun:{id}:logs` | LIST | 5-10 days | Execution logs |
| `testrun:{id}:variables` | HASH | 5-10 days | Runtime extracted variables |
| `user:{id}:recent_runs` | SORTED SET | 5-10 days | Quick access to recent runs |

### 5.2 TTL Configuration

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         REDIS TTL STRATEGY                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Test execution completes                                                   │
│          │                                                                   │
│          ▼                                                                   │
│   ┌─────────────────┐                      ┌─────────────────┐              │
│   │     REDIS       │                      │   PostgreSQL    │              │
│   │  (Hot Storage)  │                      │ (Cold Storage)  │              │
│   │                 │                      │                 │              │
│   │ • Real-time     │    Background job    │ • TestRun       │              │
│   │   results       │ ──────────────────►  │   summary       │              │
│   │ • Execution     │    (before TTL)      │ • Aggregated    │              │
│   │   logs          │                      │   metrics       │              │
│   │ • TTL: 5-10 days│                      │ • Report refs   │              │
│   └─────────────────┘                      └─────────────────┘              │
│                                                                              │
│   TTL is controlled per subscription tier:                                   │
│   • Free:       ReportRetentionDays = 7                                      │
│   • Pro:        ReportRetentionDays = 30                                     │
│   • Enterprise: ReportRetentionDays = 365                                    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Subscription Tiers & Seed Data

### 6.1 Plan Configuration (từ seed data trong SubscriptionDbContextModelSnapshot)

| Plan | PriceMonthly | PriceYearly | Currency |
|------|-------------|-------------|----------|
| Free | 0 | 0 | VND |
| Pro | 299,000 | — | VND |
| Enterprise | 999,000 | — | VND |

### 6.2 PlanLimits Seed (LimitType enum → integer)

| LimitType | Free | Pro | Enterprise |
|-----------|------|-----|------------|
| 0 = Projects | 1 | 10 | ∞ |
| 1 = ApiSpecs | 10 | 50 | ∞ |
| 2 = TestSuites | 20 | 100 | ∞ |
| 3 = TestCases | 50 | 500 | ∞ |
| 4 = Environments | 1 | 3 | 10 |
| 5 = ReportRetentionDays | 7 | 30 | 365 |
| 6 = TestRuns | 10 | 100 | ∞ |
| 7 = TestCaseVariables | 100 | 1000 | ∞ |

### 6.3 User Registration Flow

```
   User clicks "Sign Up"
           │
           ▼
   ┌───────────────────────────────────────────────────────────┐
   │  STEP 1: Create User in identity.Users                    │
   │  + UserProfile + assign Role "User"                       │
   └───────────────────────────────────────────────────────────┘
           │
           ▼
   ┌───────────────────────────────────────────────────────────┐
   │  STEP 2: Auto-assign Free Plan                            │
   │  → subscription.UserSubscriptions (Status=Active)         │
   └───────────────────────────────────────────────────────────┘
           │
           ▼
   ┌───────────────────────────────────────────────────────────┐
   │  STEP 3: Initialize Usage Tracking                        │
   │  → subscription.UsageTrackings (all counts = 0)           │
   └───────────────────────────────────────────────────────────┘
```

---

## 7. Comparison: Old ERD vs Actual Codebase

| Issue | ERD cũ (v1) | Codebase thực tế |
|-------|-------------|-----------------|
| Identity table names | `AspNetUsers`, `AspNetRoles`, ... | `Users`, `Roles`, ... (schema `identity`) |
| Identity PK types | `INT IDENTITY` cho Claims | `UUID gen_random_uuid()` cho tất cả |
| Storage table | `StorageFiles` | `FileEntries` + `DeletedFileEntries` |
| Usage tracking | `UsageTracking` (singular) | `UsageTrackings` (plural) |
| LLM cache | `LlmSuggestionCache` (singular) | `LlmSuggestionCaches` (plural) |
| Thiếu `PaymentIntents` | ✗ | ✓ — subscription schema |
| Thiếu `TestSuiteVersions` | ✗ (chỉ có trong section 6.1) | ✓ — testgen schema |
| Thiếu `TestOrderProposals` | ✗ | ✓ — testgen schema |
| Thiếu `TestCaseChangeLogs` | ✗ | ✓ — testgen schema |
| Thiếu module Notification | ✗ | ✓ — 5 bảng (notification schema) |
| Thiếu module Configuration | ✗ | ✓ — 2 bảng (configuration schema) |
| Thiếu module AuditLog | ✗ | ✓ — 2 bảng (auditlog schema) |
| Thiếu module LlmAssistant (separate) | Gộp chung section | ✓ — schema riêng `llmassistant` |
| Thiếu infra tables | ✗ | AuditLogEntries, OutboxMessages, ArchivedOutboxMessages per schema |
| Cross-module FK | Vẽ FK liên module | Chỉ Guid + index, **không có FK** liên module |
| Data types | NVARCHAR, BIT, DATETIMEOFFSET (SQL Server) | text, boolean, timestamptz (PostgreSQL) |
| Table count | 34 | 69 (49 business + 20 infra) |

---

## 8. AGENTS Compliance Report

| Requirement | Status |
|-------------|--------|
| Target DB identified | ✓ `ClassifiedAds` from `ConnectionStrings__Default` in `.env` |
| Schemas checked | ✓ 11 schemas: identity, apidoc, testgen, testexecution, testreporting, subscription, storage, notification, configuration, auditlog, llmassistant |
| Migration IDs listed | ✓ 10 modules have latest migration IDs; LlmAssistant has no migration folder |
| Preflight SQL executed | ✗ Not executed — static audit only, no DB runtime access |
| Post-change verification | ✗ Not applicable — no migration/seed was run |
| Runtime mode confirmed | N/A — document generation only |
