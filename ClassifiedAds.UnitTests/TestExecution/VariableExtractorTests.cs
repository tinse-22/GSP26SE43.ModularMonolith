using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.UnitTests.TestExecution;

public class VariableExtractorTests
{
    private readonly VariableExtractor _extractor;

    public VariableExtractorTests()
    {
        _extractor = new VariableExtractor(new Mock<ILogger<VariableExtractor>>().Object);
    }

    [Fact]
    public void Extract_ResponseBody_Should_ExtractByJsonPath()
    {
        // Arrange
        var response = new HttpTestResponse
        {
            StatusCode = 200,
            Body = "{\"data\": {\"id\": \"abc-123\", \"name\": \"Test\"}}",
            Headers = new Dictionary<string, string>(),
        };

        var variables = new List<ExecutionVariableRuleDto>
        {
            new()
            {
                VariableName = "entityId",
                ExtractFrom = "ResponseBody",
                JsonPath = "$.data.id",
            },
        };

        // Act
        var result = _extractor.Extract(response, variables);

        // Assert
        result.Should().ContainKey("entityId");
        result["entityId"].Should().Be("abc-123");
    }

    [Fact]
    public void Extract_ResponseHeader_Should_ExtractByHeaderName()
    {
        // Arrange
        var response = new HttpTestResponse
        {
            StatusCode = 200,
            Body = "{}",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Request-Id"] = "req-456",
                ["Content-Type"] = "application/json",
            },
        };

        var variables = new List<ExecutionVariableRuleDto>
        {
            new()
            {
                VariableName = "requestId",
                ExtractFrom = "ResponseHeader",
                HeaderName = "X-Request-Id",
            },
        };

        // Act
        var result = _extractor.Extract(response, variables);

        // Assert
        result.Should().ContainKey("requestId");
        result["requestId"].Should().Be("req-456");
    }

    [Fact]
    public void Extract_Status_Should_ExtractStatusCode()
    {
        // Arrange
        var response = new HttpTestResponse
        {
            StatusCode = 201,
            Body = "{}",
            Headers = new Dictionary<string, string>(),
        };

        var variables = new List<ExecutionVariableRuleDto>
        {
            new()
            {
                VariableName = "statusCode",
                ExtractFrom = "Status",
            },
        };

        // Act
        var result = _extractor.Extract(response, variables);

        // Assert
        result.Should().ContainKey("statusCode");
        result["statusCode"].Should().Be("201");
    }

    [Fact]
    public void Extract_Should_UseDefaultValue_WhenExtractionFails()
    {
        // Arrange
        var response = new HttpTestResponse
        {
            StatusCode = 200,
            Body = "{\"other\": \"value\"}",
            Headers = new Dictionary<string, string>(),
        };

        var variables = new List<ExecutionVariableRuleDto>
        {
            new()
            {
                VariableName = "missing",
                ExtractFrom = "ResponseBody",
                JsonPath = "$.nonexistent.path",
                DefaultValue = "fallback-value",
            },
        };

        // Act
        var result = _extractor.Extract(response, variables);

        // Assert
        result.Should().ContainKey("missing");
        result["missing"].Should().Be("fallback-value");
    }

    [Fact]
    public void Extract_Should_NotAddVariable_WhenExtractionFailsAndNoDefault()
    {
        // Arrange
        var response = new HttpTestResponse
        {
            StatusCode = 200,
            Body = "{\"other\": \"value\"}",
            Headers = new Dictionary<string, string>(),
        };

        var variables = new List<ExecutionVariableRuleDto>
        {
            new()
            {
                VariableName = "missing",
                ExtractFrom = "ResponseBody",
                JsonPath = "$.nonexistent.path",
                DefaultValue = null,
            },
        };

        // Act
        var result = _extractor.Extract(response, variables);

        // Assert
        result.Should().NotContainKey("missing");
    }

    [Fact]
    public void Extract_JsonPath_ArrayIndex_Should_Work()
    {
        // Arrange
        var response = new HttpTestResponse
        {
            StatusCode = 200,
            Body = "{\"items\": [{\"name\": \"first\"}, {\"name\": \"second\"}]}",
            Headers = new Dictionary<string, string>(),
        };

        var variables = new List<ExecutionVariableRuleDto>
        {
            new()
            {
                VariableName = "firstName",
                ExtractFrom = "ResponseBody",
                JsonPath = "$.items[0].name",
            },
        };

        // Act
        var result = _extractor.Extract(response, variables);

        // Assert
        result.Should().ContainKey("firstName");
        result["firstName"].Should().Be("first");
    }
}
