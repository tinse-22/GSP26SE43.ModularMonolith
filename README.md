# ClassifiedAds Modular Monolith

A simplified .NET Modular Monolith template for local development and learning.

## Tech Stack

- **.NET 10** - Runtime
- **PostgreSQL** - Database (only provider)
- **RabbitMQ** - Message broker
- **MailHog** - Email testing
- **Redis** - Distributed cache
- **Docker Compose** - Local development orchestration (legacy)
- **.NET Aspire** - Developer orchestration and observability (recommended)

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 10.0+ |
| Docker Desktop | Latest |

## Solution Structure

```
/BuildingBlocks/
  ├── ClassifiedAds.Application        # Application services, CQRS handlers
  ├── ClassifiedAds.CrossCuttingConcerns # Utilities, extensions
  ├── ClassifiedAds.Domain             # Domain entities, events, interfaces
  ├── ClassifiedAds.Infrastructure     # External integrations (messaging, storage)
  └── ClassifiedAds.Persistence.PostgreSQL # EF Core PostgreSQL implementation

/Contracts/
  └── ClassifiedAds.Contracts          # Shared DTOs and contracts

/Hosts/
  ├── ClassifiedAds.Background         # Background worker service
  ├── ClassifiedAds.Migrator           # Database migration tool
  └── ClassifiedAds.WebAPI             # REST API host

/Modules/
  ├── ClassifiedAds.Modules.AuditLog
  ├── ClassifiedAds.Modules.Configuration
  ├── ClassifiedAds.Modules.Identity
  ├── ClassifiedAds.Modules.Notification
  ├── ClassifiedAds.Modules.Product
  └── ClassifiedAds.Modules.Storage
```

## Quick Start (Docker Compose)

### 1. Start Infrastructure

```bash
# Start PostgreSQL, RabbitMQ, and MailHog
docker-compose up -d db rabbitmq mailhog
```

### 2. Run Database Migrations

```bash
dotnet run --project ClassifiedAds.Migrator
```

### 3. Start the Web API

```bash
dotnet run --project ClassifiedAds.WebAPI
```

### 4. (Optional) Start Background Worker

```bash
dotnet run --project ClassifiedAds.Background
```

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
| WebAPI (Swagger) | http://localhost:9002/swagger | Dynamic (check dashboard) | REST API with Swagger UI |
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

# Restart service
docker-compose restart webapi

# Stop all
docker-compose down

# Remove volumes (clears all data)
docker-compose down -v

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
