using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;

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
        var provenance = JsonSerializer.Deserialize<List<ExpectedProvenanceItem>>(
            result.ExpectedProvenance,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        provenance.Should().NotBeNullOrEmpty();
        provenance.Should().Contain(x =>
            x.Source == "srs" &&
            x.RequirementCode == "REQ-400" &&
            x.Evidence.Contains("invalid email", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void Resolve_Should_IncludeMarkdownConstraintEvidence_InExpectedProvenance()
    {
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            TestType = TestType.Negative,
            SrsDocumentContent = """
            ## Validation Rules
            `email` format invalid -> 400
            """,
        });

        result.Source.Should().Be(ExpectationSource.Srs);
        result.ExpectedStatusCodes.Should().Equal(400);

        var provenance = JsonSerializer.Deserialize<List<ExpectedProvenanceItem>>(
            result.ExpectedProvenance,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        provenance.Should().NotBeNullOrEmpty();
        provenance.Should().Contain(x =>
            x.Source == "srs" &&
            x.RequirementCode == "SRS-MD" &&
            x.Evidence.Contains("email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_Should_UseStructuredSrsAssertions_WhenConstraintProvidesThem()
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
                    RequirementCode = "REQ-REGISTER",
                    IsReviewed = true,
                    TestableConstraints = """
                    [{
                      "constraint": "Successful registration returns created user payload.",
                      "expectedStatusCodes": [201],
                      "bodyContains": ["success", "data"],
                      "jsonPathChecks": {
                        "$.data.id": "string",
                        "$.data.email": "string",
                        "$.data.createdAt": "datetime"
                      },
                      "evidence": "SRS section 4.2: successful registration returns id, email, and createdAt."
                    }]
                    """,
                },
            },
        });

        result.Source.Should().Be(ExpectationSource.Srs);
        result.ExpectedStatusCodes.Should().Equal(201);
        result.BodyContains.Should().Equal("success", "data");
        result.JsonPathChecks.Should().Contain(new Dictionary<string, string>
        {
            ["$.data.id"] = "string",
            ["$.data.email"] = "string",
            ["$.data.createdAt"] = "datetime",
        });

        var provenance = JsonSerializer.Deserialize<List<ExpectedProvenanceItem>>(
            result.ExpectedProvenance,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        provenance.Should().NotBeNullOrEmpty();
        provenance.Should().OnlyContain(x =>
            x.Source == "srs" &&
            x.RequirementCode == "REQ-REGISTER" &&
            x.Evidence.Contains("SRS section 4.2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_Should_AllowDirectSrsBusinessStatus_WhenOpenApiIsIncomplete()
    {
        var endpointId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = endpointId,
            HttpMethod = "POST",
            TestType = TestType.Negative,
            SwaggerResponses = new List<ApiEndpointResponseDescriptorDto>
            {
                new() { StatusCode = 201, Schema = "{}" },
                new() { StatusCode = 401, Schema = "{}" },
            },
            SrsRequirements = new List<SrsRequirement>
            {
                new()
                {
                    Id = requirementId,
                    EndpointId = endpointId,
                    RequirementCode = "TC-PROD-CRT-003",
                    IsReviewed = true,
                    TestableConstraints = """
                    [{
                      "constraint": "categoryId is not a valid ObjectId -> 400",
                      "expectedStatusCodes": [400],
                      "bodyContains": ["categoryId", "invalid"],
                      "jsonPathChecks": { "$.success": "false" }
                    }]
                    """,
                },
            },
            LlmExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = new List<int> { 401 },
                BodyContains = new List<string> { "email", "invalid" },
                JsonPathChecks = new Dictionary<string, string> { ["$.success"] = "false" },
            },
        });

        result.Source.Should().Be(ExpectationSource.Srs);
        result.ExpectedStatusCodes.Should().Equal(400);
        result.BodyContains.Should().Equal("categoryId", "invalid");
        result.JsonPathChecks.Should().Contain("$.success", "false");
    }

    [Fact]
    public void Resolve_Should_RemoveErrorAssertions_FromSuccessExpectation()
    {
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            TestType = TestType.Boundary,
            SwaggerResponses = new List<ApiEndpointResponseDescriptorDto>
            {
                new() { StatusCode = 201, Schema = "{}" },
                new() { StatusCode = 400, Schema = "{}" },
            },
            LlmExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = new List<int> { 201 },
                BodyContains = new List<string> { "errors", "message", "success" },
                JsonPathChecks = new Dictionary<string, string>
                {
                    ["$.success"] = "false",
                    ["$.errors.*"] = "email",
                },
            },
        });

        result.ExpectedStatusCodes.Should().Equal(201);
        result.BodyContains.Should().Equal("message", "success");
        result.JsonPathChecks.Should().Contain("$.success", "true");
        result.JsonPathChecks.Should().NotContainKey("$.errors.*");
    }

    [Fact]
    public void Resolve_Should_RemoveSuccessDataAssertions_FromFailureExpectation()
    {
        var resolver = new ExpectationResolver(new Mock<ILogger<ExpectationResolver>>().Object);

        var result = resolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            TestType = TestType.Negative,
            SwaggerResponses = new List<ApiEndpointResponseDescriptorDto>
            {
                new() { StatusCode = 201, Schema = "{}" },
                new() { StatusCode = 409, Schema = "{}" },
            },
            LlmExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = new List<int> { 409 },
                BodyContains = new List<string> { "data", "already exists" },
                JsonPathChecks = new Dictionary<string, string>
                {
                    ["$.success"] = "true",
                    ["$.data.id"] = "string",
                    ["$.message"] = "already exists",
                },
            },
        });

        result.ExpectedStatusCodes.Should().Equal(409);
        result.BodyContains.Should().Equal("already exists");
        result.JsonPathChecks.Should().Contain("$.success", "false");
        result.JsonPathChecks.Should().Contain("$.message", "already exists");
        result.JsonPathChecks.Should().NotContainKey("$.data.id");
    }
}
