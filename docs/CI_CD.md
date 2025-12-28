# CI/CD Pipeline Documentation

This document describes the Continuous Integration and Continuous Deployment pipelines for the ClassifiedAds Modular Monolith project.

## Table of Contents

- [Overview](#overview)
- [CI Pipeline](#ci-pipeline)
- [CD Pipeline](#cd-pipeline)
- [GitHub Configuration](#github-configuration)
- [Secrets & Variables](#secrets--variables)
- [Deployment Guide](#deployment-guide)
- [Troubleshooting](#troubleshooting)

## Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           CI/CD Flow                                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  [Push/PR to main]     [Push tag v*.*.*]      [Manual Dispatch]         │
│         │                      │                      │                  │
│         ▼                      │                      │                  │
│   ┌─────────────┐              │                      │                  │
│   │  CI Pipeline │              │                      │                  │
│   │  - Build     │              │                      │                  │
│   │  - Lint      │              │                      │                  │
│   │  - Test      │              │                      │                  │
│   │  - Docker    │              │                      │                  │
│   └─────────────┘              │                      │                  │
│                                ▼                      ▼                  │
│                         ┌─────────────────────────────────┐              │
│                         │         CD Pipeline             │              │
│                         │  ┌─────────────────────────┐    │              │
│                         │  │ Build & Push Images     │    │              │
│                         │  └───────────┬─────────────┘    │              │
│                         │              ▼                  │              │
│                         │  ┌─────────────────────────┐    │              │
│                         │  │ Deploy to Staging       │    │              │
│                         │  └───────────┬─────────────┘    │              │
│                         │              ▼                  │              │
│                         │  ┌─────────────────────────┐    │              │
│                         │  │ Manual Approval         │    │              │
│                         │  └───────────┬─────────────┘    │              │
│                         │              ▼                  │              │
│                         │  ┌─────────────────────────┐    │              │
│                         │  │ Deploy to Production    │    │              │
│                         │  └─────────────────────────┘    │              │
│                         └─────────────────────────────────┘              │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

## CI Pipeline

**File**: [.github/workflows/ci.yml](../.github/workflows/ci.yml)

### Triggers

| Event | Branches | Conditions |
|-------|----------|------------|
| `push` | `main`, `develop` | Excludes docs-only changes |
| `pull_request` | `main`, `develop` | Excludes docs-only changes |

### Jobs

#### 1. Build & Lint

```yaml
steps:
  - Checkout code
  - Setup .NET 10
  - Cache NuGet packages
  - Restore dependencies
  - Build solution (Release config)
  - Check code formatting
  - Upload build artifacts
```

#### 2. Test (placeholder)

- Currently a placeholder as no test projects exist
- Uncomment test steps when adding test projects
- Supports code coverage collection

#### 3. Docker Build Validation

- Matrix strategy for all services:
  - `webapi`
  - `background`
  - `migrator`
- Validates Dockerfiles build correctly
- Uses GitHub Actions cache for faster builds

### Running CI Locally

```bash
# Restore and build
dotnet restore ClassifiedAds.ModularMonolith.slnx
dotnet build ClassifiedAds.ModularMonolith.slnx --configuration Release

# Check formatting
dotnet format ClassifiedAds.ModularMonolith.slnx --verify-no-changes

# Build Docker images
docker build -f ClassifiedAds.WebAPI/Dockerfile -t webapi:local .
docker build -f ClassifiedAds.Background/Dockerfile -t background:local .
docker build -f ClassifiedAds.Migrator/Dockerfile -t migrator:local .
```

## CD Pipeline

**File**: [.github/workflows/cd.yml](../.github/workflows/cd.yml)

### Triggers

| Trigger | Pattern | Description |
|---------|---------|-------------|
| Tag push | `v*.*.*` | Semantic version tags (e.g., `v1.2.3`) |
| Tag push | `v*.*.*-*` | Pre-release tags (e.g., `v1.2.3-beta.1`) |
| Manual | `workflow_dispatch` | UI-triggered with environment selection |

### Jobs Flow

```
prepare → build-images → deploy-staging → deploy-production → create-release
                              │                    │
                              │                    └── (requires manual approval)
                              └── (auto for tags)
```

### Environments

| Environment | Approval | Purpose |
|-------------|----------|---------|
| `staging` | None | Pre-production testing |
| `production` | Required reviewers | Live environment |

### Docker Images

Images are pushed to GitHub Container Registry (ghcr.io):

```
ghcr.io/<owner>/classifiedads-webapi:<version>
ghcr.io/<owner>/classifiedads-background:<version>
ghcr.io/<owner>/classifiedads-migrator:<version>
```

## GitHub Configuration

### Repository Settings Checklist

#### 1. Branch Protection Rules

Navigate to: **Settings → Branches → Add rule**

For `main` branch:

- [x] Require a pull request before merging
  - [x] Require approvals: `1` (or more)
  - [x] Dismiss stale PR approvals when new commits are pushed
  - [x] Require review from Code Owners
- [x] Require status checks to pass before merging
  - [x] Require branches to be up to date
  - Required checks:
    - `Build & Lint`
    - `Docker Build Validation (webapi)`
    - `Docker Build Validation (background)`
    - `Docker Build Validation (migrator)`
- [x] Require conversation resolution before merging
- [ ] Require signed commits (optional)
- [x] Do not allow bypassing the above settings

#### 2. Environments Setup

Navigate to: **Settings → Environments**

**Create `staging` environment:**
- Protection rules: (optional) Wait timer
- Environment secrets: See [Secrets section](#secrets--variables)
- Environment variables:
  - `STAGING_URL`: `https://staging.your-domain.com`

**Create `production` environment:**
- Protection rules:
  - [x] Required reviewers: Add 1-2 team leads
  - [x] Wait timer: 0-5 minutes (optional)
- Environment secrets: See [Secrets section](#secrets--variables)
- Environment variables:
  - `PRODUCTION_URL`: `https://your-domain.com`

#### 3. Actions Permissions

Navigate to: **Settings → Actions → General**

- Actions permissions: Allow all actions
- Workflow permissions: Read repository contents
- [x] Allow GitHub Actions to create and approve pull requests

### CODEOWNERS Setup

1. Ensure [.github/CODEOWNERS](../.github/CODEOWNERS) exists
2. Teams/users must have **write access** to the repository
3. Enable in branch protection: "Require review from Code Owners"

**Example team structure:**

```
@your-org/platform-team      # Default owners
@your-org/devops-team        # Infrastructure
@your-org/backend-team       # .NET code
@your-org/security-team      # Security-sensitive files
@your-org/architects         # Architecture decisions
```

## Secrets & Variables

### Required Secrets

Navigate to: **Settings → Secrets and variables → Actions**

#### Repository Secrets

| Secret Name | Description | Example |
|-------------|-------------|---------|
| *(none required at repo level - use environment secrets)* | | |

#### Staging Environment Secrets

| Secret Name | Description |
|-------------|-------------|
| `STAGING_DB_CONNECTION_STRING` | PostgreSQL connection string |
| `STAGING_RABBITMQ_CONNECTION` | RabbitMQ connection string |

#### Production Environment Secrets

| Secret Name | Description |
|-------------|-------------|
| `PRODUCTION_DB_CONNECTION_STRING` | PostgreSQL connection string |
| `PRODUCTION_RABBITMQ_CONNECTION` | RabbitMQ connection string |

### Environment Variables

| Variable | Environment | Description |
|----------|-------------|-------------|
| `STAGING_URL` | staging | Base URL for staging |
| `PRODUCTION_URL` | production | Base URL for production |

### Adding Secrets

```bash
# Using GitHub CLI
gh secret set STAGING_DB_CONNECTION_STRING --env staging
gh secret set PRODUCTION_DB_CONNECTION_STRING --env production
```

Or via UI: **Settings → Environments → [env] → Add secret**

## Deployment Guide

### Creating a Release

#### Option 1: Tag-based Release (Recommended)

```bash
# 1. Ensure main is up to date
git checkout main
git pull origin main

# 2. Create and push a tag
git tag -a v1.2.3 -m "Release v1.2.3: Brief description"
git push origin v1.2.3

# 3. CD pipeline triggers automatically:
#    - Builds images
#    - Deploys to staging
#    - Waits for approval
#    - Deploys to production
#    - Creates GitHub Release
```

#### Option 2: Manual Dispatch

1. Go to **Actions → CD Pipeline → Run workflow**
2. Select branch (usually `main`)
3. Choose target environment
4. Optionally skip staging
5. Click **Run workflow**

### Pre-release Tags

For beta/RC releases:

```bash
git tag -a v1.2.3-beta.1 -m "Beta release"
git push origin v1.2.3-beta.1
```

Pre-releases are marked accordingly in GitHub Releases.

### Rollback Procedure

```bash
# Re-deploy previous version
git tag -a v1.2.2-rollback -m "Rollback to v1.2.2"
git push origin v1.2.2-rollback

# Or use manual dispatch with specific commit/tag
```

## Troubleshooting

### Common Issues

#### CI Build Fails

1. **NuGet restore fails**
   - Check if packages exist
   - Verify .NET version matches `global.json`

2. **Docker build fails**
   - Verify Dockerfile paths
   - Check for missing dependencies

3. **Format check fails**
   ```bash
   # Auto-fix formatting
   dotnet format ClassifiedAds.ModularMonolith.slnx
   ```

#### CD Deployment Issues

1. **Image push fails**
   - Verify `GITHUB_TOKEN` has `packages: write` permission
   - Check if ghcr.io is accessible

2. **Deployment hangs**
   - Check if approval is pending
   - Verify environment protection rules

3. **Secrets not available**
   - Confirm secrets are added to correct environment
   - Check secret names match workflow references

### Viewing Logs

- **CI/CD Logs**: Actions tab → Select workflow run → Click on job
- **Container Registry**: Packages tab → Select package

### Getting Help

- Check [GitHub Actions documentation](https://docs.github.com/en/actions)
- Review workflow files in `.github/workflows/`
- Contact DevOps team

---

## Quick Reference

### Useful Commands

```bash
# Trigger CI manually (push empty commit)
git commit --allow-empty -m "chore: trigger CI"
git push

# Create release tag
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0

# List tags
git tag -l "v*"

# Delete tag (local and remote)
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```

### Workflow URLs

After setup, access workflows at:
```
https://github.com/<owner>/<repo>/actions
```

### Status Badges

Add to README.md:
```markdown
![CI](https://github.com/<owner>/<repo>/actions/workflows/ci.yml/badge.svg)
![CD](https://github.com/<owner>/<repo>/actions/workflows/cd.yml/badge.svg)
```
