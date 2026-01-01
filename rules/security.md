# Security Rules

> **Purpose:** Ensure all code is secure by default, prevents vulnerabilities, and complies with OWASP guidelines. Security rules have the **highest priority** and override all other rules.

---

## 1. Secrets Management

### Rules

- **[SEC-001]** Secrets (connection strings, API keys, passwords, certificates) MUST NOT be hardcoded in source code.
- **[SEC-002]** Secrets MUST NOT be committed to version control (use `.gitignore` for local secrets).
- **[SEC-003]** Secrets MUST be stored in:
  - Development: User Secrets (`dotnet user-secrets`)
  - Production: Azure Key Vault, AWS Secrets Manager, or environment variables
- **[SEC-004]** Connection strings MUST be retrieved from configuration, bound via `IOptions<T>`.
- **[SEC-005]** MUST NOT use `appsettings.json` for production secrets — use environment-specific secure stores.

```csharp
// GOOD - Configuration binding
public class ProductModuleOptions
{
    public ConnectionStringsOptions ConnectionStrings { get; set; }
}

services.AddDbContext<ProductDbContext>(options => 
    options.UseNpgsql(settings.ConnectionStrings.Default));

// BAD - Hardcoded connection string
var conn = "Host=localhost;Password=supersecret123";  // VIOLATION: SEC-001
```

---

## 2. Authentication

### Rules

- **[SEC-010]** All API endpoints MUST be protected by `[Authorize]` attribute (opt-in to `[AllowAnonymous]`).
- **[SEC-011]** Authentication MUST use JWT Bearer tokens (IdentityServer or custom JWT).
- **[SEC-012]** Token validation MUST verify:
  - Issuer (`ValidIssuer`)
  - Audience (`ValidAudience`)
  - Signature (via signing certificate)
  - Expiration
- **[SEC-013]** MUST NOT disable HTTPS metadata requirement in production.
- **[SEC-014]** Tokens MUST have reasonable expiration times (recommendation: 15 minutes for access tokens).

```csharp
// GOOD - Proper JWT configuration
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = appSettings.Authentication.Authority;
        options.Audience = appSettings.Authentication.Audience;
        options.RequireHttpsMetadata = !isDevelopment;  // Only false in dev
    });
```

---

## 3. Authorization

### Rules

- **[SEC-020]** Authorization MUST use policy-based authorization with specific permissions.
- **[SEC-021]** Controllers MUST specify permission requirements: `[Authorize(Permissions.GetProduct)]`.
- **[SEC-022]** Permissions MUST be defined in module's `Authorization/Permissions.cs`.
- **[SEC-023]** MUST NOT use role-based authorization alone — use granular permissions.
- **[SEC-024]** Resource-based authorization MUST be implemented for user-owned resources.

```csharp
// GOOD - Permission-based authorization
public static class Permissions
{
    public const string GetProducts = "GetProducts";
    public const string GetProduct = "GetProduct";
    public const string AddProduct = "AddProduct";
    public const string UpdateProduct = "UpdateProduct";
    public const string DeleteProduct = "DeleteProduct";
}

[Authorize(Permissions.GetProduct)]
[HttpGet("{id}")]
public async Task<ActionResult<ProductModel>> Get(Guid id) { ... }

// BAD - Missing authorization or too broad
[HttpGet]  // VIOLATION: No authorization
public async Task<ActionResult<IEnumerable<Product>>> Get() { ... }

[Authorize(Roles = "Admin")]  // VIOLATION: Role-based, not granular
public async Task<IActionResult> Delete(Guid id) { ... }
```

---

## 4. Input Validation

### Rules

- **[SEC-030]** All user input MUST be validated before processing.
- **[SEC-031]** Use FluentValidation or DataAnnotations for model validation.
- **[SEC-032]** MUST validate:
  - Required fields
  - String lengths (min/max)
  - Numeric ranges
  - Format patterns (email, URL, etc.)
  - Allowed values (enums)
- **[SEC-033]** Invalid input MUST return HTTP 400 with `ProblemDetails`.
- **[SEC-034]** MUST NOT trust client-provided IDs for ownership validation — verify server-side.

```csharp
// GOOD - Validation attributes
public class ProductModel
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public required string Code { get; set; }
    
    [Required]
    [StringLength(200)]
    public required string Name { get; set; }
    
    [StringLength(2000)]
    public string? Description { get; set; }
}
```

---

## 5. Output Encoding

### Rules

- **[SEC-040]** All output MUST be properly encoded for the context (HTML, JSON, URL).
- **[SEC-041]** JSON serialization handles encoding automatically — do not double-encode.
- **[SEC-042]** If rendering HTML, use Razor's automatic encoding or explicit `HtmlEncoder`.
- **[SEC-043]** MUST NOT return raw user input in error messages without encoding.

---

## 6. SQL Injection Prevention

### Rules

- **[SEC-050]** All database queries MUST use EF Core parameterization.
- **[SEC-051]** MUST NOT use string interpolation or concatenation in raw SQL queries.
- **[SEC-052]** If raw SQL is required, MUST use `FromSqlInterpolated()` or parameterized queries.
- **[SEC-053]** MUST NOT use `FromSqlRaw()` with user-controlled input.

```csharp
// GOOD - EF Core parameterization (automatic)
var product = await _context.Products
    .Where(p => p.Code == userInput)
    .FirstOrDefaultAsync();

// GOOD - Parameterized raw SQL if needed
var products = await _context.Products
    .FromSqlInterpolated($"SELECT * FROM Products WHERE Code = {userInput}")
    .ToListAsync();

// BAD - SQL injection vulnerability
var products = await _context.Products
    .FromSqlRaw($"SELECT * FROM Products WHERE Code = '{userInput}'")  // VIOLATION
    .ToListAsync();
```

---

## 7. Logging Security

### Rules

- **[SEC-060]** PII (Personally Identifiable Information) MUST NOT be logged:
  - Email addresses
  - Phone numbers
  - Physical addresses
  - IP addresses (except for security audit)
  - Usernames
  - Full names
- **[SEC-061]** Secrets MUST NOT be logged:
  - Passwords
  - API keys
  - Tokens
  - Connection strings
- **[SEC-062]** Log user IDs (GUIDs) instead of usernames/emails.
- **[SEC-063]** Use structured logging with correlation IDs.
- **[SEC-064]** Sensitive data in logs MUST be masked or omitted.

```csharp
// GOOD - Log user ID, not PII
_logger.LogInformation("Product {ProductId} created by user {UserId}", 
    product.Id, 
    currentUser.UserId);

// BAD - Logging PII
_logger.LogInformation("Product created by {Email}", user.Email);  // VIOLATION: SEC-060
_logger.LogDebug("Authenticating with password: {Password}", password);  // VIOLATION: SEC-061
```

---

## 8. CORS Policy

### Rules

- **[SEC-070]** CORS policy MUST be explicitly defined for all APIs.
- **[SEC-071]** MUST NOT use wildcard (`*`) for origins in production.
- **[SEC-072]** Allowed origins MUST be explicitly listed in configuration.
- **[SEC-073]** Credentials (`AllowCredentials()`) MUST only be enabled when needed.

```csharp
// GOOD - Explicit CORS policy
services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", builder =>
    {
        builder.WithOrigins(
                "https://app.example.com",
                "https://admin.example.com")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// BAD - Wildcard origin in production
builder.AllowAnyOrigin();  // VIOLATION: SEC-071
```

---

## 9. HTTP Security Headers

### Rules

- **[SEC-080]** The following security headers MUST be set:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY` (or `SAMEORIGIN`)
  - `X-XSS-Protection: 1; mode=block`
  - `Strict-Transport-Security` (HSTS) in production
  - `Content-Security-Policy` (CSP) for web apps
- **[SEC-081]** HTTPS MUST be enforced in production.
- **[SEC-082]** SHOULD use `app.UseHsts()` and `app.UseHttpsRedirection()` in production.

---

## 10. HttpClient Security

### Rules

- **[SEC-090]** MUST use `IHttpClientFactory` for all HTTP clients.
- **[SEC-091]** MUST NOT instantiate `HttpClient` directly per request (causes socket exhaustion).
- **[SEC-092]** Named or typed clients SHOULD be configured with appropriate timeouts.
- **[SEC-093]** SSL certificate validation MUST NOT be disabled in production.

```csharp
// GOOD - IHttpClientFactory
services.AddHttpClient("ExternalApi", client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Usage
var client = _httpClientFactory.CreateClient("ExternalApi");

// BAD - Direct instantiation
using var client = new HttpClient();  // VIOLATION: SEC-091
```

---

## 11. Rate Limiting

### Rules

- **[SEC-100]** Rate limiting MUST be enabled on public API endpoints.
- **[SEC-101]** Use `[EnableRateLimiting]` attribute on controllers.
- **[SEC-102]** Rate limit policies MUST be defined per endpoint sensitivity.
- **[SEC-103]** Authentication endpoints MUST have stricter rate limits.

```csharp
// GOOD - Rate limiting enabled
[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[ApiController]
public class ProductsController : ControllerBase { ... }
```

---

## 12. Antiforgery (CSRF)

### Rules

- **[SEC-110]** Antiforgery tokens MUST be used for state-changing operations in web apps (not APIs with JWT).
- **[SEC-111]** For APIs using cookie authentication, CSRF protection MUST be implemented.
- **[SEC-112]** APIs using Bearer token authentication MAY skip antiforgery (tokens are not auto-sent).

---

## 13. File Upload Security

### Rules

- **[SEC-120]** File uploads MUST validate:
  - File extension (whitelist allowed types)
  - MIME type
  - File size (max limit)
  - Content (magic bytes if needed)
- **[SEC-121]** Uploaded files MUST NOT be stored in web-accessible directories.
- **[SEC-122]** File names MUST be sanitized or replaced with GUIDs.
- **[SEC-123]** Virus scanning SHOULD be performed for user uploads.

---

## 14. Error Handling

### Rules

- **[SEC-130]** Detailed error messages MUST NOT be exposed to clients in production.
- **[SEC-131]** Use `ProblemDetails` format for API errors.
- **[SEC-132]** Stack traces MUST NOT be returned in production responses.
- **[SEC-133]** Log full error details server-side with correlation ID.

```csharp
// GOOD - ProblemDetails without sensitive info
app.UseExceptionHandler(options =>
{
    options.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 500,
            Title = "An error occurred",
            Instance = context.TraceIdentifier
        });
    });
});
```

---

## 15. Dependency Security

### Rules

- **[SEC-140]** NuGet packages MUST be kept up to date (security patches).
- **[SEC-141]** `dotnet list package --vulnerable` SHOULD be run in CI.
- **[SEC-142]** Packages with known vulnerabilities MUST be updated or replaced.
- **[SEC-143]** Only use packages from trusted publishers.

---

## Conflict Resolution

Security rules have the HIGHEST priority. If any other rule would weaken security, the security rule MUST be followed. See `00-priority.md`.

---

## Checklist (Complete Before PR)

- [ ] No hardcoded secrets in code
- [ ] Secrets stored in User Secrets (dev) or secure store (prod)
- [ ] All endpoints protected with `[Authorize]` and specific permissions
- [ ] Input validation implemented for all user input
- [ ] SQL queries use EF Core parameterization only
- [ ] No PII or secrets logged
- [ ] CORS policy explicitly defined (no wildcards in prod)
- [ ] Security headers configured
- [ ] HttpClient via `IHttpClientFactory` only
- [ ] Rate limiting enabled on controllers
- [ ] File uploads validated (if applicable)
- [ ] Error responses use `ProblemDetails` without sensitive info
- [ ] No vulnerable NuGet packages

---

## Good Example: Secure Controller

```csharp
[EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(Dispatcher dispatcher, ILogger<ProductsController> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [Authorize(Permissions.AddProduct)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductModel>> Post([FromBody] ProductModel model)
    {
        // Input validation via model binding/FluentValidation
        // No PII in logs - only product ID and user ID
        _logger.LogInformation("Creating product {ProductCode} by user {UserId}", 
            model.Code, 
            User.FindFirstValue("sub"));
        
        var product = model.ToEntity();
        await _dispatcher.DispatchAsync(new AddUpdateProductCommand { Product = product });
        
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product.ToModel());
    }
}
```
