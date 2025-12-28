# ClassifiedAds Modular Monolith

A simplified .NET Modular Monolith template for local development and learning.

## Tech Stack

- **.NET 10** - Runtime
- **PostgreSQL** - Database (only provider)
- **RabbitMQ** - Message broker
- **MailHog** - Email testing
- **Docker Compose** - Local development orchestration

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

## Full Docker Compose Setup

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

## Service URLs

| Service | URL | Credentials |
|---------|-----|-------------|
| WebAPI (Swagger) | http://localhost:9002/swagger | - |
| RabbitMQ Management | http://localhost:15672 | guest / guest |
| MailHog (Email UI) | http://localhost:8025 | - |
| PostgreSQL | localhost:5432 | postgres / postgres123!@# |

## Connection String Format (PostgreSQL)

```
Host=127.0.0.1;Port=5432;Database=ClassifiedAds_Product;Username=postgres;Password=postgres123!@#
```

## Configuration

### Environment Variables (.env file)

The `.env` file contains all environment variables for docker-compose:

```env
ASPNETCORE_ENVIRONMENT=Development
DOTNET_ENVIRONMENT=Development
Messaging__Provider=RabbitMQ
Messaging__RabbitMQ__HostName=rabbitmq
Storage__Provider=Local
Storage__Local__Path=/files
Modules__AuditLog__ConnectionStrings__Default=Host=db;Port=5432;Database=ClassifiedAds_AuditLog;Username=postgres;Password=postgres123!@#
Modules__Configuration__ConnectionStrings__Default=Host=db;Port=5432;Database=ClassifiedAds_Configuration;Username=postgres;Password=postgres123!@#
Modules__Identity__ConnectionStrings__Default=Host=db;Port=5432;Database=ClassifiedAds_Identity;Username=postgres;Password=postgres123!@#
Modules__Notification__ConnectionStrings__Default=Host=db;Port=5432;Database=ClassifiedAds_Notification;Username=postgres;Password=postgres123!@#
Modules__Product__ConnectionStrings__Default=Host=db;Port=5432;Database=ClassifiedAds_Product;Username=postgres;Password=postgres123!@#
Modules__Storage__ConnectionStrings__Default=Host=db;Port=5432;Database=ClassifiedAds_Storage;Username=postgres;Password=postgres123!@#
```

### Local Development (without Docker for app)

Update `appsettings.Development.json` in WebAPI/Background/Migrator with localhost connection strings:

```json
{
  "Modules": {
    "Product": {
      "ConnectionStrings": {
        "Default": "Host=127.0.0.1;Port=5432;Database=ClassifiedAds_Product;Username=postgres;Password=postgres123!@#"
      }
    }
  }
}
```

## Database Migrations

### Create New Migration

```bash
# Install dotnet-ef tool
dotnet tool install --global dotnet-ef --version="10.0"

# Navigate to ClassifiedAds.Migrator and create migration
dotnet ef migrations add YourMigrationName --context ProductDbContext -o Migrations/ProductDb
```

### Apply Migrations

```bash
# Run all pending migrations
dotnet run --project ClassifiedAds.Migrator

# Or via EF CLI
dotnet ef database update --context ProductDbContext --project ClassifiedAds.Migrator
```

## Architecture Documentation

See [docs-architecture/README.md](docs-architecture/README.md) for detailed architecture documentation.

## Docker Commands Reference

```bash
# Start infrastructure only
docker-compose up -d db rabbitmq mailhog

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

# Remove volumes
docker-compose down -v
```
