# Docker Warm-Up Report

> **Generated**: January 1, 2026 18:42 (UTC+7)  
> **Repository**: GSP26SE43.ModularMonolith  
> **Status**: ✅ All infrastructure services running

---

## Table of Contents

- [Docker Environment](#docker-environment)
- [Discovered Services](#discovered-services)
- [Image Pull Commands](#image-pull-commands)
- [Pulled Images Summary](#pulled-images-summary)
- [Container Status](#container-status)
- [Service URLs](#service-urls)
- [Known Issues](#known-issues)
- [Next Steps](#next-steps)

---

## Docker Environment

### Versions

| Component | Version |
|-----------|---------|
| Docker Engine | 29.1.3 |
| Docker Compose | v2.40.3-desktop.1 |
| Docker Desktop | Latest |
| Context | desktop-linux |

### Docker Plugins Installed

- Docker Buildx v0.30.1
- Docker Compose v2.40.3
- Docker AI Agent (Gordon) v1.17.1
- Docker Debug v0.0.45
- Docker MCP v0.34.0

---

## Discovered Services

### From `docker-compose.yml`

| Service | Image | Ports | Purpose |
|---------|-------|-------|---------|
| `db` | `postgres:16` | 5432 | PostgreSQL database |
| `rabbitmq` | `rabbitmq:3-management` | 5672, 15672 | Message broker with management UI |
| `mailhog` | `mailhog/mailhog` | 1025, 8025 | Email testing (SMTP + Web UI) |
| `redis` | `redis:7-alpine` | 6379 | Cache with persistence |
| `migrator` | `classifiedads.modularmonolith.migrator` | - | Database migrations (build required) |
| `webapi` | `classifiedads.modularmonolith.webapi` | 9002 | REST API (build required) |
| `background` | `classifiedads.modularmonolith.background` | - | Background worker (build required) |

### Testcontainers (for Integration Tests)

| Image | Purpose |
|-------|---------|
| `testcontainers/ryuk:latest` | Container cleanup helper |
| `postgres:16` | Test database container |

---

## Image Pull Commands

### Infrastructure Images

```powershell
# PostgreSQL 16
docker pull postgres:16

# RabbitMQ with Management Console
docker pull rabbitmq:3-management

# MailHog for email testing
docker pull mailhog/mailhog

# Redis 7 Alpine
docker pull redis:7-alpine
```

### Testcontainers Images

```powershell
# Ryuk helper (auto-cleanup)
docker pull testcontainers/ryuk:latest
```

### Docker Compose Pull

```powershell
# Pull all infrastructure images via compose
docker compose pull db rabbitmq mailhog redis
```

---

## Pulled Images Summary

| Repository | Tag | Size | Notes |
|------------|-----|------|-------|
| `postgres` | 16 | 641 MB | Latest PostgreSQL 16 |
| `rabbitmq` | 3-management | 392 MB | Includes management plugin |
| `redis` | 7-alpine | 61.2 MB | Lightweight Alpine variant |
| `mailhog/mailhog` | latest | 572 MB | Email testing tool |
| `testcontainers/ryuk` | latest | 19 MB | Test container cleanup |

**Total Size**: ~1.7 GB

---

## Container Status

### Running Containers

| Container Name | Image | Status | Ports |
|----------------|-------|--------|-------|
| `gsp26se43modularmonolith-db-1` | postgres:16 | Up | 0.0.0.0:5432→5432/tcp |
| `gsp26se43modularmonolith-rabbitmq-1` | rabbitmq:3-management | Up | 0.0.0.0:5672→5672/tcp, 0.0.0.0:15672→15672/tcp |
| `gsp26se43modularmonolith-mailhog-1` | mailhog/mailhog | Up | 0.0.0.0:1025→1025/tcp, 0.0.0.0:8025→8025/tcp |
| `gsp26se43modularmonolith-redis-1` | redis:7-alpine | Up | 0.0.0.0:6379→6379/tcp |

### Docker Compose Volumes Created

- `gsp26se43modularmonolith_postgres_data` - PostgreSQL data persistence
- `gsp26se43modularmonolith_redis_data` - Redis AOF persistence

### Health Check Summary

| Service | Status | Notes |
|---------|--------|-------|
| PostgreSQL | ✅ Healthy | Database `ClassifiedAds` created, accepting connections |
| RabbitMQ | ✅ Healthy | Management UI ready, TCP listener on 5672 |
| MailHog | ✅ Healthy | SMTP on 1025, Web UI on 8025 |
| Redis | ✅ Healthy | AOF persistence enabled, accepting connections |

---

## Service URLs

| Service | URL | Credentials |
|---------|-----|-------------|
| PostgreSQL | `localhost:5432` | `postgres` / `<YOUR_PASSWORD>` |
| RabbitMQ Management | http://localhost:15672 | `guest` / `guest` |
| MailHog Web UI | http://localhost:8025 | - |
| Redis | `localhost:6379` | - |

---

## Known Issues

### 1. Docker Compose Version Warning

**Issue**: Warning message about `version` attribute being obsolete.

```
the attribute `version` is obsolete, it will be ignored
```

**Impact**: None - this is informational only.

**Note**: The `version: "3.6"` in `docker-compose.yml` can be removed in future cleanup, but it does not affect functionality.

### 2. Missing `.env` File

**Issue**: No `.env` file found in repository root.

**Impact**: Application services (`migrator`, `webapi`, `background`) use environment variables from `.env` that are not defined. Infrastructure services work without it.

**Workaround**: Create `.env` file before running application services:

```env
DOTNET_ENVIRONMENT=Development
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__Default=Host=localhost;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>
Storage__Provider=Local
Storage__Local__Path=/files
Messaging__Provider=RabbitMQ
Messaging__RabbitMQ__HostName=rabbitmq
```

### 3. Application Images Not Built

**Issue**: `migrator`, `webapi`, and `background` services require building from Dockerfiles.

**Note**: This is expected - those are application containers that need to be built, not pulled.

---

## Next Steps

### Quick Start (Recommended Sequence)

```powershell
# 1. Infrastructure is already running (from warm-up)
# Verify with:
docker compose ps

# 2. Run database migrations
dotnet run --project ClassifiedAds.Migrator

# 3. Start WebAPI locally
dotnet run --project ClassifiedAds.WebAPI

# 4. (Optional) Start background workers
dotnet run --project ClassifiedAds.Background

# 5. Open Swagger UI
Start-Process "http://localhost:9002/swagger"
```

### Full Docker Deployment

```powershell
# Create .env file first (see Known Issues section)

# Build and start all services
docker compose up -d --build

# Check logs
docker compose logs -f

# Stop everything
docker compose down
```

### Running Integration Tests

```powershell
# Testcontainers images are pre-pulled
# Run tests (will auto-create PostgreSQL containers)
dotnet test ClassifiedAds.IntegrationTests
```

### Stop Infrastructure

```powershell
# Stop containers (preserves volumes)
docker compose down

# Stop and remove volumes
docker compose down -v
```

---

## Appendix: Full Docker Commands Executed

```powershell
# 1. Verify Docker setup
docker --version
docker compose version
docker info

# 2. Pull infrastructure images
docker pull postgres:16
docker pull rabbitmq:3-management
docker pull mailhog/mailhog
docker pull redis:7-alpine

# 3. Pull Testcontainers helper
docker pull testcontainers/ryuk:latest

# 4. Docker Compose pull
docker compose pull db rabbitmq mailhog redis

# 5. Start infrastructure containers
docker compose up -d db rabbitmq mailhog redis

# 6. Verify status
docker ps
docker images
docker compose ps
docker compose logs --tail=50
```

---

**Report Complete** ✅
