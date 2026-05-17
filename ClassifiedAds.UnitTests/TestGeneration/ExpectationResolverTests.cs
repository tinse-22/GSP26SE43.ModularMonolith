using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class ExpectationResolverTests
{
    [Fact]
    public void Resolve_Should_UseSrsExplicitStatus_WhenLlmStatusContradictsCoveredRequirement()
    {
        var requirementId = Guid.NewGuid();
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            TestType = TestType.Negative,
            CoveredRequirementIds = new List<Guid> { requirementId },
            SrsRequirements = new List<SrsRequirement>
            {
                new()
                {
                    Id = requirementId,
                    RequirementCode = "REQ-400",
                    IsReviewed = true,
                    TestableConstraints = """[{ "constraint": "invalid email -> 400", "expectedOutcome": "400 Bad Request", "priority": "High" }]""",
                },
            },
            LlmExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = new List<int> { 200 },
                BodyContains = new List<string> { "email" },
                JsonPathChecks = new Dictionary<string, string> { ["$.success"] = "false" },
            },
        });

        result.Source.Should().Be(ExpectationSource.Srs);
        result.ExpectedStatusCodes.Should().Equal(400);
        result.PrimaryRequirementId.Should().Be(requirementId);
        result.RequirementCode.Should().Be("REQ-400");
        result.BodyContains.Should().Contain("email");
    }

    [Fact]
    public void Resolve_Should_NotUseUncoveredSrsCreatedStatus_ForGetScenario()
    {
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "GET",
            TestType = TestType.HappyPath,
            SrsRequirements = new List<SrsRequirement>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RequirementCode = "REQ-003",
                    IsReviewed = true,
                    TestableConstraints = """[{ "constraint": "user is created -> 201", "expectedOutcome": "201 Created", "priority": "High" }]""",
                },
            },
            LlmExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = new List<int> { 200 },
            },
        });

        result.Source.Should().Be(ExpectationSource.Llm);
        result.ExpectedStatusCodes.Should().Equal(200);
    }

    [Fact]
    public void Resolve_Should_NotUseUncoveredSrsCreatedStatus_ForDeleteScenario()
    {
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "DELETE",
            TestType = TestType.Boundary,
            SrsRequirements = new List<SrsRequirement>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RequirementCode = "REQ-003",
                    IsReviewed = true,
                    TestableConstraints = """[{ "constraint": "resource is created -> 201", "expectedOutcome": "201 Created", "priority": "High" }]""",
                },
            },
            LlmExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = new List<int> { 200, 404 },
            },
        });

        result.Source.Should().Be(ExpectationSource.Llm);
        result.ExpectedStatusCodes.Should().Equal(200, 404);
    }

    [Fact]
    public void Resolve_Should_NotUseCoveredSrsCreatedStatus_ForGetScenario()
    {
        var requirementId = Guid.NewGuid();
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "GET",
            TestType = TestType.HappyPath,
            CoveredRequirementIds = new List<Guid> { requirementId },
            SrsRequirements = new List<SrsRequirement>
            {
                new()
                {
                    Id = requirementId,
                    RequirementCode = "REQ-003",
                    IsReviewed = true,
                    TestableConstraints = """[{ "constraint": "resource is created -> 201", "expectedOutcome": "201 Created", "priority": "High" }]""",
                },
            },
            LlmExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = new List<int> { 200 },
            },
        });

        result.Source.Should().Be(ExpectationSource.Llm);
        result.ExpectedStatusCodes.Should().Equal(200);
    }

    [Fact]
    public void Resolve_Should_NotUseCoveredSrsCreatedStatus_ForPut_WhenOpenApiDoesNotDocumentIt()
    {
        var requirementId = Guid.NewGuid();
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "PUT",
            TestType = TestType.HappyPath,
            CoveredRequirementIds = new List<Guid> { requirementId },
            SrsRequirements = new List<SrsRequirement>
            {
                new()
                {
                    Id = requirementId,
                    RequirementCode = "REQ-003",
                    IsReviewed = true,
                    TestableConstraints = """[{ "constraint": "resource is created -> 201", "expectedOutcome": "201 Created", "priority": "High" }]""",
                },
            },
            SwaggerResponses = new List<ApiEndpointResponseDescriptorDto>
            {
                new() { StatusCode = 200, Schema = "{}" },
                new() { StatusCode = 400, Schema = "{}" },
            },
            LlmExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = new List<int> { 200 },
            },
        });

        result.Source.Should().Be(ExpectationSource.Llm);
        result.ExpectedStatusCodes.Should().Equal(200);
    }

    [Fact]
    public void Resolve_Should_KeepBoundarySuccessStatus_FromLlm()
    {
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            TestType = TestType.Boundary,
            SrsRequirements = new List<SrsRequirement>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RequirementCode = "REQ-003",
                    IsReviewed = true,
                    TestableConstraints = """[{ "constraint": "valid boundary creates resource -> 201", "expectedOutcome": "201 Created", "priority": "High" }]""",
                },
            },
            LlmExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = new List<int> { 201 },
            },
        });

        result.Source.Should().Be(ExpectationSource.Llm);
        result.ExpectedStatusCodes.Should().Equal(201);
    }

    [Fact]
    public void Resolve_Should_FilterUnsupportedLlmStatuses_ToDocumentedOpenApiResponses()
    {
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            TestType = TestType.Negative,
            SwaggerResponses = new List<ApiEndpointResponseDescriptorDto>
            {
                new() { StatusCode = 200, Schema = "{}" },
                new() { StatusCode = 400, Schema = "{}" },
                new() { StatusCode = 401, Schema = "{}" },
            },
            LlmExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = new List<int> { 422 },
            },
        });

        result.ExpectedStatusCodes.Should().Equal(400);
    }

    [Fact]
    public void Resolve_Should_NotStrictlyAssertOptionalSuccessFields_FromOpenApiAlone()
    {
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            TestType = TestType.HappyPath,
            SwaggerResponses = new List<ApiEndpointResponseDescriptorDto>
            {
                new()
                {
                    StatusCode = 200,
                    Schema = """
                    {
                      "type": "object",
                      "properties": {
                        "success": { "type": "boolean" },
                        "data": {
                          "type": "object",
                          "properties": {
                            "token": { "type": "string" }
                          }
                        }
                      }
                    }
                    """,
                },
            },
        });

        result.ExpectedStatusCodes.Should().Equal(200);
        result.BodyContains.Should().BeEmpty();
        result.JsonPathChecks.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_Should_AssertSrsRequiredToken_WhenOpenApiContainsTokenField()
    {
        var requirementId = Guid.NewGuid();
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            TestType = TestType.HappyPath,
            CoveredRequirementIds = new List<Guid> { requirementId },
            SrsRequirements = new List<SrsRequirement>
            {
                new()
                {
                    Id = requirementId,
                    RequirementCode = "REQ-LOGIN",
                    Title = "Login",
                    Description = "Token is returned in the response.",
                },
            },
            SwaggerResponses = new List<ApiEndpointResponseDescriptorDto>
            {
                new()
                {
                    StatusCode = 200,
                    Schema = """
                    {
                      "type": "object",
                      "properties": {
                        "data": {
                          "type": "object",
                          "properties": {
                            "token": { "type": "string" }
                          }
                        }
                      }
                    }
                    """,
                },
            },
        });

        result.ExpectedStatusCodes.Should().Equal(200);
        result.JsonPathChecks.Should().ContainKey("$.data.token");
        result.JsonPathChecks["$.data.token"].Should().Be("exists");
    }
}
