using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
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
        root.GetProperty("email").GetString().Should().Be("contract@example.com");
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
        root.GetProperty("email").GetString().Should().Contain("@");
        root.GetProperty("password").GetString().Should().Be("Test123!");
        root.GetProperty("price").GetDecimal().Should().BeGreaterThan(0);
        root.GetProperty("quantity").GetInt32().Should().BeGreaterThan(0);
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
