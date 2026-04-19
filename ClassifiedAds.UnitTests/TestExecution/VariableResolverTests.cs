using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ClassifiedAds.UnitTests.TestExecution;

public class VariableResolverTests
{
    private readonly VariableResolver _resolver = new();

    [Fact]
    public void Resolve_Should_ReplacePlaceholdersInUrl()
    {
        // Arrange
        var testCase = CreateTestCase(url: "/api/users/{{userId}}");
        var variables = new Dictionary<string, string> { ["userId"] = "42" };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.ResolvedUrl.Should().Contain("/api/users/42");
    }

    [Fact]
    public void Resolve_Should_ReplacePlaceholdersInHeaders()
    {
        // Arrange
        var headers = JsonSerializer.Serialize(new Dictionary<string, string> { ["Authorization"] = "Bearer {{token}}" });
        var testCase = CreateTestCase(headers: headers);
        var variables = new Dictionary<string, string> { ["token"] = "abc123" };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Headers.Should().ContainKey("Authorization");
        result.Headers["Authorization"].Should().Be("Bearer abc123");
    }

    [Fact]
    public void Resolve_Should_ReplacePlaceholdersInQueryParams()
    {
        // Arrange
        var queryParams = JsonSerializer.Serialize(new Dictionary<string, string> { ["filter"] = "{{filterValue}}" });
        var testCase = CreateTestCase(queryParams: queryParams);
        var variables = new Dictionary<string, string> { ["filterValue"] = "active" };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.QueryParams.Should().ContainKey("filter");
        result.QueryParams["filter"].Should().Be("active");
    }

    [Fact]
    public void Resolve_Should_ReplacePlaceholdersInBody()
    {
        // Arrange
        var testCase = CreateTestCase(body: "{\"name\": \"{{userName}}\"}");
        var variables = new Dictionary<string, string> { ["userName"] = "John" };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Body.Should().Be("{\"name\": \"John\"}");
    }

    [Fact]
    public void Resolve_Should_RewriteSyntheticEmailInHappyPathBody_UsingTestEmail()
    {
        // Arrange
        var testCase = CreateTestCase(
            body: "{\"email\":\"test@example.com\",\"password\":\"Test123!\"}",
            httpMethod: "POST",
            testType: "HappyPath");
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment();
        env.Variables["testEmail"] = "testrun_20260415_ab12cd34@example.com";

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Body.Should().Contain("testrun_20260415_ab12cd34@example.com");
        result.Body.Should().NotContain("test@example.com");
    }

    [Fact]
    public void Resolve_Should_RewriteNestedSyntheticEmailInHappyPathBody()
    {
        // Arrange
        var testCase = CreateTestCase(
            body: "{\"user\":{\"email\":\"demo@example.org\"}}",
            httpMethod: "POST",
            testType: "HappyPath");
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment();
        env.Variables["testEmail"] = "testrun_20260415_nested@example.com";

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Body.Should().Contain("testrun_20260415_nested@example.com");
        result.Body.Should().NotContain("demo@example.org");
    }

    [Fact]
    public void Resolve_Should_RewriteSyntheticResourceNamesInHappyPathPostBody_UsingRunSuffix()
    {
        // Arrange
        var testCase = CreateTestCase(
            body: "{\"name\":\"Electronics\",\"slug\":\"electronics\"}",
            httpMethod: "POST",
            testType: "HappyPath");
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment();
        env.Variables["runSuffix"] = "ab12cd34";

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Body.Should().Contain("Electronics-ab12cd34");
        result.Body.Should().Contain("electronics-ab12cd34");
    }

    [Fact]
    public void Resolve_Should_NotRewriteSyntheticResourceNames_ForNonPostMethod()
    {
        // Arrange
        var testCase = CreateTestCase(
            body: "{\"name\":\"Electronics\",\"slug\":\"electronics\"}",
            httpMethod: "PUT",
            testType: "HappyPath");
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment();
        env.Variables["runSuffix"] = "ab12cd34";

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Body.Should().Contain("\"name\":\"Electronics\"");
        result.Body.Should().Contain("\"slug\":\"electronics\"");
        result.Body.Should().NotContain("ab12cd34");
    }

    [Fact]
    public void Resolve_Should_NotRewriteSyntheticEmail_ForNegativeTest()
    {
        // Arrange
        var testCase = CreateTestCase(
            body: "{\"email\":\"test@example.com\"}",
            httpMethod: "POST",
            testType: "Negative");
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment();
        env.Variables["testEmail"] = "testrun_20260415_ab12cd34@example.com";

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Body.Should().Contain("test@example.com");
        result.Body.Should().NotContain("testrun_20260415_ab12cd34@example.com");
    }

    [Fact]
    public void Resolve_Should_ReplacePlaceholdersInPathParams()
    {
        // Arrange
        var pathParams = JsonSerializer.Serialize(new Dictionary<string, string> { ["id"] = "{{userId}}" });
        var testCase = CreateTestCase(url: "/api/users/{id}/profile", pathParams: pathParams);
        var variables = new Dictionary<string, string> { ["userId"] = "99" };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.ResolvedUrl.Should().Contain("/api/users/99/profile");
    }

    [Fact]
    public void Resolve_Should_ReplaceLiteralIdPathParam_FromResourceVariable()
    {
        // Arrange
        var pathParams = JsonSerializer.Serialize(new Dictionary<string, string> { ["id"] = "1" });
        var testCase = CreateTestCase(url: "/api/categories/{id}", pathParams: pathParams, httpMethod: "PUT", testType: "HappyPath");
        var variables = new Dictionary<string, string> { ["categoryId"] = "cat-777" };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.ResolvedUrl.Should().Contain("/api/categories/cat-777");
    }

    [Fact]
    public void Resolve_Should_PreferResourceSpecificId_OverGenericId_ForRouteToken()
    {
        // Arrange
        var pathParams = JsonSerializer.Serialize(new Dictionary<string, string> { ["id"] = "1" });
        var testCase = CreateTestCase(url: "/api/categories/{id}", pathParams: pathParams, httpMethod: "PUT", testType: "HappyPath");
        var variables = new Dictionary<string, string>
        {
            ["id"] = "product-123",
            ["categoryId"] = "category-456",
        };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.ResolvedUrl.Should().Contain("/api/categories/category-456");
        result.ResolvedUrl.Should().NotContain("product-123");
    }

    [Fact]
    public void Resolve_Should_ReplaceLiteralIdentifierInBody_FromVariableBag()
    {
        // Arrange
        var testCase = CreateTestCase(
            url: "/api/products",
            body: "{\"name\":\"Phone\",\"categoryId\":\"1\"}",
            httpMethod: "POST",
            testType: "HappyPath");
        var variables = new Dictionary<string, string> { ["categoryId"] = "cat-777" };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Body.Should().Contain("\"categoryId\":\"cat-777\"");
        result.Body.Should().NotContain("\"categoryId\":\"1\"");
    }

    [Fact]
    public void Resolve_Should_AddAuthorizationHeader_FromExtractedToken_WhenMissing()
    {
        // Arrange
        var testCase = CreateTestCase(url: "/api/products", httpMethod: "GET", testType: "HappyPath");
        var variables = new Dictionary<string, string> { ["authToken"] = "jwt-abc-123" };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Headers.Should().ContainKey("Authorization");
        result.Headers["Authorization"].Should().Be("Bearer jwt-abc-123");
    }

    [Fact]
    public void Resolve_Should_UseRegisteredCredentials_ForHappyPathLoginBody()
    {
        // Arrange
        var testCase = CreateTestCase(
            url: "/api/auth/login",
            body: "{\"email\":\"test@example.com\",\"password\":\"Test123!\"}",
            httpMethod: "POST",
            testType: "HappyPath");
        var variables = new Dictionary<string, string>
        {
            ["registeredEmail"] = "legacy@example.com",
            ["registeredPassword"] = "P@ssw0rd",
        };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Body.Should().Contain("legacy@example.com");
        result.Body.Should().Contain("P@ssw0rd");
        result.Body.Should().NotContain("test@example.com");
        result.Body.Should().NotContain("Test123!");
    }

    [Fact]
    public void Resolve_Should_UseExtractedVarsOverEnvVars()
    {
        // Arrange
        var testCase = CreateTestCase(body: "{{token}}");
        var extractedVars = new Dictionary<string, string> { ["token"] = "extracted-value" };
        var env = CreateEnvironment();
        env.Variables["token"] = "env-value";

        // Act
        var result = _resolver.Resolve(testCase, extractedVars, env);

        // Assert — extracted vars win over env vars
        result.Body.Should().Be("extracted-value");
    }

    [Fact]
    public void Resolve_Should_ThrowUnresolvedException_WhenPlaceholderNotFound()
    {
        // Arrange
        var testCase = CreateTestCase(url: "/api/items/{{missingVar}}");
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment();

        // Act
        var act = () => _resolver.Resolve(testCase, variables, env);

        // Assert
        act.Should().Throw<UnresolvedVariableException>();
    }

    [Fact]
    public void Resolve_Should_ReplaceMultiplePlaceholders_InSameBody()
    {
        // Arrange — realistic scenario: body references token + userId from prior login
        var testCase = CreateTestCase(
            body: "{\"createdBy\": \"{{userId}}\", \"token\": \"{{accessToken}}\", \"note\": \"Created by {{userId}}\"}");
        var variables = new Dictionary<string, string>
        {
            ["userId"] = "user-42",
            ["accessToken"] = "jwt-abc-123",
        };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.Body.Should().Be("{\"createdBy\": \"user-42\", \"token\": \"jwt-abc-123\", \"note\": \"Created by user-42\"}");
    }

    [Fact]
    public void Resolve_Should_SubstituteVarsInUrlAndHeadersAndBody_Simultaneously()
    {
        // Arrange — full auth→CRUD test: token in header, userId in path, resourceId in body
        var headers = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer {{accessToken}}",
            ["X-User-Id"] = "{{userId}}",
        });
        var pathParams = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["resourceId"] = "{{resourceId}}",
        });
        var testCase = CreateTestCase(
            url: "/api/resources/{resourceId}",
            headers: headers,
            pathParams: pathParams,
            body: "{\"updatedBy\": \"{{userId}}\"}");
        var variables = new Dictionary<string, string>
        {
            ["accessToken"] = "jwt-token",
            ["userId"] = "user-1",
            ["resourceId"] = "res-99",
        };
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.ResolvedUrl.Should().Contain("/api/resources/res-99");
        result.Headers["Authorization"].Should().Be("Bearer jwt-token");
        result.Headers["X-User-Id"].Should().Be("user-1");
        result.Body.Should().Be("{\"updatedBy\": \"user-1\"}");
    }

    [Fact]
    public void Resolve_Should_ThrowUnresolvedException_WhenPathRouteTokenRemains()
    {
        // Arrange
        var testCase = CreateTestCase(url: "/api/items/{id}");
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment();

        // Act
        var act = () => _resolver.Resolve(testCase, variables, env);

        // Assert
        act.Should().Throw<UnresolvedVariableException>()
            .WithMessage("*Path parameter 'id'*");
    }

    [Fact]
    public void Resolve_Should_ClampTimeout_BelowMinimum()
    {
        // Arrange
        var testCase = CreateTestCase(timeout: 100);
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.TimeoutMs.Should().Be(1000);
    }

    [Fact]
    public void Resolve_Should_ClampTimeout_AboveMaximum()
    {
        // Arrange
        var testCase = CreateTestCase(timeout: 120000);
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment();

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.TimeoutMs.Should().Be(60000);
    }

    [Fact]
    public void Resolve_Should_BuildAbsoluteUrl_FromRelativePath()
    {
        // Arrange
        var testCase = CreateTestCase(url: "/api/users");
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment(baseUrl: "https://api.example.com");

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.ResolvedUrl.Should().Be("https://api.example.com/api/users");
    }

    [Fact]
    public void Resolve_Should_KeepAbsoluteUrl_AsIs()
    {
        // Arrange
        var testCase = CreateTestCase(url: "https://other-api.com/v2/items");
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment(baseUrl: "https://api.example.com");

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert
        result.ResolvedUrl.Should().Be("https://other-api.com/v2/items");
    }

    [Fact]
    public void Resolve_RequestHeaders_Should_OverrideEnvHeaders()
    {
        // Arrange — env has Authorization from auth resolution, request overrides it
        var requestHeaders = JsonSerializer.Serialize(new Dictionary<string, string> { ["Authorization"] = "Bearer request-token" });
        var testCase = CreateTestCase(headers: requestHeaders);
        var variables = new Dictionary<string, string>();
        var env = CreateEnvironment();
        env.DefaultHeaders["Authorization"] = "Bearer env-token";

        // Act
        var result = _resolver.Resolve(testCase, variables, env);

        // Assert — request-level auth wins over environment auth
        result.Headers["Authorization"].Should().Be("Bearer request-token");
    }

    #region Helpers

    private static ExecutionTestCaseDto CreateTestCase(
        string url = "/api/test",
        string? headers = null,
        string? pathParams = null,
        string? queryParams = null,
        string? body = null,
        int timeout = 30000,
        string httpMethod = "GET",
        string? testType = null)
    {
        return new ExecutionTestCaseDto
        {
            TestCaseId = Guid.NewGuid(),
            Name = "Test Case",
            TestType = testType,
            OrderIndex = 0,
            DependencyIds = Array.Empty<Guid>(),
            Request = new ExecutionTestCaseRequestDto
            {
                HttpMethod = httpMethod,
                Url = url,
                Headers = headers,
                PathParams = pathParams,
                QueryParams = queryParams,
                Body = body,
                BodyType = "JSON",
                Timeout = timeout,
            },
            Expectation = null,
            Variables = Array.Empty<ExecutionVariableRuleDto>(),
        };
    }

    private static ResolvedExecutionEnvironment CreateEnvironment(string baseUrl = "https://api.example.com")
    {
        return new ResolvedExecutionEnvironment
        {
            EnvironmentId = Guid.NewGuid(),
            Name = "Test Env",
            BaseUrl = baseUrl,
            Variables = new Dictionary<string, string>(),
            DefaultHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            DefaultQueryParams = new Dictionary<string, string>(),
        };
    }

    #endregion
}
