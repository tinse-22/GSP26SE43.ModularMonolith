# Testing Guide

> **Purpose:** Guide for running and writing tests in the ClassifiedAds Modular Monolith project.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Test Projects Structure](#test-projects-structure)
- [Running Tests](#running-tests)
- [Writing New Tests](#writing-new-tests)
- [Integration Test Infrastructure](#integration-test-infrastructure)
- [Code Coverage](#code-coverage)
- [CI/CD Integration](#cicd-integration)
- [Troubleshooting](#troubleshooting)

---

## Prerequisites

| Requirement | Version | Purpose |
|-------------|---------|---------|
| .NET SDK | 10.0+ | Build and run tests |
| Docker Desktop | Latest | Run integration tests with Testcontainers |
| IDE | VS 2022+ or VS Code | Development and debugging |

### Docker Configuration for Testcontainers

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) to spin up PostgreSQL containers. Ensure:

1. **Docker Desktop** is installed and running
2. Docker is accessible from the command line: `docker info`
3. For Windows, ensure Docker is configured for Linux containers

---

## Test Projects Structure

```
ClassifiedAds.ModularMonolith/
├── ClassifiedAds.UnitTests/                    # Unit tests
│   └── CrossCuttingConcerns/
│       ├── ValidationExceptionTests.cs
│       └── NotFoundExceptionTests.cs
│
├── ClassifiedAds.IntegrationTests/             # Integration tests
│   ├── Infrastructure/
│   │   ├── CustomWebApplicationFactory.cs      # Test server factory
│   │   ├── PostgreSqlContainerFixture.cs       # Testcontainers fixture
│   │   ├── IntegrationTestCollection.cs        # xUnit collection definition
│   │   └── TestAuthHandler.cs                  # Test authentication handler
│   └── Smoke/
│       └── ApplicationSmokeTests.cs            # Smoke integration tests
```

### Test Packages

| Package | Purpose |
|---------|---------|
| `xunit` | Test framework |
| `FluentAssertions` | Fluent assertion library |
| `Moq` | Mocking framework |
| `Microsoft.AspNetCore.Mvc.Testing` | WebApplicationFactory for integration tests |
| `Testcontainers.PostgreSql` | PostgreSQL container for tests |
| `Respawn` | Database reset between tests |
| `coverlet.collector` | Code coverage collection |

---

## Running Tests

### Run All Tests

```powershell
# From solution root
dotnet test ClassifiedAds.ModularMonolith.slnx --configuration Release

# With verbose output
dotnet test ClassifiedAds.ModularMonolith.slnx --configuration Release --verbosity normal
```

### Run Unit Tests Only

```powershell
dotnet test ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj --configuration Release
```

### Run Integration Tests Only

```powershell
# Requires Docker Desktop running
dotnet test ClassifiedAds.IntegrationTests/ClassifiedAds.IntegrationTests.csproj --configuration Release
```

### Run Specific Test

```powershell
# By test name filter
dotnet test --filter "FullyQualifiedName~ValidationExceptionTests"

# By trait/category (if defined)
dotnet test --filter "Category=Smoke"
```

---

## Writing New Tests

### Test Naming Convention

Follow the pattern: `{MethodUnderTest}_Should{ExpectedBehavior}_When{Condition}`

```csharp
[Fact]
public async Task GetProduct_ShouldReturnProduct_WhenProductExists()
{
    // ...
}

[Fact]
public void Requires_ShouldThrowValidationException_WhenConditionIsFalse()
{
    // ...
}
```

### AAA Pattern (Arrange-Act-Assert)

All tests MUST follow the AAA pattern with comments:

```csharp
[Fact]
public async Task GetProduct_ShouldReturnProduct_WhenProductExists()
{
    // Arrange
    var productId = Guid.NewGuid();
    var expectedProduct = new Product { Id = productId, Code = "P001" };
    _repositoryMock
        .Setup(r => r.FirstOrDefaultAsync(It.IsAny<IQueryable<Product>>()))
        .ReturnsAsync(expectedProduct);

    // Act
    var result = await _handler.HandleAsync(new GetProductQuery { Id = productId });

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().Be(productId);
}
```

### Unit Test Example

```csharp
using ClassifiedAds.CrossCuttingConcerns.Exceptions;

namespace ClassifiedAds.UnitTests.CrossCuttingConcerns;

public class ValidationExceptionTests
{
    [Fact]
    public void Requires_ShouldNotThrow_WhenConditionIsTrue()
    {
        // Arrange
        const bool condition = true;

        // Act
        var action = () => ValidationException.Requires(condition, "Error");

        // Assert
        action.Should().NotThrow<ValidationException>();
    }
}
```

### Integration Test Example

```csharp
using ClassifiedAds.IntegrationTests.Infrastructure;
using System.Net;

namespace ClassifiedAds.IntegrationTests.Controllers;

[Collection("IntegrationTests")]
public class ProductsControllerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _dbFixture;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ProductsControllerTests(PostgreSqlContainerFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory(_dbFixture.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Get_ShouldReturnOk_WhenCalled()
    {
        // Arrange - already set up

        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

---

## Integration Test Infrastructure

### CustomWebApplicationFactory

The `CustomWebApplicationFactory` configures the test server with:
- Testcontainers PostgreSQL connection string
- Test authentication handler (bypasses real auth)
- Disabled external services (monitoring, distributed cache)

### PostgreSqlContainerFixture

Shared fixture that manages PostgreSQL container lifecycle:
- Starts container once per test collection
- Provides connection string to test classes
- Disposes container after all tests complete

### TestAuthHandler

Custom authentication handler for testing:
- Bypasses real JWT/OAuth authentication
- Creates test user with configurable claims
- Supports testing unauthenticated requests via `X-Skip-Auth` header

---

## Code Coverage

### Generate Coverage Report

```powershell
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Generate HTML report (requires reportgenerator tool)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:./TestResults/**/coverage.cobertura.xml -targetdir:./TestResults/CoverageReport -reporttypes:Html
```

### View Coverage

Open `./TestResults/CoverageReport/index.html` in a browser.

### Coverage Thresholds

Per `rules/testing.md`:
- Minimum **80% line coverage**
- Minimum **80% branch coverage**
- Command/Query handlers: **100% coverage**

---

## CI/CD Integration

Tests run automatically in GitHub Actions CI pipeline (`.github/workflows/ci.yml`):

1. **Build Job**: Compiles solution
2. **Test Job**: 
   - Runs unit tests
   - Runs integration tests (with Docker service)
   - Collects code coverage
   - Uploads test results as artifacts

### Pipeline Requirements

PRs must pass:
- ✅ `dotnet build --configuration Release`
- ✅ `dotnet test` (all tests pass)
- ✅ `dotnet format --verify-no-changes` (code formatting)

---

## Troubleshooting

### Docker Not Running

**Error:** `Docker is either not running or misconfigured`

**Solution:**
1. Start Docker Desktop
2. Ensure Docker is accessible: `docker info`
3. On Windows, ensure Linux containers mode is enabled

### Container Startup Timeout

**Error:** Container failed to start within timeout

**Solution:**
1. Increase timeout in `PostgreSqlContainerFixture`
2. Check Docker resource limits
3. Pull image manually: `docker pull postgres:16`

### Test Database Connection Failed

**Error:** Connection refused to PostgreSQL

**Solution:**
1. Ensure container started successfully
2. Check connection string in `CustomWebApplicationFactory`
3. Verify port binding isn't conflicting

### Authentication Issues

**Error:** 401 Unauthorized in integration tests

**Solution:**
1. Ensure `TestAuthHandler` is properly registered
2. Check if endpoint requires specific claims
3. Verify authentication scheme override in factory

---

## Best Practices

1. **Isolation**: Each test should be independent and idempotent
2. **Speed**: Unit tests should be fast (<100ms each)
3. **Determinism**: No random data that causes flaky tests
4. **Cleanup**: Always dispose resources in `DisposeAsync`
5. **Naming**: Follow `Method_ShouldExpected_WhenCondition` pattern
6. **Coverage**: Test happy paths AND error paths

---

*Last updated: January 1, 2026*
