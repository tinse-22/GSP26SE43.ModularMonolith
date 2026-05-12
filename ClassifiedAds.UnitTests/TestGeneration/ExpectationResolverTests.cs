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
}
