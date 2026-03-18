using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// FE-05B: TestCaseExpectationBuilder unit tests.
/// Verifies expected status serialization, JSON path checks, header checks, response schema mapping.
/// </summary>
public class TestCaseExpectationBuilderTests
{
    private readonly TestCaseExpectationBuilder _builder = new();

    [Fact]
    public void Build_Should_MapAllFields_FromN8nExpectation()
    {
        var testCaseId = Guid.NewGuid();
        var source = new N8nTestCaseExpectation
        {
            ExpectedStatus = new List<int> { 200, 201 },
            ResponseSchema = "{\"type\":\"object\"}",
            HeaderChecks = new Dictionary<string, string> { { "Content-Type", "application/json" } },
            BodyContains = new List<string> { "\"id\"", "\"name\"" },
            BodyNotContains = new List<string> { "error" },
            JsonPathChecks = new Dictionary<string, string> { { "$.id", "not_null" } },
            MaxResponseTime = 5000,
        };

        var result = _builder.Build(testCaseId, source);

        result.TestCaseId.Should().Be(testCaseId);
        result.ExpectedStatus.Should().Contain("200");
        result.ExpectedStatus.Should().Contain("201");
        result.ResponseSchema.Should().Be("{\"type\":\"object\"}");
        result.HeaderChecks.Should().Contain("Content-Type");
        result.BodyContains.Should().Contain("id");
        result.BodyNotContains.Should().Contain("error");
        result.JsonPathChecks.Should().Contain("$.id");
        result.MaxResponseTime.Should().Be(5000);
    }

    [Fact]
    public void Build_Should_BuildMinimalExpectation_WhenSourceIsNull()
    {
        var testCaseId = Guid.NewGuid();

        var result = _builder.Build(testCaseId, null);

        result.TestCaseId.Should().Be(testCaseId);
        result.Id.Should().NotBe(Guid.Empty);

        // Should default to [200]
        var statusArray = JsonSerializer.Deserialize<int[]>(result.ExpectedStatus);
        statusArray.Should().Contain(200);
    }

    [Fact]
    public void Build_Should_DefaultToStatus200_WhenEmptyStatusList()
    {
        var source = new N8nTestCaseExpectation
        {
            ExpectedStatus = new List<int>(),
        };

        var result = _builder.Build(Guid.NewGuid(), source);

        var statusArray = JsonSerializer.Deserialize<int[]>(result.ExpectedStatus);
        statusArray.Should().Contain(200);
    }

    [Fact]
    public void Build_Should_GenerateNewId()
    {
        var result = _builder.Build(Guid.NewGuid(), null);
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Build_Should_SetNullForEmptyCollections()
    {
        var source = new N8nTestCaseExpectation
        {
            ExpectedStatus = new List<int> { 200 },
            HeaderChecks = new Dictionary<string, string>(),
            BodyContains = new List<string>(),
            BodyNotContains = new List<string>(),
            JsonPathChecks = new Dictionary<string, string>(),
        };

        var result = _builder.Build(Guid.NewGuid(), source);

        result.HeaderChecks.Should().BeNull();
        result.BodyContains.Should().BeNull();
        result.BodyNotContains.Should().BeNull();
        result.JsonPathChecks.Should().BeNull();
    }
}
