using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;
using TestGenHttpMethod = ClassifiedAds.Modules.TestGeneration.Entities.HttpMethod;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class GeneratedTestCaseDependencyEnricherTests
{
    [Fact]
    public void Enrich_Should_BindBodyIdentifierPlaceholders_ToDependencyProducer()
    {
        var categoryEndpointId = Guid.NewGuid();
        var productEndpointId = Guid.NewGuid();
        var createCategory = CreateTestCase(
            endpointId: categoryEndpointId,
            httpMethod: TestGenHttpMethod.POST,
            path: "/api/categories",
            testType: TestType.HappyPath,
            orderIndex: 0,
            body: "{\"name\":\"Electronics\"}");
        var createProduct = CreateTestCase(
            endpointId: productEndpointId,
            httpMethod: TestGenHttpMethod.POST,
            path: "/api/products",
            testType: TestType.HappyPath,
            orderIndex: 1,
            body: "{\"name\":\"Phone\",\"categoryId\":\"00000000-0000-0000-0000-000000000000\"}");

        var approvedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = categoryEndpointId, HttpMethod = "POST", Path = "/api/categories", OrderIndex = 0 },
            new() { EndpointId = productEndpointId, HttpMethod = "POST", Path = "/api/products", OrderIndex = 1, DependsOnEndpointIds = new List<Guid> { categoryEndpointId } },
        };

        var result = GeneratedTestCaseDependencyEnricher.Enrich(new[] { createCategory, createProduct }, approvedOrder);

        createProduct.Request.Body.Should().Contain("\"categoryId\":\"{{categoryId}}\"");
        createProduct.Dependencies.Should().ContainSingle(x => x.DependsOnTestCaseId == createCategory.Id);
        createCategory.Variables.Should().Contain(x =>
            x.VariableName == "categoryId" &&
            x.ExtractFrom == ExtractFrom.ResponseBody &&
            x.JsonPath == "$.id");
        createCategory.Variables.Should().Contain(x =>
            x.VariableName == "categoryId" &&
            x.ExtractFrom == ExtractFrom.ResponseHeader &&
            x.HeaderName == "Location" &&
            x.Regex == "([^/?#]+)$");
        result.ExistingProducerVariablesToPersist.Should().BeEmpty();
    }

    [Fact]
    public void Enrich_Should_NotBindNonIdentifierBodyFields_ToProducerIds()
    {
        var categoryEndpointId = Guid.NewGuid();
        var productEndpointId = Guid.NewGuid();
        var createCategory = CreateTestCase(
            endpointId: categoryEndpointId,
            httpMethod: TestGenHttpMethod.POST,
            path: "/api/categories",
            testType: TestType.HappyPath,
            orderIndex: 0,
            body: "{\"name\":\"Electronics\"}");
        var createProduct = CreateTestCase(
            endpointId: productEndpointId,
            httpMethod: TestGenHttpMethod.POST,
            path: "/api/products",
            testType: TestType.HappyPath,
            orderIndex: 1,
            body: "{\"name\":\"Phone\",\"price\":12345,\"stock\":1,\"categoryId\":\"00000000-0000-0000-0000-000000000000\"}");

        var approvedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = categoryEndpointId, HttpMethod = "POST", Path = "/api/categories", OrderIndex = 0 },
            new() { EndpointId = productEndpointId, HttpMethod = "POST", Path = "/api/products", OrderIndex = 1, DependsOnEndpointIds = new List<Guid> { categoryEndpointId } },
        };

        GeneratedTestCaseDependencyEnricher.Enrich(new[] { createCategory, createProduct }, approvedOrder);

        createProduct.Request.Body.Should().Contain("\"categoryId\":\"{{categoryId}}\"");
        createProduct.Request.Body.Should().Contain("\"price\":12345");
        createProduct.Request.Body.Should().Contain("\"stock\":1");
        createProduct.Request.Body.Should().NotContain("{{price}}");
        createProduct.Request.Body.Should().NotContain("{{stock}}");
    }

    [Fact]
    public void Enrich_Should_ChainRegisterLoginAndAuthorizedRequests_Generically()
    {
        var registerEndpointId = Guid.NewGuid();
        var loginEndpointId = Guid.NewGuid();
        var profileEndpointId = Guid.NewGuid();

        var register = CreateTestCase(
            endpointId: registerEndpointId,
            httpMethod: TestGenHttpMethod.POST,
            path: "/api/auth/register",
            testType: TestType.HappyPath,
            orderIndex: 0,
            body: "{\"email\":\"testuser@example.com\",\"password\":\"Test123!\"}");

        var login = CreateTestCase(
            endpointId: loginEndpointId,
            httpMethod: TestGenHttpMethod.POST,
            path: "/api/auth/login",
            testType: TestType.HappyPath,
            orderIndex: 1,
            body: "{\"email\":\"testuser@example.com\",\"password\":\"Test123!\"}");

        var getProfile = CreateTestCase(
            endpointId: profileEndpointId,
            httpMethod: TestGenHttpMethod.GET,
            path: "/api/profile",
            testType: TestType.HappyPath,
            orderIndex: 2,
            body: null);

        var approvedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = registerEndpointId, HttpMethod = "POST", Path = "/api/auth/register", OrderIndex = 0, IsAuthRelated = true },
            new() { EndpointId = loginEndpointId, HttpMethod = "POST", Path = "/api/auth/login", OrderIndex = 1, DependsOnEndpointIds = new List<Guid> { registerEndpointId }, IsAuthRelated = true },
            new() { EndpointId = profileEndpointId, HttpMethod = "GET", Path = "/api/profile", OrderIndex = 2, DependsOnEndpointIds = new List<Guid> { loginEndpointId } },
        };

        GeneratedTestCaseDependencyEnricher.Enrich(new[] { register, login, getProfile }, approvedOrder);

        login.Request.Body.Should().Contain("\"email\":\"{{registeredEmail}}\"");
        login.Request.Body.Should().Contain("\"password\":\"{{registeredPassword}}\"");
        login.Dependencies.Should().ContainSingle(x => x.DependsOnTestCaseId == register.Id);

        getProfile.Request.Headers.Should().NotBeNull();
        getProfile.Request.Headers.Should().Contain("Authorization");
        getProfile.Request.Headers.Should().Contain("{{authToken}}");
        getProfile.Dependencies.Should().ContainSingle(x => x.DependsOnTestCaseId == login.Id);

        register.Dependencies.Should().NotContain(x => x.DependsOnTestCaseId == login.Id);
        register.Request.Headers.Should().BeNull();

        register.Variables.Should().Contain(x =>
            x.VariableName == "registeredEmail" &&
            x.ExtractFrom == ExtractFrom.RequestBody &&
            x.JsonPath == "$.email");
        register.Variables.Should().Contain(x =>
            x.VariableName == "registeredPassword" &&
            x.ExtractFrom == ExtractFrom.RequestBody &&
            x.JsonPath == "$.password");
        login.Variables.Should().Contain(x =>
            x.VariableName == "authToken" &&
            x.ExtractFrom == ExtractFrom.ResponseHeader &&
            x.HeaderName == "Authorization" &&
            x.Regex == "(?:Bearer\\s+)?(?<value>[^\\s]+)$");
    }

    [Fact]
    public void Enrich_Should_ReplaceHappyPathRouteParamLiteral_WithDependencyPlaceholder()
    {
        var categoryEndpointId = Guid.NewGuid();
        var updateCategoryEndpointId = Guid.NewGuid();

        var createCategory = CreateTestCase(
            endpointId: categoryEndpointId,
            httpMethod: TestGenHttpMethod.POST,
            path: "/api/categories",
            testType: TestType.HappyPath,
            orderIndex: 0,
            body: "{\"name\":\"Electronics\"}");

        var updateCategory = CreateTestCase(
            endpointId: updateCategoryEndpointId,
            httpMethod: TestGenHttpMethod.PUT,
            path: "/api/categories/{id}",
            testType: TestType.HappyPath,
            orderIndex: 1,
            body: "{\"name\":\"Updated\"}");
        updateCategory.Request.PathParams = "{\"id\":\"12345\"}";

        var approvedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = categoryEndpointId, HttpMethod = "POST", Path = "/api/categories", OrderIndex = 0 },
            new() { EndpointId = updateCategoryEndpointId, HttpMethod = "PUT", Path = "/api/categories/{id}", OrderIndex = 1, DependsOnEndpointIds = new List<Guid> { categoryEndpointId } },
        };

        GeneratedTestCaseDependencyEnricher.Enrich(new[] { createCategory, updateCategory }, approvedOrder);

        updateCategory.Request.PathParams.Should().Contain("\"id\":\"{{categoryId}}\"");
        updateCategory.Dependencies.Should().ContainSingle(x => x.DependsOnTestCaseId == createCategory.Id);
    }

    [Fact]
    public void Enrich_Should_NormalizeLiteralRouteUrl_AndBindHappyPathDependency()
    {
        var categoryEndpointId = Guid.NewGuid();
        var updateCategoryEndpointId = Guid.NewGuid();

        var createCategory = CreateTestCase(
            endpointId: categoryEndpointId,
            httpMethod: TestGenHttpMethod.POST,
            path: "/api/categories",
            testType: TestType.HappyPath,
            orderIndex: 0,
            body: "{\"name\":\"Electronics\"}");

        var updateCategory = CreateTestCase(
            endpointId: updateCategoryEndpointId,
            httpMethod: TestGenHttpMethod.PUT,
            path: "/api/categories/{id}",
            testType: TestType.HappyPath,
            orderIndex: 1,
            body: "{\"name\":\"Updated\"}");

        updateCategory.Request.Url = "/api/categories/12345";
        updateCategory.Request.PathParams = null;

        var approvedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = categoryEndpointId, HttpMethod = "POST", Path = "/api/categories", OrderIndex = 0 },
            new() { EndpointId = updateCategoryEndpointId, HttpMethod = "PUT", Path = "/api/categories/{id}", OrderIndex = 1, DependsOnEndpointIds = new List<Guid> { categoryEndpointId } },
        };

        GeneratedTestCaseDependencyEnricher.Enrich(new[] { createCategory, updateCategory }, approvedOrder);

        updateCategory.Request.Url.Should().Be("/api/categories/{id}");
        updateCategory.Request.PathParams.Should().Contain("\"id\":\"{{categoryId}}\"");
        updateCategory.Dependencies.Should().ContainSingle(x => x.DependsOnTestCaseId == createCategory.Id);
    }

    [Fact]
    public void Enrich_Should_KeepNonHappyPathRouteParamLiteral_Unchanged()
    {
        var categoryEndpointId = Guid.NewGuid();
        var updateCategoryEndpointId = Guid.NewGuid();

        var createCategory = CreateTestCase(
            endpointId: categoryEndpointId,
            httpMethod: TestGenHttpMethod.POST,
            path: "/api/categories",
            testType: TestType.HappyPath,
            orderIndex: 0,
            body: "{\"name\":\"Electronics\"}");

        var boundaryUpdate = CreateTestCase(
            endpointId: updateCategoryEndpointId,
            httpMethod: TestGenHttpMethod.PUT,
            path: "/api/categories/{id}",
            testType: TestType.Boundary,
            orderIndex: 1,
            body: "{\"name\":\"Updated\"}");
        boundaryUpdate.Request.PathParams = "{\"id\":\"12345\"}";

        var approvedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = categoryEndpointId, HttpMethod = "POST", Path = "/api/categories", OrderIndex = 0 },
            new() { EndpointId = updateCategoryEndpointId, HttpMethod = "PUT", Path = "/api/categories/{id}", OrderIndex = 1, DependsOnEndpointIds = new List<Guid> { categoryEndpointId } },
        };

        GeneratedTestCaseDependencyEnricher.Enrich(new[] { createCategory, boundaryUpdate }, approvedOrder);

        boundaryUpdate.Request.PathParams.Should().Contain("\"id\":\"12345\"");
    }

    [Fact]
    public void Enrich_Should_NormalizeLiteralRouteUrl_ButKeepBoundaryLiteralValue()
    {
        var categoryEndpointId = Guid.NewGuid();
        var updateCategoryEndpointId = Guid.NewGuid();

        var createCategory = CreateTestCase(
            endpointId: categoryEndpointId,
            httpMethod: TestGenHttpMethod.POST,
            path: "/api/categories",
            testType: TestType.HappyPath,
            orderIndex: 0,
            body: "{\"name\":\"Electronics\"}");

        var boundaryUpdate = CreateTestCase(
            endpointId: updateCategoryEndpointId,
            httpMethod: TestGenHttpMethod.PUT,
            path: "/api/categories/{id}",
            testType: TestType.Boundary,
            orderIndex: 1,
            body: "{\"name\":\"Updated\"}");

        boundaryUpdate.Request.Url = "/api/categories/12345";
        boundaryUpdate.Request.PathParams = null;

        var approvedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = categoryEndpointId, HttpMethod = "POST", Path = "/api/categories", OrderIndex = 0 },
            new() { EndpointId = updateCategoryEndpointId, HttpMethod = "PUT", Path = "/api/categories/{id}", OrderIndex = 1, DependsOnEndpointIds = new List<Guid> { categoryEndpointId } },
        };

        GeneratedTestCaseDependencyEnricher.Enrich(new[] { createCategory, boundaryUpdate }, approvedOrder);

        boundaryUpdate.Request.Url.Should().Be("/api/categories/{id}");
        boundaryUpdate.Request.PathParams.Should().Contain("\"id\":\"12345\"");
        boundaryUpdate.Dependencies.Should().ContainSingle(x => x.DependsOnTestCaseId == createCategory.Id);
    }

    private static TestCase CreateTestCase(
        Guid endpointId,
        TestGenHttpMethod httpMethod,
        string path,
        TestType testType,
        int orderIndex,
        string body)
    {
        var testCaseId = Guid.NewGuid();
        return new TestCase
        {
            Id = testCaseId,
            EndpointId = endpointId,
            TestType = testType,
            OrderIndex = orderIndex,
            Name = $"{httpMethod} {path}",
            Request = new TestCaseRequest
            {
                Id = Guid.NewGuid(),
                TestCaseId = testCaseId,
                HttpMethod = httpMethod,
                Url = path,
                Headers = null,
                PathParams = null,
                QueryParams = null,
                BodyType = string.IsNullOrWhiteSpace(body) ? BodyType.None : BodyType.JSON,
                Body = body,
            },
        };
    }
}
