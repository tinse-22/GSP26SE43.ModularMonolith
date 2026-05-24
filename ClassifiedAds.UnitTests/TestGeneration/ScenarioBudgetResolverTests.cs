using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class ScenarioBudgetResolverTests
{
    [Fact]
    public void Resolve_Should_KeepSimpleGetNearLeanDefault()
    {
        var budget = CreateResolver().Resolve(
            new ApiOrderItemModel
            {
                EndpointId = Guid.NewGuid(),
                HttpMethod = "GET",
                Path = "/api/users",
            },
            new ApiEndpointMetadataDto
            {
                HttpMethod = "GET",
                Path = "/api/users",
                Responses = new List<ApiEndpointResponseDescriptorDto>
                {
                    new() { StatusCode = 200 },
                },
            });

        budget.SoftLimit.Should().Be(new ScenarioGenerationBudgetOptions().SimpleEndpointSoftLimit);
        budget.Target.Should().Be(3);
        budget.HardLimit.Should().Be(new ScenarioGenerationBudgetOptions().DefaultHardLimitPerEndpoint);
    }

    [Fact]
    public void Resolve_Should_KeepBodyWritingEndpointNearStandardDefault()
    {
        var budget = CreateResolver().Resolve(
            new ApiOrderItemModel
            {
                EndpointId = Guid.NewGuid(),
                HttpMethod = "POST",
                Path = "/api/auth/login",
            },
            new ApiEndpointMetadataDto
            {
                HttpMethod = "POST",
                Path = "/api/auth/login",
                HasRequiredRequestBody = true,
                ParameterSchemaPayloads = new List<string>
                {
                    "{\"type\":\"object\",\"properties\":{\"email\":{\"type\":\"string\"}}}",
                },
                Responses = new List<ApiEndpointResponseDescriptorDto>
                {
                    new() { StatusCode = 200 },
                    new() { StatusCode = 400 },
                },
            });

        budget.SoftLimit.Should().Be(new ScenarioGenerationBudgetOptions().ComplexEndpointSoftLimit);
        budget.Target.Should().Be(10);
    }

    [Fact]
    public void Resolve_Should_AllowComplexEndpointAboveStandardDefaultUpToHardLimit()
    {
        var budget = CreateResolver().Resolve(
            new ApiOrderItemModel
            {
                EndpointId = Guid.NewGuid(),
                HttpMethod = "PATCH",
                Path = "/api/products/{productId}/variants/{variantId}",
            },
            new ApiEndpointMetadataDto
            {
                HttpMethod = "PATCH",
                Path = "/api/products/{productId}/variants/{variantId}",
                HasRequiredRequestBody = true,
                RequiredPathParameterNames = new[] { "productId", "variantId" },
                RequiredQueryParameterNames = new[] { "locale" },
                ParameterSchemaPayloads = new List<string>
                {
                    """
                    {
                      "type": "object",
                      "required": ["name", "sku", "price", "stock"],
                      "properties": {
                        "name": { "type": "string" },
                        "sku": { "type": "string" },
                        "price": { "type": "number" },
                        "stock": { "type": "integer" }
                      }
                    }
                    """,
                },
                Responses = new List<ApiEndpointResponseDescriptorDto>
                {
                    new() { StatusCode = 200 },
                    new() { StatusCode = 400 },
                    new() { StatusCode = 404 },
                    new() { StatusCode = 409 },
                },
            },
            businessContext: "Products have unique SKUs.",
            coverableRequirementCount: 1);

        budget.Target.Should().BeGreaterThan(new ScenarioGenerationBudgetOptions().ComplexEndpointSoftLimit);
        budget.Target.Should().BeLessThanOrEqualTo(budget.HardLimit);
        budget.HardLimit.Should().Be(15);
        budget.Reason.Should().Contain("mapped requirement");
    }

    [Fact]
    public void Normalize_Should_KeepNumericDefaultsCentralizedInOptions()
    {
        var defaults = new ScenarioGenerationBudgetOptions();

        defaults.SimpleEndpointSoftLimit.Should().Be(3);
        defaults.ComplexEndpointSoftLimit.Should().Be(10);
        defaults.DefaultHardLimitPerEndpoint.Should().Be(15);
        defaults.MaxScenarioBudgetPerBatch.Should().Be(20);

        var normalized = ScenarioBudgetResolver.Normalize(new ScenarioGenerationBudgetOptions
        {
            SimpleEndpointSoftLimit = 0,
            ComplexEndpointSoftLimit = -1,
            DefaultHardLimitPerEndpoint = 0,
            MaxScenarioBudgetPerBatch = -1,
        });

        normalized.SimpleEndpointSoftLimit.Should().Be(defaults.SimpleEndpointSoftLimit);
        normalized.ComplexEndpointSoftLimit.Should().Be(defaults.ComplexEndpointSoftLimit);
        normalized.DefaultHardLimitPerEndpoint.Should().Be(defaults.DefaultHardLimitPerEndpoint);
        normalized.MaxScenarioBudgetPerBatch.Should().Be(defaults.MaxScenarioBudgetPerBatch);
    }

    private static ScenarioBudgetResolver CreateResolver()
    {
        return new ScenarioBudgetResolver(new ScenarioGenerationBudgetOptions());
    }
}
