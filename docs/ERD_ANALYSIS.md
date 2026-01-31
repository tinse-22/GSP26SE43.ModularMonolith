# ERD Analysis - API Testing Automation System

## 1. Overview

Tài liệu này phân tích chi tiết Entity-Relationship Diagram (ERD) cho hệ thống API Testing Automation dựa trên requirements đã định nghĩa.

### 1.1 Storage Strategy

| Data Type | Storage | Retention | Lý do |
|-----------|---------|-----------|-------|
| **User, Project, ApiSpec** | PostgreSQL | Permanent | Core business data |
| **TestCase, TestSuite** | PostgreSQL | Permanent | Reusable test definitions |
| **TestRun Results** | Redis → PostgreSQL | 5-10 days in Redis | Hot data for real-time access |
| **TestExecution Logs** | Redis | 5-10 days | Temporary, high-frequency writes |
| **Reports (generated)** | File Storage + PostgreSQL metadata | Permanent | User-requested exports |

### 1.2 Redis Caching Strategy cho Test Results

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        REDIS CACHING ARCHITECTURE                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   User executes tests                                                        │
│          │                                                                   │
│          ▼                                                                   │
│   ┌─────────────────┐                                                       │
│   │  Test Executor  │                                                       │
│   └─────────────────┘                                                       │
│          │                                                                   │
│          ├──────────────────────────────────────────┐                       │
│          ▼                                          ▼                       │
│   ┌─────────────────┐                      ┌─────────────────┐              │
│   │     REDIS       │                      │   PostgreSQL    │              │
│   │  (Hot Storage)  │                      │ (Cold Storage)  │              │
│   │                 │                      │                 │              │
│   │ • Real-time     │    After 5-10 days   │ • TestRun       │              │
│   │   results       │ ──────────────────►  │   summary only  │              │
│   │ • Execution     │    (Background Job)  │ • Aggregated    │              │
│   │   logs          │                      │   metrics       │              │
│   │ • TTL: 5-10 days│                      │ • Report refs   │              │
│   └─────────────────┘                      └─────────────────┘              │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Domain Modules & Entities

### 2.1 Module Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           MODULE ARCHITECTURE                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────┐     ┌─────────────────┐     ┌─────────────────┐          │
│   │  Identity   │     │ ApiDocumentation│     │  TestGeneration │          │
│   │  Module     │     │     Module      │     │     Module      │          │
│   └─────────────┘     └─────────────────┘     └─────────────────┘          │
│         │                     │                       │                     │
│         │                     ▼                       │                     │
│         │              ┌─────────────────┐            │                     │
│         └──────────────│   TestExecution │◄───────────┘                     │
│                        │      Module     │                                  │
│                        └─────────────────┘                                  │
│                               │                                             │
│                               ▼                                             │
│                        ┌─────────────────┐     ┌─────────────────┐          │
│                        │  TestReporting  │     │   Subscription  │          │
│                        │     Module      │     │     Module      │          │
│                        └─────────────────┘     └─────────────────┘          │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Detailed ERD by Module

### 3.1 Identity Module (ASP.NET Core Identity)

Sử dụng **ASP.NET Core Identity** library với các tables mặc định + custom extensions:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ASP.NET CORE IDENTITY TABLES                             │
│                    (Built-in - DO NOT MODIFY SCHEMA)                        │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│            AspNetUsers                   │  ← IdentityUser<Guid>
├─────────────────────────────────────────┤
│ PK  Id                    : GUID        │
│     UserName              : NVARCHAR(256)│
│     NormalizedUserName    : NVARCHAR(256)│
│     Email                 : NVARCHAR(256)│
│     NormalizedEmail       : NVARCHAR(256)│
│     EmailConfirmed        : BIT         │
│     PasswordHash          : NVARCHAR(MAX)│
│     SecurityStamp         : NVARCHAR(MAX)│
│     ConcurrencyStamp      : NVARCHAR(MAX)│
│     PhoneNumber           : NVARCHAR(MAX)│
│     PhoneNumberConfirmed  : BIT         │
│     TwoFactorEnabled      : BIT         │
│     LockoutEnd            : DATETIMEOFFSET│
│     LockoutEnabled        : BIT         │
│     AccessFailedCount     : INT         │
└─────────────────────────────────────────┘
           │
           │ 1:N
           ▼
┌─────────────────────────────────────────┐
│           AspNetUserRoles                │
├─────────────────────────────────────────┤
│ PK  UserId          : GUID (FK)         │
│ PK  RoleId          : GUID (FK)         │
└─────────────────────────────────────────┘
           │
           │ N:1
           ▼
┌─────────────────────────────────────────┐
│            AspNetRoles                   │  ← IdentityRole<Guid>
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│     Name            : NVARCHAR(256)     │
│     NormalizedName  : NVARCHAR(256)     │
│     ConcurrencyStamp: NVARCHAR(MAX)     │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│          AspNetUserClaims                │
├─────────────────────────────────────────┤
│ PK  Id              : INT (Identity)    │
│ FK  UserId          : GUID              │
│     ClaimType       : NVARCHAR(MAX)     │
│     ClaimValue      : NVARCHAR(MAX)     │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│          AspNetUserLogins                │  ← External logins (Google, etc.)
├─────────────────────────────────────────┤
│ PK  LoginProvider       : NVARCHAR(128) │
│ PK  ProviderKey         : NVARCHAR(128) │
│ FK  UserId              : GUID          │
│     ProviderDisplayName : NVARCHAR(MAX) │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│          AspNetUserTokens                │  ← 2FA, refresh tokens
├─────────────────────────────────────────┤
│ PK  UserId          : GUID (FK)         │
│ PK  LoginProvider   : NVARCHAR(128)     │
│ PK  Name            : NVARCHAR(128)     │
│     Value           : NVARCHAR(MAX)     │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│          AspNetRoleClaims                │
├─────────────────────────────────────────┤
│ PK  Id              : INT (Identity)    │
│ FK  RoleId          : GUID              │
│     ClaimType       : NVARCHAR(MAX)     │
│     ClaimValue      : NVARCHAR(MAX)     │
└─────────────────────────────────────────┘
```

### 3.1.1 Custom User Extension (Optional)

Nếu cần thêm fields cho User (profile info), có thể extend `IdentityUser`:

```
┌─────────────────────────────────────────┐
│          UserProfiles                    │  ← Custom extension table
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  UserId          : GUID → AspNetUsers│  ← 1:1 relationship
│     DisplayName     : NVARCHAR(200)     │
│     AvatarUrl       : NVARCHAR(500)     │
│     Timezone        : NVARCHAR(50)      │
│     CreatedAt       : TIMESTAMP         │
│     UpdatedAt       : TIMESTAMP         │
└─────────────────────────────────────────┘
```

**Hoặc** extend trực tiếp `IdentityUser<Guid>`:

```csharp
public class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Timezone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<Project> Projects { get; set; }
    public virtual UserSubscription? Subscription { get; set; }
}
```

**Roles định nghĩa (Seed Data):**
- `Admin` - Quản lý hệ thống, users
- `User` - Default role cho tất cả users (upload docs, config tests, execute, view reports)

---

### 3.2 Storage Module (File Management)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             STORAGE MODULE                                  │
│                           (Local Storage Only)                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│              FileEntries                 │  (FE-02, FE-10)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  OwnerId         : GUID → AspNetUsers│  ← Who uploaded
│     FileName        : VARCHAR(255)      │  ← Original filename
│     ContentType     : VARCHAR(100)      │  ← MIME type (application/json, etc.)
│     FileSize        : BIGINT            │  ← Size in bytes
│     StoragePath     : VARCHAR(500)      │  ← Relative path on disk
│     FileCategory    : ENUM              │  ← ApiSpec/Report/Export/Attachment
│     IsDeleted       : BOOLEAN           │  ← Soft delete
│     DeletedAt       : TIMESTAMP         │
│     CreatedDateTime : TIMESTAMP         │  ← From base Entity
│     UpdatedDateTime : TIMESTAMP         │  ← From base Entity
│     ExpiresAt       : TIMESTAMP         │  ← Auto-delete after (for temp files)
└─────────────────────────────────────────┘
```

**Storage Configuration:**

```json
{
  "Storage": {
    "BasePath": "./uploads"
  }
}
```

**File Categories:**
- `ApiSpec` - OpenAPI/Postman/Swagger files
- `Report` - Generated PDF/CSV reports
- `Export` - Exported test results
- `Attachment` - User attachments

---

### 3.3 ApiDocumentation Module

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            APIDOCUMENTATION MODULE                          │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│               Projects                   │  (FE-01, FE-02)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  OwnerId         : GUID → AspNetUsers│
│ FK  ActiveSpecId    : GUID → ApiSpecs   │  ← NEW: Default spec for testing
│     Name            : VARCHAR(200)      │
│     Description     : TEXT              │
│     BaseUrl         : VARCHAR(500)      │  ← Default execution URL
│     Status          : ENUM              │  ← Active/Archived
│     CreatedDateTime : TIMESTAMP         │
│     UpdatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘
           │
           │ 1:N
           ▼
┌─────────────────────────────────────────┐
│            ApiSpecifications             │  (FE-02, FE-03)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  ProjectId       : GUID → Projects   │
│ FK  OriginalFileId  : GUID → StorageFiles│  ← Reference to uploaded file
│     Name            : VARCHAR(200)      │
│     SourceType      : ENUM              │  ← OpenAPI/Postman/Manual/cURL
│     Version         : VARCHAR(50)       │
│     IsActive        : BOOLEAN           │  ← NEW: Active version for this project
│     ParsedAt        : TIMESTAMP         │
│     ParseStatus     : ENUM              │  ← Pending/Success/Failed
│     ParseErrors     : JSONB             │  ← Parse error details
│     CreatedDateTime : TIMESTAMP         │
│     UpdatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘
           │
           │ 1:N
           ▼
┌─────────────────────────────────────────┐
│              ApiEndpoints                │  (FE-03)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  ApiSpecId       : GUID → ApiSpecs   │
│     HttpMethod      : ENUM              │  ← GET/POST/PUT/DELETE/PATCH
│     Path            : VARCHAR(500)      │  ← /api/users/{id}
│     OperationId     : VARCHAR(200)      │  ← Optional OpenAPI operationId
│     Summary         : VARCHAR(500)      │
│     Description     : TEXT              │
│     Tags            : VARCHAR[]         │  ← Array of tags
│     IsDeprecated    : BOOLEAN           │
│     CreatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘
           │
           ├─────────────────────────────────────────────┐
           │ 1:N                                         │ 1:N
           ▼                                             ▼
┌─────────────────────────────────┐      ┌─────────────────────────────────┐
│       EndpointParameters         │      │      EndpointSecurityReqs        │
├─────────────────────────────────┤      ├─────────────────────────────────┤
│ PK  Id           : GUID         │      │ PK  Id             : GUID       │
│ FK  EndpointId   : GUID         │      │ FK  EndpointId     : GUID       │
│     Name         : VARCHAR(100) │      │     SecurityType   : ENUM       │ ← Bearer/ApiKey/OAuth2/Basic
│     Location     : ENUM         │      │     SchemeName     : VARCHAR    │
│         ← Path/Query/Header/Body│      │     Scopes         : VARCHAR[]  │
│     DataType     : VARCHAR(50)  │      └─────────────────────────────────┘
│     Format       : VARCHAR(50)  │
│     IsRequired   : BOOLEAN      │
│     DefaultValue : TEXT         │
│     Schema       : JSONB        │  ← Full JSON Schema
│     Examples     : JSONB        │  ← Example values
└─────────────────────────────────┘

┌─────────────────────────────────────────┐
│          EndpointResponses               │
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  EndpointId      : GUID              │
│     StatusCode      : INT               │  ← 200, 400, 401, 404, 500
│     Description     : TEXT              │
│     Schema          : JSONB             │  ← Response JSON Schema
│     Examples        : JSONB             │
│     Headers         : JSONB             │  ← Expected response headers
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│         SecuritySchemes                  │  (Per ApiSpec)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  ApiSpecId       : GUID              │
│     Name            : VARCHAR(100)      │  ← "bearerAuth", "apiKey"
│     Type            : ENUM              │  ← http/apiKey/oauth2/openIdConnect
│     Scheme          : VARCHAR(50)       │  ← "bearer", "basic"
│     BearerFormat    : VARCHAR(50)       │  ← "JWT"
│     In              : ENUM              │  ← header/query/cookie (for apiKey)
│     ParameterName   : VARCHAR(100)      │  ← "Authorization", "X-API-Key"
│     Configuration   : JSONB             │  ← OAuth2 flows, OpenID config
└─────────────────────────────────────────┘
```

### 3.3.1 Flow Upload & Parse OpenAPI/Scalar File

Khi người dùng upload **1 file OpenAPI/Scalar** chứa nhiều API endpoints, hệ thống xử lý như sau:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    OPENAPI FILE UPLOAD & PARSE FLOW                         │
└─────────────────────────────────────────────────────────────────────────────┘

   User uploads: petstore-api.yaml (50 endpoints)
                          │
                          ▼
   ┌──────────────────────────────────────────────────────────────────────────┐
   │  STEP 1: Store Original File                                             │
   │  ┌────────────────────────────────────────────────────────────────────┐  │
   │  │  Storage Module (File Storage - Azure Blob/S3/Local)               │  │
   │  │  • FileId: file-guid-001                                           │  │
   │  │  • FileName: petstore-api.yaml                                     │  │
   │  │  • ContentType: application/x-yaml                                 │  │
   │  │  • Size: 125KB                                                     │  │
   │  └────────────────────────────────────────────────────────────────────┘  │
   └──────────────────────────────────────────────────────────────────────────┘
                          │
                          ▼
   ┌──────────────────────────────────────────────────────────────────────────┐
   │  STEP 2: Create ApiSpecification Record (PostgreSQL)                     │
   │  ┌────────────────────────────────────────────────────────────────────┐  │
   │  │  ApiSpecifications                                                 │  │
   │  │  • Id: spec-guid-001                                               │  │
   │  │  • ProjectId: project-abc                                          │  │
   │  │  • Name: "PetStore API v3"                                         │  │
   │  │  • SourceType: OpenAPI                                             │  │
   │  │  • OriginalFileId: file-guid-001  ← Reference to stored file       │  │
   │  │  • Version: "3.0.0"                                                │  │
   │  │  • ParseStatus: Pending → Processing → Success/Failed              │  │
   │  └────────────────────────────────────────────────────────────────────┘  │
   └──────────────────────────────────────────────────────────────────────────┘
                          │
                          ▼
   ┌──────────────────────────────────────────────────────────────────────────┐
   │  STEP 3: Parse & Extract Endpoints (Background Job)                      │
   │                                                                          │
   │  OpenAPI Parser reads file → Extracts:                                   │
   │  • paths (endpoints)                                                     │
   │  • components/schemas (data models)                                      │
   │  • securitySchemes (auth methods)                                        │
   │  • servers (base URLs)                                                   │
   └──────────────────────────────────────────────────────────────────────────┘
                          │
                          ▼
   ┌──────────────────────────────────────────────────────────────────────────┐
   │  STEP 4: Insert Extracted Data (PostgreSQL)                              │
   │                                                                          │
   │  ┌─────────────────────────────────────────────────────────────────┐     │
   │  │  ApiEndpoints (50 records từ 1 file)                            │     │
   │  │  ┌───────────────────────────────────────────────────────────┐  │     │
   │  │  │ Id: ep-001, ApiSpecId: spec-guid-001                      │  │     │
   │  │  │ HttpMethod: GET, Path: /pets                              │  │     │
   │  │  │ OperationId: listPets, Summary: "List all pets"           │  │     │
   │  │  │ Tags: ["pets"], IsDeprecated: false                       │  │     │
   │  │  └───────────────────────────────────────────────────────────┘  │     │
   │  │  ┌───────────────────────────────────────────────────────────┐  │     │
   │  │  │ Id: ep-002, ApiSpecId: spec-guid-001                      │  │     │
   │  │  │ HttpMethod: POST, Path: /pets                             │  │     │
   │  │  │ OperationId: createPet, Summary: "Create a pet"           │  │     │
   │  │  └───────────────────────────────────────────────────────────┘  │     │
   │  │  ┌───────────────────────────────────────────────────────────┐  │     │
   │  │  │ Id: ep-003, ApiSpecId: spec-guid-001                      │  │     │
   │  │  │ HttpMethod: GET, Path: /pets/{petId}                      │  │     │
   │  │  │ OperationId: getPetById, Summary: "Find pet by ID"        │  │     │
   │  │  └───────────────────────────────────────────────────────────┘  │     │
   │  │  ... (47 more endpoints)                                        │     │
   │  └─────────────────────────────────────────────────────────────────┘     │
   │                                                                          │
   │  ┌─────────────────────────────────────────────────────────────────┐     │
   │  │  EndpointParameters (N records per endpoint)                    │     │
   │  │  • ep-003: petId (path, required, integer)                      │     │
   │  │  • ep-001: limit (query, optional, integer, default: 20)        │     │
   │  │  • ep-001: status (query, optional, enum: available|pending)    │     │
   │  └─────────────────────────────────────────────────────────────────┘     │
   │                                                                          │
   │  ┌─────────────────────────────────────────────────────────────────┐     │
   │  │  EndpointResponses (N records per endpoint)                     │     │
   │  │  • ep-001: 200 OK → Schema: array of Pet                        │     │
   │  │  • ep-001: 400 Bad Request → Schema: Error                      │     │
   │  │  • ep-002: 201 Created → Schema: Pet                            │     │
   │  │  • ep-002: 422 Validation Error → Schema: ValidationErrors      │     │
   │  └─────────────────────────────────────────────────────────────────┘     │
   │                                                                          │
   │  ┌─────────────────────────────────────────────────────────────────┐     │
   │  │  SecuritySchemes (extracted from components/securitySchemes)    │     │
   │  │  • bearerAuth: type=http, scheme=bearer, bearerFormat=JWT       │     │
   │  │  • apiKey: type=apiKey, in=header, name=X-API-Key               │     │
   │  └─────────────────────────────────────────────────────────────────┘     │
   └──────────────────────────────────────────────────────────────────────────┘
                          │
                          ▼
   ┌──────────────────────────────────────────────────────────────────────────┐
   │  STEP 5: Update Parse Status                                             │
   │  • ApiSpecifications.ParseStatus = Success                               │
   │  • ApiSpecifications.ParsedAt = 2026-01-26T10:00:00Z                     │
   │  • ApiSpecifications.ParseErrors = null                                  │
   └──────────────────────────────────────────────────────────────────────────┘
```

**Ví dụ Query Lấy Endpoints từ 1 File:**

```sql
-- Lấy tất cả endpoints từ file đã upload
SELECT 
    e.Id,
    e.HttpMethod,
    e.Path,
    e.OperationId,
    e.Summary,
    e.Tags
FROM ApiEndpoints e
JOIN ApiSpecifications s ON e.ApiSpecId = s.Id
WHERE s.Id = 'spec-guid-001'  -- hoặc s.Name = 'PetStore API v3'
ORDER BY e.Path, e.HttpMethod;

-- Kết quả: 50 rows (1 row per endpoint)
-- | Id     | HttpMethod | Path            | OperationId  | Summary          |
-- |--------|------------|-----------------|--------------|------------------|
-- | ep-001 | GET        | /pets           | listPets     | List all pets    |
-- | ep-002 | POST       | /pets           | createPet    | Create a pet     |
-- | ep-003 | GET        | /pets/{petId}   | getPetById   | Find pet by ID   |
-- | ep-004 | PUT        | /pets/{petId}   | updatePet    | Update a pet     |
-- | ep-005 | DELETE     | /pets/{petId}   | deletePet    | Delete a pet     |
-- | ...    | ...        | ...             | ...          | ...              |
```

**Relationship Summary:**

| Quan hệ | Cardinality | Giải thích |
|---------|-------------|------------|
| `ApiSpecifications` → `ApiEndpoints` | 1:N | 1 file OpenAPI → nhiều endpoints |
| `ApiEndpoints` → `EndpointParameters` | 1:N | 1 endpoint → nhiều params |
| `ApiEndpoints` → `EndpointResponses` | 1:N | 1 endpoint → nhiều response codes |
| `ApiSpecifications` → `SecuritySchemes` | 1:N | 1 file → nhiều security schemes |
| `Projects` → `ApiSpecifications` | 1:N | 1 project → nhiều file specs |

**Lợi ích thiết kế:**

1. **Giữ nguyên file gốc** - `OriginalFileId` reference để có thể download/re-parse
2. **Query nhanh** - Không cần parse lại file mỗi lần, đã extract sẵn vào tables
3. **Versioning** - Upload file mới → tạo `ApiSpecifications` mới, giữ history
4. **Selective Testing** - User chọn 1 hoặc nhiều endpoints để generate test cases
5. **Schema Validation** - Lưu JSON Schema cho validation khi execute tests

---

### 3.4 TestGeneration Module

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           TESTGENERATION MODULE                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│              TestSuites                  │  (FE-05, FE-06)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  ProjectId       : GUID → Projects   │
│ FK  ApiSpecId       : GUID → ApiSpecs   │ ← Optional, NULL for manual
│     Name            : VARCHAR(200)      │
│     Description     : TEXT              │
│     GenerationType  : ENUM              │ ← Auto/Manual/LLMAssisted
│     Status          : ENUM              │ ← Draft/Ready/Archived
│     CreatedById     : GUID → AspNetUsers│
│     CreatedDateTime : TIMESTAMP         │
│     UpdatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘
           │
           │ 1:N
           ▼
┌─────────────────────────────────────────┐
│              TestCases                   │  (FE-05, FE-06, FE-11, FE-12)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  TestSuiteId     : GUID → TestSuites │
│ FK  EndpointId      : GUID → Endpoints  │ ← NULL for manual entry
│     Name            : VARCHAR(200)      │
│     Description     : TEXT              │
│     TestType        : ENUM              │ ← HappyPath/Boundary/Negative
│     Priority        : ENUM              │ ← Critical/High/Medium/Low
│     IsEnabled       : BOOLEAN           │
│     DependsOnId     : GUID → TestCases  │ ← For dependency chaining (FE-07)
│     OrderIndex      : INT               │ ← Execution order
│     Tags            : VARCHAR[]         │
│     CreatedDateTime : TIMESTAMP         │
│     UpdatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘
           │
           │ 1:1
           ▼
┌─────────────────────────────────────────┐
│          TestCaseRequests                │  (FE-11, FE-12, FE-13)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  TestCaseId      : GUID → TestCases  │
│     HttpMethod      : ENUM              │
│     Url             : VARCHAR(1000)     │ ← Can include {placeholders}
│     Headers         : JSONB             │
│     PathParams      : JSONB             │ ← {"id": "123", "userId": "abc"}
│     QueryParams     : JSONB             │
│     BodyType        : ENUM              │ ← JSON/FormData/UrlEncoded/Raw
│     Body            : TEXT              │
│     Timeout         : INT               │ ← Milliseconds
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│        TestCaseExpectations              │  (FE-08)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  TestCaseId      : GUID → TestCases  │
│     ExpectedStatus  : INT[]             │ ← [200, 201] acceptable statuses
│     ResponseSchema  : JSONB             │ ← JSON Schema to validate
│     HeaderChecks    : JSONB             │ ← Expected headers
│     BodyContains    : VARCHAR[]         │ ← Strings that must exist
│     BodyNotContains : VARCHAR[]         │ ← Strings that must NOT exist
│     JsonPathChecks  : JSONB             │ ← {"$.data.id": "not_null"}
│     MaxResponseTime : INT               │ ← Milliseconds
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│        TestCaseVariables                 │  (FE-07 - Variable extraction)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  TestCaseId      : GUID → TestCases  │
│     VariableName    : VARCHAR(100)      │ ← "accessToken", "userId"
│     ExtractFrom     : ENUM              │ ← ResponseBody/ResponseHeader/Status
│     JsonPath        : VARCHAR(500)      │ ← "$.data.token"
│     HeaderName      : VARCHAR(100)      │ ← For header extraction
│     Regex           : VARCHAR(500)      │ ← Alternative regex extraction
│     DefaultValue    : TEXT              │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│       TestDataSets (Optional)            │
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  TestCaseId      : GUID → TestCases  │
│     Name            : VARCHAR(100)      │
│     Data            : JSONB             │ ← Data-driven testing
│     IsEnabled       : BOOLEAN           │
└─────────────────────────────────────────┘
```

### 3.4.1 Human Validation & Test Order Management

Hệ thống hỗ trợ:
1. **AI đề xuất thứ tự test** → User review/approve/modify trước khi chạy
2. **User tự custom thứ tự** → Ghi đè AI suggestion
3. **Version history** → Track mọi thay đổi

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    TEST ORDER PROPOSAL & APPROVAL FLOW                      │
└─────────────────────────────────────────────────────────────────────────────┘

   AI generates test cases
           │
           ▼
   ┌───────────────────────────────────────────────────────────────────────┐
   │  STEP 1: AI Proposes Test Execution Order                             │
   │  ┌─────────────────────────────────────────────────────────────────┐  │
   │  │  TestOrderProposals                                             │  │
   │  │  • Source: Ai                                                   │  │
   │  │  • Status: Pending                                              │  │
   │  │  • ProposedOrder: [                                             │  │
   │  │      {TestCaseId: "login", Order: 1, Reason: "Auth first"},     │  │
   │  │      {TestCaseId: "get-users", Order: 2, Reason: "Need token"}, │  │
   │  │      {TestCaseId: "create-user", Order: 3, Reason: "CRUD ops"}  │  │
   │  │    ]                                                            │  │
   │  │  • AiReasoning: "Login first for token, then protected APIs"    │  │
   │  └─────────────────────────────────────────────────────────────────┘  │
   └───────────────────────────────────────────────────────────────────────┘
           │
           ▼
   ┌───────────────────────────────────────────────────────────────────────┐
   │  STEP 2: Human Review (Required before execution)                     │
   │  ┌─────────────────────────────────────────────────────────────────┐  │
   │  │  TestSuites                                                     │  │
   │  │  • ApprovalStatus: PendingReview (cannot execute until Approved)│  │
   │  └─────────────────────────────────────────────────────────────────┘  │
   │                                                                       │
   │  User can:                                                            │
   │  ├── ✅ Approve as-is → Status: Approved                             │
   │  ├── ✏️  Modify order → Status: ModifiedAndApproved                   │
   │  │       User drags/drops to reorder tests                           │
   │  │       UserModifiedOrder: [{TestCaseId: "...", Order: N}, ...]     │
   │  └── ❌ Reject → Status: Rejected (request new proposal)             │
   └───────────────────────────────────────────────────────────────────────┘
           │
           ▼
   ┌───────────────────────────────────────────────────────────────────────┐
   │  STEP 3: Apply Order to TestCases                                     │
   │  ┌─────────────────────────────────────────────────────────────────┐  │
   │  │  TestCases (updated)                                            │  │
   │  │  • OrderIndex: AI's original suggestion                         │  │
   │  │  • CustomOrderIndex: User's modified order (if changed)         │  │
   │  │  • IsOrderCustomized: true/false                                │  │
   │  └─────────────────────────────────────────────────────────────────┘  │
   │                                                                       │
   │  Execution uses: CustomOrderIndex ?? OrderIndex                       │
   └───────────────────────────────────────────────────────────────────────┘
           │
           ▼
   ┌───────────────────────────────────────────────────────────────────────┐
   │  STEP 4: Version History (Automatic)                                  │
   │  ┌─────────────────────────────────────────────────────────────────┐  │
   │  │  TestSuiteVersions                                              │  │
   │  │  • VersionNumber: 2                                             │  │
   │  │  • ChangeType: UserOrderCustomized                              │  │
   │  │  • ChangedById: user-guid                                       │  │
   │  │  • TestCaseOrderSnapshot: [current order state]                 │  │
   │  │  • PreviousState: [AI order]                                    │  │
   │  │  • NewState: [User modified order]                              │  │
   │  └─────────────────────────────────────────────────────────────────┘  │
   │                                                                       │
   │  ┌─────────────────────────────────────────────────────────────────┐  │
   │  │  TestCaseChangeLogs (per test case change)                      │  │
   │  │  • ChangeType: UserCustomizedOrder                              │  │
   │  │  • FieldName: "CustomOrderIndex"                                │  │
   │  │  • OldValue: null                                               │  │
   │  │  • NewValue: "3"                                                │  │
   │  │  • ChangedById: user-guid                                       │  │
   │  └─────────────────────────────────────────────────────────────────┘  │
   └───────────────────────────────────────────────────────────────────────┘
```

**New Entities for Test Order Management:**

```
┌─────────────────────────────────────────┐
│         TestOrderProposals               │  (NEW - Human Validation)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  TestSuiteId     : GUID → TestSuites │
│     ProposalNumber  : INT               │ ← Sequential per suite
│     Source          : ENUM              │ ← Ai/User/System/Imported
│     Status          : ENUM              │ ← Pending/Approved/Rejected/Modified
│     ProposedOrder   : JSONB             │ ← [{TestCaseId, Order, Reason}]
│     AiReasoning     : TEXT              │ ← AI explanation
│     ConsideredFactors: JSONB            │ ← {dependencies, authFlow, etc.}
│     ReviewedById    : GUID → AspNetUsers│
│     ReviewedAt      : TIMESTAMP         │
│     ReviewNotes     : TEXT              │ ← User feedback
│     UserModifiedOrder: JSONB            │ ← User's custom order
│     AppliedOrder    : JSONB             │ ← Final applied order
│     AppliedAt       : TIMESTAMP         │
│     LlmModel        : VARCHAR(100)      │
│     TokensUsed      : INT               │
│     CreatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│         TestSuiteVersions                │  (NEW - Version History)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  TestSuiteId     : GUID → TestSuites │
│     VersionNumber   : INT               │
│     ChangedById     : GUID → AspNetUsers│
│     ChangeType      : ENUM              │ ← Created/TestOrderChanged/etc.
│     ChangeDescription: TEXT             │
│     TestCaseOrderSnapshot: JSONB        │ ← Order at this version
│     ApprovalStatusSnapshot: ENUM        │
│     PreviousState   : JSONB             │
│     NewState        : JSONB             │
│     CreatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│         TestCaseChangeLogs               │  (NEW - Audit Trail)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  TestCaseId      : GUID → TestCases  │
│     ChangedById     : GUID → AspNetUsers│
│     ChangeType      : ENUM              │ ← OrderChanged/NameChanged/etc.
│     FieldName       : VARCHAR(100)      │
│     OldValue        : TEXT              │
│     NewValue        : TEXT              │
│     ChangeReason    : TEXT              │
│     VersionAfterChange: INT             │
│     IpAddress       : VARCHAR(45)       │
│     UserAgent       : VARCHAR(500)      │
│     CreatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘
```

**Updated TestSuite Entity:**

```
┌─────────────────────────────────────────┐
│              TestSuites                  │  (UPDATED)
├─────────────────────────────────────────┤
│ ...existing fields...                   │
│     ApprovalStatus  : ENUM              │ ← NEW: NotApplicable/PendingReview/
│                                         │        Approved/Rejected/ModifiedAndApproved
│     ApprovedById    : GUID → AspNetUsers│ ← NEW: Who approved
│     ApprovedAt      : TIMESTAMP         │ ← NEW: When approved
│     Version         : INT               │ ← NEW: Current version number
│     LastModifiedById: GUID → AspNetUsers│ ← NEW: Last modifier
└─────────────────────────────────────────┘
```

**Updated TestCase Entity:**

```
┌─────────────────────────────────────────┐
│              TestCases                   │  (UPDATED)
├─────────────────────────────────────────┤
│ ...existing fields...                   │
│     OrderIndex      : INT               │ ← AI-suggested order
│     CustomOrderIndex: INT               │ ← NEW: User-customized order (nullable)
│     IsOrderCustomized: BOOLEAN          │ ← NEW: True if user changed order
│     LastModifiedById: GUID → AspNetUsers│ ← NEW: Last modifier
│     Version         : INT               │ ← NEW: Current version number
└─────────────────────────────────────────┘
```

---

### 3.5 TestExecution Module

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           TESTEXECUTION MODULE                              │
│                                                                              │
│   ⚠️  TEST RESULTS LƯU TRÊN REDIS VỚI TTL 5-10 NGÀY                         │
│   📊  CHỈ SUMMARY ĐƯỢC SYNC VỀ POSTGRESQL SAU KHI TTL HẾT                   │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│         ExecutionEnvironments            │  (FE-04)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  ProjectId       : GUID → Projects   │
│     Name            : VARCHAR(100)      │ ← "Development", "Staging", "Prod"
│     BaseUrl         : VARCHAR(500)      │
│     Variables       : JSONB             │ ← Environment variables
│     Headers         : JSONB             │ ← Default headers
│     AuthConfig      : JSONB             │ ← Auth credentials (encrypted)
│     IsDefault       : BOOLEAN           │
│     CreatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│              TestRuns                    │  (FE-07, FE-08)
├─────────────────────────────────────────┤  ← PostgreSQL (summary)
│ PK  Id              : GUID              │
│ FK  TestSuiteId     : GUID → TestSuites │
│ FK  EnvironmentId   : GUID → Environments│
│ FK  TriggeredById   : GUID → AspNetUsers│
│     RunNumber       : INT               │ ← Auto-increment per suite
│     Status          : ENUM              │ ← Pending/Running/Completed/Failed/Cancelled
│     StartedAt       : TIMESTAMP         │
│     CompletedAt     : TIMESTAMP         │
│     TotalTests      : INT               │
│     PassedCount     : INT               │
│     FailedCount     : INT               │
│     SkippedCount    : INT               │
│     DurationMs      : BIGINT            │
│     RedisKey        : VARCHAR(200)      │ ← Key to fetch detailed results
│     ResultsExpireAt : TIMESTAMP         │ ← When Redis data expires
│     CreatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘

```

### 3.5.1 Redis Schema cho Test Results (Hot Storage)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          REDIS DATA STRUCTURES                              │
│                          TTL: 5-10 DAYS (Configurable)                      │
└─────────────────────────────────────────────────────────────────────────────┘

# Key Pattern: testrun:{runId}:results
# Type: HASH
# TTL: 5-10 days

HSET testrun:{runId}:results
    status          "Completed"
    startedAt       "2026-01-26T10:00:00Z"
    completedAt     "2026-01-26T10:05:00Z"
    totalTests      "50"
    passed          "45"
    failed          "3"
    skipped         "2"
    durationMs      "300000"

# Key Pattern: testrun:{runId}:execution:{testCaseId}
# Type: HASH
# TTL: 5-10 days

HSET testrun:{runId}:execution:{testCaseId}
    status          "Passed|Failed|Skipped|Error"
    startedAt       "2026-01-26T10:00:05Z"
    completedAt     "2026-01-26T10:00:06Z"
    durationMs      "1200"
    
    # Request sent
    requestUrl      "https://api.example.com/users/123"
    requestMethod   "GET"
    requestHeaders  "{\"Authorization\":\"Bearer xxx\"}"
    requestBody     "{}"
    
    # Response received
    responseStatus  "200"
    responseHeaders "{\"Content-Type\":\"application/json\"}"
    responseBody    "{\"id\":123,\"name\":\"John\"}"
    responseTimeMs  "450"
    
    # Validation results
    validations     "[{\"check\":\"status\",\"expected\":200,\"actual\":200,\"pass\":true}]"
    failureReason   ""
    
    # LLM Explanation (FE-09)
    llmExplanation  "The response matches expected schema..."

# Key Pattern: testrun:{runId}:logs
# Type: LIST (append-only log)
# TTL: 5-10 days

RPUSH testrun:{runId}:logs
    "{\"timestamp\":\"2026-01-26T10:00:00Z\",\"level\":\"INFO\",\"message\":\"Test run started\"}"
    "{\"timestamp\":\"2026-01-26T10:00:01Z\",\"level\":\"INFO\",\"message\":\"Executing test: Login API\"}"
    "{\"timestamp\":\"2026-01-26T10:00:02Z\",\"level\":\"DEBUG\",\"message\":\"Request sent to /auth/login\"}"

# Key Pattern: testrun:{runId}:variables
# Type: HASH (runtime variables)
# TTL: 5-10 days

HSET testrun:{runId}:variables
    accessToken     "eyJhbGciOiJIUzI1NiIs..."
    userId          "12345"
    sessionId       "sess_abc123"

# Key Pattern: user:{userId}:recent_runs
# Type: SORTED SET (for quick access to recent runs)
# Score: timestamp

ZADD user:{userId}:recent_runs 1706270400 "runId1"
ZADD user:{userId}:recent_runs 1706356800 "runId2"
```

---

### 3.6 TestReporting Module

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           TESTREPORTING MODULE                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│            TestReports                   │  (FE-10)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  TestRunId       : GUID → TestRuns   │
│ FK  GeneratedById   : GUID → AspNetUsers│
│ FK  FileId          : GUID → StorageFiles│ ← Reference to generated file
│     ReportType      : ENUM              │ ← Summary/Detailed/Coverage
│     Format          : ENUM              │ ← PDF/CSV/JSON/HTML
│     GeneratedAt     : TIMESTAMP         │
│     ExpiresAt       : TIMESTAMP         │ ← Optional auto-delete
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│         CoverageMetrics                  │  (FE-10)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  TestRunId       : GUID → TestRuns   │
│     TotalEndpoints  : INT               │
│     TestedEndpoints : INT               │
│     CoveragePercent : DECIMAL(5,2)      │
│     ByMethod        : JSONB             │ ← {"GET": 90, "POST": 85}
│     ByTag           : JSONB             │ ← {"users": 100, "orders": 75}
│     UncoveredPaths  : VARCHAR[]         │
│     CalculatedAt    : TIMESTAMP         │
└─────────────────────────────────────────┘
```

---

### 3.7 Subscription Module

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           SUBSCRIPTION MODULE                               │
│                                 (FE-14)                                     │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│          SubscriptionPlans               │
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│     Name            : VARCHAR(100)      │ ← "Free", "Pro", "Enterprise"
│     DisplayName     : VARCHAR(200)      │
│     Description     : TEXT              │
│     PriceMonthly    : DECIMAL(10,2)     │
│     PriceYearly     : DECIMAL(10,2)     │
│     Currency        : VARCHAR(3)        │ ← "USD", "VND"
│     IsActive        : BOOLEAN           │
│     SortOrder       : INT               │
└─────────────────────────────────────────┘
           │
           │ 1:N
           ▼
┌─────────────────────────────────────────┐
│           PlanLimits                     │
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  PlanId          : GUID → Plans      │
│     LimitType       : ENUM              │
│         ← MaxProjects/                  │
│           MaxEndpointsPerProject/       │
│           MaxTestCasesPerSuite/         │
│           MaxTestRunsPerMonth/          │
│           MaxConcurrentRuns/            │
│           RetentionDays/                │
│           MaxLlmCallsPerMonth/          │
│           MaxStorageMB                  │
│     LimitValue      : INT               │
│     IsUnlimited     : BOOLEAN           │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│          UserSubscriptions               │
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  UserId          : GUID → AspNetUsers│
│ FK  PlanId          : GUID → Plans      │
│     Status          : ENUM              │ ← Trial/Active/PastDue/Cancelled/Expired
│     BillingCycle    : ENUM              │ ← Monthly/Yearly
│     StartDate       : DATE              │
│     EndDate         : DATE              │
│     NextBillingDate : DATE              │ ← NEW: When next payment is due
│     TrialEndsAt     : TIMESTAMP         │
│     CancelledAt     : TIMESTAMP         │
│     AutoRenew       : BOOLEAN           │ ← NEW: Auto-renew subscription
│     ExternalSubId   : VARCHAR(200)      │ ← Stripe subscription ID
│     ExternalCustId  : VARCHAR(200)      │ ← NEW: Stripe customer ID
│     CreatedDateTime : TIMESTAMP         │
│     UpdatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘
           │
           │ 1:N
           ▼
┌─────────────────────────────────────────┐
│       SubscriptionHistories              │  ← NEW: Track plan changes
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  SubscriptionId  : GUID              │
│ FK  OldPlanId       : GUID → Plans      │ ← Previous plan (NULL if first)
│ FK  NewPlanId       : GUID → Plans      │ ← New plan
│     ChangeType      : ENUM              │ ← Created/Upgraded/Downgraded/Cancelled/Reactivated
│     ChangeReason    : TEXT              │ ← Optional reason
│     EffectiveDate   : DATE              │
│     CreatedAt       : TIMESTAMP         │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│            UsageTracking                 │
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  UserId          : GUID → AspNetUsers│
│     PeriodStart     : DATE              │ ← First day of billing period
│     PeriodEnd       : DATE              │
│     ProjectCount    : INT               │
│     EndpointCount   : INT               │
│     TestSuiteCount  : INT               │ ← NEW: Track test suites
│     TestCaseCount   : INT               │ ← NEW: Track test cases
│     TestRunCount    : INT               │
│     LlmCallCount    : INT               │ ← Track LLM API usage (FE-06, FE-09)
│     StorageUsedMB   : DECIMAL(10,2)     │ ← Track file storage usage
│     UpdatedAt       : TIMESTAMP         │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│          PaymentTransactions             │
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  UserId          : GUID → AspNetUsers│
│ FK  SubscriptionId  : GUID              │
│     Amount          : DECIMAL(10,2)     │
│     Currency        : VARCHAR(3)        │
│     Status          : ENUM              │ ← Pending/Succeeded/Failed/Refunded
│     PaymentMethod   : VARCHAR(50)       │ ← "card", "bank_transfer"
│     ExternalTxnId   : VARCHAR(200)      │ ← Stripe payment intent ID
│     InvoiceUrl      : VARCHAR(500)      │
│     FailureReason   : TEXT              │
│     CreatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘
```

---

### 3.8 LlmAssistant Module

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           LLMASSISTANT MODULE                               │
│                              (FE-06, FE-09)                                 │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│          LlmInteractions                 │
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  UserId          : GUID → AspNetUsers│
│     InteractionType : ENUM              │
│         ← ScenarioSuggestion/           │
│           FailureExplanation/           │
│           DocumentationParsing          │
│     InputContext    : TEXT              │ ← What was sent to LLM
│     LlmResponse     : TEXT              │ ← What LLM returned
│     ModelUsed       : VARCHAR(100)      │ ← "gpt-4", "claude-3"
│     TokensUsed      : INT               │
│     LatencyMs       : INT               │
│     CreatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│        LlmSuggestionCache                │  (Optional caching)
├─────────────────────────────────────────┤
│ PK  Id              : GUID              │
│ FK  EndpointId      : GUID → Endpoints  │
│     SuggestionType  : ENUM              │ ← BoundaryCase/NegativeCase
│     CacheKey        : VARCHAR(500)      │ ← Hash of input context
│     Suggestions     : JSONB             │
│     ExpiresAt       : TIMESTAMP         │
│     CreatedDateTime : TIMESTAMP         │
└─────────────────────────────────────────┘
```

---

## 4. Complete ERD Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                                    COMPLETE ERD                                                              │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

                                                    ┌─────────────┐
                                                    │    Users    │
                                                    └──────┬──────┘
                                                           │
                    ┌──────────────────────────────────────┼───────────────────────────────────────────┐
                    │                                      │                                           │
                    ▼                                      ▼                                           ▼
           ┌────────────────┐                    ┌─────────────────┐                         ┌─────────────────────┐
           │ UserSubscription│                    │    Projects     │                         │  LlmInteractions   │
           └────────┬───────┘                    └────────┬────────┘                         └─────────────────────┘
                    │                                     │
                    ▼                                     │
           ┌────────────────┐                             │
           │ SubscriptionPlan│                            │
           └────────┬───────┘                             │
                    │                                     │
                    ▼                                     ▼
           ┌────────────────┐              ┌──────────────────────────┐
           │   PlanLimits   │              │    ApiSpecifications     │
           └────────────────┘              └────────────┬─────────────┘
                                                        │
                                    ┌───────────────────┼───────────────────┐
                                    │                   │                   │
                                    ▼                   ▼                   ▼
                           ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐
                           │ ApiEndpoints │    │SecuritySchemes│    │   TestSuites     │
                           └──────┬───────┘    └──────────────┘    └────────┬─────────┘
                                  │                                         │
             ┌────────────────────┼────────────────────┐                    │
             │                    │                    │                    │
             ▼                    ▼                    ▼                    ▼
    ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐
    │EndpointParameters│  │EndpointResponses│  │EndpointSecurityReqs│  │  TestCases  │
    └─────────────────┘  └─────────────────┘  └─────────────────┘  └──────┬──────┘
                                                                          │
                                              ┌───────────────────────────┼───────────────────────────┐
                                              │                           │                           │
                                              ▼                           ▼                           ▼
                                     ┌─────────────────┐        ┌─────────────────┐        ┌─────────────────┐
                                     │TestCaseRequests │        │TestCaseExpects  │        │TestCaseVariables│
                                     └─────────────────┘        └─────────────────┘        └─────────────────┘

                                                        ┌─────────────────────┐
                                                        │ExecutionEnvironments│
                                                        └──────────┬──────────┘
                                                                   │
                                                                   ▼
                                                        ┌─────────────────────┐
                                                        │      TestRuns       │ ←──── PostgreSQL (Summary)
                                                        └──────────┬──────────┘
                                                                   │
                                          ┌────────────────────────┴────────────────────────┐
                                          │                                                 │
                                          ▼                                                 ▼
                               ┌─────────────────────┐                          ┌─────────────────────┐
                               │   TestReports       │                          │    REDIS CACHE      │
                               └─────────────────────┘                          │  (5-10 days TTL)    │
                                                                                │                     │
                                                                                │ • Execution Details │
                                                                                │ • Request/Response  │
                                                                                │ • Logs              │
                                                                                │ • Variables         │
                                                                                └─────────────────────┘
```

---

## 5. Redis Cleanup Strategy

### 5.1 Background Job cho Data Expiration

```csharp
// Pseudocode for cleanup job
public class TestResultCleanupJob : IHostedService
{
    // Chạy mỗi ngày
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // 1. Tìm TestRuns có ResultsExpireAt < now
        var expiredRuns = await _testRunRepository.GetExpiredRuns();
        
        foreach (var run in expiredRuns)
        {
            // 2. Redis keys đã tự động expire (TTL)
            // 3. Cập nhật trạng thái trong PostgreSQL
            run.ResultsArchived = true;
            run.RedisKey = null;
            
            // 4. Giữ lại summary metrics trong PostgreSQL
            await _testRunRepository.UpdateAsync(run);
        }
    }
}
```

### 5.2 TTL Configuration

```json
{
  "Redis": {
    "TestResults": {
      "DefaultTTLDays": 7,
      "MaxTTLDays": 10,
      "MinTTLDays": 5
    }
  },
  "Subscription": {
    "Free": { "ResultRetentionDays": 5 },
    "Pro": { "ResultRetentionDays": 7 },
    "Enterprise": { "ResultRetentionDays": 10 }
  }
}
```

---

## 6. Summary

### 6.1 PostgreSQL Tables (Permanent Storage)

| Module | Tables |
|--------|--------|
| Identity (ASP.NET Core Identity) | AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens, AspNetRoleClaims, UserProfiles (optional) |
| **Storage** | **FileEntries** |
| ApiDocumentation | Projects, ApiSpecifications, ApiEndpoints, EndpointParameters, EndpointResponses, EndpointSecurityReqs, SecuritySchemes |
| TestGeneration | TestSuites, TestCases, TestCaseRequests, TestCaseExpectations, TestCaseVariables, TestDataSets, **TestSuiteVersions**, **TestCaseChangeLogs**, **TestOrderProposals** |
| TestExecution | ExecutionEnvironments, TestRuns (summary only) |
| TestReporting | TestReports, CoverageMetrics |
| Subscription | SubscriptionPlans, PlanLimits, UserSubscriptions, SubscriptionHistories, UsageTracking, PaymentTransactions |
| LlmAssistant | LlmInteractions, LlmSuggestionCache |

### 6.2 Redis Keys (Temporary Storage - 5-10 days)

| Key Pattern | Data Type | Purpose |
|-------------|-----------|---------|
| `testrun:{id}:results` | HASH | Run summary |
| `testrun:{id}:execution:{testId}` | HASH | Individual test results |
| `testrun:{id}:logs` | LIST | Execution logs |
| `testrun:{id}:variables` | HASH | Runtime variables |
| `user:{id}:recent_runs` | SORTED SET | Quick access to recent runs |

### 6.3 Estimated Table Counts

| Category | Count |
|----------|-------|
| ASP.NET Core Identity tables | 7 |
| Storage table | 1 |
| Custom business tables | ~23 |
| Redis key patterns | ~5 |
| **Total PostgreSQL tables** | **~31** |

---

## 7. Subscription Tiers & Usage Limits (FE-14)

### 7.1 Default Plan Configuration

Khi user đăng ký mới **không mua gói**, hệ thống tự động assign **Free Plan** với các giới hạn sau:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         SUBSCRIPTION TIERS MATRIX                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌────────────────────────────────────────────────────────────────────┐    │
│   │                           FREE TIER                                │    │
│   │                     (Auto-assigned on signup)                      │    │
│   ├────────────────────────────────────────────────────────────────────┤    │
│   │  Price: $0/month                                                   │    │
│   │                                                                    │    │
│   │  Limits:                                                           │    │
│   │  ├── MaxProjects           : 1                                     │    │
│   │  ├── MaxEndpointsPerProject: 10                                    │    │
│   │  ├── MaxTestCasesPerSuite  : 20                                    │    │
│   │  ├── MaxTestRunsPerMonth   : 30                                    │    │
│   │  ├── MaxConcurrentRuns     : 1                                     │    │
│   │  ├── RetentionDays         : 5                                     │    │
│   │  ├── MaxLlmCallsPerMonth   : 10 (limited AI assistance)            │    │
│   │  └── ExportFormats         : CSV only                              │    │
│   │                                                                    │    │
│   │  Features:                                                         │    │
│   │  ✓ Manual API entry (FE-11)                                        │    │
│   │  ✓ cURL import (FE-13)                                             │    │
│   │  ✓ OpenAPI/Postman upload (limited endpoints)                      │    │
│   │  ✓ Happy-path test generation                                      │    │
│   │  ✗ Boundary/Negative test generation (LLM-assisted) - LIMITED      │    │
│   │  ✗ LLM failure explanations - LIMITED                              │    │
│   │  ✗ PDF export                                                      │    │
│   └────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│   ┌────────────────────────────────────────────────────────────────────┐    │
│   │                           PRO TIER                                 │    │
│   │                        $19/month or $190/year                      │    │
│   ├────────────────────────────────────────────────────────────────────┤    │
│   │  Limits:                                                           │    │
│   │  ├── MaxProjects           : 10                                    │    │
│   │  ├── MaxEndpointsPerProject: 100                                   │    │
│   │  ├── MaxTestCasesPerSuite  : 200                                   │    │
│   │  ├── MaxTestRunsPerMonth   : 500                                   │    │
│   │  ├── MaxConcurrentRuns     : 3                                     │    │
│   │  ├── RetentionDays         : 7                                     │    │
│   │  ├── MaxLlmCallsPerMonth   : 200                                   │    │
│   │  └── ExportFormats         : CSV, PDF                              │    │
│   │                                                                    │    │
│   │  Features:                                                         │    │
│   │  ✓ All Free features                                               │    │
│   │  ✓ Full LLM-assisted test generation (FE-06)                       │    │
│   │  ✓ LLM failure explanations (FE-09)                                │    │
│   │  ✓ PDF report export                                               │    │
│   │  ✓ Priority support                                                │    │
│   └────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│   ┌────────────────────────────────────────────────────────────────────┐    │
│   │                       ENTERPRISE TIER                              │    │
│   │                        Custom pricing                              │    │
│   ├────────────────────────────────────────────────────────────────────┤    │
│   │  Limits:                                                           │    │
│   │  ├── MaxProjects           : Unlimited                             │    │
│   │  ├── MaxEndpointsPerProject: Unlimited                             │    │
│   │  ├── MaxTestCasesPerSuite  : Unlimited                             │    │
│   │  ├── MaxTestRunsPerMonth   : Unlimited                             │    │
│   │  ├── MaxConcurrentRuns     : 10+                                   │    │
│   │  ├── RetentionDays         : 10+ (configurable)                    │    │
│   │  ├── MaxLlmCallsPerMonth   : Unlimited                             │    │
│   │  └── ExportFormats         : CSV, PDF, JSON, HTML                  │    │
│   │                                                                    │    │
│   │  Features:                                                         │    │
│   │  ✓ All Pro features                                                │    │
│   │  ✓ SSO/SAML integration                                            │    │
│   │  ✓ Custom retention policies                                       │    │
│   │  ✓ Dedicated support                                               │    │
│   │  ✓ On-premise deployment option                                    │    │
│   └────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 7.2 User Registration Flow với Auto-assign Free Plan

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    USER REGISTRATION & SUBSCRIPTION FLOW                    │
└─────────────────────────────────────────────────────────────────────────────┘

   User clicks "Sign Up"
           │
           ▼
   ┌───────────────────────────────────────────────────────────────────────┐
   │  STEP 1: Create User Account                                          │
   │  ┌─────────────────────────────────────────────────────────────────┐  │
   │  │  Users table                                                    │  │
   │  │  • Id: user-guid-001                                            │  │
   │  │  • Email: john@example.com                                      │  │
   │  │  • CreatedDateTime: 2026-01-31T10:00:00Z                        │  │
   │  └─────────────────────────────────────────────────────────────────┘  │
   └───────────────────────────────────────────────────────────────────────┘
           │
           ▼
   ┌───────────────────────────────────────────────────────────────────────┐
   │  STEP 2: Auto-assign Free Plan (Event Handler / Domain Service)       │
   │  ┌─────────────────────────────────────────────────────────────────┐  │
   │  │  UserSubscriptions table                                        │  │
   │  │  • Id: sub-guid-001                                             │  │
   │  │  • UserId: user-guid-001                                        │  │
   │  │  • PlanId: FREE_PLAN_ID (seeded GUID)                           │  │
   │  │  • Status: Active                                               │  │
   │  │  • BillingCycle: NULL                                           │  │
   │  │  • StartDate: 2026-01-31                                        │  │
   │  │  • EndDate: NULL (no expiration for free)                       │  │
   │  │  • ExternalSubId: NULL (no payment provider)                    │  │
   │  └─────────────────────────────────────────────────────────────────┘  │
   └───────────────────────────────────────────────────────────────────────┘
           │
           ▼
   ┌───────────────────────────────────────────────────────────────────────┐
   │  STEP 3: Initialize Usage Tracking                                    │
   │  ┌─────────────────────────────────────────────────────────────────┐  │
   │  │  UsageTracking table                                            │  │
   │  │  • Id: usage-guid-001                                           │  │
   │  │  • UserId: user-guid-001                                        │  │
   │  │  • PeriodStart: 2026-01-01 (first of month)                     │  │
   │  │  • PeriodEnd: 2026-01-31                                        │  │
   │  │  • ProjectCount: 0                                              │  │
   │  │  • EndpointCount: 0                                             │  │
   │  │  • TestRunCount: 0                                              │  │
   │  └─────────────────────────────────────────────────────────────────┘  │
   └───────────────────────────────────────────────────────────────────────┘
           │
           ▼
   User can now use system with FREE tier limits
```

### 7.3 Usage Limit Enforcement Logic

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      USAGE LIMIT CHECK FLOW                                 │
└─────────────────────────────────────────────────────────────────────────────┘

   User attempts action (e.g., Create Project, Run Test)
           │
           ▼
   ┌───────────────────────────────────────────────────────────────────────┐
   │  UsageLimitService.CheckLimit(userId, limitType)                      │
   │                                                                       │
   │  1. Get UserSubscription → PlanId                                     │
   │  2. Get PlanLimits WHERE PlanId AND LimitType                         │
   │  3. Get UsageTracking for current period                              │
   │  4. Compare: currentUsage < limitValue                                │
   └───────────────────────────────────────────────────────────────────────┘
           │
           ├─────────────────────────────────┐
           │                                 │
           ▼                                 ▼
   ┌─────────────────┐             ┌─────────────────────────────────────┐
   │  Within Limit   │             │       Limit Exceeded                │
   │  ✓ Allow action │             │  ✗ Return 403 + upgrade prompt      │
   └─────────────────┘             │  Response:                          │
                                   │  {                                  │
                                   │    "error": "LIMIT_EXCEEDED",       │
                                   │    "limitType": "MaxTestRunsPerMonth",│
                                   │    "currentUsage": 30,              │
                                   │    "limit": 30,                     │
                                   │    "upgradeUrl": "/pricing"         │
                                   │  }                                  │
                                   └─────────────────────────────────────┘
```

### 7.4 Bổ sung LimitType ENUM

```sql
-- Updated LimitType enum values
CREATE TYPE limit_type AS ENUM (
    'MaxProjects',
    'MaxEndpointsPerProject',
    'MaxTestCasesPerSuite',
    'MaxTestRunsPerMonth',
    'MaxConcurrentRuns',
    'RetentionDays',
    'MaxLlmCallsPerMonth',       -- Track LLM API usage
    'MaxStorageMB'               -- File storage limit
);
```

### 7.5 Seed Data for Plans

```sql
-- Seed SubscriptionPlans
INSERT INTO SubscriptionPlans (Id, Name, DisplayName, PriceMonthly, PriceYearly, IsActive, SortOrder)
VALUES 
    ('FREE_PLAN_GUID', 'Free', 'Free Plan', 0.00, 0.00, true, 1),
    ('PRO_PLAN_GUID', 'Pro', 'Pro Plan', 19.00, 190.00, true, 2),
    ('ENTERPRISE_PLAN_GUID', 'Enterprise', 'Enterprise Plan', NULL, NULL, true, 3);

-- Seed PlanLimits for Free Plan
INSERT INTO PlanLimits (Id, PlanId, LimitType, LimitValue, IsUnlimited)
VALUES
    (gen_random_uuid(), 'FREE_PLAN_GUID', 'MaxProjects', 1, false),
    (gen_random_uuid(), 'FREE_PLAN_GUID', 'MaxEndpointsPerProject', 10, false),
    (gen_random_uuid(), 'FREE_PLAN_GUID', 'MaxTestCasesPerSuite', 20, false),
    (gen_random_uuid(), 'FREE_PLAN_GUID', 'MaxTestRunsPerMonth', 30, false),
    (gen_random_uuid(), 'FREE_PLAN_GUID', 'MaxConcurrentRuns', 1, false),
    (gen_random_uuid(), 'FREE_PLAN_GUID', 'RetentionDays', 5, false),
    (gen_random_uuid(), 'FREE_PLAN_GUID', 'MaxLlmCallsPerMonth', 10, false);

-- Seed PlanLimits for Pro Plan
INSERT INTO PlanLimits (Id, PlanId, LimitType, LimitValue, IsUnlimited)
VALUES
    (gen_random_uuid(), 'PRO_PLAN_GUID', 'MaxProjects', 10, false),
    (gen_random_uuid(), 'PRO_PLAN_GUID', 'MaxEndpointsPerProject', 100, false),
    (gen_random_uuid(), 'PRO_PLAN_GUID', 'MaxTestCasesPerSuite', 200, false),
    (gen_random_uuid(), 'PRO_PLAN_GUID', 'MaxTestRunsPerMonth', 500, false),
    (gen_random_uuid(), 'PRO_PLAN_GUID', 'MaxConcurrentRuns', 3, false),
    (gen_random_uuid(), 'PRO_PLAN_GUID', 'RetentionDays', 7, false),
    (gen_random_uuid(), 'PRO_PLAN_GUID', 'MaxLlmCallsPerMonth', 200, false);

-- Seed PlanLimits for Enterprise Plan (Unlimited)
INSERT INTO PlanLimits (Id, PlanId, LimitType, LimitValue, IsUnlimited)
VALUES
    (gen_random_uuid(), 'ENTERPRISE_PLAN_GUID', 'MaxProjects', NULL, true),
    (gen_random_uuid(), 'ENTERPRISE_PLAN_GUID', 'MaxEndpointsPerProject', NULL, true),
    (gen_random_uuid(), 'ENTERPRISE_PLAN_GUID', 'MaxTestCasesPerSuite', NULL, true),
    (gen_random_uuid(), 'ENTERPRISE_PLAN_GUID', 'MaxTestRunsPerMonth', NULL, true),
    (gen_random_uuid(), 'ENTERPRISE_PLAN_GUID', 'MaxConcurrentRuns', 10, false),
    (gen_random_uuid(), 'ENTERPRISE_PLAN_GUID', 'RetentionDays', 10, false),
    (gen_random_uuid(), 'ENTERPRISE_PLAN_GUID', 'MaxLlmCallsPerMonth', NULL, true);
```

---

## 8. Complete Mermaid ERD Diagram

### 8.1 Full ERD with All Tables

```mermaid
erDiagram
    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 1: ASP.NET CORE IDENTITY (Built-in tables)
    %% ═══════════════════════════════════════════════════════════════════════
    
    AspNetUsers {
        UUID Id PK
        string UserName "NVARCHAR(256)"
        string NormalizedUserName "NVARCHAR(256)"
        string Email "NVARCHAR(256)"
        string NormalizedEmail "NVARCHAR(256)"
        boolean EmailConfirmed
        string PasswordHash "NVARCHAR(MAX)"
        string SecurityStamp "NVARCHAR(MAX)"
        string ConcurrencyStamp "NVARCHAR(MAX)"
        string PhoneNumber "NVARCHAR(MAX)"
        boolean PhoneNumberConfirmed
        boolean TwoFactorEnabled
        datetimeoffset LockoutEnd
        boolean LockoutEnabled
        int AccessFailedCount
    }
    
    AspNetRoles {
        UUID Id PK
        string Name "NVARCHAR(256)"
        string NormalizedName "NVARCHAR(256)"
        string ConcurrencyStamp "NVARCHAR(MAX)"
    }
    
    AspNetUserRoles {
        UUID UserId PK_FK "AspNetUsers"
        UUID RoleId PK_FK "AspNetRoles"
    }
    
    AspNetUserClaims {
        int Id PK "IDENTITY"
        UUID UserId FK "AspNetUsers"
        string ClaimType "NVARCHAR(MAX)"
        string ClaimValue "NVARCHAR(MAX)"
    }
    
    AspNetUserLogins {
        string LoginProvider PK "NVARCHAR(128)"
        string ProviderKey PK "NVARCHAR(128)"
        UUID UserId FK "AspNetUsers"
        string ProviderDisplayName "NVARCHAR(MAX)"
    }
    
    AspNetUserTokens {
        UUID UserId PK_FK "AspNetUsers"
        string LoginProvider PK "NVARCHAR(128)"
        string Name PK "NVARCHAR(128)"
        string Value "NVARCHAR(MAX)"
    }
    
    AspNetRoleClaims {
        int Id PK "IDENTITY"
        UUID RoleId FK "AspNetRoles"
        string ClaimType "NVARCHAR(MAX)"
        string ClaimValue "NVARCHAR(MAX)"
    }
    
    %% Custom User Profile Extension (Optional)
    UserProfiles {
        UUID Id PK
        UUID UserId FK_UK "AspNetUsers (1:1)"
        string DisplayName "NVARCHAR(200)"
        string AvatarUrl "NVARCHAR(500)"
        string Timezone "NVARCHAR(50)"
        datetime CreatedAt
        datetime UpdatedAt
    }
    
    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 2: STORAGE (File Management)
    %% ═══════════════════════════════════════════════════════════════════════
    
    StorageFiles {
        UUID Id PK
        UUID OwnerId FK "AspNetUsers"
        string FileName "VARCHAR(255)"
        string ContentType "VARCHAR(100)"
        bigint FileSize "bytes"
        enum StorageProvider "Local|AzureBlob|AwsS3"
        string BucketName "VARCHAR(100)"
        string StoragePath "VARCHAR(500)"
        string PublicUrl "VARCHAR(1000)"
        string Checksum "VARCHAR(64) SHA256"
        enum FileCategory "ApiSpec|Report|Export"
        boolean IsDeleted
        datetime DeletedAt
        datetime CreatedAt
        datetime ExpiresAt
    }
    
    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 3: API DOCUMENTATION
    %% ═══════════════════════════════════════════════════════════════════════
    
    Projects {
        UUID Id PK
        UUID OwnerId FK "AspNetUsers"
        UUID ActiveSpecId FK "ApiSpecifications"
        string Name "VARCHAR(200)"
        text Description
        string BaseUrl "VARCHAR(500)"
        enum Status "Active|Archived"
        datetime CreatedDateTime
        datetime UpdatedDateTime
    }
    
    ApiSpecifications {
        UUID Id PK
        UUID ProjectId FK "Projects"
        UUID OriginalFileId FK "StorageFiles"
        string Name "VARCHAR(200)"
        enum SourceType "OpenAPI|Postman|Manual|cURL"
        string Version "VARCHAR(50)"
        boolean IsActive
        datetime ParsedAt
        enum ParseStatus "Pending|Success|Failed"
        json ParseErrors
        datetime CreatedDateTime
        datetime UpdatedDateTime
    }
    
    ApiEndpoints {
        UUID Id PK
        UUID ApiSpecId FK "ApiSpecifications"
        enum HttpMethod "GET|POST|PUT|DELETE|PATCH"
        string Path "VARCHAR(500)"
        string OperationId "VARCHAR(200)"
        string Summary "VARCHAR(500)"
        text Description
        array Tags "VARCHAR[]"
        boolean IsDeprecated
        datetime CreatedDateTime
    }
    
    EndpointParameters {
        UUID Id PK
        UUID EndpointId FK "ApiEndpoints"
        string Name "VARCHAR(100)"
        enum Location "Path|Query|Header|Body"
        string DataType "VARCHAR(50)"
        string Format "VARCHAR(50)"
        boolean IsRequired
        text DefaultValue
        json Schema "JSON Schema"
        json Examples
    }
    
    EndpointResponses {
        UUID Id PK
        UUID EndpointId FK "ApiEndpoints"
        int StatusCode "200|400|401|404|500"
        text Description
        json Schema "Response JSON Schema"
        json Examples
        json Headers
    }
    
    EndpointSecurityReqs {
        UUID Id PK
        UUID EndpointId FK "ApiEndpoints"
        enum SecurityType "Bearer|ApiKey|OAuth2|Basic"
        string SchemeName "VARCHAR(100)"
        array Scopes "VARCHAR[]"
    }
    
    SecuritySchemes {
        UUID Id PK
        UUID ApiSpecId FK "ApiSpecifications"
        string Name "VARCHAR(100)"
        enum Type "http|apiKey|oauth2|openIdConnect"
        string Scheme "VARCHAR(50)"
        string BearerFormat "VARCHAR(50)"
        enum In "header|query|cookie"
        string ParameterName "VARCHAR(100)"
        json Configuration
    }
    
    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 4: TEST GENERATION
    %% ═══════════════════════════════════════════════════════════════════════
    
    TestSuites {
        UUID Id PK
        UUID ProjectId FK "Projects"
        UUID ApiSpecId FK "ApiSpecifications (nullable)"
        string Name "VARCHAR(200)"
        text Description
        enum GenerationType "Auto|Manual|LLMAssisted"
        enum Status "Draft|Ready|Archived"
        UUID CreatedById FK "AspNetUsers"
        datetime CreatedDateTime
        datetime UpdatedDateTime
    }
    
    TestCases {
        UUID Id PK
        UUID TestSuiteId FK "TestSuites"
        UUID EndpointId FK "ApiEndpoints (nullable)"
        string Name "VARCHAR(200)"
        text Description
        enum TestType "HappyPath|Boundary|Negative"
        enum Priority "Critical|High|Medium|Low"
        boolean IsEnabled
        UUID DependsOnId FK "TestCases (self-ref)"
        int OrderIndex
        array Tags "VARCHAR[]"
        datetime CreatedDateTime
        datetime UpdatedDateTime
    }
    
    TestCaseRequests {
        UUID Id PK
        UUID TestCaseId FK_UK "TestCases (1:1)"
        enum HttpMethod "GET|POST|PUT|DELETE|PATCH"
        string Url "VARCHAR(1000)"
        json Headers
        json PathParams
        json QueryParams
        enum BodyType "JSON|FormData|UrlEncoded|Raw"
        text Body
        int Timeout "milliseconds"
    }
    
    TestCaseExpectations {
        UUID Id PK
        UUID TestCaseId FK_UK "TestCases (1:1)"
        array ExpectedStatus "INT[]"
        json ResponseSchema
        json HeaderChecks
        array BodyContains "VARCHAR[]"
        array BodyNotContains "VARCHAR[]"
        json JsonPathChecks
        int MaxResponseTime "milliseconds"
    }
    
    TestCaseVariables {
        UUID Id PK
        UUID TestCaseId FK "TestCases"
        string VariableName "VARCHAR(100)"
        enum ExtractFrom "ResponseBody|ResponseHeader|Status"
        string JsonPath "VARCHAR(500)"
        string HeaderName "VARCHAR(100)"
        string Regex "VARCHAR(500)"
        text DefaultValue
    }
    
    TestDataSets {
        UUID Id PK
        UUID TestCaseId FK "TestCases"
        string Name "VARCHAR(100)"
        json Data "Data-driven testing"
        boolean IsEnabled
    }
    
    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 5: TEST EXECUTION
    %% ═══════════════════════════════════════════════════════════════════════
    
    ExecutionEnvironments {
        UUID Id PK
        UUID ProjectId FK "Projects"
        string Name "VARCHAR(100)"
        string BaseUrl "VARCHAR(500)"
        json Variables
        json Headers
        json AuthConfig "encrypted"
        boolean IsDefault
        datetime CreatedDateTime
    }
    
    TestRuns {
        UUID Id PK
        UUID TestSuiteId FK "TestSuites"
        UUID EnvironmentId FK "ExecutionEnvironments"
        UUID TriggeredById FK "AspNetUsers"
        int RunNumber "auto-increment per suite"
        enum Status "Pending|Running|Completed|Failed|Cancelled"
        datetime StartedAt
        datetime CompletedAt
        int TotalTests
        int PassedCount
        int FailedCount
        int SkippedCount
        bigint DurationMs
        string RedisKey "VARCHAR(200)"
        datetime ResultsExpireAt
        datetime CreatedDateTime
    }
    
    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 6: TEST REPORTING
    %% ═══════════════════════════════════════════════════════════════════════
    
    TestReports {
        UUID Id PK
        UUID TestRunId FK "TestRuns"
        UUID GeneratedById FK "AspNetUsers"
        UUID FileId FK "StorageFiles"
        enum ReportType "Summary|Detailed|Coverage"
        enum Format "PDF|CSV|JSON|HTML"
        datetime GeneratedAt
        datetime ExpiresAt
    }
    
    CoverageMetrics {
        UUID Id PK
        UUID TestRunId FK "TestRuns"
        int TotalEndpoints
        int TestedEndpoints
        decimal CoveragePercent "DECIMAL(5,2)"
        json ByMethod
        json ByTag
        array UncoveredPaths "VARCHAR[]"
        datetime CalculatedAt
    }
    
    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 7: SUBSCRIPTION & BILLING
    %% ═══════════════════════════════════════════════════════════════════════
    
    SubscriptionPlans {
        UUID Id PK
        string Name "VARCHAR(100)"
        string DisplayName "VARCHAR(200)"
        text Description
        decimal PriceMonthly "DECIMAL(10,2)"
        decimal PriceYearly "DECIMAL(10,2)"
        string Currency "VARCHAR(3)"
        boolean IsActive
        int SortOrder
    }
    
    PlanLimits {
        UUID Id PK
        UUID PlanId FK "SubscriptionPlans"
        enum LimitType "MaxProjects|MaxEndpoints|MaxTestRuns|..."
        int LimitValue
        boolean IsUnlimited
    }
    
    UserSubscriptions {
        UUID Id PK
        UUID UserId FK "AspNetUsers"
        UUID PlanId FK "SubscriptionPlans"
        enum Status "Trial|Active|PastDue|Cancelled|Expired"
        enum BillingCycle "Monthly|Yearly"
        date StartDate
        date EndDate
        date NextBillingDate
        datetime TrialEndsAt
        datetime CancelledAt
        boolean AutoRenew
        string ExternalSubId "Stripe subscription ID"
        string ExternalCustId "Stripe customer ID"
        datetime CreatedDateTime
        datetime UpdatedDateTime
    }
    
    SubscriptionHistories {
        UUID Id PK
        UUID SubscriptionId FK "UserSubscriptions"
        UUID OldPlanId FK "SubscriptionPlans (nullable)"
        UUID NewPlanId FK "SubscriptionPlans"
        enum ChangeType "Created|Upgraded|Downgraded|Cancelled|Reactivated"
        text ChangeReason
        date EffectiveDate
        datetime CreatedAt
    }
    
    UsageTracking {
        UUID Id PK
        UUID UserId FK "AspNetUsers"
        date PeriodStart
        date PeriodEnd
        int ProjectCount
        int EndpointCount
        int TestSuiteCount
        int TestCaseCount
        int TestRunCount
        int LlmCallCount
        decimal StorageUsedMB "DECIMAL(10,2)"
        datetime UpdatedAt
    }
    
    PaymentTransactions {
        UUID Id PK
        UUID UserId FK "AspNetUsers"
        UUID SubscriptionId FK "UserSubscriptions"
        decimal Amount "DECIMAL(10,2)"
        string Currency "VARCHAR(3)"
        enum Status "Pending|Succeeded|Failed|Refunded"
        string PaymentMethod "VARCHAR(50)"
        string ExternalTxnId "Stripe payment intent ID"
        string InvoiceUrl "VARCHAR(500)"
        text FailureReason
        datetime CreatedDateTime
    }
    
    %% ═══════════════════════════════════════════════════════════════════════
    %% MODULE 8: LLM ASSISTANT
    %% ═══════════════════════════════════════════════════════════════════════
    
    LlmInteractions {
        UUID Id PK
        UUID UserId FK "AspNetUsers"
        enum InteractionType "ScenarioSuggestion|FailureExplanation|DocParsing"
        text InputContext
        text LlmResponse
        string ModelUsed "VARCHAR(100)"
        int TokensUsed
        int LatencyMs
        datetime CreatedDateTime
    }
    
    LlmSuggestionCache {
        UUID Id PK
        UUID EndpointId FK "ApiEndpoints"
        enum SuggestionType "BoundaryCase|NegativeCase"
        string CacheKey "VARCHAR(500)"
        json Suggestions
        datetime ExpiresAt
        datetime CreatedDateTime
    }
    
    %% ═══════════════════════════════════════════════════════════════════════
    %% RELATIONSHIPS
    %% ═══════════════════════════════════════════════════════════════════════
    
    %% Identity Relationships
    AspNetUsers ||--o{ AspNetUserRoles : has
    AspNetRoles ||--o{ AspNetUserRoles : assigned_to
    AspNetUsers ||--o{ AspNetUserClaims : has
    AspNetUsers ||--o{ AspNetUserLogins : has
    AspNetUsers ||--o{ AspNetUserTokens : has
    AspNetRoles ||--o{ AspNetRoleClaims : has
    AspNetUsers ||--o| UserProfiles : has
    
    %% Storage Relationships
    AspNetUsers ||--o{ StorageFiles : uploads
    
    %% Project & API Documentation Relationships
    AspNetUsers ||--o{ Projects : owns
    Projects ||--o{ ApiSpecifications : contains
    Projects ||--o| ApiSpecifications : active_spec
    StorageFiles ||--o| ApiSpecifications : original_file
    ApiSpecifications ||--o{ ApiEndpoints : defines
    ApiSpecifications ||--o{ SecuritySchemes : includes
    ApiEndpoints ||--o{ EndpointParameters : has
    ApiEndpoints ||--o{ EndpointResponses : returns
    ApiEndpoints ||--o{ EndpointSecurityReqs : requires
    
    %% Test Generation Relationships
    Projects ||--o{ TestSuites : has
    ApiSpecifications ||--o{ TestSuites : referenced_by
    AspNetUsers ||--o{ TestSuites : creates
    TestSuites ||--o{ TestCases : contains
    ApiEndpoints ||--o{ TestCases : tested_by
    TestCases ||--o| TestCases : depends_on
    TestCases ||--|| TestCaseRequests : defines_request
    TestCases ||--|| TestCaseExpectations : defines_expectations
    TestCases ||--o{ TestCaseVariables : extracts
    TestCases ||--o{ TestDataSets : uses
    
    %% Test Execution Relationships
    Projects ||--o{ ExecutionEnvironments : configures
    TestSuites ||--o{ TestRuns : executed_as
    ExecutionEnvironments ||--o{ TestRuns : used_in
    AspNetUsers ||--o{ TestRuns : triggers
    
    %% Test Reporting Relationships
    TestRuns ||--o| TestReports : generates
    TestRuns ||--o| CoverageMetrics : calculates
    AspNetUsers ||--o{ TestReports : generates
    StorageFiles ||--o| TestReports : stores
    
    %% Subscription Relationships
    SubscriptionPlans ||--o{ PlanLimits : defines
    SubscriptionPlans ||--o{ UserSubscriptions : subscribed_to
    AspNetUsers ||--o| UserSubscriptions : has
    UserSubscriptions ||--o{ SubscriptionHistories : tracks
    SubscriptionPlans ||--o{ SubscriptionHistories : old_plan
    SubscriptionPlans ||--o{ SubscriptionHistories : new_plan
    AspNetUsers ||--o{ UsageTracking : tracked_for
    AspNetUsers ||--o{ PaymentTransactions : pays
    UserSubscriptions ||--o{ PaymentTransactions : for
    
    %% LLM Assistant Relationships
    AspNetUsers ||--o{ LlmInteractions : uses
    ApiEndpoints ||--o{ LlmSuggestionCache : cached_for
```

### 8.2 Table Naming Convention

| Convention | Example | Rule |
|------------|---------|------|
| **Identity Tables** | `AspNetUsers`, `AspNetRoles` | Prefix `AspNet` (built-in) |
| **Business Tables** | `Projects`, `TestSuites` | PascalCase, Plural |
| **Junction Tables** | `AspNetUserRoles` | Combine both entity names |
| **History Tables** | `SubscriptionHistories` | Suffix `Histories` |
| **Metrics Tables** | `CoverageMetrics`, `UsageTracking` | Descriptive name |

### 8.3 Table Count Summary

| Module | Table Count | Tables |
|--------|-------------|--------|
| **Identity** | 8 | AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens, AspNetRoleClaims, UserProfiles |
| **Storage** | 1 | StorageFiles |
| **ApiDocumentation** | 7 | Projects, ApiSpecifications, ApiEndpoints, EndpointParameters, EndpointResponses, EndpointSecurityReqs, SecuritySchemes |
| **TestGeneration** | 6 | TestSuites, TestCases, TestCaseRequests, TestCaseExpectations, TestCaseVariables, TestDataSets |
| **TestExecution** | 2 | ExecutionEnvironments, TestRuns |
| **TestReporting** | 2 | TestReports, CoverageMetrics |
| **Subscription** | 6 | SubscriptionPlans, PlanLimits, UserSubscriptions, SubscriptionHistories, UsageTracking, PaymentTransactions |
| **LlmAssistant** | 2 | LlmInteractions, LlmSuggestionCache |
| **TOTAL** | **34** | |
