using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using System.Collections.Generic;
using System.Text.Json;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class ContractAwareRequestSynthesizerTests
{
    [Fact]
    public void BuildRequestData_Should_ResolveExamplesDefaultsEnumsAcrossAllOf()
    {
        var context = new ContractAwareRequestContext
        {
            HttpMethod = "POST",
            Path = "/api/users",
            RequiresBody = true,
            RequestBodySchema = """
            {
              "allOf": [
                {
                  "type": "object",
                  "required": ["email", "password"],
                  "properties": {
                    "email": {
                      "type": "string",
                      "format": "email",
                      "example": "contract@example.com",
                      "default": "ignored@example.com"
                    },
                    "password": {
                      "type": "string",
                      "example": "StrongPass1!"
                    }
                  }
                },
                {
                  "type": "object",
                  "required": ["role", "status"],
                  "properties": {
                    "role": {
                      "type": "string",
                      "enum": ["admin", "user"]
                    },
                    "status": {
                      "type": "string",
                      "default": "draft"
                    }
                  }
                }
              ]
            }
            """,
        };

        var result = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.HappyPath);

        result.BodyType.Should().Be("JSON");
        using var document = JsonDocument.Parse(result.Body);
        var root = document.RootElement;
        root.GetProperty("email").GetString().Should().Be("user_{{tcUniqueId}}@yourdomain.com");
        root.GetProperty("password").GetString().Should().Be("StrongPass1!");
        root.GetProperty("role").GetString().Should().Be("admin");
        root.GetProperty("status").GetString().Should().Be("draft");
    }

    [Fact]
    public void BuildRequestData_Should_ChooseOneOfVariant_AndApplyDependencyPlaceholders()
    {
        var context = new ContractAwareRequestContext
        {
            HttpMethod = "POST",
            Path = "/api/products",
            RequiresBody = true,
            PlaceholderByFieldName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["categoryId"] = "categoryId",
            },
            RequestBodySchema = """
            {
              "oneOf": [
                {
                  "type": "object",
                  "required": ["categoryId", "name"],
                  "properties": {
                    "categoryId": { "type": "string", "format": "uuid" },
                    "name": { "type": "string" }
                  }
                },
                {
                  "type": "object",
                  "required": ["sku"],
                  "properties": {
                    "sku": { "type": "string" }
                  }
                }
              ]
            }
            """,
        };

        var result = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.HappyPath);

        using var document = JsonDocument.Parse(result.Body);
        var root = document.RootElement;
        root.GetProperty("categoryId").GetString().Should().Be("{{categoryId}}");
        root.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
        root.TryGetProperty("sku", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildRequestData_Should_NotReuseDependencyPlaceholders_ForBoundaryOrNegative()
    {
        var context = new ContractAwareRequestContext
        {
            HttpMethod = "POST",
            Path = "/api/products/{id}",
            RequiresBody = true,
            RequiredPathParams = new[] { "id" },
            RequiredQueryParams = new[] { "categoryId" },
            PlaceholderByFieldName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = "productId",
                ["categoryId"] = "categoryId",
            },
            Parameters = new List<ParameterDetailDto>
            {
                new() { Name = "categoryId", Location = "Query", DataType = "string", Format = "uuid", IsRequired = true },
            },
            RequestBodySchema = """
            {
              "type": "object",
              "required": ["categoryId", "name"],
              "properties": {
                "categoryId": { "type": "string", "format": "uuid" },
                "name": { "type": "string", "minLength": 1 }
              }
            }
            """,
        };

        var boundary = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.Boundary);
        var negative = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.Negative);

        boundary.PathParams["id"].Should().NotBe("{{productId}}");
        boundary.QueryParams["categoryId"].Should().NotBe("{{categoryId}}");
        negative.PathParams["id"].Should().NotBe("{{productId}}");
        negative.QueryParams["categoryId"].Should().NotBe("{{categoryId}}");

        using var boundaryDocument = JsonDocument.Parse(boundary.Body);
        boundaryDocument.RootElement.GetProperty("categoryId").GetString().Should().NotBe("{{categoryId}}");

        using var negativeDocument = JsonDocument.Parse(negative.Body);
        negativeDocument.RootElement.TryGetProperty("categoryId", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildRequestData_Should_UseSrsAuthModeSentinel_ForMissingAuthNegative()
    {
        var context = new ContractAwareRequestContext
        {
            HttpMethod = "DELETE",
            Path = "/api/products/{id}",
            RequiresAuth = true,
            RequiresAuthFromSrs = true,
            RequiredPathParams = new[] { "id" },
        };

        var negative = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.Negative);
        var boundary = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.Boundary);

        negative.Headers.Should().ContainKey("X-Test-Auth-Mode")
            .WhoseValue.Should().Be("none");
        negative.Headers.Should().NotContainKey("Authorization");
        boundary.Headers.Should().ContainKey("Authorization")
            .WhoseValue.Should().Be("Bearer {{authToken}}");
    }

    [Fact]
    public void RepairScenario_Should_NotOverwriteFailureValue_WithDependencyPlaceholder()
    {
        var scenario = new LlmSuggestedScenario
        {
            SuggestedTestType = TestType.Negative,
            SuggestedBodyType = "JSON",
            SuggestedBody = """{"categoryId":"nonexistent_{{tcUniqueId}}","name":"Auto Name"}""",
        };

        var context = new ContractAwareRequestContext
        {
            HttpMethod = "POST",
            Path = "/api/products",
            RequiresBody = true,
            RequestContentType = "application/json",
            PlaceholderByFieldName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["categoryId"] = "categoryId",
            },
            RequestBodySchema = """
            {
              "type": "object",
              "required": ["categoryId", "name"],
              "properties": {
                "categoryId": { "type": "string", "format": "uuid" },
                "name": { "type": "string" }
              }
            }
            """,
        };

        ContractAwareRequestSynthesizer.RepairScenario(scenario, context);

        using var document = JsonDocument.Parse(scenario.SuggestedBody);
        document.RootElement.GetProperty("categoryId").GetString().Should().Be("nonexistent_{{tcUniqueId}}");
    }

    [Fact]
    public void BuildRequestData_Should_FallBackToHeuristics_WhenSchemaHasNoExamples()
    {
        var context = new ContractAwareRequestContext
        {
            HttpMethod = "POST",
            Path = "/api/products",
            RequiresBody = true,
            RequestBodySchema = """
            {
              "type": "object",
              "required": ["email", "password", "price", "quantity"],
              "properties": {
                "email": { "type": "string", "format": "email" },
                "password": { "type": "string", "minLength": 8 },
                "price": { "type": "number", "minimum": 1 },
                "quantity": { "type": "integer", "minimum": 1 }
              }
            }
            """,
        };

        var result = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.HappyPath);

        using var document = JsonDocument.Parse(result.Body);
        var root = document.RootElement;
        root.GetProperty("email").GetString().Should().Be("user_{{tcUniqueId}}@yourdomain.com");
        root.GetProperty("password").GetString().Should().Be("Password123");
        root.GetProperty("price").GetDecimal().Should().BeGreaterThan(0);
        root.GetProperty("quantity").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public void BuildRequestData_Should_SerializeBoundaryPayloads_WithoutThrowing()
    {
        var context = new ContractAwareRequestContext
        {
            HttpMethod = "POST",
            Path = "/api/products",
            RequiresBody = true,
            RequestBodySchema = """
            {
              "type": "object",
              "required": ["name", "quantity"],
              "properties": {
                "name": { "type": "string", "minLength": 1 },
                "quantity": { "type": "integer", "minimum": 1 }
              }
            }
            """,
        };

        ContractAwareRequestData? result = null;
        var action = () => result = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.Boundary);

        action.Should().NotThrow();
        result.Should().NotBeNull();
        result.BodyType.Should().Be("JSON");

        using var document = JsonDocument.Parse(result!.Body);
        var root = document.RootElement;
        root.GetProperty("name").GetString().Should().BeEmpty();
    }

    [Fact]
    public void BuildRequestData_Should_UseUrlEncodedBodyType_ForSwagger2FormFields()
    {
        var context = new ContractAwareRequestContext
        {
            HttpMethod = "POST",
            Path = "/pet/{petId}",
            RequiresBody = true,
            Parameters = new List<ParameterDetailDto>
            {
                new() { Name = "name", Location = "Body", DataType = "string" },
                new() { Name = "status", Location = "Body", DataType = "string" },
            },
        };

        var result = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.HappyPath);

        result.BodyType.Should().Be("UrlEncoded");
        using var document = JsonDocument.Parse(result.Body);
        var root = document.RootElement;
        root.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("application/json", "JSON")]
    [InlineData("multipart/form-data", "FormData")]
    [InlineData("application/x-www-form-urlencoded", "UrlEncoded")]
    [InlineData("text/plain", "Raw")]
    public void BuildRequestData_Should_UseOpenApiContentType_AsBodyTypeFrame(string contentType, string expectedBodyType)
    {
        var context = new ContractAwareRequestContext
        {
            HttpMethod = "POST",
            Path = "/api/products",
            RequiresBody = true,
            RequestContentType = contentType,
            RequestBodySchema = """
            {
              "type": "object",
              "required": ["name"],
              "properties": {
                "name": { "type": "string" }
              }
            }
            """,
        };

        var result = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.HappyPath);

        result.BodyType.Should().Be(expectedBodyType);
    }

    [Fact]
    public void RepairScenario_Should_PruneFieldsNotDeclaredByOpenApiSchema()
    {
        var scenario = new LlmSuggestedScenario
        {
            SuggestedTestType = TestType.HappyPath,
            SuggestedBody = """{"email":"test@example.com","password":"Password123","invented":true}""",
        };

        var context = new ContractAwareRequestContext
        {
            HttpMethod = "POST",
            Path = "/api/auth/login",
            RequiresBody = true,
            RequestContentType = "application/json",
            RequestBodySchema = """
            {
              "type": "object",
              "required": ["email", "password"],
              "properties": {
                "email": { "type": "string", "format": "email" },
                "password": { "type": "string", "minLength": 6 }
              }
            }
            """,
        };

        ContractAwareRequestSynthesizer.RepairScenario(scenario, context);

        using var document = JsonDocument.Parse(scenario.SuggestedBody);
        var root = document.RootElement;
        root.TryGetProperty("invented", out _).Should().BeFalse();
        root.TryGetProperty("email", out _).Should().BeTrue();
        root.TryGetProperty("password", out _).Should().BeTrue();
        scenario.SuggestedBodyType.Should().Be("JSON");
    }

    [Fact]
    public void BuildRequestData_Should_UseFormDataBodyType_WhenFileParameterExists()
    {
        var context = new ContractAwareRequestContext
        {
            HttpMethod = "POST",
            Path = "/pet/{petId}/uploadImage",
            RequiresBody = true,
            Parameters = new List<ParameterDetailDto>
            {
                new() { Name = "additionalMetadata", Location = "Body", DataType = "string" },
                new() { Name = "file", Location = "Body", DataType = "file" },
            },
        };

        var result = ContractAwareRequestSynthesizer.BuildRequestData(context, TestType.HappyPath);

        result.BodyType.Should().Be("FormData");
        using var document = JsonDocument.Parse(result.Body);
        var root = document.RootElement;
        root.GetProperty("file").GetString().Should().Be("sample-file.txt");
    }
}
