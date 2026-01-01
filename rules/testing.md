# Testing Rules

> **Purpose:** Guarantee code correctness, prevent regressions, and ensure all features are covered by automated tests. Testing rules are enforced by CI pipelines and code review.

---

## 1. Test Project Structure

### Rules

- **[TEST-001]** Each module MUST have corresponding test projects:
  - `{Module}.UnitTests` — Unit tests for handlers, services
  - `{Module}.IntegrationTests` — API integration tests
  - `{Module}.EndToEndTests` — E2E tests (optional)

- **[TEST-002]** Test projects MUST follow naming convention:
  ```
  ClassifiedAds.Modules.Product.UnitTests/
  ClassifiedAds.Modules.Product.IntegrationTests/
  ClassifiedAds.Modules.Product.EndToEndTests/
  ```

- **[TEST-003]** Test projects MUST reference:
  - xUnit (test framework)
  - FluentAssertions (assertion library)
  - Moq or NSubstitute (mocking)
  - Microsoft.AspNetCore.Mvc.Testing (integration tests)

---

## 2. Test Naming Convention

### Rules

- **[TEST-010]** Test methods MUST follow naming convention:
  ```
  {MethodUnderTest}_Should{ExpectedBehavior}_When{Condition}
  ```

- **[TEST-011]** Examples of proper naming:
  ```csharp
  GetProduct_ShouldReturnProduct_WhenProductExists()
  GetProduct_ShouldThrowNotFoundException_WhenProductDoesNotExist()
  CreateProduct_ShouldReturnCreated_WhenModelIsValid()
  CreateProduct_ShouldReturnBadRequest_WhenCodeIsDuplicate()
  ```

- **[TEST-012]** Test class naming:
  - Unit tests: `{ClassUnderTest}Tests` (e.g., `GetProductQueryHandlerTests`)
  - Integration tests: `{Controller}Tests` (e.g., `ProductsControllerTests`)

---

## 3. Arrange-Act-Assert (AAA) Pattern

### Rules

- **[TEST-020]** All tests MUST follow the AAA pattern with clear section comments.
- **[TEST-021]** Each section MUST be visually separated (blank line or comment).
- **[TEST-022]** Only ONE logical assertion per test (multiple asserts on same object OK).

```csharp
[Fact]
public async Task GetProduct_ShouldReturnProduct_WhenProductExists()
{
    // Arrange
    var productId = Guid.NewGuid();
    var expectedProduct = new Product { Id = productId, Code = "P001", Name = "Test Product" };
    _repositoryMock
        .Setup(r => r.FirstOrDefaultAsync(It.IsAny<IQueryable<Product>>()))
        .ReturnsAsync(expectedProduct);

    var handler = new GetProductQueryHandler(_repositoryMock.Object);
    var query = new GetProductQuery { Id = productId, ThrowNotFoundIfNull = false };

    // Act
    var result = await handler.HandleAsync(query);

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().Be(productId);
    result.Code.Should().Be("P001");
}
```

---

## 4. Unit Tests

### 4.1 Scope

- **[TEST-030]** Unit tests MUST test a single unit in isolation (handler, service, mapper).
- **[TEST-031]** All external dependencies MUST be mocked.
- **[TEST-032]** Unit tests MUST NOT access real databases, file systems, or external APIs.

### 4.2 Coverage Requirements

- **[TEST-040]** Minimum code coverage: **80% line coverage** and **80% branch coverage**.
- **[TEST-041]** Command handlers MUST have 100% test coverage.
- **[TEST-042]** Query handlers MUST have 100% test coverage.
- **[TEST-043]** Domain event handlers MUST have test coverage.

### 4.3 What to Test

- **[TEST-050]** Happy path (success scenarios).
- **[TEST-051]** Error paths (exceptions, null returns).
- **[TEST-052]** Edge cases (empty collections, boundary values).
- **[TEST-053]** Validation failures.

```csharp
public class AddUpdateProductCommandHandlerTests
{
    private readonly Mock<ICrudService<Product>> _crudServiceMock;
    private readonly AddUpdateProductCommandHandler _handler;

    public AddUpdateProductCommandHandlerTests()
    {
        _crudServiceMock = new Mock<ICrudService<Product>>();
        _handler = new AddUpdateProductCommandHandler(_crudServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldCallAddOrUpdate_WhenProductProvided()
    {
        // Arrange
        var product = new Product { Id = Guid.NewGuid(), Code = "P001", Name = "Test" };
        var command = new AddUpdateProductCommand { Product = product };

        // Act
        await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        _crudServiceMock.Verify(
            s => s.AddOrUpdateAsync(product, CancellationToken.None), 
            Times.Once);
    }
}
```

---

## 5. Integration Tests

### 5.1 WebApplicationFactory

- **[TEST-060]** Integration tests MUST use `WebApplicationFactory<Program>`.
- **[TEST-061]** Tests MUST configure test-specific services (in-memory DB, mock external services).
- **[TEST-062]** Tests MUST NOT share state between test runs.

### 5.2 Database Strategy

- **[TEST-070]** Integration tests MUST use one of:
  - **Testcontainers** (PostgreSQL container) — preferred for accuracy
  - **SQLite InMemory** — faster but less accurate
  - **EF Core InMemory** — not recommended (behavior differs from real DB)

- **[TEST-071]** Each test MUST run against a clean database state.
- **[TEST-072]** Use `IClassFixture<T>` to share expensive fixtures (containers).

### 5.3 Test Structure

```csharp
public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_ShouldReturnOk_WhenProductsExist()
    {
        // Arrange
        // Seed test data if needed

        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductModel>>();
        products.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenProductDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/products/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_ShouldReturnCreated_WhenModelIsValid()
    {
        // Arrange
        var model = new ProductModel { Code = "NEW001", Name = "New Product" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_ShouldReturnBadRequest_WhenCodeIsEmpty()
    {
        // Arrange
        var model = new ProductModel { Code = "", Name = "Test" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/products", model);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

### 5.4 Custom WebApplicationFactory

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real DbContext
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ProductDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add test database (SQLite InMemory or Testcontainers)
            services.AddDbContext<ProductDbContext>(options =>
            {
                options.UseSqlite("DataSource=:memory:");
            });

            // Ensure database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
```

---

## 6. Authentication in Tests

### Rules

- **[TEST-080]** Integration tests MUST configure test authentication.
- **[TEST-081]** Use `AddAuthentication("Test")` with custom handler for tests.
- **[TEST-082]** Tests SHOULD cover both authenticated and unauthenticated scenarios.

```csharp
// In CustomWebApplicationFactory
builder.ConfigureServices(services =>
{
    services.AddAuthentication("Test")
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
});

// TestAuthHandler
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("permission", Permissions.GetProducts),
            new Claim("permission", Permissions.AddProduct),
            // Add required permissions
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

---

## 7. Test Data

### Rules

- **[TEST-090]** Test data SHOULD use builders or factories, not inline construction.
- **[TEST-091]** SHOULD use Bogus or similar library for generating realistic test data.
- **[TEST-092]** Test data MUST NOT contain real PII or production data.

```csharp
public class ProductTestDataBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _code = "P001";
    private string _name = "Test Product";
    private string _description = "Test Description";

    public ProductTestDataBuilder WithId(Guid id) { _id = id; return this; }
    public ProductTestDataBuilder WithCode(string code) { _code = code; return this; }
    public ProductTestDataBuilder WithName(string name) { _name = name; return this; }

    public Product Build() => new Product
    {
        Id = _id,
        Code = _code,
        Name = _name,
        Description = _description
    };
}

// Usage
var product = new ProductTestDataBuilder()
    .WithCode("TEST001")
    .WithName("Integration Test Product")
    .Build();
```

---

## 8. Mocking

### Rules

- **[TEST-100]** Use Moq or NSubstitute consistently within a module.
- **[TEST-101]** Mock interfaces, not concrete classes.
- **[TEST-102]** Verify mock calls were made with expected parameters.
- **[TEST-103]** SHOULD NOT over-mock — if a class has no external dependencies, test it directly.

```csharp
// GOOD - Mocking repository interface
var repositoryMock = new Mock<IProductRepository>();
repositoryMock
    .Setup(r => r.FirstOrDefaultAsync(It.IsAny<IQueryable<Product>>()))
    .ReturnsAsync(expectedProduct);

// Verify
repositoryMock.Verify(
    r => r.AddOrUpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), 
    Times.Once);
```

---

## 9. Async Testing

### Rules

- **[TEST-110]** All async tests MUST use `async Task` (not `async void`).
- **[TEST-111]** Use `await` for async operations, not `.Result` or `.Wait()`.
- **[TEST-112]** Test `CancellationToken` behavior when relevant.

---

## 10. CI/CD Integration

### Rules

- **[TEST-120]** All tests MUST pass in CI before merge.
- **[TEST-121]** Code coverage MUST be reported in CI.
- **[TEST-122]** PRs MUST NOT decrease code coverage below threshold.
- **[TEST-123]** Flaky tests MUST be fixed or quarantined immediately.

### CI Configuration

```yaml
# Example CI step
- name: Run Tests
  run: |
    dotnet test --configuration Release --collect:"XPlat Code Coverage" --results-directory ./coverage

- name: Check Coverage
  run: |
    # Fail if coverage < 80%
    dotnet tool run reportgenerator -reports:./coverage/**/coverage.cobertura.xml -targetdir:./coverage/report
```

---

## 11. Performance Testing

### Rules (SHOULD)

- **[TEST-130]** SHOULD have performance benchmarks for critical paths.
- **[TEST-131]** SHOULD use BenchmarkDotNet for micro-benchmarks.
- **[TEST-132]** SHOULD document expected response times for API endpoints.

---

## Conflict Resolution

If a testing rule conflicts with a higher-priority rule (Security, Architecture), follow the higher-priority rule as defined in `00-priority.md`.

---

## Checklist (Complete Before PR)

- [ ] Unit tests written for all new handlers/services
- [ ] Integration tests written for new API endpoints
- [ ] Test naming follows `Method_ShouldExpected_WhenCondition`
- [ ] All tests follow AAA pattern
- [ ] Code coverage meets or exceeds 80%
- [ ] Happy path and error paths tested
- [ ] Mocks verify expected interactions
- [ ] No flaky tests introduced
- [ ] Tests pass in CI
- [ ] No real PII in test data

---

## Good Example: Complete Test Class

```csharp
namespace ClassifiedAds.Modules.Product.UnitTests.Queries;

public class GetProductQueryHandlerTests
{
    private readonly Mock<IProductRepository> _repositoryMock;
    private readonly GetProductQueryHandler _handler;

    public GetProductQueryHandlerTests()
    {
        _repositoryMock = new Mock<IProductRepository>();
        _handler = new GetProductQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnProduct_WhenProductExists()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var expectedProduct = new Product
        {
            Id = productId,
            Code = "P001",
            Name = "Test Product"
        };

        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<IQueryable<Product>>()))
            .ReturnsAsync(expectedProduct);

        var query = new GetProductQuery { Id = productId, ThrowNotFoundIfNull = false };

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedProduct);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNull_WhenProductDoesNotExistAndThrowNotFoundIsFalse()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<IQueryable<Product>>()))
            .ReturnsAsync((Product?)null);

        var query = new GetProductQuery { Id = Guid.NewGuid(), ThrowNotFoundIfNull = false };

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowNotFoundException_WhenProductDoesNotExistAndThrowNotFoundIsTrue()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<IQueryable<Product>>()))
            .ReturnsAsync((Product?)null);

        var query = new GetProductQuery { Id = productId, ThrowNotFoundIfNull = true };

        // Act
        var act = async () => await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{productId}*");
    }
}
```
