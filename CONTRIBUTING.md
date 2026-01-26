# Contributing to API Testing Automation System

Thank you for considering contributing to this project! This document outlines the process and guidelines for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Pull Request Process](#pull-request-process)
- [Coding Standards](#coding-standards)
- [Commit Message Guidelines](#commit-message-guidelines)
- [Architecture Guidelines](#architecture-guidelines)

## Code of Conduct

Please be respectful and constructive in all interactions. We are committed to providing a welcoming and inclusive environment for everyone.

## Getting Started

### Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 10.0+ |
| Docker Desktop | Latest |
| Git | 2.x+ |

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-org/GSP26SE43.ModularMonolith.git
   cd GSP26SE43.ModularMonolith
   ```

2. **Start infrastructure services**
   ```bash
   docker-compose up -d db rabbitmq mailhog
   ```

3. **Run database migrations**
   ```bash
   dotnet run --project ClassifiedAds.Migrator
   ```

4. **Start the API**
   ```bash
   dotnet run --project ClassifiedAds.WebAPI
   ```

5. **Verify**: Open http://localhost:9002/swagger

## Development Workflow

### Branch Naming Convention

| Branch Type | Pattern | Example |
|-------------|---------|---------|
| Feature | `feature/<ticket-id>-<short-description>` | `feature/PROJ-123-user-authentication` |
| Bugfix | `bugfix/<ticket-id>-<short-description>` | `bugfix/PROJ-456-fix-login-error` |
| Hotfix | `hotfix/<ticket-id>-<short-description>` | `hotfix/PROJ-789-security-patch` |
| Release | `release/v<version>` | `release/v1.2.0` |

### Workflow Steps

1. **Create a branch** from `main` (or `develop` if using GitFlow)
   ```bash
   git checkout main
   git pull origin main
   git checkout -b feature/PROJ-123-my-feature
   ```

2. **Make changes** following coding standards

3. **Run tests locally** (when test projects exist)
   ```bash
   dotnet test
   ```

4. **Commit changes** following commit message guidelines

5. **Push and create PR**
   ```bash
   git push -u origin feature/PROJ-123-my-feature
   ```

## Pull Request Process

### Before Submitting

- [ ] Code compiles without errors
- [ ] All tests pass
- [ ] Code follows project style guidelines
- [ ] Documentation is updated if needed
- [ ] No secrets or sensitive data committed

### PR Requirements

1. **Title**: Use format `[TYPE] Brief description`
   - Types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`
   - Example: `[feat] Add user profile endpoint`

2. **Description**: Include:
   - What changes were made
   - Why changes were needed
   - How to test the changes
   - Related issue/ticket numbers

3. **Reviews**: 
   - Minimum 1 approval required
   - Code owners auto-assigned via CODEOWNERS
   - Address all review comments

4. **CI Checks**: All CI checks must pass
   - Build
   - Lint/Format
   - Tests (when available)
   - Docker build validation

### Merging

- Use **Squash and merge** for feature branches
- Delete branch after merge
- Ensure commit message is clean and descriptive

## Coding Standards

### C# Guidelines

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- StyleCop analyzer is enabled - fix all warnings
- Use meaningful names for classes, methods, and variables
- Keep methods small and focused (single responsibility)

### File Organization

```
ClassifiedAds.Modules.<ModuleName>/
├── Authorization/          # Authorization policies
├── ConfigurationOptions/   # Module configuration classes
├── Controllers/            # API Controllers
├── DbConfigurations/       # EF Core configurations
├── Entities/               # Domain entities
├── Persistence/            # DbContext and repositories
├── Queries/                # Query handlers (CQRS)
├── Commands/               # Command handlers (CQRS)
├── Services/               # Business logic services
└── ServiceCollectionExtensions.cs  # DI registration
```

### API Guidelines

- Use proper HTTP methods (GET, POST, PUT, DELETE)
- Return appropriate status codes
- Use DTOs for request/response, not entities
- Implement proper validation
- Document with XML comments for Swagger

## Commit Message Guidelines

### Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

| Type | Description |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Code style (formatting, semicolons) |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `perf` | Performance improvement |
| `test` | Adding or fixing tests |
| `chore` | Build process, auxiliary tools |

### Examples

```
feat(product): add product search endpoint

Implement full-text search for products using PostgreSQL.
Supports filtering by category, price range, and location.

Closes #123
```

```
fix(identity): resolve JWT token expiration issue

Token was using UTC offset incorrectly causing premature expiration.

Fixes #456
```

## Architecture Guidelines

### Module Design

1. **Modules are self-contained** - Each module should handle its own:
   - Data persistence
   - Business logic
   - API endpoints
   - Configuration

2. **Cross-module communication**:
   - Use integration events (via RabbitMQ)
   - Use contracts from `ClassifiedAds.Contracts`
   - Avoid direct database access across modules

3. **Dependencies flow inward**:
   ```
   Controllers → Services → Domain → Entities
   ```

### Adding a New Module

1. Create project: `ClassifiedAds.Modules.<Name>`
2. Add project reference to solution
3. Create `ServiceCollectionExtensions.cs`
4. Register in `ClassifiedAds.WebAPI/Program.cs`
5. Add database migrations
6. Update CODEOWNERS

### See Also

- [Architecture Documentation](docs-architecture/README.md)
- [CI/CD Guide](docs/CI_CD.md)
- [Security Policy](SECURITY.md)

---

## Questions?

If you have questions, feel free to:
- Open a GitHub Discussion
- Create an issue with the `question` label
- Contact the maintainers

Thank you for contributing! 🎉
