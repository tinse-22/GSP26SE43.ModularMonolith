# ClassifiedAds Modular Monolith

A production-ready .NET Modular Monolith template demonstrating clean architecture, CQRS, domain events, and the outbox pattern for reliable event-driven communication.

## What is Modular Monolith?

A **Modular Monolith** combines the simplicity of monolithic deployment with the maintainability of modular architecture:
- ✅ Single deployment unit (no microservices complexity)
- ✅ Strong module boundaries enforced by architecture tests
- ✅ Shared database with logical separation
- ✅ Event-driven communication via outbox pattern
- ✅ Independent module development within a monolith

## Tech Stack

- **.NET 10** - Runtime
- **PostgreSQL** - Database (single database, module-specific schemas)
- **RabbitMQ** - Message broker for async inter-module communication
- **MailHog** - Email testing (dev environment)
- **Redis** - Distributed cache
- **Docker Compose** - Local development orchestration
- **.NET Aspire** - Developer orchestration and observability (recommended)

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 10.0+ |
| Docker Desktop | Latest |

## Solution Structure

```
ClassifiedAds.ModularMonolith/
│
├── Shared Layers (Building Blocks)/     # Core shared layers
│   ├── ClassifiedAds.Application        # CQRS (Dispatcher, ICommand, IQuery)
│   ├── ClassifiedAds.CrossCuttingConcerns # Utilities (CSV, PDF, Excel, DateTime)
│   ├── ClassifiedAds.Domain             # Domain entities, events, interfaces
│   ├── ClassifiedAds.Infrastructure     # Messaging, storage, monitoring
│   └── ClassifiedAds.Persistence.PostgreSQL # EF Core + PostgreSQL
│
├── Contracts/                           # Inter-module contracts
│   └── ClassifiedAds.Contracts          # Shared interfaces/DTOs (ICurrentUser, etc.)
│
├── Hosts/                               # Application entry points
│   ├── ClassifiedAds.WebAPI             # REST API (Scalar UI)
│   ├── ClassifiedAds.Background         # Background workers (outbox publisher)
│   ├── ClassifiedAds.Migrator           # Database migrations
│   └── ClassifiedAds.AppHost            # .NET Aspire orchestration
│
├── Modules/                             # Vertical slice modules (self-contained)
│   ├── ClassifiedAds.Modules.Product    # Product catalog (reference implementation)
│   ├── ClassifiedAds.Modules.Identity   # Users, roles, authentication
│   ├── ClassifiedAds.Modules.Storage    # File upload/download
│   ├── ClassifiedAds.Modules.Notification # Email, SMS, push notifications
│   ├── ClassifiedAds.Modules.AuditLog   # Centralized audit logging
│   └── ClassifiedAds.Modules.Configuration # App configuration management
│
├── tests/
│   ├── ClassifiedAds.UnitTests          # Unit tests + Architecture tests
│   └── ClassifiedAds.IntegrationTests   # Smoke tests
│
├── docs-architecture/                   # Architecture documentation
├── rules/                               # Enforced architecture rules
└── libs/                                # Third-party dependencies
```

### Module Structure (Standard Convention)

Each module follows this structure per `rules/architecture.md [ARCH-003]`:

```
ClassifiedAds.Modules.{ModuleName}/
├── Authorization/           # Permission policies and handlers
├── Commands/                # CQRS commands + handlers
├── ConfigurationOptions/    # {ModuleName}ModuleOptions.cs
├── Constants/               # EventTypeConstants, keys
├── Controllers/             # API endpoints (thin, delegate to Dispatcher)
├── DbConfigurations/        # EF Core entity configurations
├── Entities/                # Domain entities + OutboxMessage
├── EventHandlers/           # Domain event handlers (create outbox)
├── HostedServices/          # Background workers (publish outbox)
├── Models/                  # API request/response DTOs
├── Persistence/             # DbContext, repositories
├── Queries/                 # CQRS queries + handlers
└── ServiceCollectionExtensions.cs  # DI registration entry point
```

## Quick Start

### Option A: .NET Aspire (Recommended)

**Best for:** Full observability, automatic service orchestration, one-click start.

```bash
# Run the Aspire AppHost (starts everything)
dotnet run --project ClassifiedAds.AppHost

# Open Aspire Dashboard (URL shown in console)
# - View logs, traces, metrics in real-time
# - Access WebAPI via dynamic port shown in dashboard
```

Aspire automatically:
- Starts PostgreSQL, RabbitMQ, Redis, MailHog containers
- Runs database migrations
- Starts WebAPI and Background worker
- Configures OpenTelemetry observability

---

### Option B: Docker Compose + .NET CLI

**Best for:** Standard Docker workflow without Aspire.

```bash
# 1. Start infrastructure
docker-compose up -d db rabbitmq mailhog redis

# 2. Run migrations
dotnet run --project ClassifiedAds.Migrator

# 3. Start Web API
dotnet run --project ClassifiedAds.WebAPI

# 4. (Optional) Start Background Worker
dotnet run --project ClassifiedAds.Background
```

**Access points:**
- WebAPI: http://localhost:9002/docs (Scalar UI)
- RabbitMQ: http://localhost:15672 (guest/guest)
- MailHog: http://localhost:8025

---

### Option C: Docker Compose (Full Containerized)

To run everything in Docker (including the app services):

```bash
# Build and start all services
docker-compose up -d --build

# View logs
docker-compose logs -f

# Stop all services
docker-compose down

# Stop and remove volumes (clears database)
docker-compose down -v
```

---

## Service URLs (All Modes)

| Service | Docker Compose | Aspire | Notes |
|---------|----------------|--------|-------|
| WebAPI (Scalar) | http://localhost:9002/docs | Dynamic (check dashboard) | REST API with Scalar UI |
| RabbitMQ Management | http://localhost:15672 | http://localhost:15672 | guest / guest |
| MailHog (Email UI) | http://localhost:8025 | http://localhost:8025 | Catches dev emails |
| PostgreSQL | localhost:5432 | localhost:5432 | postgres / Postgres123@ |
| pgAdmin | - | http://localhost:5050 | Aspire only |
| Redis | localhost:6379 | localhost:6379 | Distributed cache |

---

## Database Migrations

### Running Migrations

**With Aspire (Automatic)**:
- Migrations run automatically when you start the AppHost
- The migrator runs before WebAPI and Background start

**Manually** (Docker Compose or standalone):
```bash
dotnet run --project ClassifiedAds.Migrator
```

**Via EF CLI**:
```bash
dotnet ef database update --context ProductDbContext --project ClassifiedAds.Migrator
```

### Creating New Migrations

```bash
# Install dotnet-ef tool if not already installed
dotnet tool install --global dotnet-ef --version="10.0"

# Navigate to ClassifiedAds.Migrator and create migration
cd ClassifiedAds.Migrator
dotnet ef migrations add YourMigrationName --context ProductDbContext -o Migrations/ProductDb

# Apply the migration
dotnet run
```

### Resetting Local Database

**With Aspire**:
1. Stop the AppHost (`Ctrl+C`)
2. Remove Docker volumes:
   ```bash
   docker volume rm aspire-postgres_data
   docker volume rm aspire-redis_data
   ```
3. Restart AppHost

**With Docker Compose**:
```bash
docker-compose down -v
docker-compose up -d db
dotnet run --project ClassifiedAds.Migrator
```

---

## Observability

### With Aspire

Aspire provides built-in observability via the Aspire Dashboard:
- **Logs**: Real-time structured logs from all services
- **Traces**: Distributed tracing across HTTP and database calls
- **Metrics**: Performance metrics (request rates, errors, latency)
- **Resources**: Health status of all containers and projects

All services automatically export telemetry to the Aspire Dashboard (OpenTelemetry OTLP endpoint).

### Without Aspire (Standalone)

Each service can export telemetry independently if configured in `appsettings.json`:

```json
{
  "Monitoring": {
    "OpenTelemetry": {
      "IsEnabled": true,
      "Otlp": {
        "IsEnabled": true,
        "Endpoint": "http://localhost:4317"
      }
    }
  }
}
```

You can run a local OpenTelemetry collector or Jaeger to visualize traces.

---

## Common Tasks

### Re-run Migrations Only

```bash
# Aspire (stop AppHost, run migrator, restart AppHost)
dotnet run --project ClassifiedAds.Migrator

# Docker Compose
docker-compose up -d db
dotnet run --project ClassifiedAds.Migrator
```

### Check if Services are Healthy

**Aspire**: Check the Aspire Dashboard

**Manual**:
```bash
curl http://localhost:9002/health
curl http://localhost:9002/alive
```

### View Service Logs

**Aspire**: Use the Aspire Dashboard (Logs view)

**Docker Compose**:
```bash
docker-compose logs -f webapi
docker-compose logs -f background
```

**Standalone**:
Logs are written to console and optionally to files (configured in `appsettings.json`).

---

## Configuration

### Connection Strings

**Aspire**: Automatically injected. All modules share a single PostgreSQL database.

**Docker Compose**: Configure in `.env` file (see `.env` for defaults).

**Standalone**: Configure in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Host=127.0.0.1;Port=5432;Database=ClassifiedAds;Username=postgres;Password=Postgres123@"
  }
}
```

Each module reads `ConnectionStrings:Default` or falls back to `Modules:{ModuleName}:ConnectionStrings:Default`.

### Messaging (RabbitMQ)

**Aspire**: Automatically configured with service discovery.

**Docker Compose / Standalone**:
```json
{
  "Messaging": {
    "Provider": "RabbitMQ",
    "RabbitMQ": {
      "HostName": "localhost" // or "rabbitmq" in Docker
    }
  }
}
```

### Email (MailHog)

**Aspire**: SMTP host/port injected automatically into Background worker.

**Docker Compose / Standalone**:
```json
{
  "Modules": {
    "Notification": {
      "Email": {
        "Provider": "SmtpClient",
        "SmtpClient": {
          "Host": "localhost", // or "mailhog" in Docker
          "Port": 1025
        }
      }
    }
  }
}
```

---

## Docker Commands Reference

```bash
# Start infrastructure only (for local .NET development)
docker-compose up -d db rabbitmq mailhog redis

# Build images
docker-compose build

# Start specific service
docker-compose up -d webapi

# View service logs
docker-compose logs -f webapi
Key Architectural Patterns

### 1. **Module Boundaries**
- Modules **MUST NOT** reference each other directly
- Cross-module communication:
  - **Sync**: Via `ClassifiedAds.Contracts` interfaces
  - **Async**: Via domain events + outbox pattern
- Enforced by architecture tests (see [ClassifiedAds.UnitTests/Architecture/](ClassifiedAds.UnitTests/Architecture/))

### 2. **CQRS with Custom Dispatcher**
- Commands: Write operations (no return values)
- Queries: Read operations (return data)
- All dispatched via `Dispatcher` (NOT MediatR)

```csharp
// Controller example (thin, delegates to Dispatcher)
[HttpGet("{id}")]
public async Task<ActionResult<ProductModel>> Get(Guid id)
{
    var product = await _dispatcher.DispatchAsync(
        new GetProductQuery { Id = id, ThrowNotFoundIfNull = true });
    return Ok(product.ToModel());
}
```

### 3. **Outbox Pattern for Reliability**
- Domain events saved to `OutboxMessage` table transactionally
- Background worker publishes to message bus (RabbitMQ)
- Guarantees at-least-once delivery
- Recent improvements:
  - ✅ Throws exception if publisher missing (no silent failures)
  - ✅ Retry logic for failed events

### 4. **Clean Architecture Layers**
```
Controllers → Dispatcher → Command/Query Handlers → Domain Services → Repositories → DbContext
```
- **Domain** layer has no dependencies
- **Application** layer depends only on Domain
- **Infrastructure** implements Domain interfaces

### 5. **Database Per Module Pattern**
- Single PostgreSQL database
- Each module has its own `DbContext`
- Table naming: `{ModuleName}_{TableName}` (e.g., `Product_Products`)
- Modules **MUST NOT** access other module's DbContext

---

## Running Tests

### Unit Tests + Architecture Tests
```bash
# Run all tests
dotnet test

# Run only architecture tests
dotnet test --filter "FullyQualifiedName~Architecture"

# Run specific module tests
dotnet test --filter "FullyQualifiedName~Product"
```

**Architecture Tests Enforce:**
- ✅ Modules do not reference each other
- ✅ Domain layer has no external dependencies
- ✅ Controllers use Dispatcher (not direct DbContext)
- ✅ Contracts project has no module dependencies

### Integration Tests
```bash
cd ClassifiedAds.IntegrationTests
dotnet test
```

---

## Architecture Documentation

| Document | Description |
|----------|-------------|
| [rules/architecture.md](rules/architecture.md) | **Enforced rules** (ARCH-001 to ARCH-100+) |
| [docs-architecture/](docs-architecture/) | Deep dive into patterns and decisions |
| [PROJECT_GUIDE.md](PROJECT_GUIDE.md) | Developer onboarding guide |
| [ARCHITECTURE_IMPROVEMENTS.md](ARCHITECTURE_IMPROVEMENTS.md) | Recent reliability improvements |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Contribution guidelines |

---

## Key Features Demonstrated

- ✅ **Modular Monolith** with strict boundaries
- ✅ **CQRS** with custom Dispatcher (no MediatR)
- ✅ **Outbox Pattern** for reliable messaging
- ✅ **Domain Events** for decoupled side effects
- ✅ **Repository Pattern** with EF Core
- ✅ **JWT Authentication** + Permission-based authorization
- ✅ **Rate Limiting** per endpoint
- ✅ **Architecture Tests** (NetArchTest)
- ✅ **OpenTelemetry** observability (traces, logs, metrics)
- ✅ **Aspire Integration** for local development
- ✅ **Scalar UI** for API documentation
- ✅ **Background Workers** for async processing
- ✅ **Multi-storage** support (Azure Blob, AWS S3, Local)

---

## Troubleshooting

### Migrations fail with "database already exists"
```bash
# Reset database
docker-compose down -v
docker-compose up -d db
dotnet run --project ClassifiedAds.Migrator
```

### RabbitMQ connection refused
```bash
# Restart RabbitMQ container
docker-compose restart rabbitmq

# Check if RabbitMQ is ready
docker-compose logs rabbitmq
```

### Architecture tests fail
```bash
# Rebuild solution to ensure latest references
dotnet clean
dotnet build
dotnet test --filter "FullyQualifiedName~Architecture" --verbosity detailed
```

### Background worker not publishing events
- Check RabbitMQ connection in logs
- Verify outbox events exist: `SELECT * FROM Product_OutboxMessages WHERE Published = false`
- Check for missing event publishers (recent fix prevents silent failures)

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on contributing to this project.

**Before submitting PR:**
1. ✅ Run architecture tests: `dotnet test --filter Architecture`
2. ✅ Ensure controllers are thin (delegate to Dispatcher)
3. ✅ Follow module structure convention ([ARCH-003])
4. ✅ Update documentation if adding new patterns

# List volumes
docker volume ls

# Prune unused volumes
docker volume prune
```

---

## Architecture Documentation

See [docs-architecture/README.md](docs-architecture/README.md) for detailed architecture documentation.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on contributing to this project.
