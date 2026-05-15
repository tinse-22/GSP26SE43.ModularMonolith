using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Domain.Entities;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class TestGenerationPayloadBuilderTests
{
    [Fact]
    public async Task BuildPayloadAsync_Should_IncludeSrsRequirements_WhenSuiteHasSrsDocument()
    {
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var srsDocumentId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();

        var suiteRepo = MockRepository(new List<TestSuite>
        {
            new()
            {
                Id = suiteId,
                Name = "Suite with SRS",
                SrsDocumentId = srsDocumentId,
            },
        });
        var proposalRepo = MockRepository(new List<TestOrderProposal>
        {
            new()
            {
                Id = proposalId,
                TestSuiteId = suiteId,
                AppliedOrder = "applied-order-json",
            },
        });
        var requirementRepo = MockRepository(new List<SrsRequirement>
        {
            new()
            {
                Id = requirementId,
                SrsDocumentId = srsDocumentId,
                RequirementCode = "REQ-001",
                Title = "Invalid email rejected",
                Description = "Registration rejects invalid email.",
                RequirementType = SrsRequirementType.Functional,
                TestableConstraints = """[{ "constraint": "invalid email -> 422" }]""",
                RefinedConstraints = """[{ "constraint": "invalid email -> 400", "expectedOutcome": "400" }]""",
                Assumptions = """["email confirmation is not required"]""",
                Ambiguities = """[]""",
                ConfidenceScore = 0.7f,
                RefinedConfidenceScore = 0.95f,
                DisplayOrder = 1,
            },
        });

        var orderService = new Mock<IApiTestOrderService>();
        orderService.Setup(x => x.DeserializeOrderJson("applied-order-json"))
            .Returns(new List<ApiOrderItemModel>
            {
                new()
                {
                    EndpointId = endpointId,
                    HttpMethod = "POST",
                    Path = "/api/auth/register",
                    OrderIndex = 0,
                },
            });

        var builder = new TestGenerationPayloadBuilder(
            suiteRepo.Object,
            proposalRepo.Object,
            requirementRepo.Object,
            new Mock<IApiEndpointMetadataService>().Object,
            new Mock<IObservationConfirmationPromptBuilder>().Object,
            orderService.Object,
            Options.Create(new N8nIntegrationOptions
            {
                BeBaseUrl = "http://localhost:5099",
                CallbackApiKey = "secret",
            }),
            new Mock<ILogger<TestGenerationPayloadBuilder>>().Object);

        var payload = await builder.BuildPayloadAsync(suiteId, proposalId);

        payload.SrsRequirements.Should().ContainSingle();
        var requirement = payload.SrsRequirements[0];
        requirement.Id.Should().Be(requirementId);
        requirement.Code.Should().Be("REQ-001");
        requirement.EffectiveConstraints.Should().Contain("400");
        requirement.EffectiveConstraints.Should().NotContain("422");
        requirement.ConfidenceScore.Should().Be(0.95f);
        payload.CallbackUrl.Should().Be($"http://localhost:5099/api/test-suites/{suiteId}/test-cases/from-ai");
    }

    [Fact]
    public async Task BuildPayloadAsync_Should_CompactHeavyPromptInputs_AndExposeFastN8nControls()
    {
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var suiteRepo = MockRepository(new List<TestSuite>
        {
            new()
            {
                Id = suiteId,
                Name = "Compact suite",
                ApiSpecId = specId,
                GlobalBusinessRules = new string('g', 200),
                EndpointBusinessContexts = new Dictionary<Guid, string>
                {
                    [endpointId] = new string('b', 200),
                },
            },
        });
        var proposalRepo = MockRepository(new List<TestOrderProposal>
        {
            new()
            {
                Id = proposalId,
                TestSuiteId = suiteId,
                AppliedOrder = "applied-order-json",
            },
        });
        var requirementRepo = MockRepository(new List<SrsRequirement>());

        var orderService = new Mock<IApiTestOrderService>();
        orderService.Setup(x => x.DeserializeOrderJson("applied-order-json"))
            .Returns(new List<ApiOrderItemModel>
            {
                new()
                {
                    EndpointId = endpointId,
                    HttpMethod = "POST",
                    Path = "/api/products",
                    OrderIndex = 0,
                },
            });

        var metadataService = new Mock<IApiEndpointMetadataService>();
        metadataService
            .Setup(x => x.GetEndpointMetadataAsync(
                specId,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(endpointId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = endpointId,
                    HttpMethod = "POST",
                    Path = "/api/products",
                    OperationId = "createProduct",
                    ParameterSchemaPayloads = new List<string>
                    {
                        "{ \"type\": \"object\", \"properties\": { \"name\": { \"type\": \"string\" }, \"description\": { \"type\": \"string\" } } }",
                        "{ \"ignored\": true }",
                    },
                    ResponseSchemaPayloads = new List<string>
                    {
                        "{ \"type\": \"object\", \"properties\": { \"id\": { \"type\": \"string\" } } }",
                    },
                },
            });

        var promptBuilder = new Mock<IObservationConfirmationPromptBuilder>();
        promptBuilder
            .Setup(x => x.BuildForSequence(It.IsAny<IReadOnlyList<EndpointPromptContext>>()))
            .Returns(new List<ObservationConfirmationPrompt>
            {
                new()
                {
                    SystemPrompt = new string('s', 200),
                    CombinedPrompt = new string('c', 200),
                    ObservationPrompt = new string('o', 200),
                    ConfirmationPromptTemplate = new string('t', 200),
                },
            });

        var builder = new TestGenerationPayloadBuilder(
            suiteRepo.Object,
            proposalRepo.Object,
            requirementRepo.Object,
            metadataService.Object,
            promptBuilder.Object,
            orderService.Object,
            Options.Create(new N8nIntegrationOptions
            {
                BeBaseUrl = "http://localhost:5099",
                CallbackApiKey = "secret",
                GenerationModel = "gpt-4.1-mini",
                GenerationMinOutputTokens = 1024,
                GenerationMaxOutputTokens = 2048,
                GenerationOutputTokensPerEndpoint = 512,
                GenerationMaxSchemaPayloadCountPerKind = 1,
                GenerationMaxSchemaPayloadLength = 60,
                GenerationMaxPromptLength = 50,
                GenerationMaxBusinessContextLength = 40,
            }),
            new Mock<ILogger<TestGenerationPayloadBuilder>>().Object);

        var payload = await builder.BuildPayloadAsync(suiteId, proposalId);

        payload.Model.Should().Be("gpt-4.1-mini");
        payload.MaxOutputTokens.Should().Be(1024);
        payload.PreferJsonObjectResponse.Should().BeTrue();
        payload.GlobalBusinessRules.Should().HaveLength(40);
        payload.EndpointBusinessContexts[endpointId].Should().HaveLength(40);

        var endpoint = payload.Endpoints.Should().ContainSingle().Subject;
        endpoint.ParameterSchemaPayloads.Should().ContainSingle();
        endpoint.ParameterSchemaPayloads[0].Length.Should().BeLessThanOrEqualTo(60);
        endpoint.ParameterSchemaPayloads[0].Should().NotContain("ignored");
        endpoint.ResponseSchemaPayloads.Should().ContainSingle();
        endpoint.Prompt.CombinedPrompt.Should().HaveLength(50);
        endpoint.Prompt.ObservationPrompt.Should().HaveLength(50);
    }

    private static Mock<IRepository<TEntity, Guid>> MockRepository<TEntity>(List<TEntity> entities)
        where TEntity : Entity<Guid>, IAggregateRoot
    {
        var repo = new Mock<IRepository<TEntity, Guid>>();
        repo.Setup(x => x.GetQueryableSet()).Returns(entities.AsQueryable());
        repo.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TEntity>>()))
            .Returns<IQueryable<TEntity>>(query => Task.FromResult(query.FirstOrDefault()));
        repo.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TEntity>>()))
            .Returns<IQueryable<TEntity>>(query => Task.FromResult(query.ToList()));
        return repo;
    }
}
