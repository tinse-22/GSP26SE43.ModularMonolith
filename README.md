# API Testing Automation System

A comprehensive API testing platform built on .NET Modular Monolith architecture, combining rule-based validation with LLM-assisted test generation and failure analysis.

## What is This Project?

An **automated API testing system** that:
- ✅ Ingests API documentation (OpenAPI/Swagger, Postman, cURL, Manual Entry)
- ✅ Generates comprehensive test cases (happy-path, boundary, negative)
- ✅ Executes tests with dependency-aware chaining
- ✅ Validates using deterministic rule-based evaluation
- ✅ Provides LLM-assisted failure explanations
- ✅ Generates detailed execution reports (PDF/CSV)

## Key Features

| Feature | Description |
|---------|-------------|
| **Multi-Source Input** | OpenAPI/Swagger, Postman Collections, Manual Entry, cURL Import |
| **Smart Test Generation** | Rule-based mutations + LLM-assisted scenario suggestions |
| **Dependency-Aware Execution** | Token extraction, request chaining, variable reuse |
| **Deterministic Validation** | Status codes, schema validation, contract conformance |
| **LLM Explanations** | AI-assisted failure analysis (does NOT affect pass/fail) |
| **Subscription Management** | Plan-based limits, usage tracking, billing integration |

## Tech Stack

- **.NET 10** - Runtime
- **PostgreSQL** - Database (single database, module-specific schemas)
- **RabbitMQ** - Message broker for async communication
- **Redis** - Distributed cache
- **OpenAI/Azure OpenAI** - LLM integration
- **Docker Compose** - Local development orchestration
- **.NET Aspire** - Developer orchestration and observability

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 10.0+ |
| Docker Desktop | Latest |

## Solution Structure

```
GSP26SE43.ModularMonolith/
│
├── Shared Layers (Building Blocks)/
│   ├── ClassifiedAds.Application        # CQRS (Dispatcher, ICommand, IQuery)
│   ├── ClassifiedAds.CrossCuttingConcerns # Utilities (CSV, PDF, JSON parsing)
│   ├── ClassifiedAds.Domain             # Domain entities, events, interfaces
│   ├── ClassifiedAds.Infrastructure     # Messaging, storage, LLM clients
│   └── ClassifiedAds.Persistence.PostgreSQL # EF Core + PostgreSQL
│
├── Contracts/
│   └── ClassifiedAds.Contracts          # Shared interfaces/DTOs
│
├── Hosts/
│   ├── ClassifiedAds.WebAPI             # REST API (Scalar UI)
│   ├── ClassifiedAds.Background         # Background workers (test execution)
│   ├── ClassifiedAds.Migrator           # Database migrations
│   └── ClassifiedAds.AppHost            # .NET Aspire orchestration
│
├── Modules/                             # Business modules
│   ├── ClassifiedAds.Modules.Identity   # User auth, roles, permissions
│   ├── ClassifiedAds.Modules.Storage    # File upload (API docs storage)
│   ├── ClassifiedAds.Modules.Notification # Alerts, completion notifications
│   ├── ClassifiedAds.Modules.AuditLog   # Action tracking
│   ├── ClassifiedAds.Modules.Configuration # App settings
│   └── [NEW MODULES TO BE ADDED]        # ApiDocumentation, TestGeneration, etc.
│
├── tests/
│   ├── ClassifiedAds.UnitTests          # Unit tests + Architecture tests
│   └── ClassifiedAds.IntegrationTests   # Integration tests
│
├── docs-architecture/                   # Architecture documentation
└── rules/                               # Enforced architecture rules
```

### Planned New Modules

| Module | Purpose |
|--------|---------|
| **ApiDocumentation** | Upload, parse, normalize API specs |
| **TestGeneration** | Generate test cases (happy-path, boundary, negative) |
| **TestExecution** | Execute tests, validate results |
| **TestReporting** | Generate reports, PDF/CSV export |
| **LlmAssistant** | LLM integration for suggestions and explanations |
| **Subscription** | Billing, plans, usage tracking |

## Quick Start

### Option A: .NET Aspire (Recommended)

```bash
# Run the Aspire AppHost (starts everything)
dotnet run --project ClassifiedAds.AppHost

# Open Aspire Dashboard (URL shown in console)
```

### Option B: Docker Compose + .NET CLI

```bash
# 1. Start infrastructure
docker-compose up -d db rabbitmq redis

# 2. Run migrations
dotnet run --project ClassifiedAds.Migrator

# 3. Start Web API
dotnet run --project ClassifiedAds.WebAPI

# 4. (Optional) Start Background Worker
dotnet run --project ClassifiedAds.Background
```

## Service URLs

| Service | URL | Notes |
|---------|-----|-------|
| WebAPI (Scalar) | http://localhost:9002/docs | REST API documentation |
| RabbitMQ | http://localhost:15672 | guest / guest |
| PostgreSQL | localhost:5432 | postgres / Postgres123@ |
| Redis | localhost:6379 | Distributed cache |

## Project Documentation

| Document | Description |
|----------|-------------|
| [PROJECT_REQUIREMENTS.md](PROJECT_REQUIREMENTS.md) | Functional & non-functional requirements |
| [PROJECT_GUIDE.md](PROJECT_GUIDE.md) | Developer onboarding guide |
| [docs-architecture/](docs-architecture/) | Architecture deep dive |
| [rules/](rules/) | Enforced architecture rules |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Contribution guidelines |

## API Testing System Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              WORKFLOW OVERVIEW                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   1. INPUT                    2. PARSE                   3. GENERATE        │
│   ┌─────────────┐            ┌─────────────┐            ┌─────────────┐    │
│   │ OpenAPI/    │            │   Unified   │            │   Test      │    │
│   │ Swagger     │──────────► │   Internal  │──────────► │   Cases     │    │
│   │ Postman     │            │   Model     │            │ (Rule+LLM)  │    │
│   │ cURL        │            │             │            │             │    │
│   │ Manual      │            │             │            │             │    │
│   └─────────────┘            └─────────────┘            └─────────────┘    │
│                                                                │             │
│                                                                ▼             │
│   6. REPORT                  5. EXPLAIN                  4. EXECUTE        │
│   ┌─────────────┐            ┌─────────────┐            ┌─────────────┐    │
│   │  Coverage   │◄────────── │    LLM      │◄────────── │  Dependency │    │
│   │  Summary    │            │  Failure    │            │   Aware     │    │
│   │  PDF/CSV    │            │  Analysis   │            │  Executor   │    │
│   └─────────────┘            └─────────────┘            └─────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Key Architecture Patterns

| Pattern | Usage |
|---------|-------|
| **Modular Monolith** | Single deployment with strict module boundaries |
| **CQRS** | Separate read/write operations via Dispatcher |
| **Outbox Pattern** | Reliable event publishing |
| **Clean Architecture** | Domain → Application → Infrastructure |
| **Repository Pattern** | Data access abstraction |

## Running Tests

```bash
# Run all tests
dotnet test

# Run architecture tests
dotnet test --filter "FullyQualifiedName~Architecture"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

**Before submitting PR:**
1. ✅ Run architecture tests
2. ✅ Follow module structure convention
3. ✅ Update documentation if adding new patterns

## License

This project is licensed under the MIT License.
