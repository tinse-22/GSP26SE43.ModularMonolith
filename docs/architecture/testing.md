# Testing Guide

> **Purpose:** Comprehensive guide for running and writing tests in the ClassifiedAds Modular Monolith project, including unit tests, integration tests, and CI/CD integration.

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

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) to automatically spin up PostgreSQL containers. This ensures tests run against a real database without manual setup.

**Setup Requirements**:

1. **Docker Desktop** must be installed and running
2. Verify Docker is accessible: `docker info`
3. For Windows, ensure Docker is configured for **Linux containers**
4. Docker must have sufficient resources allocated (recommended: 4GB RAM minimum)

**Pre-pull the PostgreSQL image** (optional, speeds up first test run):
```bash
docker pull postgres:16
```

---

## Test Projects Structure

The solution includes two test projects with different scopes and purposes:

```
ClassifiedAds.ModularMonolith/
├── ClassifiedAds.UnitTests/                    # Unit tests
│   ├── CrossCuttingConcerns/
│   │   ├── ValidationExceptionTests.cs         # Exception validation tests
│   │   └── NotFoundExceptionTests.cs           # Not found exception tests
│   ├── Domain/
│   │   └── (Entity and value object tests)
│   └── Infrastructure/
│       └── (Service implementation tests)
│
└── ClassifiedAds.IntegrationTests/             # Integration tests
    ├── Infrastructure/
    │   ├── CustomWebApplicationFactory.cs      # Test server factory
    │   ├── PostgreSqlContainerFixture.cs       # Testcontainers fixture
    │   ├── IntegrationTestCollection.cs        # xUnit collection definition
    │   └── TestAuthHandler.cs                  # Test authentication handler
    └── Smoke/
        └── ApplicationSmokeTests.cs            # Application startup tests
```

### Test Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `xunit` | 2.9.3 | Test framework - provides `[Fact]`, `[Theory]` attributes |
| `xunit.runner.visualstudio` | 3.0.2 | Visual Studio / VS Code test runner integration |
| `FluentAssertions` | 8.0.1 | Fluent assertion library for readable test assertions |
| `Moq` | 4.20.72 | Mocking framework for creating test doubles |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.0 | WebApplicationFactory for integration tests |
| `Testcontainers.PostgreSql` | 4.3.0 | PostgreSQL container management |
| `Respawn` | 6.2.1 | Database reset between tests |
| `coverlet.collector` | 6.0.4 | Code coverage collection during tests |
| `Microsoft.NET.Test.Sdk` | 17.13.0 | Test SDK for dotnet test command |

### Project References

**ClassifiedAds.UnitTests**:
- `ClassifiedAds.CrossCuttingConcerns` - Test exception classes, utilities
- `ClassifiedAds.Domain` - Test entities, value objects
- `ClassifiedAds.Infrastructure` - Test service implementations

**ClassifiedAds.IntegrationTests**:
- `ClassifiedAds.WebAPI` - Full application reference for WebApplicationFactory

---

## Running Tests

### Run All Tests

```powershell
# From solution root - runs both unit and integration tests
dotnet test ClassifiedAds.ModularMonolith.slnx --configuration Release

# With verbose output for debugging
dotnet test ClassifiedAds.ModularMonolith.slnx --configuration Release --verbosity normal

# With detailed output (shows individual test names)
dotnet test ClassifiedAds.ModularMonolith.slnx --configuration Release --verbosity detailed
```

### Run Unit Tests Only

Unit tests are fast and don't require Docker:

```powershell
# Run all unit tests
dotnet test ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj --configuration Release

# Run with coverage
dotnet test ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj \
    --configuration Release \
    --collect:"XPlat Code Coverage"
```

### Run Integration Tests Only

Integration tests require Docker Desktop to be running:

```powershell
# Run all integration tests (starts PostgreSQL container automatically)
dotnet test ClassifiedAds.IntegrationTests/ClassifiedAds.IntegrationTests.csproj --configuration Release

# Run with detailed logging
dotnet test ClassifiedAds.IntegrationTests/ClassifiedAds.IntegrationTests.csproj \
    --configuration Release \
    --verbosity normal \
    --logger "console;verbosity=detailed"
```

### Run Specific Tests

```powershell
# By test class name
dotnet test --filter "FullyQualifiedName~ValidationExceptionTests"

# By test method name
dotnet test --filter "FullyQualifiedName~Requires_ShouldNotThrow"

# By namespace
dotnet test --filter "FullyQualifiedName~ClassifiedAds.UnitTests.CrossCuttingConcerns"

# Run tests matching a pattern
dotnet test --filter "Name~Should"
```

### Run Tests in Watch Mode

For TDD (Test-Driven Development) workflow:

```powershell
# Watch for changes and re-run tests
dotnet watch test --project ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj
```

---

## Writing New Tests

### Test Naming Convention

Follow the pattern: `{MethodUnderTest}_Should{ExpectedBehavior}_When{Condition}`

This pattern clearly communicates:
- **What** is being tested (method name)
- **Expected outcome** (should do something)
- **Conditions** (when/given certain state)

```csharp
// Good examples
[Fact]
public async Task GetProduct_ShouldReturnProduct_WhenProductExists()

[Fact]
public void Requires_ShouldThrowValidationException_WhenConditionIsFalse()

[Fact]
public async Task CreateOrder_ShouldPublishEvent_WhenOrderIsValid()

[Fact]
public void Constructor_ShouldThrowArgumentNullException_WhenNameIsNull()
```

### AAA Pattern (Arrange-Act-Assert)

All tests MUST follow the AAA pattern with explicit comments. This improves readability and maintenance:

```csharp
[Fact]
public async Task GetProduct_ShouldReturnProduct_WhenProductExists()
{
    // Arrange - Set up test data and dependencies
    var productId = Guid.NewGuid();
    var expectedProduct = new Product { Id = productId, Code = "P001", Name = "Test Product" };
    _repositoryMock
        .Setup(r => r.FirstOrDefaultAsync(It.IsAny<IQueryable<Product>>()))
        .ReturnsAsync(expectedProduct);

    // Act - Execute the method being tested
    var result = await _handler.HandleAsync(new GetProductQuery { Id = productId });

    // Assert - Verify the expected outcome
    result.Should().NotBeNull();
    result.Id.Should().Be(productId);
    result.Code.Should().Be("P001");
}
```

### Unit Test Example

Unit tests are fast, isolated, and don't require external dependencies:

```csharp
using ClassifiedAds.CrossCuttingConcerns.Exceptions;

namespace ClassifiedAds.UnitTests.CrossCuttingConcerns;

/// <summary>
/// Tests for ValidationException static helper methods.
/// Validates that the Requires method correctly throws or doesn't throw
/// based on the condition provided.
/// </summary>
public class ValidationExceptionTests
{
    [Fact]
    public void Requires_ShouldNotThrow_WhenConditionIsTrue()
    {
        // Arrange
        const bool condition = true;
        const string errorMessage = "This should not appear";

        // Act
        var action = () => ValidationException.Requires(condition, errorMessage);

        // Assert
        action.Should().NotThrow<ValidationException>();
    }

    [Fact]
    public void Requires_ShouldThrowValidationException_WhenConditionIsFalse()
    {
        // Arrange
        const bool condition = false;
        const string expectedMessage = "Validation failed";

        // Act
        var action = () => ValidationException.Requires(condition, expectedMessage);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage(expectedMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void RequiresNotEmpty_ShouldThrow_WhenStringIsNullOrWhitespace(string? value)
    {
        // Arrange
        const string paramName = "testParam";

        // Act
        var action = () => ValidationException.RequiresNotEmpty(value, paramName);

        // Assert
        action.Should().Throw<ValidationException>();
    }
}
```

### Integration Test Example

Integration tests verify the full request/response cycle with real infrastructure:

```csharp
using ClassifiedAds.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace ClassifiedAds.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for the Products API endpoint.
/// Tests use Testcontainers for real PostgreSQL database
/// and TestAuthHandler to bypass authentication.
/// </summary>
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
        // Create test server with real database connection
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
    public async Task GetProducts_ShouldReturnOk_WhenCalled()
    {
        // Arrange - client already configured

        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetProduct_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/products/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateProduct_ShouldReturnCreated_WhenProductIsValid()
    {
        // Arrange
        var newProduct = new
        {
            Code = "TEST001",
            Name = "Test Product",
            Description = "A test product"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", newProduct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }
}
```

---

## Integration Test Infrastructure

The integration test infrastructure provides a complete testing environment with real database and bypassed authentication.

### CustomWebApplicationFactory

The `CustomWebApplicationFactory` creates a test version of the application with:

| Configuration | Purpose |
|--------------|---------|
| PostgreSQL connection | Connects to Testcontainers PostgreSQL |
| Test authentication | Bypasses real JWT/OAuth with `TestAuthHandler` |
| Disabled services | Turns off external services (monitoring, distributed cache) |
| In-memory configuration | Overrides appsettings for test environment |

**Key Features**:
- Inherits from `WebApplicationFactory<Program>`
- Configures `TestServer` for HTTP testing
- Replaces database connection string with test container
- Registers `TestAuthHandler` for authentication bypass

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CustomWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace database connection
            // Register TestAuthHandler
            // Configure test-specific services
        });
    }
}
```

### PostgreSqlContainerFixture

**Purpose**: Manages PostgreSQL container lifecycle using Testcontainers.

**Features**:
- Starts PostgreSQL 16 container once per test collection
- Provides connection string to all test classes
- Automatically disposes container after tests complete
- Handles container startup timeout and health checks

```csharp
public class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    public string ConnectionString => _container.GetConnectionString();

    public PostgreSqlContainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("ClassifiedAds_Test")
            .WithUsername("postgres")
            .WithPassword("postgres123!@#")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
```

### IntegrationTestCollection

**Purpose**: xUnit collection definition that ensures all integration tests share the same PostgreSQL container.

```csharp
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
    // This class has no code - it's just a marker for xUnit
    // All test classes with [Collection("IntegrationTests")] will share
    // the same PostgreSqlContainerFixture instance
}
```

### TestAuthHandler

**Purpose**: Custom authentication handler that bypasses real authentication for testing.

**Features**:
- Creates authenticated test user with configurable claims
- Allows testing both authenticated and unauthenticated requests
- Supports custom user ID and roles via headers

```csharp
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for skip auth header
        if (Request.Headers.ContainsKey("X-Skip-Auth"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Create test user claims
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

**Usage in Tests**:
```csharp
// Authenticated request (default)
var response = await _client.GetAsync("/api/products");

// Unauthenticated request
_client.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
var response = await _client.GetAsync("/api/products");
```

---

## Code Coverage

Code coverage measures how much of your code is executed during tests. This project uses Coverlet for coverage collection.

### Generate Coverage Report

```powershell
# Run tests with coverage collection
dotnet test ClassifiedAds.ModularMonolith.slnx \
    --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults

# Run only unit tests with coverage
dotnet test ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj \
    --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults/UnitTests

# Run only integration tests with coverage
dotnet test ClassifiedAds.IntegrationTests/ClassifiedAds.IntegrationTests.csproj \
    --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults/IntegrationTests
```

### Generate HTML Report

Install the ReportGenerator tool and create visual reports:

```powershell
# Install ReportGenerator globally (one time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report from coverage files
reportgenerator \
    -reports:"./TestResults/**/coverage.cobertura.xml" \
    -targetdir:"./TestResults/CoverageReport" \
    -reporttypes:Html

# Open the report
start ./TestResults/CoverageReport/index.html
```

### View Coverage in VS Code

VS Code with the Coverage Gutters extension can display coverage inline:

1. Install "Coverage Gutters" extension
2. Run tests with coverage: `dotnet test --collect:"XPlat Code Coverage"`
3. Click "Watch" in the Coverage Gutters status bar
4. Coverage highlights appear in the editor

### Coverage Thresholds

Recommended minimum coverage targets:

| Component Type | Line Coverage | Branch Coverage |
|----------------|---------------|-----------------|
| Domain Logic | 90%+ | 85%+ |
| Command/Query Handlers | 100% | 100% |
| Utility Classes | 80%+ | 80%+ |
| Controllers | 70%+ | 70%+ |
| Overall Project | 80%+ | 80%+ |

### Coverage Report Output

The coverage report shows:
- **Line Coverage**: Percentage of code lines executed
- **Branch Coverage**: Percentage of conditional branches taken
- **Method Coverage**: Percentage of methods called
- **Class Coverage**: Percentage of classes touched

```
TestResults/
├── UnitTests/
│   └── {guid}/
│       └── coverage.cobertura.xml
├── IntegrationTests/
│   └── {guid}/
│       └── coverage.cobertura.xml
└── CoverageReport/
    ├── index.html              # Main report
    ├── summary.html            # Summary view
    └── {namespace}_{class}.html # Per-class details
```

---

## CI/CD Integration

Tests are automatically run in the GitHub Actions CI pipeline on every push and pull request.

### CI Pipeline Test Configuration

**Location**: [.github/workflows/ci.yml](../.github/workflows/ci.yml)

The CI pipeline includes a dedicated test job that:

1. **Sets up PostgreSQL service** for integration tests (fallback if Testcontainers unavailable)
2. **Runs unit tests** with coverage collection
3. **Runs integration tests** with coverage collection
4. **Uploads test results** as artifacts
5. **Uploads coverage reports** for analysis

### Test Job Steps

```yaml
test:
  name: Run Tests
  runs-on: ubuntu-latest
  needs: build

  services:
    postgres:
      image: postgres:16
      env:
        POSTGRES_USER: postgres
        POSTGRES_PASSWORD: postgres123!@#
        POSTGRES_DB: ClassifiedAds_Test
      ports:
        - 5432:5432
      options: >-
        --health-cmd pg_isready
        --health-interval 10s
        --health-timeout 5s
        --health-retries 5

  steps:
    - name: Run unit tests
      run: |
        dotnet test ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj \
          --configuration Release \
          --no-build \
          --verbosity normal \
          --logger "trx;LogFileName=unit-test-results.trx" \
          --collect:"XPlat Code Coverage" \
          --results-directory ./TestResults/UnitTests

    - name: Run integration tests
      run: |
        dotnet test ClassifiedAds.IntegrationTests/ClassifiedAds.IntegrationTests.csproj \
          --configuration Release \
          --no-build \
          --verbosity normal \
          --logger "trx;LogFileName=integration-test-results.trx" \
          --collect:"XPlat Code Coverage" \
          --results-directory ./TestResults/IntegrationTests

    - name: Upload test results
      uses: actions/upload-artifact@v4
      with:
        name: test-results
        path: "./TestResults/**"

    - name: Upload code coverage
      uses: actions/upload-artifact@v4
      with:
        name: code-coverage
        path: "./TestResults/**/coverage.cobertura.xml"
```

### Pipeline Requirements

All PRs must pass these checks before merging:

| Check | Description |
|-------|-------------|
| ✅ Build & Lint | Solution compiles without errors |
| ✅ Run Tests | All unit and integration tests pass |
| ✅ Docker Build | All Dockerfiles build successfully |

### Viewing Test Results

1. **GitHub Actions UI**: Go to Actions tab → Select workflow run → Click on "Run Tests" job
2. **Artifacts**: Download `test-results` artifact for TRX files
3. **Coverage**: Download `code-coverage` artifact for Cobertura XML files

### Failed Test Debugging

When tests fail in CI:

1. Check the job logs for the specific failure
2. Download the `test-results` artifact
3. Open the `.trx` file in Visual Studio or VS Code
4. Look for stack traces and assertion messages

---

## Troubleshooting

### Docker Not Running

**Error:** `Docker is either not running or misconfigured` or `Cannot connect to Docker daemon`

**Causes & Solutions:**

| Cause | Solution |
|-------|----------|
| Docker Desktop not started | Start Docker Desktop application |
| Docker daemon not running | Restart Docker Desktop |
| Linux containers not enabled | Switch to Linux containers in Docker settings |
| Insufficient permissions | Run as administrator or add user to docker group |

**Verification Commands:**
```powershell
# Check Docker is running
docker info

# Check Docker can run containers
docker run hello-world

# Check Docker Compose
docker compose version
```

### Container Startup Timeout

**Error:** `Container failed to start within the configured timeout`

**Solutions:**

1. **Increase timeout** in `PostgreSqlContainerFixture`:
   ```csharp
   _container = new PostgreSqlBuilder()
       .WithWaitStrategy(Wait.ForUnixContainer()
           .UntilPortIsAvailable(5432)
           .WithTimeout(TimeSpan.FromMinutes(2)))
       .Build();
   ```

2. **Check Docker resources** - Increase memory/CPU allocation in Docker Desktop settings

3. **Pre-pull the image**:
   ```powershell
   docker pull postgres:16
   ```

4. **Check for port conflicts**:
   ```powershell
   netstat -ano | findstr 5432
   ```

### Test Database Connection Failed

**Error:** `Connection refused` or `Could not connect to PostgreSQL`

**Solutions:**

| Cause | Solution |
|-------|----------|
| Container not started | Check `PostgreSqlContainerFixture.InitializeAsync()` |
| Wrong connection string | Verify `_container.GetConnectionString()` output |
| Port conflict | Testcontainers uses random ports by default |
| Firewall blocking | Check Windows Firewall settings |

**Debug Steps:**
```powershell
# List running containers
docker ps

# Check container logs
docker logs <container_id>

# Test connection manually
psql -h localhost -p <port> -U postgres -d ClassifiedAds_Test
```

### Authentication Issues

**Error:** `401 Unauthorized` in integration tests

**Solutions:**

1. **Verify TestAuthHandler registration**:
   ```csharp
   services.AddAuthentication("Test")
       .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);
   ```

2. **Check if endpoint requires specific claims**:
   - Add required claims to `TestAuthHandler`
   - Or modify endpoint authorization policy

3. **Test unauthenticated endpoints**:
   ```csharp
   _client.DefaultRequestHeaders.Add("X-Skip-Auth", "true");
   ```

### Tests Pass Locally But Fail in CI

**Common Causes:**

| Issue | Solution |
|-------|----------|
| Time zone differences | Use UTC in tests |
| Path differences (Windows vs Linux) | Use `Path.Combine()` |
| Race conditions | Add proper synchronization |
| Missing environment variables | Check CI workflow configuration |
| Docker availability | CI uses service containers |

### Flaky Tests

**Problem:** Tests intermittently pass or fail

**Solutions:**

1. **Add retries for network operations**:
   ```csharp
   // Use Polly for retry logic
   var response = await Policy
       .Handle<HttpRequestException>()
       .RetryAsync(3)
       .ExecuteAsync(() => _client.GetAsync("/api/products"));
   ```

2. **Ensure proper test isolation**:
   - Use unique test data per test
   - Reset database state between tests
   - Don't share state between tests

3. **Add explicit waits for async operations**:
   ```csharp
   await Task.Delay(100); // Only as last resort
   ```

---

## Best Practices

### Test Design Principles

| Principle | Description |
|-----------|-------------|
| **Isolation** | Each test should be independent and idempotent |
| **Speed** | Unit tests should be fast (<100ms each) |
| **Determinism** | No random data that causes flaky tests |
| **Cleanup** | Always dispose resources in `DisposeAsync` |
| **Single Responsibility** | Test one thing per test method |

### Naming Guidelines

| Element | Convention | Example |
|---------|------------|---------|
| Test Class | `{ClassUnderTest}Tests` | `ValidationExceptionTests` |
| Test Method | `{Method}_Should{Expected}_When{Condition}` | `Requires_ShouldThrow_WhenFalse` |
| Test Data | Descriptive variable names | `expectedProduct`, `invalidInput` |

### Assertions Best Practices

```csharp
// ✅ Good - specific assertion
result.Should().NotBeNull();
result.Name.Should().Be("Expected Name");
result.Items.Should().HaveCount(3);

// ❌ Bad - vague assertion
Assert.True(result != null);
Assert.Equal("Expected Name", result.Name);

// ✅ Good - assert on all relevant properties
response.StatusCode.Should().Be(HttpStatusCode.OK);
response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

// ✅ Good - assert exceptions properly
var action = () => service.DoSomething(invalidInput);
action.Should().Throw<ValidationException>()
    .WithMessage("*required*");
```

### Test Organization

```csharp
public class ProductServiceTests
{
    // Shared setup
    private readonly Mock<IRepository> _repositoryMock;
    private readonly ProductService _sut; // System Under Test

    public ProductServiceTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _sut = new ProductService(_repositoryMock.Object);
    }

    // Group related tests with #region or nested classes
    #region GetProduct Tests

    [Fact]
    public void GetProduct_ShouldReturnProduct_WhenExists() { }

    [Fact]
    public void GetProduct_ShouldReturnNull_WhenNotExists() { }

    #endregion

    #region CreateProduct Tests

    [Fact]
    public void CreateProduct_ShouldSucceed_WhenValid() { }

    [Fact]
    public void CreateProduct_ShouldThrow_WhenInvalid() { }

    #endregion
}
```

---

*Last updated: January 1, 2026*
