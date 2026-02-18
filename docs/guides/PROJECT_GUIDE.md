# API Testing Automation System - Comprehensive Project Guide

> **Purpose**: A single-source-of-truth document for developers and AI agents to understand, navigate, and extend the API Testing Automation System codebase.
>
> **Target Audience**: Developers new to the codebase, AI coding agents implementing features.
>
> **Tech Stack**: .NET 10 / C# / PostgreSQL / RabbitMQ / EF Core / LLM Integration

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [High-Level End-to-End Flow](#2-high-level-end-to-end-flow)
3. [Repository Layout & Key Directories](#3-repository-layout--key-directories)
4. [Architecture Deep Dive](#4-architecture-deep-dive)
5. [Configuration & Runtime](#5-configuration--runtime)
6. [Database & Persistence](#6-database--persistence)
7. [Migrations: Step-by-Step Guide](#7-migrations-step-by-step-guide)
8. [Testing Strategy & How-To](#8-testing-strategy--how-to)
9. [Add a New Feature Example](#9-add-a-new-feature-example)
10. [Common Developer Workflows](#10-common-developer-workflows)
11. [Appendix: Index of Key Files](#11-appendix-index-of-key-files)

---

## 1. Executive Summary

### What This Project Is

API Testing Automation System is a **.NET 10 application** that automates the end-to-end workflow from ingesting API documentation to generating, executing, and reporting API tests. Built on a Modular Monolith architecture, it combines:

- **Rule-based testing** for deterministic validation
- **LLM assistance** for test scenario suggestions and failure explanations
- **Multi-source input** support (OpenAPI, Postman, cURL, Manual Entry)

### What Problem It Solves

The project addresses challenges in API testing:

- **Automated test generation** from API documentation
- **Comprehensive coverage** with happy-path, boundary, and negative tests
- **Dependency-aware execution** with variable extraction and request chaining
- **Deterministic validation** with clear pass/fail criteria
- **Intelligent explanations** for failed tests via LLM

### Key Architectural Patterns

| Pattern | Description |
|---------|-------------|
| **Modular Monolith** | Single deployment with well-defined module boundaries |
| **Single Database** | One PostgreSQL database with module-specific tables |
| **CQRS** | Command Query Responsibility Segregation via custom `Dispatcher` |
| **Clean Architecture** | Domain → Application → Infrastructure layering |
| **Outbox Pattern** | Reliable event publishing with transactional consistency |
| **Repository Pattern** | `IRepository<T, TKey>` abstraction over EF Core |

### High-Level Module Overview

| Module | Purpose |
|--------|---------|
| **Identity** | User/Role management, authentication, permissions |
| **Storage** | File upload/download for API documentation |
| **Notification** | Test completion alerts, email notifications |
| **AuditLog** | Centralized audit logging for all actions |
| **Configuration** | Application configuration entries |
| **ApiDocumentation** *(NEW)* | Parse and manage API specs |
| **TestGeneration** *(NEW)* | Generate test cases |
| **TestExecution** *(NEW)* | Execute tests, validate results |
| **TestReporting** *(NEW)* | Generate reports, export PDF/CSV |
| **LlmAssistant** *(NEW)* | LLM integration for suggestions/explanations |
| **Subscription** *(NEW)* | Billing, plans, usage tracking |

### How Code Is Organized

```
GSP26SE43.ModularMonolith/
├── Hosts/                      → Entry points (WebAPI, Background, Migrator)
├── Shared Layers/              → Core shared layers:
│   ├── Domain                  → Domain entities, events, interfaces
│   ├── Application             → CQRS handlers, services
│   ├── Infrastructure          → External integrations (LLM, messaging)
│   └── CrossCuttingConcerns    → Utilities (CSV, PDF, JSON parsing)
├── Modules/                    → Vertical slices (Identity, Storage, etc.)
├── Persistence/                → Database providers (PostgreSQL)
└── Contracts/                  → Shared interfaces/DTOs between modules
```

### Navigating the Codebase

1. **Start with**: [ClassifiedAds.WebAPI/Program.cs](ClassifiedAds.WebAPI/Program.cs) - composition root
2. **Reference module**: [ClassifiedAds.Modules.Product](ClassifiedAds.Modules.Product) - shows all patterns
3. **CQRS patterns**: [ClassifiedAds.Application](ClassifiedAds.Application) - Dispatcher, handlers
4. **Domain abstractions**: [ClassifiedAds.Domain](ClassifiedAds.Domain) - entities, events, interfaces

---

## 2. High-Level End-to-End Flow

### API Testing Workflow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        API TESTING SYSTEM FLOW                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                        1. INPUT SOURCES                              │    │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐            │    │
│  │  │ OpenAPI/ │  │ Postman  │  │  cURL    │  │  Manual  │            │    │
│  │  │ Swagger  │  │Collection│  │  Import  │  │  Entry   │            │    │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘            │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    2. DOCUMENTATION PARSER                           │    │
│  │  • Extract endpoints, methods, parameters                           │    │
│  │  • Parse request/response schemas                                   │    │
│  │  • Identify security requirements                                   │    │
│  │  • Normalize to unified internal model                              │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    3. TEST GENERATION ENGINE                         │    │
│  │  ┌─────────────────────────┐  ┌─────────────────────────────┐       │    │
│  │  │   Rule-Based Generator  │  │  LLM-Assisted Suggester     │       │    │
│  │  │  • Happy-path tests     │  │  • Boundary scenarios       │       │    │
│  │  │  • Schema-derived inputs│  │  • Edge case identification │       │    │
│  │  │  • Required param tests │  │  • Domain-specific cases    │       │    │
│  │  └─────────────────────────┘  └─────────────────────────────┘       │    │
│  │  ┌──────────────────────────────────────────────────────────┐       │    │
│  │  │                    Mutation Engine                        │       │    │
│  │  │  • Missing required fields  • Out-of-range values        │       │    │
│  │  │  • Invalid types            • Malformed requests         │       │    │
│  │  └──────────────────────────────────────────────────────────┘       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    4. TEST EXECUTION ENGINE                          │    │
│  │  • Dependency-aware execution (auth → protected APIs)               │    │
│  │  • Variable extraction (tokens, IDs)                                │    │
│  │  • Request chaining and reuse                                       │    │
│  │  • HTTP client with retry logic                                     │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                 5. RULE-BASED VALIDATOR (Deterministic)              │    │
│  │  • HTTP status code verification                                    │    │
│  │  • Response schema validation                                       │    │
│  │  • Contract conformance checks                                      │    │
│  │  • Pass/Fail decision (DETERMINISTIC - no LLM influence)           │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    6. LLM FAILURE ANALYSIS                           │    │
│  │  • Summarize observed mismatches                                    │    │
│  │  • Compare against documentation                                    │    │
│  │  • Suggest plausible causes                                         │    │
│  │  • (Does NOT affect pass/fail decisions)                           │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                     │                                        │
│                                     ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    7. REPORTING & EXPORT                             │    │
│  │  • Coverage summaries                                               │    │
│  │  • Run history                                                      │    │
│  │  • Failure details and logs                                         │    │
│  │  • PDF/CSV export                                                   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Request Lifecycle (HTTP API)

Every HTTP request follows this path:

```
Client → Middleware Pipeline → Controller → Dispatcher → Handler → Repository → Response
```

### Key Separation of Concerns

| Component | Responsibility |
|-----------|----------------|
| **Rule-Based Engine** | Deterministic execution and pass/fail evaluation |
| **LLM Component** | Interpretation, scenario suggestion, failure explanation |

**Critical**: LLM does NOT determine pass/fail outcomes. All validation is rule-based.

---

## 3. Repository Layout & Key Directories

### Solution Structure

```
D:\GSP26SE43.ModularMonolith\
│
├── ClassifiedAds.ModularMonolith.slnx    # Solution file
├── docker-compose.yml                     # Local development environment
├── .env                                   # Environment variables for Docker
├── global.json                            # .NET SDK version pinning
│
├── rules/                                 # ⚠️ STRICT RULES - MUST READ
│   ├── 00-priority.md                     # Rule priority order
│   ├── security.md                        # Security requirements
│   ├── architecture.md                    # Architecture rules
│   ├── testing.md                         # Testing requirements
│   ├── coding.md                          # C# coding standards
│   └── git-workflow.md                    # Git conventions
│
├── docs-architecture/                     # Architecture documentation
│   ├── 01-solution-structure.md
│   ├── 02-architecture-overview.md
│   ├── 03-request-lifecycle.md
│   ├── 04-cqrs-and-mediator.md
│   ├── 05-persistence-and-transactions.md
│   ├── 06-events-and-outbox.md
│   ├── 07-modules.md
│   └── ...
│
├── ClassifiedAds.WebAPI/                  # Main HTTP API host
├── ClassifiedAds.Background/              # Background worker service
├── ClassifiedAds.Migrator/                # Database migration runner
├── ClassifiedAds.AppHost/                 # .NET Aspire orchestration
│
├── ClassifiedAds.Application/             # CQRS infrastructure
├── ClassifiedAds.Domain/                  # Domain layer
├── ClassifiedAds.Infrastructure/          # External integrations
├── ClassifiedAds.Contracts/               # Shared interfaces
├── ClassifiedAds.CrossCuttingConcerns/    # Utilities
├── ClassifiedAds.Persistence.PostgreSQL/  # EF Core PostgreSQL
│
├── ClassifiedAds.Modules.AuditLog/
├── ClassifiedAds.Modules.Configuration/
├── ClassifiedAds.Modules.Identity/
├── ClassifiedAds.Modules.Notification/
├── ClassifiedAds.Modules.Storage/
└── ClassifiedAds.Modules.Product/         # ⭐ Reference module
```

### Module Structure Convention

Each module follows this structure:

```
ClassifiedAds.Modules.{ModuleName}/
├── Authorization/
│   └── Permissions.cs                    # Permission constants
├── Commands/
│   ├── AddUpdate{Entity}Command.cs       # Create/Update handler
│   ├── Delete{Entity}Command.cs          # Delete handler
│   └── PublishEventsCommand.cs           # Outbox publisher
├── ConfigurationOptions/
│   └── {ModuleName}ModuleOptions.cs      # Module settings
├── Constants/
│   └── EventTypeConstants.cs             # Event type strings
├── Controllers/
│   └── {Entity}Controller.cs             # API endpoints
├── DbConfigurations/
│   ├── {Entity}Configuration.cs          # EF entity config
│   └── OutboxMessageConfiguration.cs
├── Entities/
│   ├── {Entity}.cs                       # Main domain entity
│   └── OutboxMessage.cs                  # Outbox for events
├── EventHandlers/
│   ├── {Entity}CreatedEventHandler.cs
│   ├── {Entity}UpdatedEventHandler.cs
│   └── {Entity}DeletedEventHandler.cs
├── HostedServices/
│   └── PublishEventWorker.cs             # Background outbox publisher
├── Models/
│   └── {Entity}Model.cs                  # API DTOs
├── Persistence/
│   ├── {ModuleName}DbContext.cs          # Module's DbContext
│   ├── I{Entity}Repository.cs            # Repository interface
│   └── {Entity}Repository.cs             # Repository implementation
├── Queries/
│   ├── Get{Entity}Query.cs               # Get single entity
│   └── Get{Entities}Query.cs             # Get all entities
├── ServiceCollectionExtensions.cs         # DI registration
└── ClassifiedAds.Modules.{ModuleName}.csproj
```

---

## 4. Architecture Deep Dive

### 4.1 CQRS Pattern

#### Command (Write Operations)

```csharp
// Definition
public class AddUpdateTestCaseCommand : ICommand
{
    public TestCase TestCase { get; set; }
}

// Handler
public class AddUpdateTestCaseCommandHandler : ICommandHandler<AddUpdateTestCaseCommand>
{
    private readonly ICrudService<TestCase> _testCaseService;

    public async Task HandleAsync(AddUpdateTestCaseCommand command, CancellationToken ct = default)
    {
        await _testCaseService.AddOrUpdateAsync(command.TestCase, ct);
    }
}
```

#### Query (Read Operations)

```csharp
// Definition
public class GetTestCaseQuery : IQuery<TestCase>
{
    public Guid Id { get; set; }
    public bool ThrowNotFoundIfNull { get; set; }
}

// Handler
public class GetTestCaseQueryHandler : IQueryHandler<GetTestCaseQuery, TestCase>
{
    private readonly ITestCaseRepository _repository;

    public async Task<TestCase> HandleAsync(GetTestCaseQuery query, CancellationToken ct = default)
    {
        var testCase = await _repository.FirstOrDefaultAsync(
            _repository.GetQueryableSet().Where(x => x.Id == query.Id));
        
        if (query.ThrowNotFoundIfNull && testCase == null)
            throw new NotFoundException($"TestCase {query.Id} not found.");
        
        return testCase;
    }
}
```

#### Dispatcher Usage

```csharp
// In Controller
[HttpGet("{id}")]
public async Task<ActionResult<TestCaseModel>> Get(Guid id)
{
    var testCase = await _dispatcher.DispatchAsync(new GetTestCaseQuery 
    { 
        Id = id, 
        ThrowNotFoundIfNull = true 
    });
    return Ok(testCase.ToModel());
}
```

### 4.2 Domain Events & Outbox Pattern

```
CrudService.AddOrUpdateAsync()
    │
    ├── Repository.AddAsync(entity)
    ├── UnitOfWork.SaveChangesAsync()      ← Entity saved
    │
    └── Dispatcher.DispatchAsync(EntityCreatedEvent<T>)
            │
            └── EventHandler.HandleAsync()
                    │
                    ├── AuditLogRepository.Add()   ← Audit log
                    ├── OutboxRepository.Add()     ← Outbox message
                    └── SaveChangesAsync()
```

---

## 5. Configuration & Runtime

### Configuration Files

| File | Purpose |
|------|---------|
| `appsettings.json` | Base configuration |
| `appsettings.Development.json` | Development overrides |
| `.env` | Docker environment variables |

### Key Configuration Sections

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=ApiTestingSystem;Username=postgres;Password=xxx"
  },
  "Messaging": {
    "Provider": "RabbitMQ",
    "RabbitMQ": { "HostName": "localhost" }
  },
  "Modules": {
    "Identity": { ... },
    "LlmAssistant": {
      "Provider": "OpenAI",
      "ApiKey": "sk-xxx",
      "Model": "gpt-4"
    }
  }
}
```

---

## 6. Database & Persistence

### Single Database Strategy

All modules share one PostgreSQL database with table prefixes:

| Module | Table Prefix | Example Tables |
|--------|--------------|----------------|
| Identity | `Identity_` | `Identity_Users`, `Identity_Roles` |
| Storage | `Storage_` | `Storage_Files` |
| ApiDocumentation | `ApiDoc_` | `ApiDoc_Specifications`, `ApiDoc_Endpoints` |
| TestGeneration | `TestGen_` | `TestGen_TestCases`, `TestGen_TestSuites` |
| TestExecution | `TestExec_` | `TestExec_Runs`, `TestExec_Results` |

### Repository Pattern

```csharp
public interface ITestCaseRepository : IRepository<TestCase, Guid>
{
    Task<List<TestCase>> GetByEndpointIdAsync(Guid endpointId);
}
```

---

## 7. Migrations: Step-by-Step Guide

### Prerequisites

```powershell
# Install EF Core tools
dotnet tool install --global dotnet-ef --version 10.0.0
```

### Creating a Migration

```powershell
# Navigate to solution root
cd D:\GSP26SE43.ModularMonolith

# Create migration for a module
dotnet ef migrations add AddTestCaseEntity `
    --context TestGenerationDbContext `
    --project ClassifiedAds.Modules.TestGeneration `
    --startup-project ClassifiedAds.Migrator `
    -o Migrations/TestGenerationDb
```

### Applying Migrations

```powershell
# Apply all pending migrations
dotnet run --project ClassifiedAds.Migrator
```

---

## 8. Testing Strategy & How-To

### Test Categories

| Category | Purpose | Location |
|----------|---------|----------|
| Unit Tests | Test individual components | `ClassifiedAds.UnitTests` |
| Integration Tests | Test API endpoints | `ClassifiedAds.IntegrationTests` |
| Architecture Tests | Enforce module boundaries | `ClassifiedAds.UnitTests/Architecture` |

### Running Tests

```powershell
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific tests
dotnet test --filter "FullyQualifiedName~TestGeneration"
```

---

## 9. Add a New Feature Example

### Scenario: Add "Priority" Field to TestCase Entity

#### Step 1: Modify the Entity

```csharp
// ClassifiedAds.Modules.TestGeneration/Entities/TestCase.cs
public class TestCase : Entity<Guid>, IAggregateRoot
{
    public string Name { get; set; }
    public string Description { get; set; }
    public TestPriority Priority { get; set; }  // NEW
}

public enum TestPriority { Low, Medium, High, Critical }
```

#### Step 2: Update EF Configuration

```csharp
// DbConfigurations/TestCaseConfiguration.cs
builder.Property(x => x.Priority)
    .HasConversion<string>()
    .HasMaxLength(20);
```

#### Step 3: Create Migration

```powershell
dotnet ef migrations add AddTestCasePriority `
    --context TestGenerationDbContext `
    --project ClassifiedAds.Modules.TestGeneration `
    --startup-project ClassifiedAds.Migrator `
    -o Migrations/TestGenerationDb
```

#### Step 4: Update DTO

```csharp
// Models/TestCaseModel.cs
public class TestCaseModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Priority { get; set; }  // NEW
}
```

#### Step 5: Update Mapping

```csharp
public static TestCaseModel ToModel(this TestCase entity) => new()
{
    Id = entity.Id,
    Name = entity.Name,
    Description = entity.Description,
    Priority = entity.Priority.ToString()  // NEW
};
```

#### Step 6: Write Tests

```csharp
[Fact]
public async Task AddTestCase_WithPriority_ShouldSaveSuccessfully()
{
    var testCase = new TestCase
    {
        Name = "Login Test",
        Priority = TestPriority.High
    };

    await _handler.HandleAsync(new AddUpdateTestCaseCommand { TestCase = testCase });

    var saved = await _repository.GetByIdAsync(testCase.Id);
    Assert.Equal(TestPriority.High, saved.Priority);
}
```

---

## 10. Common Developer Workflows

### Adding a New Module

1. Create project: `ClassifiedAds.Modules.{ModuleName}`
2. Follow module structure convention
3. Add `ServiceCollectionExtensions.cs`
4. Register in `ClassifiedAds.WebAPI/Program.cs`
5. Create DbContext and migrations
6. Add architecture tests

### Adding a New API Endpoint

1. Create/update Entity in `Entities/`
2. Create Command/Query in `Commands/` or `Queries/`
3. Create Handler
4. Add endpoint to Controller
5. Create/update DTO in `Models/`
6. Write unit tests

### Git Workflow

```bash
# 1. Create feature branch
git checkout -b feature/add-test-priority

# 2. Make changes and test
dotnet build
dotnet test

# 3. Commit
git add .
git commit -m "feat: add priority field to TestCase"

# 4. Push and create PR
git push -u origin feature/add-test-priority
```

---

## 11. Appendix: Index of Key Files

| Purpose | File Path |
|---------|-----------|
| **Entry Point** | `ClassifiedAds.WebAPI/Program.cs` |
| **CQRS Dispatcher** | `ClassifiedAds.Application/Dispatcher.cs` |
| **Domain Entities** | `ClassifiedAds.Domain/Entities/` |
| **Repository Base** | `ClassifiedAds.Persistence.PostgreSQL/DbContextRepository.cs` |
| **Module Example** | `ClassifiedAds.Modules.Product/` |
| **Architecture Rules** | `rules/architecture.md` |
| **Project Requirements** | `PROJECT_REQUIREMENTS.md` |

---

## Quick Reference

### Thứ tự đọc file để hiểu dự án

1. **[PROJECT_REQUIREMENTS.md](PROJECT_REQUIREMENTS.md)** - Yêu cầu dự án API Testing
2. **[README.md](README.md)** - Quick start
3. **[PROJECT_GUIDE.md](PROJECT_GUIDE.md)** - Hướng dẫn chi tiết (file này)
4. **[docs-architecture/](docs-architecture/)** - Kiến trúc chi tiết
5. **[ClassifiedAds.Modules.Product/](ClassifiedAds.Modules.Product/)** - Module mẫu

### Cách thêm tính năng mới

```
1. Domain Entity      → Thêm/sửa property
2. EF Configuration   → Configure database mapping
3. Migration          → Create & Apply migration
4. DTO/Model          → Thêm field tương ứng
5. Command/Query      → Cập nhật logic xử lý
6. Controller         → Expose qua API
7. Unit Tests         → Test coverage
```
