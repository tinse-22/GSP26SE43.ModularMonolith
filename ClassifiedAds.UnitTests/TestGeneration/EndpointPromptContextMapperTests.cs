using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// FE-05B: EndpointPromptContextMapper unit tests.
/// Verifies mapping from ApiEndpointMetadataDto + TestSuite business context to EndpointPromptContext.
/// </summary>
public class EndpointPromptContextMapperTests
{
    [Fact]
    public void Map_Should_ReturnEmpty_WhenEndpointsNull()
    {
        var result = EndpointPromptContextMapper.Map(null, null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Map_Should_ReturnEmpty_WhenEndpointsEmpty()
    {
        var result = EndpointPromptContextMapper.Map(
            Array.Empty<ApiEndpointMetadataDto>(),
            new TestSuite());
        result.Should().BeEmpty();
    }

    [Fact]
    public void Map_Should_MapBasicFields()
    {
        var endpointId = Guid.NewGuid();
        var endpoints = new List<ApiEndpointMetadataDto>
        {
            new()
            {
                EndpointId = endpointId,
                HttpMethod = "GET",
                Path = "/api/users",
                OperationId = "getUsers",
            },
        };

        var suite = new TestSuite { EndpointBusinessContexts = new Dictionary<Guid, string>() };

        var result = EndpointPromptContextMapper.Map(endpoints, suite);

        result.Should().HaveCount(1);
        result[0].HttpMethod.Should().Be("GET");
        result[0].Path.Should().Be("/api/users");
        result[0].OperationId.Should().Be("getUsers");
    }

    [Fact]
    public void Map_Should_IncludeGlobalBusinessRules()
    {
        var endpointId = Guid.NewGuid();
        var endpoints = new List<ApiEndpointMetadataDto>
        {
            new() { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/users" },
        };

        var suite = new TestSuite
        {
            GlobalBusinessRules = "All users must have unique email",
            EndpointBusinessContexts = new Dictionary<Guid, string>(),
        };

        var result = EndpointPromptContextMapper.Map(endpoints, suite);

        result[0].BusinessContext.Should().Contain("All users must have unique email");
        result[0].BusinessContext.Should().Contain("Global Rules");
    }

    [Fact]
    public void Map_Should_IncludeEndpointSpecificBusinessContext()
    {
        var endpointId = Guid.NewGuid();
        var endpoints = new List<ApiEndpointMetadataDto>
        {
            new() { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/orders" },
        };

        var suite = new TestSuite
        {
            EndpointBusinessContexts = new Dictionary<Guid, string>
            {
                { endpointId, "Orders must have at least one item" },
            },
        };

        var result = EndpointPromptContextMapper.Map(endpoints, suite);

        result[0].BusinessContext.Should().Contain("Orders must have at least one item");
        result[0].BusinessContext.Should().Contain("Endpoint-specific");
    }

    [Fact]
    public void Map_Should_CombineGlobalAndEndpointRules()
    {
        var endpointId = Guid.NewGuid();
        var endpoints = new List<ApiEndpointMetadataDto>
        {
            new() { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/orders" },
        };

        var suite = new TestSuite
        {
            GlobalBusinessRules = "Global rule",
            EndpointBusinessContexts = new Dictionary<Guid, string>
            {
                { endpointId, "Endpoint rule" },
            },
        };

        var result = EndpointPromptContextMapper.Map(endpoints, suite);

        result[0].BusinessContext.Should().Contain("Global rule");
        result[0].BusinessContext.Should().Contain("Endpoint rule");
    }

    [Fact]
    public void Map_Should_MapSchemaPayloads()
    {
        var endpoints = new List<ApiEndpointMetadataDto>
        {
            new()
            {
                EndpointId = Guid.NewGuid(),
                HttpMethod = "POST",
                Path = "/api/items",
                ParameterSchemaPayloads = new List<string> { "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}}}" },
                ResponseSchemaPayloads = new List<string> { "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"}}}" },
            },
        };

        var suite = new TestSuite { EndpointBusinessContexts = new Dictionary<Guid, string>() };

        var result = EndpointPromptContextMapper.Map(endpoints, suite);

        result[0].RequestBodySchema.Should().Contain("name");
        result[0].ResponseBodySchema.Should().Contain("id");
    }

    [Fact]
    public void Map_Should_HandleNullBusinessContextGracefully()
    {
        var endpoints = new List<ApiEndpointMetadataDto>
        {
            new() { EndpointId = Guid.NewGuid(), HttpMethod = "GET", Path = "/api/test" },
        };

        var suite = new TestSuite { EndpointBusinessContexts = null };

        var result = EndpointPromptContextMapper.Map(endpoints, suite);

        result.Should().HaveCount(1);
        result[0].BusinessContext.Should().BeNull();
    }
}
