using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class EndpointRequirementMapperTests
{
    [Fact]
    public void MapRequirementsToEndpoint_Should_MapRegisterRequirements_AndExcludeUnrelatedRequirements()
    {
        var endpoint = CreateEndpoint("POST", "/api/auth/register", "registerUser");
        var requirements = CreateAuthRequirements();
        var mapper = new EndpointRequirementMapper();

        var matches = mapper.MapRequirementsToEndpoint(endpoint, requirements);

        matches.Single(x => x.RequirementCode == "REQ-002").Relevance.Should().Be(RequirementRelevance.Direct);
        matches.Single(x => x.RequirementCode == "REQ-006").Relevance.Should().Be(RequirementRelevance.Partial);
        matches.Single(x => x.RequirementCode == "REQ-001").IsCoverable.Should().BeFalse();
        matches.Single(x => x.RequirementCode == "REQ-003").IsCoverable.Should().BeFalse();

        matches
            .Where(x => x.IsCoverable)
            .Select(x => x.RequirementCode)
            .Should()
            .BeEquivalentTo(new[] { "REQ-002", "REQ-006" });
    }

    [Fact]
    public void MapRequirementsToEndpoint_Should_MapLoginRequirements_AndTreatRegistrationAsDependencyOnly()
    {
        var endpoint = CreateEndpoint("POST", "/api/auth/login", "login");
        var requirements = CreateAuthRequirements();
        var mapper = new EndpointRequirementMapper();

        var matches = mapper.MapRequirementsToEndpoint(endpoint, requirements);

        matches.Single(x => x.RequirementCode == "REQ-003").Relevance.Should().Be(RequirementRelevance.Direct);
        matches.Single(x => x.RequirementCode == "REQ-006").Relevance.Should().Be(RequirementRelevance.Partial);
        matches.Single(x => x.RequirementCode == "REQ-002").Relevance.Should().Be(RequirementRelevance.Dependency);

        matches
            .Where(x => x.IsCoverable)
            .Select(x => x.RequirementCode)
            .Should()
            .BeEquivalentTo(new[] { "REQ-003", "REQ-006" });
    }

    private static ApiEndpointMetadataDto CreateEndpoint(string method, string path, string operationId) =>
        new()
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = method,
            Path = path,
            OperationId = operationId,
            Parameters = new List<ApiEndpointParameterDescriptorDto>
            {
                new()
                {
                    Name = "body",
                    Location = "Body",
                    ContentType = "application/json",
                    Schema = """
                    {
                      "type": "object",
                      "properties": {
                        "email": { "type": "string", "format": "email" },
                        "password": { "type": "string", "minLength": 6 }
                      }
                    }
                    """,
                },
            },
        };

    private static List<SrsRequirement> CreateAuthRequirements() =>
        new()
        {
            new()
            {
                RequirementCode = "REQ-001",
                Title = "System Health Check",
                Description = "Health endpoint returns service status.",
                RequirementType = SrsRequirementType.Functional,
            },
            new()
            {
                RequirementCode = "REQ-002",
                Title = "Registration",
                Description = "A user can register with a valid unique email and password.",
                RequirementType = SrsRequirementType.Functional,
            },
            new()
            {
                RequirementCode = "REQ-003",
                Title = "Login",
                Description = "A user can login with the correct email and password and receives a token.",
                RequirementType = SrsRequirementType.Functional,
            },
            new()
            {
                RequirementCode = "REQ-004",
                Title = "Category Creation",
                Description = "A manager can create product categories.",
                RequirementType = SrsRequirementType.Functional,
            },
            new()
            {
                RequirementCode = "REQ-006",
                Title = "Security",
                Description = "Validate email format and password length before authentication.",
                RequirementType = SrsRequirementType.Security,
            },
        };
}
