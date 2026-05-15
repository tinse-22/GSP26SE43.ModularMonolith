using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.Services;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// FE-06: LlmScenarioSuggester unit tests.
/// Verifies cache lookup, n8n webhook invocation, interaction logging,
/// result caching, empty/error handling, and FromCache flag behavior.
/// </summary>
public class LlmScenarioSuggesterTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly Mock<IObservationConfirmationPromptBuilder> _promptBuilderMock;
    private readonly Mock<IN8nIntegrationService> _n8nServiceMock;
    private readonly Mock<ILlmAssistantGatewayService> _llmGatewayServiceMock;
    private readonly Mock<ILlmSuggestionFeedbackContextService> _feedbackContextServiceMock;
    private readonly Mock<ILogger<LlmScenarioSuggester>> _loggerMock;
    private readonly LlmScenarioSuggester _sut;

    private static readonly Guid EndpointId1 = Guid.NewGuid();
    private static readonly Guid EndpointId2 = Guid.NewGuid();
    private static readonly Guid TestSuiteId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SpecificationId = Guid.NewGuid();

    public LlmScenarioSuggesterTests()
    {
        _promptBuilderMock = new Mock<IObservationConfirmationPromptBuilder>();
        _n8nServiceMock = new Mock<IN8nIntegrationService>();
        _llmGatewayServiceMock = new Mock<ILlmAssistantGatewayService>();
        _feedbackContextServiceMock = new Mock<ILlmSuggestionFeedbackContextService>();
        _loggerMock = new Mock<ILogger<LlmScenarioSuggester>>();

        _feedbackContextServiceMock
            .Setup(x => x.BuildAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmSuggestionFeedbackContextResult.Empty);

        var expectationResolver = new ExpectationResolver(
            new Mock<ILogger<ExpectationResolver>>().Object);

        _sut = new LlmScenarioSuggester(
            _promptBuilderMock.Object,
            _n8nServiceMock.Object,
            _llmGatewayServiceMock.Object,
            _feedbackContextServiceMock.Object,
            expectationResolver,
            new EndpointRequirementMapper(),
            _loggerMock.Object);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_UseCache_WhenAllEndpointsHaveCacheHit()
    {
        // Arrange
        var context = CreateDefaultContext();

        var cachedScenarios1 = new List<LlmSuggestedScenario>
        {
            new() { EndpointId = EndpointId1, ScenarioName = "Cached scenario 1" },
        };
        var cachedScenarios2 = new List<LlmSuggestedScenario>
        {
            new() { EndpointId = EndpointId2, ScenarioName = "Cached scenario 2" },
        };

        SetupCacheHit(EndpointId1, cachedScenarios1);
        SetupCacheHit(EndpointId2, cachedScenarios2);
        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert
        result.FromCache.Should().BeTrue();
        result.Scenarios.Should().Contain(x => x.ScenarioName == "Cached scenario 1");
        result.Scenarios.Should().Contain(x => x.ScenarioName == "Cached scenario 2");

        _n8nServiceMock.Verify(
            x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_CallN8n_WhenCacheMiss()
    {
        // Arrange
        var context = CreateDefaultContext();

        // First endpoint has cache, second does not => cache miss overall
        var cachedScenarios1 = new List<LlmSuggestedScenario>
        {
            new() { EndpointId = EndpointId1, ScenarioName = "Cached scenario 1" },
        };
        SetupCacheHit(EndpointId1, cachedScenarios1);
        SetupCacheMiss(EndpointId2);

        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert
        result.FromCache.Should().BeFalse();
        _n8nServiceMock.Verify(
            x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                N8nWebhookNames.GenerateLlmSuggestions,
                It.IsAny<N8nBoundaryNegativePayload>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_TargetScenarioCount_ByHttpMethod()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert
        var scenarioCounts = result.Scenarios
            .GroupBy(x => x.EndpointId)
            .ToDictionary(g => g.Key, g => g.Count());

        scenarioCounts.Should().ContainKey(EndpointId1);
        scenarioCounts.Should().ContainKey(EndpointId2);
        scenarioCounts[EndpointId1].Should().BeInRange(1, 10);
        scenarioCounts[EndpointId2].Should().BeInRange(1, 3);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_TargetThreeScenarios_ForDeleteEndpoints()
    {
        // Arrange
        var context = CreateSingleEndpointContext();
        context.EndpointMetadata[0].HttpMethod = "DELETE";
        context.EndpointMetadata[0].Path = "/api/users/{id}";
        context.OrderedEndpoints[0].HttpMethod = "DELETE";
        context.OrderedEndpoints[0].Path = "/api/users/{id}";

        SetupAllCacheMiss();
        SetupPromptBuilder(1);

        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 50,
            });

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert
        result.Scenarios.Should().HaveCount(3);
        result.Scenarios.Should().OnlyContain(x => x.EndpointId == EndpointId1);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_NotPadSuccessOnlyGetEndpoints_WithBoundaryNegative()
    {
        // Arrange
        var context = CreateSingleEndpointContext();
        context.EndpointMetadata[0].HttpMethod = "GET";
        context.EndpointMetadata[0].Path = "/api/health";
        context.EndpointMetadata[0].OperationId = "health";
        context.EndpointMetadata[0].ParameterSchemaPayloads = new List<string>();
        context.EndpointMetadata[0].ResponseSchemaPayloads = new List<string> { "{\"type\":\"object\"}" };
        context.EndpointMetadata[0].Responses = new List<ApiEndpointResponseDescriptorDto>
        {
            new() { StatusCode = 200, Schema = "{\"type\":\"object\"}" },
        };
        context.OrderedEndpoints[0].HttpMethod = "GET";
        context.OrderedEndpoints[0].Path = "/api/health";

        SetupAllCacheMiss();
        SetupPromptBuilder(1);

        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 50,
            });

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert
        result.Scenarios.Should().ContainSingle();
        result.Scenarios[0].SuggestedTestType.Should().Be(TestType.HappyPath);
        result.Scenarios[0].ExpectedStatusCodes.Should().BeEquivalentTo(new[] { 200 });
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_EmitMethodBasedScenarioTargets_InPromptPayload()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        N8nBoundaryNegativePayload capturedPayload = null;
        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, N8nBoundaryNegativePayload, CancellationToken>((_, payload, _) =>
            {
                capturedPayload = payload;
            })
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 100,
            });

        // Act
        await _sut.SuggestScenariosAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        capturedPayload.PromptConfig.Rules.Should().Contain("GET and DELETE endpoints target up to 3 scenarios total");
        capturedPayload.PromptConfig.Rules.Should().Contain("POST, PUT, and PATCH endpoints target up to 10 scenarios total");
        capturedPayload.PromptConfig.TaskInstruction.Should().Contain("POST /api/auth/login: target up to 10 scenarios");
        capturedPayload.PromptConfig.TaskInstruction.Should().Contain("GET /api/users: target up to 3 scenarios");
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_CompactPayload_BeforeCallingN8n()
    {
        // Arrange
        var context = CreateDefaultContext();
        context.Suite.GlobalBusinessRules = new string('G', 5000);
        context.Suite.EndpointBusinessContexts[EndpointId1] = new string('B', 5000);

        var endpoint1Metadata = context.EndpointMetadata.First(x => x.EndpointId == EndpointId1);
        endpoint1Metadata.ParameterSchemaPayloads = new List<string>
        {
            new string('P', 3000),
            new string('Q', 3000),
            new string('R', 3000),
        };
        endpoint1Metadata.ResponseSchemaPayloads = new List<string>
        {
            new string('X', 3000),
            new string('Y', 3000),
            new string('Z', 3000),
        };

        context.EndpointParameterDetails = new Dictionary<Guid, EndpointParameterDetailDto>
        {
            [EndpointId1] = new EndpointParameterDetailDto
            {
                EndpointId = EndpointId1,
                EndpointPath = "/api/auth/login",
                EndpointHttpMethod = "POST",
                Parameters = Enumerable.Range(1, 24)
                    .Select(i => new ParameterDetailDto
                    {
                        ParameterId = Guid.NewGuid(),
                        Name = $"param{i:00}",
                        Location = "Body",
                        DataType = "string",
                        IsRequired = i % 2 == 0,
                        DefaultValue = new string('D', 500),
                    })
                    .ToList(),
            },
        };

        _promptBuilderMock
            .Setup(x => x.BuildForSequence(It.IsAny<IReadOnlyList<EndpointPromptContext>>()))
            .Returns(new List<ObservationConfirmationPrompt>
            {
                new()
                {
                    SystemPrompt = new string('S', 4000),
                    CombinedPrompt = new string('C', 9000),
                    ObservationPrompt = new string('O', 7000),
                    ConfirmationPromptTemplate = new string('T', 4000),
                },
                new()
                {
                    SystemPrompt = new string('S', 4000),
                    CombinedPrompt = new string('C', 9000),
                    ObservationPrompt = new string('O', 7000),
                    ConfirmationPromptTemplate = new string('T', 4000),
                },
            });

        SetupAllCacheMiss();

        N8nBoundaryNegativePayload capturedPayload = null;
        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, N8nBoundaryNegativePayload, CancellationToken>((_, payload, _) =>
            {
                capturedPayload = payload;
            })
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 100,
            });

        // Act
        await _sut.SuggestScenariosAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        capturedPayload.GlobalBusinessRules.Length.Should().BeLessThanOrEqualTo(1200);
        capturedPayload.PromptConfig.SystemPrompt.Length.Should().BeLessThanOrEqualTo(1200);
        capturedPayload.PromptConfig.TaskInstruction.Length.Should().BeLessThanOrEqualTo(5000);
        capturedPayload.PromptConfig.Rules.Length.Should().BeLessThanOrEqualTo(4500);
        capturedPayload.PromptConfig.ResponseFormat.Length.Should().BeLessThanOrEqualTo(2500);

        var endpointPayload = capturedPayload.Endpoints.First(x => x.EndpointId == EndpointId1);
        endpointPayload.BusinessContext.Length.Should().BeLessThanOrEqualTo(1200);
        endpointPayload.ParameterSchemaPayloads.Count.Should().BeLessThanOrEqualTo(2);
        endpointPayload.ResponseSchemaPayloads.Count.Should().BeLessThanOrEqualTo(2);
        endpointPayload.ParameterSchemaPayloads.Should().OnlyContain(x => x.Length <= 2500);
        endpointPayload.ResponseSchemaPayloads.Should().OnlyContain(x => x.Length <= 2500);
        endpointPayload.ParameterDetails.Count.Should().BeLessThanOrEqualTo(16);
        endpointPayload.ParameterDetails.Should().OnlyContain(x => (x.DefaultValue ?? string.Empty).Length <= 200);
        endpointPayload.Prompt.SystemPrompt.Length.Should().BeLessThanOrEqualTo(1200);
        endpointPayload.Prompt.CombinedPrompt.Length.Should().BeLessThanOrEqualTo(5000);
        endpointPayload.Prompt.ObservationPrompt.Length.Should().BeLessThanOrEqualTo(3000);
        endpointPayload.Prompt.ConfirmationPromptTemplate.Length.Should().BeLessThanOrEqualTo(1200);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_SaveInteraction_AfterN8nCall()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        // Act
        await _sut.SuggestScenariosAsync(context);

        // Assert
        _llmGatewayServiceMock.Verify(
            x => x.SaveInteractionAsync(
                It.Is<SaveLlmInteractionRequest>(r =>
                    r.UserId == UserId &&
                    r.InteractionType == 0 &&
                    r.ModelUsed == "gpt-4o"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_CacheResults_AfterN8nCall()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        // Act
        await _sut.SuggestScenariosAsync(context);

        // Assert: CacheSuggestionsAsync called once for each endpoint
        _llmGatewayServiceMock.Verify(
            x => x.CacheSuggestionsAsync(
                EndpointId1,
                1, // SuggestionTypeBoundaryNegative
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _llmGatewayServiceMock.Verify(
            x => x.CacheSuggestionsAsync(
                EndpointId2,
                1, // SuggestionTypeBoundaryNegative
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_HandleEmptyN8nResponse()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        // n8n returns empty scenarios
        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = null,
                Model = "gpt-4o",
                TokensUsed = 50,
            });

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert
        result.Scenarios.Should().HaveCountGreaterThanOrEqualTo(4);
        result.Scenarios.Should().Contain(x => x.EndpointId == EndpointId1 && x.SuggestedTestType == TestType.HappyPath);
        result.Scenarios.Should().Contain(x => x.EndpointId == EndpointId1 && x.SuggestedTestType == TestType.Boundary);
        result.Scenarios.Should().Contain(x => x.EndpointId == EndpointId2 && x.SuggestedTestType == TestType.Negative);
        result.FromCache.Should().BeFalse();
        result.LlmModel.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_SynthesizeNonEmptyHappyPathBody_WhenContractRequiresBody()
    {
        // Arrange
        var context = CreateRequiredBodyLoginContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(1);

        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 42,
            });

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert
        var happyPath = result.Scenarios.Single(x =>
            x.EndpointId == EndpointId1 &&
            x.SuggestedTestType == TestType.HappyPath);

        happyPath.SuggestedBodyType.Should().Match(x =>
            string.Equals(x, "JSON", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x, "UrlEncoded", StringComparison.OrdinalIgnoreCase));
        happyPath.SuggestedBody.Should().NotBeNullOrWhiteSpace();
        happyPath.SuggestedBody.Should().NotBe("{}");
        happyPath.Variables.Should().Contain(x =>
            x.VariableName == "authToken" &&
            x.ExtractFrom == "ResponseBody" &&
            x.JsonPath == "$.token");

        if (string.Equals(happyPath.SuggestedBodyType, "JSON", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(happyPath.SuggestedBody);
            document.RootElement.TryGetProperty("email", out var email).Should().BeTrue();
            document.RootElement.TryGetProperty("password", out var password).Should().BeTrue();
            email.GetString().Should().Contain("@");
            password.GetString().Should().NotBeNullOrWhiteSpace();
        }
        else
        {
            happyPath.SuggestedBody.Should().Contain("email");
            happyPath.SuggestedBody.Should().Contain("password");
        }
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_NotInjectDuplicatedIdPlaceholder_ForCreateBodyWithoutRouteId()
    {
        // Arrange
        var context = CreateRequiredBodyCreatePetContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(1);

        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 32,
            });

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert
        var happyPath = result.Scenarios.Single(x =>
            x.EndpointId == EndpointId1 &&
            x.SuggestedTestType == TestType.HappyPath);

        happyPath.SuggestedBody.Should().NotContain("{{idId}}");
        happyPath.SuggestedBody.Should().NotContain("{{petId}}");

        using var document = JsonDocument.Parse(happyPath.SuggestedBody);
        document.RootElement.TryGetProperty("id", out var id).Should().BeTrue();
        if (id.ValueKind == JsonValueKind.String)
        {
            id.GetString().Should().NotContain("{{");
        }
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_PropagateN8nError()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("n8n webhook failed"));

        // Act
        var act = () => _sut.SuggestScenariosAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("n8n webhook failed");
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_FallbackLocally_WhenN8nFailureIsTransient()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new N8nTransientException(
                "timeout",
                N8nWebhookNames.GenerateLlmSuggestions,
                "https://example.test/webhook/generate-llm-suggestions",
                524,
                true,
                false));

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert
        result.UsedLocalFallback.Should().BeTrue();
        result.LlmModel.Should().Be("local-fallback");
        result.Scenarios.Should().HaveCountGreaterThanOrEqualTo(4);
        result.Scenarios.Should().Contain(x => x.EndpointId == EndpointId1);
        result.Scenarios.Should().Contain(x => x.EndpointId == EndpointId2);

        _n8nServiceMock.Verify(
            x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                N8nWebhookNames.GenerateLlmSuggestions,
                It.IsAny<N8nBoundaryNegativePayload>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _llmGatewayServiceMock.Verify(
            x => x.CacheSuggestionsAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_ReturnCachedResult_WhenCacheExists()
    {
        // Arrange: single endpoint, all cached
        var context = CreateSingleEndpointContext();

        var cachedScenarios = new List<LlmSuggestedScenario>
        {
            new() { EndpointId = EndpointId1, ScenarioName = "Boundary null input" },
            new() { EndpointId = EndpointId1, ScenarioName = "Negative invalid format" },
        };
        SetupCacheHit(EndpointId1, cachedScenarios);
        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert
        result.FromCache.Should().BeTrue();
        result.Scenarios.Should().Contain(x => x.ScenarioName == "Boundary null input");
        result.Scenarios.Should().Contain(x => x.ScenarioName == "Negative invalid format");

        _n8nServiceMock.Verify(
            x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _llmGatewayServiceMock.Verify(
            x => x.SaveInteractionAsync(It.IsAny<SaveLlmInteractionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_NotThrow_WhenAuditLogFails()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        // Make audit log throw
        _llmGatewayServiceMock
            .Setup(x => x.SaveInteractionAsync(It.IsAny<SaveLlmInteractionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection lost"));

        // Act
        var act = () => _sut.SuggestScenariosAsync(context);

        // Assert — should NOT throw, graceful degradation
        var result = await act.Should().NotThrowAsync();
        result.Subject.Scenarios.Should().NotBeEmpty();
        result.Subject.FromCache.Should().BeFalse();
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_NotThrow_WhenCacheSaveFails()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        // Make cache save throw
        _llmGatewayServiceMock
            .Setup(x => x.CacheSuggestionsAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache service unavailable"));

        // Act
        var act = () => _sut.SuggestScenariosAsync(context);

        // Assert — should NOT throw, graceful degradation
        var result = await act.Should().NotThrowAsync();
        result.Subject.Scenarios.Should().NotBeEmpty();
        result.Subject.FromCache.Should().BeFalse();
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_UseStableCacheKey_ForIdenticalContexts()
    {
        // Arrange — two identical contexts should produce the same cache key
        var context1 = CreateDefaultContext();
        var context2 = CreateDefaultContext();

        // Capture all cache keys across both calls
        var capturedKeys = new List<string>();
        _llmGatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                It.IsAny<Guid>(), 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, int, string, CancellationToken>((_, _, key, _) =>
            {
                capturedKeys.Add(key);
            })
            .ReturnsAsync(new CachedSuggestionsDto { HasCache = false });

        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        // Act
        await _sut.SuggestScenariosAsync(context1);
        await _sut.SuggestScenariosAsync(context2);

        capturedKeys.Should().HaveCount(2);
        capturedKeys.Should().OnlyContain(x => x == capturedKeys[0]);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_BypassCache_WhenBypassCacheIsTrue()
    {
        var context = CreateDefaultContext();
        context.BypassCache = true;

        var cachedScenarios1 = new List<LlmSuggestedScenario>
        {
            new() { EndpointId = EndpointId1, ScenarioName = "Cached scenario 1" },
        };
        var cachedScenarios2 = new List<LlmSuggestedScenario>
        {
            new() { EndpointId = EndpointId2, ScenarioName = "Cached scenario 2" },
        };

        SetupCacheHit(EndpointId1, cachedScenarios1);
        SetupCacheHit(EndpointId2, cachedScenarios2);
        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        var result = await _sut.SuggestScenariosAsync(context);

        result.FromCache.Should().BeFalse();

        _llmGatewayServiceMock.Verify(
            x => x.GetCachedSuggestionsAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _n8nServiceMock.Verify(
            x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                N8nWebhookNames.GenerateLlmSuggestions,
                It.IsAny<N8nBoundaryNegativePayload>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_ChangeCacheKey_WhenGlobalBusinessRulesChange()
    {
        var context1 = CreateDefaultContext();
        var context2 = CreateDefaultContext();
        context2.Suite.GlobalBusinessRules = "Updated business rule set";

        var capturedKeys = new List<string>();
        _llmGatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                It.IsAny<Guid>(), 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, int, string, CancellationToken>((_, _, key, _) => capturedKeys.Add(key))
            .ReturnsAsync(new CachedSuggestionsDto { HasCache = false });

        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        await _sut.SuggestScenariosAsync(context1);
        await _sut.SuggestScenariosAsync(context2);

        capturedKeys.Should().HaveCount(2);
        capturedKeys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_ChangeCacheKey_WhenFeedbackFingerprintChanges()
    {
        var context = CreateDefaultContext();
        var capturedKeys = new List<string>();

        _feedbackContextServiceMock
            .SetupSequence(x => x.BuildAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmSuggestionFeedbackContextResult
            {
                FeedbackFingerprint = "AAAAAAAAAAAAAAAA",
                EndpointFeedbackContexts = new Dictionary<Guid, string>(),
            })
            .ReturnsAsync(new LlmSuggestionFeedbackContextResult
            {
                FeedbackFingerprint = "BBBBBBBBBBBBBBBB",
                EndpointFeedbackContexts = new Dictionary<Guid, string>(),
            });

        _llmGatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                It.IsAny<Guid>(), 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, int, string, CancellationToken>((_, _, key, _) => capturedKeys.Add(key))
            .ReturnsAsync(new CachedSuggestionsDto { HasCache = false });

        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        await _sut.SuggestScenariosAsync(context);
        await _sut.SuggestScenariosAsync(context);

        capturedKeys.Should().HaveCount(2);
        capturedKeys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_KeepCacheKeyStable_WhenDependencyAwareOrderingNormalizesEndpointOrder()
    {
        var context1 = CreateDefaultContext();
        var context2 = CreateDefaultContext();
        context2.OrderedEndpoints = context2.OrderedEndpoints.Reverse().ToList();

        var capturedKeys = new List<string>();
        _llmGatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                It.IsAny<Guid>(), 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, int, string, CancellationToken>((_, _, key, _) => capturedKeys.Add(key))
            .ReturnsAsync(new CachedSuggestionsDto { HasCache = false });

        SetupPromptBuilder(2);
        SetupN8nReturnsScenarios();

        await _sut.SuggestScenariosAsync(context1);
        await _sut.SuggestScenariosAsync(context2);

        capturedKeys.Should().HaveCount(2);
        capturedKeys.Should().OnlyContain(x => x == capturedKeys[0]);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_IncludeEndpointSpecificFeedbackContext_InPayload()
    {
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        _feedbackContextServiceMock
            .Setup(x => x.BuildAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmSuggestionFeedbackContextResult
            {
                FeedbackFingerprint = "FEEDBACK12345678",
                EndpointFeedbackContexts = new Dictionary<Guid, string>
                {
                    { EndpointId1, "Helpful: 1\nNotHelpful: 0\n- Keep invalid email scenarios" },
                },
            });

        N8nBoundaryNegativePayload capturedPayload = null;
        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, N8nBoundaryNegativePayload, CancellationToken>((_, payload, _) => capturedPayload = payload)
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 100,
            });

        await _sut.SuggestScenariosAsync(context);

        capturedPayload.Should().NotBeNull();
        capturedPayload.Endpoints.First(x => x.EndpointId == EndpointId1).FeedbackContext
            .Should().Contain("Keep invalid email scenarios");
        capturedPayload.Endpoints.First(x => x.EndpointId == EndpointId2).FeedbackContext
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_FallBackToEmptyFeedback_WhenFeedbackServiceFails()
    {
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        _feedbackContextServiceMock
            .Setup(x => x.BuildAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("feedback service unavailable"));

        N8nBoundaryNegativePayload capturedPayload = null;
        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, N8nBoundaryNegativePayload, CancellationToken>((_, payload, _) => capturedPayload = payload)
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 100,
            });

        var act = () => _sut.SuggestScenariosAsync(context);

        await act.Should().NotThrowAsync();
        capturedPayload.Should().NotBeNull();
        capturedPayload.Endpoints.Should().OnlyContain(x => string.IsNullOrEmpty(x.FeedbackContext));
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_IncludeParameterDetails_InN8nPayload()
    {
        // Arrange
        var context = CreateDefaultContext();
        context.EndpointParameterDetails = new Dictionary<Guid, EndpointParameterDetailDto>
        {
            {
                EndpointId1, new EndpointParameterDetailDto
                {
                    EndpointId = EndpointId1,
                    EndpointPath = "/api/auth/login",
                    EndpointHttpMethod = "POST",
                    Parameters = new List<ParameterDetailDto>
                    {
                        new()
                        {
                            ParameterId = Guid.NewGuid(),
                            Name = "email",
                            Location = "Body",
                            DataType = "string",
                            Format = "email",
                            IsRequired = true,
                            DefaultValue = "user@example.com",
                        },
                        new()
                        {
                            ParameterId = Guid.NewGuid(),
                            Name = "password",
                            Location = "Body",
                            DataType = "string",
                            IsRequired = true,
                        },
                    },
                }
            },
        };

        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        N8nBoundaryNegativePayload capturedPayload = null;
        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, N8nBoundaryNegativePayload, CancellationToken>((_, payload, _) =>
            {
                capturedPayload = payload;
            })
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 100,
            });

        // Act
        await _sut.SuggestScenariosAsync(context);

        // Assert — endpoint1 should have ParameterDetails populated
        capturedPayload.Should().NotBeNull();
        var ep1Payload = capturedPayload.Endpoints.First(e => e.EndpointId == EndpointId1);
        ep1Payload.ParameterDetails.Should().HaveCount(2);
        ep1Payload.ParameterDetails[0].Name.Should().Be("email");
        ep1Payload.ParameterDetails[0].DataType.Should().Be("string");
        ep1Payload.ParameterDetails[0].Format.Should().Be("email");
        ep1Payload.ParameterDetails[0].IsRequired.Should().BeTrue();
        ep1Payload.ParameterDetails[0].DefaultValue.Should().Be("user@example.com");
        ep1Payload.ParameterDetails[1].Name.Should().Be("password");

        // endpoint2 has no parameter details, should have empty list
        var ep2Payload = capturedPayload.Endpoints.First(e => e.EndpointId == EndpointId2);
        ep2Payload.ParameterDetails.Should().BeEmpty();
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_PreserveVariables_FromN8nResponse()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        var response = new N8nBoundaryNegativeResponse
        {
            Model = "gpt-4o",
            TokensUsed = 800,
            Scenarios = new List<N8nSuggestedScenario>
            {
                new()
                {
                    EndpointId = EndpointId1,
                    ScenarioName = "Login with extraction",
                    Description = "Login and extract token",
                    TestType = "Negative",
                    Priority = "High",
                    Tags = new List<string> { "auth" },
                    Variables = new List<N8nTestCaseVariable>
                    {
                        new()
                        {
                            VariableName = "authToken",
                            ExtractFrom = "ResponseBody",
                            JsonPath = "$.token",
                            DefaultValue = "fallback-token",
                        },
                        new()
                        {
                            VariableName = "sessionId",
                            ExtractFrom = "ResponseHeader",
                            HeaderName = "X-Session-Id",
                        },
                    },
                },
            },
        };

        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert — variables should be preserved in the parsed scenarios
        var scenario = result.Scenarios.Single(x =>
            x.EndpointId == EndpointId1 &&
            x.ScenarioName == "Login with extraction");
        scenario.Variables.Should().Contain(x =>
            x.VariableName == "authToken" &&
            x.ExtractFrom == "ResponseBody" &&
            x.JsonPath == "$.token" &&
            x.DefaultValue == "fallback-token");
        scenario.Variables.Should().Contain(x =>
            x.VariableName == "sessionId" &&
            x.ExtractFrom == "ResponseHeader" &&
            x.HeaderName == "X-Session-Id");
    }

    #region New spec-driven assertion tests

    [Fact]
    public async Task SuggestScenariosAsync_Should_IncludeErrorResponses_InN8nPayload()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        N8nBoundaryNegativePayload capturedPayload = null;
        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, N8nBoundaryNegativePayload, CancellationToken>((_, payload, _) => capturedPayload = payload)
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 100,
            });

        // Act
        await _sut.SuggestScenariosAsync(context);

        // Assert — endpoint1 (POST login) should have error responses from Swagger
        capturedPayload.Should().NotBeNull();
        var ep1 = capturedPayload.Endpoints.First(e => e.EndpointId == EndpointId1);
        ep1.ErrorResponses.Should().ContainKey("400");
        ep1.ErrorResponses.Should().ContainKey("422");
        ep1.ErrorResponses.Should().NotContainKey("200"); // success codes excluded
        ep1.ErrorResponses["400"].Description.Should().Be("Validation error");

        // endpoint2 (GET users) should have 401
        var ep2 = capturedPayload.Endpoints.First(e => e.EndpointId == EndpointId2);
        ep2.ErrorResponses.Should().ContainKey("401");
        ep2.ErrorResponses.Should().NotContainKey("200");
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_UseSwaggerCodes_InExpectedStatus()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Model = "gpt-4o",
                TokensUsed = 100,
                Scenarios = new List<N8nSuggestedScenario>
                {
                    new()
                    {
                        EndpointId = EndpointId1,
                        ScenarioName = "Invalid login",
                        TestType = "Boundary",
                        Priority = "High",
                        Expectation = new N8nTestCaseExpectation
                        {
                            ExpectedStatus = new List<int> { 400 },
                        },
                    },
                },
            });

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert — expectedStatusCodes should only contain Swagger-defined codes
        var scenario = result.Scenarios.First(s =>
            s.EndpointId == EndpointId1 && s.ScenarioName == "Invalid login");
        scenario.ExpectedStatusCodes.Should().Contain(400);
        // Should NOT contain codes not in Swagger spec
        scenario.ExpectedStatusCodes.Should().NotContain(422);
        scenario.ExpectedStatusCodes.Should().NotContain(409);
        scenario.ExpectedStatusCodes.Should().NotContain(415);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_RepairJsonPathChecks_WhenLlmLeftEmpty()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Model = "gpt-4o",
                TokensUsed = 100,
                Scenarios = new List<N8nSuggestedScenario>
                {
                    new()
                    {
                        EndpointId = EndpointId1,
                        ScenarioName = "Empty assertion scenario",
                        TestType = "Negative",
                        Priority = "High",
                        Expectation = new N8nTestCaseExpectation
                        {
                            ExpectedStatus = new List<int> { 400 },
                            // LLM left these empty
                            JsonPathChecks = null,
                            BodyContains = null,
                        },
                    },
                },
            });

        // Act
        var result = await _sut.SuggestScenariosAsync(context);

        // Assert — assertions should be repaired from Swagger 400 response schema
        var scenario = result.Scenarios.First(s =>
            s.EndpointId == EndpointId1 && s.ScenarioName == "Empty assertion scenario");

        // Schema has "success" and "message" fields
        scenario.SuggestedJsonPathChecks.Should().NotBeNull();
        scenario.SuggestedJsonPathChecks.Should().NotBeEmpty();
        scenario.SuggestedBodyContains.Should().NotBeNull();
        scenario.SuggestedBodyContains.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_PopulateSrsTestableConstraints_InPayload()
    {
        // Arrange
        var context = CreateDefaultContext();
        context.SrsDocument = new SrsDocument
        {
            Id = Guid.NewGuid(),
            Title = "Test SRS",
            ParsedMarkdown = "# Requirements",
        };
        context.SrsRequirements = new List<SrsRequirement>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RequirementCode = "REQ-001",
                Title = "Password validation",
                Description = "Password must be at least 6 characters",
                EndpointId = EndpointId1,
                TestableConstraints = """[{"constraint": "password >= 6 chars \u2192 400", "priority": "High"}]""",
            },
        };

        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        N8nBoundaryNegativePayload capturedPayload = null;
        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, N8nBoundaryNegativePayload, CancellationToken>((_, payload, _) => capturedPayload = payload)
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 100,
            });

        // Act
        await _sut.SuggestScenariosAsync(context);

        // Assert — SRS context should have testable constraints populated
        capturedPayload.Should().NotBeNull();
        capturedPayload.SrsContext.Should().NotBeNull();
        capturedPayload.SrsContext.Requirements.Should().HaveCount(1);

        var req = capturedPayload.SrsContext.Requirements[0];
        req.Code.Should().Be("REQ-001");
        req.EndpointId.Should().Be(EndpointId1);
        req.TestableConstraints.Should().HaveCount(1);
        req.TestableConstraints[0].Constraint.Should().Contain("password");
        req.TestableConstraints[0].ExpectedOutcome.Should().Contain("400");
        req.TestableConstraints[0].Priority.Should().Be("High");
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_IncludeUpdatedRules_InPayload()
    {
        // Arrange
        var context = CreateDefaultContext();
        SetupAllCacheMiss();
        SetupPromptBuilder(2);

        N8nBoundaryNegativePayload capturedPayload = null;
        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()))
            .Callback<string, N8nBoundaryNegativePayload, CancellationToken>((_, payload, _) => capturedPayload = payload)
            .ReturnsAsync(new N8nBoundaryNegativeResponse
            {
                Scenarios = new List<N8nSuggestedScenario>(),
                Model = "gpt-4o",
                TokensUsed = 100,
            });

        // Act
        await _sut.SuggestScenariosAsync(context);

        // Assert — compact rules keep OpenAPI as frame and SRS as endpoint-scoped business source.
        capturedPayload.PromptConfig.Rules.Should().Contain("OpenAPI is the structural contract");
        capturedPayload.PromptConfig.Rules.Should().Contain("SRS is the business scenario driver");
        capturedPayload.PromptConfig.Rules.Should().NotContain("SRS constraints OVERRIDE");
    }

    #endregion

    #region Helpers

    private static LlmScenarioSuggestionContext CreateDefaultContext()
    {
        return new LlmScenarioSuggestionContext
        {
            TestSuiteId = TestSuiteId,
            UserId = UserId,
            SpecificationId = SpecificationId,
            Suite = new TestSuite
            {
                Id = TestSuiteId,
                Name = "Test Suite Boundary",
                GlobalBusinessRules = "Users must be 18+",
                EndpointBusinessContexts = new Dictionary<Guid, string>
                {
                    { EndpointId1, "Login endpoint context" },
                    { EndpointId2, "Users endpoint context" },
                },
            },
            EndpointMetadata = new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = EndpointId1,
                    HttpMethod = "POST",
                    Path = "/api/auth/login",
                    OperationId = "login",
                    ParameterSchemaPayloads = new List<string> { "{\"type\":\"object\"}" },
                    ResponseSchemaPayloads = new List<string> { "{\"type\":\"object\"}" },
                    Responses = new List<ApiEndpointResponseDescriptorDto>
                    {
                        new() { StatusCode = 200, Schema = "{\"type\":\"object\",\"properties\":{\"token\":{\"type\":\"string\"}}}" },
                        new() { StatusCode = 400, Description = "Validation error", Schema = "{\"type\":\"object\",\"properties\":{\"success\":{\"type\":\"boolean\"},\"message\":{\"type\":\"string\"}}}" },
                        new() { StatusCode = 422, Description = "Unprocessable entity" },
                    },
                },
                new()
                {
                    EndpointId = EndpointId2,
                    HttpMethod = "GET",
                    Path = "/api/users",
                    OperationId = "getUsers",
                    ParameterSchemaPayloads = new List<string>(),
                    ResponseSchemaPayloads = new List<string> { "{\"type\":\"array\"}" },
                    Responses = new List<ApiEndpointResponseDescriptorDto>
                    {
                        new() { StatusCode = 200, Schema = "{\"type\":\"array\"}" },
                        new() { StatusCode = 401, Description = "Unauthorized" },
                    },
                },
            },
            OrderedEndpoints = new List<ApiOrderItemModel>
            {
                new() { EndpointId = EndpointId1, HttpMethod = "POST", Path = "/api/auth/login", OrderIndex = 0 },
                new() { EndpointId = EndpointId2, HttpMethod = "GET", Path = "/api/users", OrderIndex = 1 },
            },
        };
    }

    private static LlmScenarioSuggestionContext CreateSingleEndpointContext()
    {
        return new LlmScenarioSuggestionContext
        {
            TestSuiteId = TestSuiteId,
            UserId = UserId,
            SpecificationId = SpecificationId,
            Suite = new TestSuite
            {
                Id = TestSuiteId,
                Name = "Single Endpoint Suite",
                EndpointBusinessContexts = new Dictionary<Guid, string>(),
            },
            EndpointMetadata = new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = EndpointId1,
                    HttpMethod = "POST",
                    Path = "/api/auth/login",
                    OperationId = "login",
                    ParameterSchemaPayloads = new List<string> { "{\"type\":\"object\"}" },
                    ResponseSchemaPayloads = new List<string> { "{\"type\":\"object\"}" },
                },
            },
            OrderedEndpoints = new List<ApiOrderItemModel>
            {
                new() { EndpointId = EndpointId1, HttpMethod = "POST", Path = "/api/auth/login", OrderIndex = 0 },
            },
        };
    }

    private static LlmScenarioSuggestionContext CreateRequiredBodyLoginContext()
    {
        return new LlmScenarioSuggestionContext
        {
            TestSuiteId = TestSuiteId,
            UserId = UserId,
            SpecificationId = SpecificationId,
            Suite = new TestSuite
            {
                Id = TestSuiteId,
                Name = "Login Contract Suite",
                EndpointBusinessContexts = new Dictionary<Guid, string>(),
            },
            EndpointMetadata = new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = EndpointId1,
                    HttpMethod = "POST",
                    Path = "/api/auth/login",
                    OperationId = "login",
                    HasRequiredRequestBody = true,
                    Parameters = new List<ApiEndpointParameterDescriptorDto>
                    {
                        new()
                        {
                            Name = "body",
                            Location = "Body",
                            IsRequired = true,
                            Schema = """
                            {
                              "type": "object",
                              "required": ["email", "password"],
                              "properties": {
                                "email": { "type": "string", "format": "email" },
                                "password": { "type": "string", "minLength": 8 }
                              }
                            }
                            """,
                        },
                    },
                    Responses = new List<ApiEndpointResponseDescriptorDto>
                    {
                        new()
                        {
                            StatusCode = 200,
                            Schema = """
                            {
                              "type": "object",
                              "properties": {
                                "token": { "type": "string" }
                              }
                            }
                            """,
                        },
                    },
                    ParameterSchemaPayloads = new List<string>
                    {
                        """
                        {
                          "type": "object",
                          "required": ["email", "password"],
                          "properties": {
                            "email": { "type": "string", "format": "email" },
                            "password": { "type": "string", "minLength": 8 }
                          }
                        }
                        """,
                    },
                    ResponseSchemaPayloads = new List<string>
                    {
                        """
                        {
                          "type": "object",
                          "properties": {
                            "token": { "type": "string" }
                          }
                        }
                        """,
                    },
                },
            },
            EndpointParameterDetails = new Dictionary<Guid, EndpointParameterDetailDto>
            {
                [EndpointId1] = new()
                {
                    EndpointId = EndpointId1,
                    EndpointPath = "/api/auth/login",
                    EndpointHttpMethod = "POST",
                    Parameters = new List<ParameterDetailDto>
                    {
                        new()
                        {
                            ParameterId = Guid.NewGuid(),
                            Name = "email",
                            Location = "Body",
                            DataType = "string",
                            Format = "email",
                            IsRequired = true,
                        },
                        new()
                        {
                            ParameterId = Guid.NewGuid(),
                            Name = "password",
                            Location = "Body",
                            DataType = "string",
                            IsRequired = true,
                            Schema = """
                            {
                              "type": "string",
                              "minLength": 8
                            }
                            """,
                        },
                    },
                },
            },
            OrderedEndpoints = new List<ApiOrderItemModel>
            {
                new() { EndpointId = EndpointId1, HttpMethod = "POST", Path = "/api/auth/login", OrderIndex = 0 },
            },
        };
    }

    private static LlmScenarioSuggestionContext CreateRequiredBodyCreatePetContext()
    {
        return new LlmScenarioSuggestionContext
        {
            TestSuiteId = TestSuiteId,
            UserId = UserId,
            SpecificationId = SpecificationId,
            Suite = new TestSuite
            {
                Id = TestSuiteId,
                Name = "Pet Create Contract Suite",
                EndpointBusinessContexts = new Dictionary<Guid, string>(),
            },
            EndpointMetadata = new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = EndpointId1,
                    HttpMethod = "POST",
                    Path = "/pet",
                    OperationId = "addPet",
                    HasRequiredRequestBody = true,
                    Parameters = new List<ApiEndpointParameterDescriptorDto>
                    {
                        new()
                        {
                            Name = "body",
                            Location = "Body",
                            IsRequired = true,
                            Schema = """
                            {
                              "type": "object",
                              "required": ["id", "name", "photoUrls"],
                              "properties": {
                                "id": { "type": "integer", "format": "int64" },
                                "name": { "type": "string" },
                                "photoUrls": {
                                  "type": "array",
                                  "items": { "type": "string" }
                                }
                              }
                            }
                            """,
                        },
                    },
                    ParameterSchemaPayloads = new List<string>
                    {
                        """
                        {
                          "type": "object",
                          "required": ["id", "name", "photoUrls"],
                          "properties": {
                            "id": { "type": "integer", "format": "int64" },
                            "name": { "type": "string" },
                            "photoUrls": {
                              "type": "array",
                              "items": { "type": "string" }
                            }
                          }
                        }
                        """,
                    },
                },
            },
            EndpointParameterDetails = new Dictionary<Guid, EndpointParameterDetailDto>
            {
                [EndpointId1] = new()
                {
                    EndpointId = EndpointId1,
                    EndpointPath = "/pet",
                    EndpointHttpMethod = "POST",
                    Parameters = new List<ParameterDetailDto>
                    {
                        new()
                        {
                            ParameterId = Guid.NewGuid(),
                            Name = "id",
                            Location = "Body",
                            DataType = "integer",
                            Format = "int64",
                            IsRequired = true,
                        },
                        new()
                        {
                            ParameterId = Guid.NewGuid(),
                            Name = "name",
                            Location = "Body",
                            DataType = "string",
                            IsRequired = true,
                        },
                    },
                },
            },
            OrderedEndpoints = new List<ApiOrderItemModel>
            {
                new() { EndpointId = EndpointId1, HttpMethod = "POST", Path = "/pet", OrderIndex = 0 },
            },
        };
    }

    private void SetupCacheHit(Guid endpointId, List<LlmSuggestedScenario> scenarios)
    {
        var json = JsonSerializer.Serialize(scenarios, JsonOpts);
        _llmGatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                endpointId, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedSuggestionsDto { HasCache = true, SuggestionsJson = json });
    }

    private void SetupCacheMiss(Guid endpointId)
    {
        _llmGatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                endpointId, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedSuggestionsDto { HasCache = false });
    }

    private void SetupAllCacheMiss()
    {
        _llmGatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                It.IsAny<Guid>(), 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedSuggestionsDto { HasCache = false });
    }

    private void SetupPromptBuilder(int promptCount)
    {
        var prompts = Enumerable.Range(0, promptCount)
            .Select(_ => new ObservationConfirmationPrompt
            {
                SystemPrompt = "You are a test engineer.",
                CombinedPrompt = "Observe and confirm constraints.",
                ObservationPrompt = "List all constraints.",
                ConfirmationPromptTemplate = "Confirm each constraint.",
            })
            .ToList();

        _promptBuilderMock
            .Setup(x => x.BuildForSequence(It.IsAny<IReadOnlyList<EndpointPromptContext>>()))
            .Returns(prompts);
    }

    private void SetupN8nReturnsScenarios()
    {
        var response = new N8nBoundaryNegativeResponse
        {
            Model = "gpt-4o",
            TokensUsed = 1200,
            Scenarios = new List<N8nSuggestedScenario>
            {
                new()
                {
                    EndpointId = EndpointId1,
                    ScenarioName = "Null email on login",
                    Description = "Send null email to login endpoint",
                    TestType = "Negative",
                    Priority = "High",
                    Tags = new List<string> { "auth", "negative" },
                },
                new()
                {
                    EndpointId = EndpointId2,
                    ScenarioName = "Invalid page number",
                    Description = "Request users with negative page index",
                    TestType = "Boundary",
                    Priority = "Medium",
                    Tags = new List<string> { "pagination", "boundary" },
                },
            },
        };

        _n8nServiceMock
            .Setup(x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                N8nWebhookNames.GenerateLlmSuggestions,
                It.IsAny<N8nBoundaryNegativePayload>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    #endregion
}
