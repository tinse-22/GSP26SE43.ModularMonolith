# Environment Variables & Security Best Practices

## Overview

This project uses environment variables to manage sensitive configuration data, keeping credentials and secrets out of version control.

## File Structure

- **`.env`** - Contains actual sensitive values (NEVER commit to git)
- **`.env.example`** - Template file showing required variables (safe to commit)
- **`docker-compose.yml`** - References environment variables using `${VARIABLE_NAME}` syntax

## Security Improvements Implemented

### ✅ What Was Changed

1. **Database Credentials** - Moved from hardcoded values in docker-compose.yml to .env file
   - `POSTGRES_USER`
   - `POSTGRES_PASSWORD`
   - `POSTGRES_DB`

2. **Connection Strings** - Already using environment variables, maintained in .env

3. **Application Settings** - Centralized in .env file

## Quick Start

### For New Developers

1. Copy the example file:
   ```bash
   cp .env.example .env
   ```

2. Update `.env` with your actual credentials:
   ```dotenv
   POSTGRES_PASSWORD=your_actual_password
   ConnectionStrings__Default=Host=127.0.0.1;Port=55432;Database=ClassifiedAds;Username=postgres;Password=your_actual_password
   ```

3. Start the recommended local flow:
   ```bash
   docker compose up -d db rabbitmq redis mailhog
   dotnet run --project ClassifiedAds.Migrator
   dotnet run --project ClassifiedAds.WebAPI
   dotnet run --project ClassifiedAds.Background
   ```

### Mode Separation

- `.env` is the source of truth for the standalone local flow (`docker compose` + `dotnet run`)
- `ClassifiedAds.AppHost` uses its own local PostgreSQL container by default
- AppHost local PostgreSQL now persists in Docker volume `classifiedads_apphost_postgres_data`
- AppHost local PostgreSQL binds to `localhost:5432` for a stable pgAdmin/DBeaver connection
- `docker ps` may still show a random container port under Aspire; use `localhost:5432` from host-side tools
- Do not run AppHost and standalone hosts at the same time unless you intentionally set the same `ConnectionStrings__Default` for AppHost in the current shell

## Environment Variables Reference

### Database Configuration
| Variable | Description | Example |
|----------|-------------|---------|
| `POSTGRES_USER` | PostgreSQL username | `postgres` |
| `POSTGRES_PASSWORD` | PostgreSQL password | `SecurePass123!` |
| `POSTGRES_DB` | Database name | `ClassifiedAds` |

### Application Environment
| Variable | Description | Example |
|----------|-------------|---------|
| `DOTNET_ENVIRONMENT` | .NET environment | `Development` / `Production` |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Development` / `Production` |

### Connection Strings
| Variable | Description |
|----------|-------------|
| `ConnectionStrings__Default` | Primary database connection string |

### Storage Configuration
| Variable | Description | Example |
|----------|-------------|---------|
| `Storage__Provider` | Storage provider type | `Local` / `Azure` / `AWS` |
| `Storage__Local__Path` | Local storage path | `/files` |

### Messaging Configuration
| Variable | Description | Example |
|----------|-------------|---------|
| `Messaging__Provider` | Message broker type | `RabbitMQ` |
| `Messaging__RabbitMQ__HostName` | RabbitMQ host | `rabbitmq` |

## Best Practices

### ✅ DO
- Use `.env` for local development
- Keep `.env.example` updated with new variables
- Use strong passwords in production
- Rotate credentials regularly
- Use different credentials for each environment
- Document all environment variables

### ❌ DON'T
- Commit `.env` file to version control (already in .gitignore)
- Use production credentials in development
- Hardcode sensitive values in code or docker-compose.yml
- Share credentials via email or chat
- Use default/weak passwords

## Production Deployment

For production environments, use secure secret management:

### Docker Swarm
```bash
echo "MySecretPassword" | docker secret create postgres_password -
```

### Kubernetes
```bash
kubectl create secret generic postgres-credentials \
  --from-literal=username=postgres \
  --from-literal=password=SecurePass123!
```

### Azure
Use Azure Key Vault for secret management

### AWS
Use AWS Secrets Manager or Parameter Store

## Verification

To verify environment variables are loaded correctly:

```bash
# Check docker compose config
docker compose config

# View environment variables in a container
docker compose exec webapi env | grep POSTGRES
```

## Troubleshooting

### Issue: Variables not loading
**Solution**: Ensure `.env` file is in the same directory as `docker-compose.yml`

### Issue: Connection refused
**Solution**: Verify database credentials in `.env` match those in `ConnectionStrings__Default`, and that local standalone mode uses `localhost:55432`

### Issue: Permission denied
**Solution**: Check file permissions on `.env` file:
```bash
chmod 600 .env
```

## Security Checklist

- [ ] `.env` is in `.gitignore`
- [ ] `.env.example` contains no sensitive data
- [ ] All team members have their own `.env` file
- [ ] Production uses secure secret management
- [ ] Passwords are strong and unique
- [ ] Credentials are rotated regularly
- [ ] Access logs are monitored

---

**Remember**: Security is everyone's responsibility. When in doubt, ask before committing!
