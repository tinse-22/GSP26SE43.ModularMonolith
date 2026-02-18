# CI/CD Pipeline Documentation

This document provides comprehensive documentation for the Continuous Integration (CI) and Continuous Deployment (CD) pipelines for the ClassifiedAds Modular Monolith project. The pipelines are implemented using GitHub Actions and follow industry best practices for automated testing, building, and deployment.

## Table of Contents

- [Overview](#overview)
- [CI Pipeline](#ci-pipeline)
  - [Build & Lint Job](#1-build--lint)
  - [Test Job](#2-test)
  - [Docker Build Validation](#3-docker-build-validation)
- [CD Pipeline](#cd-pipeline)
  - [Prepare Release](#prepare-release)
  - [Build & Push Images](#build--push-images)
  - [Staging Deployment](#staging-deployment)
  - [Production Deployment](#production-deployment)
  - [GitHub Release Creation](#github-release-creation)
- [GitHub Configuration](#github-configuration)
- [Secrets & Variables](#secrets--variables)
- [Deployment Guide](#deployment-guide)
- [Troubleshooting](#troubleshooting)

---

## Overview

The CI/CD system consists of two main pipelines that work together to ensure code quality and enable reliable deployments:

- **CI Pipeline**: Validates code on every push/PR through building, linting, testing, and Docker validation
- **CD Pipeline**: Handles release builds, container image publishing, and multi-environment deployments

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           CI/CD Flow                                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  [Push/PR to main/develop]   [Push tag v*.*.*]    [Manual Dispatch]     │
│         │                          │                      │              │
│         ▼                          │                      │              │
│   ┌─────────────────┐              │                      │              │
│   │   CI Pipeline   │              │                      │              │
│   │  ┌───────────┐  │              │                      │              │
│   │  │ Build     │  │              │                      │              │
│   │  │ + Lint    │  │              │                      │              │
│   │  └─────┬─────┘  │              │                      │              │
│   │        ▼        │              │                      │              │
│   │  ┌───────────┐  │              │                      │              │
│   │  │ Unit Tests│  │              │                      │              │
│   │  │ + Integ.  │  │              │                      │              │
│   │  │ Tests     │  │              │                      │              │
│   │  └─────┬─────┘  │              │                      │              │
│   │        ▼        │              │                      │              │
│   │  ┌───────────┐  │              │                      │              │
│   │  │ Docker    │  │              │                      │              │
│   │  │ Validate  │  │              │                      │              │
│   │  └───────────┘  │              │                      │              │
│   └─────────────────┘              │                      │              │
│                                    ▼                      ▼              │
│                         ┌─────────────────────────────────┐              │
│                         │         CD Pipeline             │              │
│                         │  ┌─────────────────────────┐    │              │
│                         │  │ Prepare Release         │    │              │
│                         │  │ (Extract version)       │    │              │
│                         │  └───────────┬─────────────┘    │              │
│                         │              ▼                  │              │
│                         │  ┌─────────────────────────┐    │              │
│                         │  │ Build & Push Images     │    │              │
│                         │  │ (webapi, background,    │    │              │
│                         │  │  migrator)              │    │              │
│                         │  └───────────┬─────────────┘    │              │
│                         │              ▼                  │              │
│                         │  ┌─────────────────────────┐    │              │
│                         │  │ Deploy to Staging       │    │              │
│                         │  │ + Smoke Tests           │    │              │
│                         │  └───────────┬─────────────┘    │              │
│                         │              ▼                  │              │
│                         │  ┌─────────────────────────┐    │              │
│                         │  │ Manual Approval         │    │              │
│                         │  │ (Required Reviewers)    │    │              │
│                         │  └───────────┬─────────────┘    │              │
│                         │              ▼                  │              │
│                         │  ┌─────────────────────────┐    │              │
│                         │  │ Deploy to Production    │    │              │
│                         │  │ + Health Check          │    │              │
│                         │  └───────────┬─────────────┘    │              │
│                         │              ▼                  │              │
│                         │  ┌─────────────────────────┐    │              │
│                         │  │ Create GitHub Release   │    │              │
│                         │  └─────────────────────────┘    │              │
│                         └─────────────────────────────────┘              │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## CI Pipeline

**File**: [.github/workflows/ci.yml](../.github/workflows/ci.yml)

The CI pipeline ensures code quality through automated building, testing, and validation. It runs on every code change to catch issues early in the development cycle.

### Triggers

| Event | Branches | Conditions |
|-------|----------|------------|
| `push` | `main`, `develop` | Excludes docs-only changes (`**.md`, `docs/**`, `docs-architecture/**`) |
| `pull_request` | `main`, `develop` | Excludes docs-only changes |

### Concurrency Control

```yaml
concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true
```

This ensures that:
- Only one CI run per branch/PR at a time
- Older runs are cancelled when new commits are pushed
- Saves GitHub Actions minutes and provides faster feedback

### Jobs

#### 1. Build & Lint

**Purpose**: Compiles the entire solution and validates code formatting to ensure consistency across the codebase.

**Detailed Steps**:

| Step | Description | Details |
|------|-------------|---------|
| Checkout code | Clones repository | Uses `fetch-depth: 0` for full history (needed for proper versioning) |
| Setup .NET | Installs .NET SDK | Uses .NET 10.0.x as specified in `global.json` |
| Cache NuGet | Restores package cache | Key based on `.csproj` files, dramatically speeds up builds |
| Restore dependencies | Downloads packages | Runs `dotnet restore ClassifiedAds.ModularMonolith.slnx` |
| Build solution | Compiles all projects | Runs in `Release` configuration for production-like validation |
| Check formatting | Validates code style | Uses `dotnet format --verify-no-changes` (continues on error) |
| Upload artifacts | Saves build output | Retains for 7 days for debugging and further stages |

**Build Command**:
```bash
dotnet build ClassifiedAds.ModularMonolith.slnx --configuration Release --no-restore
```

**Format Check Command**:
```bash
dotnet format ClassifiedAds.ModularMonolith.slnx --verify-no-changes --verbosity diagnostic
```

---

#### 2. Test

**Purpose**: Runs both unit tests and integration tests with code coverage collection to ensure code correctness and prevent regressions.

**Dependencies**: Requires `build` job to complete successfully.

**Services**: Starts a PostgreSQL 16 container as fallback database for integration tests.

**Test Projects**:

| Project | Type | Description |
|---------|------|-------------|
| `ClassifiedAds.UnitTests` | Unit Tests | Fast isolated tests for domain logic, exception handling, and utilities |
| `ClassifiedAds.IntegrationTests` | Integration Tests | Full stack tests using Testcontainers with PostgreSQL |

**Unit Tests**:
```bash
dotnet test ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj \
  --configuration Release \
  --no-build \
  --verbosity normal \
  --logger "trx;LogFileName=unit-test-results.trx" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults/UnitTests
```

**Integration Tests**:
```bash
dotnet test ClassifiedAds.IntegrationTests/ClassifiedAds.IntegrationTests.csproj \
  --configuration Release \
  --no-build \
  --verbosity normal \
  --logger "trx;LogFileName=integration-test-results.trx" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults/IntegrationTests
```

**Test Framework Stack**:
- **xUnit**: Test framework
- **FluentAssertions**: Assertion library for readable test code
- **Moq**: Mocking framework for unit tests
- **Testcontainers.PostgreSql**: Spins up real PostgreSQL containers for integration tests
- **Respawn**: Database reset utility for test isolation
- **Microsoft.AspNetCore.Mvc.Testing**: WebApplicationFactory for HTTP integration tests

**Artifacts Generated**:
- `test-results`: TRX files for test run details
- `code-coverage`: Cobertura XML files for coverage analysis

---

#### 3. Docker Build Validation

**Purpose**: Validates that all Dockerfiles build correctly without pushing images. This catches Docker-related issues before they reach the CD pipeline.

**Dependencies**: Requires `build` job to complete successfully.

**Matrix Strategy**:

| Service | Dockerfile | Description |
|---------|------------|-------------|
| `webapi` | `ClassifiedAds.WebAPI/Dockerfile` | Main REST API service |
| `background` | `ClassifiedAds.Background/Dockerfile` | Background worker service for async processing |
| `migrator` | `ClassifiedAds.Migrator/Dockerfile` | Database migration runner |

**Features**:
- Uses Docker Buildx for efficient multi-platform builds
- Leverages GitHub Actions cache (`type=gha`) for layer caching
- Tags with `ci-<sha>` for traceability
- Does **not** push images (validation only)

```yaml
- name: Build Docker image (${{ matrix.service }})
  uses: docker/build-push-action@v6
  with:
    context: .
    file: ${{ matrix.dockerfile }}
    push: false  # Validation only
    tags: classifiedads-${{ matrix.service }}:ci-${{ github.sha }}
    cache-from: type=gha
    cache-to: type=gha,mode=max
```

### Running CI Locally

You can validate your changes locally before pushing:

```bash
# Full CI simulation
# Step 1: Restore and build
dotnet restore ClassifiedAds.ModularMonolith.slnx
dotnet build ClassifiedAds.ModularMonolith.slnx --configuration Release

# Step 2: Check formatting (will fail if formatting issues exist)
dotnet format ClassifiedAds.ModularMonolith.slnx --verify-no-changes

# Step 3: Auto-fix formatting issues if needed
dotnet format ClassifiedAds.ModularMonolith.slnx

# Step 4: Run unit tests
dotnet test ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj --configuration Release

# Step 5: Run integration tests (requires Docker)
dotnet test ClassifiedAds.IntegrationTests/ClassifiedAds.IntegrationTests.csproj --configuration Release

# Step 6: Build Docker images locally
docker build -f ClassifiedAds.WebAPI/Dockerfile -t webapi:local .
docker build -f ClassifiedAds.Background/Dockerfile -t background:local .
docker build -f ClassifiedAds.Migrator/Dockerfile -t migrator:local .
```

---

## CD Pipeline

**File**: [.github/workflows/cd.yml](../.github/workflows/cd.yml)

The CD pipeline handles building production Docker images, pushing to a container registry, and deploying to multiple environments with manual approval gates.

### Triggers

| Trigger | Pattern | Description |
|---------|---------|-------------|
| Tag push | `v*.*.*` | Semantic version tags (e.g., `v1.0.0`, `v2.3.1`) |
| Tag push | `v*.*.*-*` | Pre-release tags (e.g., `v1.0.0-beta.1`, `v2.0.0-rc.1`) |
| Manual | `workflow_dispatch` | UI-triggered with environment selection and options |

### Manual Dispatch Options

When triggering manually via GitHub UI:

| Input | Type | Description |
|-------|------|-------------|
| `environment` | Choice | Target environment: `staging` or `production` |
| `skip_staging` | Boolean | Skip staging deployment (go directly to production) |

### Jobs Flow

```
┌─────────────┐     ┌───────────────┐     ┌─────────────────┐
│   prepare   │────▶│  build-images │────▶│ deploy-staging  │
│ (version)   │     │ (3 services)  │     │ + smoke tests   │
└─────────────┘     └───────────────┘     └────────┬────────┘
                                                   │
                                          (manual approval)
                                                   │
                                          ┌────────▼────────┐     ┌─────────────────┐
                                          │deploy-production│────▶│ create-release  │
                                          │ + health check  │     │ (GitHub Release)│
                                          └─────────────────┘     └─────────────────┘
```

### Prepare Release

**Purpose**: Extracts version information from the Git tag and determines if this is a pre-release.

**Outputs**:
- `version`: The semantic version (e.g., `1.0.0` or `1.0.0-beta.1`)
- `is_prerelease`: Boolean indicating if this is a pre-release (`true` for tags containing `-`)

**Version Extraction Logic**:
```bash
# For tag triggers: v1.2.3 → 1.2.3
VERSION="${GITHUB_REF#refs/tags/v}"

# For manual triggers: manual-<run_number>
VERSION="manual-${{ github.run_number }}"

# Pre-release detection (contains hyphen)
if [[ "$VERSION" == *"-"* ]]; then
  IS_PRERELEASE="true"
fi
```

### Build & Push Images

**Purpose**: Builds production-optimized Docker images for all services and pushes them to GitHub Container Registry (ghcr.io).

**Permissions Required**:
- `contents: read` - Read repository
- `packages: write` - Push to ghcr.io

**Matrix Strategy** (parallel builds):

| Service | Dockerfile | Final Image |
|---------|------------|-------------|
| webapi | `ClassifiedAds.WebAPI/Dockerfile` | `ghcr.io/<owner>/classifiedads-webapi` |
| background | `ClassifiedAds.Background/Dockerfile` | `ghcr.io/<owner>/classifiedads-background` |
| migrator | `ClassifiedAds.Migrator/Dockerfile` | `ghcr.io/<owner>/classifiedads-migrator` |

**Image Tags Generated**:

| Tag Pattern | Example | Description |
|-------------|---------|-------------|
| `{{version}}` | `1.2.3` | Full semantic version |
| `{{major}}.{{minor}}` | `1.2` | Minor version (for rolling updates) |
| `<sha>` | `abc1234` | Git commit SHA (exact build traceability) |
| `latest` | `latest` | Only for default branch builds |

**Container Registry URL**:
```
ghcr.io/<owner>/classifiedads-<service>:<tag>
```

### Staging Deployment

**Purpose**: Deploys to staging environment for pre-production validation and runs smoke tests.

**Triggers**: 
- Tag pushes (automatic)
- Manual dispatch (unless `skip_staging` is true)

**Environment**: `staging`
- No approval required by default
- Uses environment-specific secrets and variables

**Deployment Steps**:
1. Checkout code
2. Deploy images to staging infrastructure
3. Run smoke tests against staging URL

**Configuration Used**:
```yaml
env:
  STAGING_DB_CONNECTION_STRING: ${{ secrets.STAGING_DB_CONNECTION_STRING }}
  STAGING_RABBITMQ_CONNECTION: ${{ secrets.STAGING_RABBITMQ_CONNECTION }}
```

### Production Deployment

**Purpose**: Deploys to production environment after manual approval.

**Requirements**:
- `build-images` must succeed
- `deploy-staging` must succeed (or be skipped)
- **Manual approval** from required reviewers

**Environment**: `production`
- Required reviewers configured in GitHub Environment settings
- Optional wait timer before deployment

**Deployment Steps**:
1. Wait for manual approval
2. Checkout code
3. Deploy images to production infrastructure
4. Run production health check

### GitHub Release Creation

**Purpose**: Creates an official GitHub Release with auto-generated release notes.

**Triggers**: Only for tag-based deployments (not manual dispatch)

**Release Contents**:
- **Name**: `Release <version>`
- **Body**: Auto-generated commit changelog since last tag
- **Pre-release flag**: Set based on `is_prerelease` output
- **Docker image references**: Links to all published images

**Release Notes Generation**:
```bash
# Get commits since last tag
LAST_TAG=$(git describe --tags --abbrev=0 HEAD^)
COMMITS=$(git log --pretty=format:"- %s (%h)" $LAST_TAG..HEAD)
```

### Environments

| Environment | Approval | Purpose | Typical Use |
|-------------|----------|---------|-------------|
| `staging` | None | Pre-production testing | Automated deployment for all releases |
| `production` | Required reviewers | Live environment | Requires human verification |

### Docker Images

All images are pushed to GitHub Container Registry:

```
ghcr.io/<owner>/classifiedads-webapi:<version>
ghcr.io/<owner>/classifiedads-background:<version>
ghcr.io/<owner>/classifiedads-migrator:<version>
```

**Image Variants**:
- **webapi**: ASP.NET Core REST API with Swagger, authentication, and all modules
- **background**: Worker service for outbox publishing, email/SMS sending, message consumers
- **migrator**: EF Core migrations + DbUp scripts runner

---

## GitHub Configuration

Proper GitHub repository configuration is essential for the CI/CD pipelines to function correctly.

### Repository Settings Checklist

#### 1. Branch Protection Rules

Navigate to: **Settings → Branches → Add rule**

For `main` branch, configure these protection rules:

| Setting | Value | Purpose |
|---------|-------|---------|
| Require pull request before merging | ✅ | Ensures code review |
| Required approvals | `1` (or more) | At least one reviewer must approve |
| Dismiss stale approvals | ✅ | Re-review required after new commits |
| Require review from Code Owners | ✅ | Domain experts must approve |
| Require status checks to pass | ✅ | CI must pass before merge |
| Require branches up to date | ✅ | Must be current with base branch |
| Require conversation resolution | ✅ | All review comments must be resolved |
| Require signed commits | ⬜ Optional | Extra security for commit verification |
| No bypass | ✅ | Even admins must follow rules |

**Required Status Checks** (must pass before merge):
- `Build & Lint`
- `Run Tests`
- `Docker Build Validation (webapi)`
- `Docker Build Validation (background)`
- `Docker Build Validation (migrator)`

#### 2. Environments Setup

Navigate to: **Settings → Environments**

**Create `staging` environment:**

| Configuration | Value | Purpose |
|---------------|-------|---------|
| Protection rules | Wait timer (optional) | Delay before deployment starts |
| Environment secrets | See [Secrets section](#secrets--variables) | Database/service credentials |
| Environment variables | `STAGING_URL=https://staging.your-domain.com` | Deployment target URL |

**Create `production` environment:**

| Configuration | Value | Purpose |
|---------------|-------|---------|
| Required reviewers | 1-2 team leads | Manual approval gate |
| Wait timer | 0-5 minutes (optional) | Additional delay after approval |
| Environment secrets | See [Secrets section](#secrets--variables) | Production credentials |
| Environment variables | `PRODUCTION_URL=https://your-domain.com` | Production URL |

#### 3. Actions Permissions

Navigate to: **Settings → Actions → General**

| Setting | Value |
|---------|-------|
| Actions permissions | Allow all actions and reusable workflows |
| Workflow permissions | Read repository contents |
| Allow GitHub Actions to create PRs | ✅ |

### CODEOWNERS Setup

1. Create/verify [.github/CODEOWNERS](../.github/CODEOWNERS) file
2. Ensure all teams/users have **write access** to the repository
3. Enable "Require review from Code Owners" in branch protection

**Example CODEOWNERS Structure:**

```
# Default owners for everything in the repo
*                                   @your-org/platform-team

# Infrastructure and DevOps files
/.github/                           @your-org/devops-team
/docker-compose*.yml                @your-org/devops-team
/**/Dockerfile                      @your-org/devops-team

# Backend .NET code
/ClassifiedAds.*/                   @your-org/backend-team

# Module-specific ownership
/ClassifiedAds.Modules.Identity/    @your-org/security-team
/ClassifiedAds.Modules.Product/     @your-org/product-team

# Architecture documentation and decisions
/docs-architecture/                 @your-org/architects
/rules/                             @your-org/architects

# Security-sensitive files
**/appsettings*.json                @your-org/security-team
```

---

## Secrets & Variables

Secrets and variables are used to configure environment-specific settings without hardcoding sensitive values.

### Required Secrets

Navigate to: **Settings → Secrets and variables → Actions**

#### Repository Secrets

| Secret Name | Required | Description |
|-------------|----------|-------------|
| `GITHUB_TOKEN` | Auto-provided | Automatically provided by GitHub Actions |

> **Note**: Most secrets should be configured at the environment level for better security isolation.

#### Staging Environment Secrets

| Secret Name | Description | Example Value |
|-------------|-------------|---------------|
| `STAGING_DB_CONNECTION_STRING` | PostgreSQL connection string for staging | `Host=staging-db;Database=ClassifiedAds;Username=app;Password=xxx` |
| `STAGING_RABBITMQ_CONNECTION` | RabbitMQ connection string for staging | `amqp://guest:guest@staging-rabbitmq:5672` |

#### Production Environment Secrets

| Secret Name | Description | Example Value |
|-------------|-------------|---------------|
| `PRODUCTION_DB_CONNECTION_STRING` | PostgreSQL connection string for production | `Host=prod-db;Database=ClassifiedAds;Username=app;Password=xxx;SSL Mode=Require` |
| `PRODUCTION_RABBITMQ_CONNECTION` | RabbitMQ connection string for production | `amqp://user:pass@prod-rabbitmq:5672` |

### Environment Variables

| Variable | Environment | Description |
|----------|-------------|-------------|
| `STAGING_URL` | staging | Base URL for staging deployment (e.g., `https://staging.example.com`) |
| `PRODUCTION_URL` | production | Base URL for production deployment (e.g., `https://example.com`) |

### Adding Secrets

**Using GitHub CLI:**
```bash
# Add repository secret
gh secret set SECRET_NAME

# Add environment-specific secret
gh secret set STAGING_DB_CONNECTION_STRING --env staging
gh secret set PRODUCTION_DB_CONNECTION_STRING --env production

# List secrets
gh secret list
gh secret list --env staging
```

**Using GitHub UI:**
1. Navigate to **Settings → Secrets and variables → Actions**
2. Click **New repository secret** (for repo-level) or **Environments → [env] → Add secret**
3. Enter name and value
4. Click **Add secret**

### Best Practices for Secrets

| Practice | Description |
|----------|-------------|
| Environment isolation | Use environment-specific secrets instead of repository secrets |
| Least privilege | Grant minimum required permissions |
| Rotation | Rotate secrets regularly (quarterly recommended) |
| No logging | Never print secrets in logs or output |
| Encryption | Use encrypted connections (SSL/TLS) in connection strings |

---

## Deployment Guide

This section provides step-by-step instructions for creating releases and deploying to environments.

### Creating a Release

#### Option 1: Tag-based Release (Recommended)

This is the standard release workflow that triggers automatic CI/CD:

```bash
# 1. Ensure your local main branch is up to date
git checkout main
git pull origin main

# 2. Verify all tests pass locally (optional but recommended)
dotnet test ClassifiedAds.ModularMonolith.slnx --configuration Release

# 3. Create an annotated tag with semantic version
git tag -a v1.2.3 -m "Release v1.2.3: Brief description of changes"

# 4. Push the tag to trigger CD pipeline
git push origin v1.2.3

# 5. Monitor the deployment
# Go to: https://github.com/<owner>/<repo>/actions
```

**What happens automatically:**
1. CD pipeline detects the tag push
2. Prepares release (extracts version `1.2.3`)
3. Builds all three Docker images in parallel
4. Pushes images to ghcr.io with multiple tags
5. Deploys to staging environment
6. **Waits for manual approval** for production
7. After approval, deploys to production
8. Creates GitHub Release with auto-generated notes

#### Option 2: Manual Dispatch

For emergency deployments or testing specific branches:

1. Navigate to **Actions → CD Pipeline → Run workflow**
2. Configure deployment:
   - **Branch**: Select the branch (usually `main`)
   - **Environment**: Choose `staging` or `production`
   - **Skip staging**: Check to go directly to production
3. Click **Run workflow**
4. Monitor progress in the Actions tab

### Pre-release Tags

For beta, RC (Release Candidate), or alpha releases:

```bash
# Beta release
git tag -a v1.2.3-beta.1 -m "Beta release for testing new features"
git push origin v1.2.3-beta.1

# Release candidate
git tag -a v1.2.3-rc.1 -m "Release candidate for v1.2.3"
git push origin v1.2.3-rc.1

# Alpha release
git tag -a v2.0.0-alpha.1 -m "Alpha release for major version"
git push origin v2.0.0-alpha.1
```

Pre-releases are:
- Marked with `prerelease: true` in GitHub Releases
- Not shown as "Latest release" on the repository page
- Ideal for testing with early adopters

### Rollback Procedure

**Method 1: Deploy Previous Version**

```bash
# Re-tag the previous stable version
git tag -a v1.2.2-rollback -m "Emergency rollback to v1.2.2"
git push origin v1.2.2-rollback
```

**Method 2: Manual Dispatch with Previous Tag**

1. Go to **Actions → CD Pipeline → Run workflow**
2. Select the branch/tag of the previous stable version
3. Choose target environment
4. Run workflow

**Method 3: Container Image Rollback**

If you have direct access to your deployment infrastructure:

```bash
# Pull and deploy the previous image version
docker pull ghcr.io/<owner>/classifiedads-webapi:1.2.2
docker pull ghcr.io/<owner>/classifiedads-background:1.2.2
docker pull ghcr.io/<owner>/classifiedads-migrator:1.2.2

# Update your deployment to use these images
```

### Post-Deployment Verification

After deployment, verify the release:

```bash
# Check application health
curl https://your-domain.com/health

# Check API version (if exposed)
curl https://your-domain.com/api/version

# Monitor logs
# (Use your logging/monitoring platform)
```

---

## Troubleshooting

This section covers common issues and their solutions.

### CI Pipeline Issues

#### Build Fails

**Problem**: `dotnet build` or `dotnet restore` fails

| Possible Cause | Solution |
|----------------|----------|
| NuGet package not found | Verify package exists and version is correct in `.csproj` files |
| .NET version mismatch | Check `global.json` matches workflow's `DOTNET_VERSION: "10.0.x"` |
| Missing project reference | Ensure all `<ProjectReference>` paths are correct |
| Cache corruption | Clear NuGet cache: Delete the cache key or change hash |

**Debug commands:**
```bash
# Check .NET version
dotnet --version

# Clear local NuGet cache
dotnet nuget locals all --clear

# Restore with detailed output
dotnet restore ClassifiedAds.ModularMonolith.slnx --verbosity detailed
```

#### Format Check Fails

**Problem**: `dotnet format --verify-no-changes` fails

```bash
# View what needs to be formatted
dotnet format ClassifiedAds.ModularMonolith.slnx --verify-no-changes --verbosity diagnostic

# Auto-fix all formatting issues
dotnet format ClassifiedAds.ModularMonolith.slnx

# Commit the fixes
git add .
git commit -m "style: fix code formatting"
```

#### Test Failures

**Problem**: Unit or integration tests fail

| Test Type | Common Issues | Solutions |
|-----------|---------------|-----------|
| Unit Tests | Missing mock setup | Verify all dependencies are mocked |
| Unit Tests | Assertion failures | Check expected vs actual values in test output |
| Integration Tests | Docker not available | Ensure Docker service is running in CI |
| Integration Tests | Container timeout | Increase timeout in `PostgreSqlContainerFixture` |
| Integration Tests | Database connection | Check connection string and port availability |

**Debug commands:**
```bash
# Run tests with detailed output
dotnet test ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj --verbosity detailed

# Run specific failing test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Check Docker availability (for integration tests)
docker info
docker ps
```

#### Docker Build Fails

**Problem**: Docker image fails to build

| Possible Cause | Solution |
|----------------|----------|
| Dockerfile path wrong | Verify path in workflow matches actual location |
| Missing dependencies | Check `.dockerignore` isn't excluding needed files |
| Build context too large | Review `.dockerignore` to exclude unnecessary files |
| Multi-stage build failure | Check each stage builds independently |

**Debug commands:**
```bash
# Build locally with verbose output
docker build -f ClassifiedAds.WebAPI/Dockerfile -t test:local . --progress=plain

# Check what's being sent to Docker daemon
docker build -f ClassifiedAds.WebAPI/Dockerfile -t test:local . --progress=plain 2>&1 | head -100
```

### CD Pipeline Issues

#### Image Push Fails

**Problem**: Cannot push to ghcr.io

| Possible Cause | Solution |
|----------------|----------|
| Missing permissions | Ensure workflow has `packages: write` permission |
| Authentication failure | Verify `GITHUB_TOKEN` is valid |
| Registry unavailable | Check GitHub status page |
| Rate limiting | Wait and retry, or use authenticated requests |

#### Deployment Hangs

**Problem**: Deployment job doesn't complete

| Possible Cause | Solution |
|----------------|----------|
| Waiting for approval | Check if production environment requires manual approval |
| Environment not found | Create the environment in repository settings |
| Infrastructure timeout | Check deployment target is accessible |

#### Secrets Not Available

**Problem**: Workflow can't access secrets

| Possible Cause | Solution |
|----------------|----------|
| Wrong environment | Ensure secrets are added to the correct environment |
| Secret name mismatch | Verify secret names match exactly (case-sensitive) |
| Missing environment | Create the environment before adding secrets |
| Forked repository | Secrets aren't available in forks by default |

### Viewing Logs

| Log Type | Location |
|----------|----------|
| CI/CD Pipeline Logs | **Actions** tab → Select workflow run → Click on job |
| Container Registry | **Packages** tab → Select package → View versions |
| Deployment Logs | Check your deployment platform (K8s, Azure, etc.) |

### Getting Help

1. **Check GitHub Actions documentation**: [docs.github.com/en/actions](https://docs.github.com/en/actions)
2. **Review workflow files**: [.github/workflows/](../.github/workflows/)
3. **Search GitHub Issues**: Look for similar problems in the repository
4. **Contact DevOps team**: For infrastructure-related issues
5. **Check GitHub Status**: [githubstatus.com](https://www.githubstatus.com/)

---

## Quick Reference

### Useful Commands

```bash
# ═══════════════════════════════════════════════════
# Git Tag Management
# ═══════════════════════════════════════════════════

# Create release tag
git tag -a v1.0.0 -m "Release v1.0.0: Description"
git push origin v1.0.0

# Create pre-release tag
git tag -a v1.0.0-beta.1 -m "Beta release"
git push origin v1.0.0-beta.1

# List all version tags
git tag -l "v*"

# Delete tag (local and remote)
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0

# View tag details
git show v1.0.0

# ═══════════════════════════════════════════════════
# CI Debugging
# ═══════════════════════════════════════════════════

# Trigger CI manually (empty commit)
git commit --allow-empty -m "chore: trigger CI"
git push

# Local CI simulation
dotnet restore ClassifiedAds.ModularMonolith.slnx
dotnet build ClassifiedAds.ModularMonolith.slnx --configuration Release
dotnet test ClassifiedAds.ModularMonolith.slnx --configuration Release
dotnet format ClassifiedAds.ModularMonolith.slnx --verify-no-changes

# ═══════════════════════════════════════════════════
# Docker Commands
# ═══════════════════════════════════════════════════

# Build all images locally
docker build -f ClassifiedAds.WebAPI/Dockerfile -t classifiedads-webapi:local .
docker build -f ClassifiedAds.Background/Dockerfile -t classifiedads-background:local .
docker build -f ClassifiedAds.Migrator/Dockerfile -t classifiedads-migrator:local .

# Pull released images
docker pull ghcr.io/<owner>/classifiedads-webapi:latest
docker pull ghcr.io/<owner>/classifiedads-background:latest
docker pull ghcr.io/<owner>/classifiedads-migrator:latest
```

### Workflow URLs

After setup, access workflows at:
```
https://github.com/<owner>/<repo>/actions
https://github.com/<owner>/<repo>/actions/workflows/ci.yml
https://github.com/<owner>/<repo>/actions/workflows/cd.yml
```

### Status Badges

Add these to your README.md to show pipeline status:

```markdown
[![CI Pipeline](https://github.com/<owner>/<repo>/actions/workflows/ci.yml/badge.svg)](https://github.com/<owner>/<repo>/actions/workflows/ci.yml)
[![CD Pipeline](https://github.com/<owner>/<repo>/actions/workflows/cd.yml/badge.svg)](https://github.com/<owner>/<repo>/actions/workflows/cd.yml)
```

---

*Last updated: January 1, 2026*
