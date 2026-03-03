using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.Services;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
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
        _loggerMock = new Mock<ILogger<LlmScenarioSuggester>>();

        _sut = new LlmScenarioSuggester(
            _promptBuilderMock.Object,
            _n8nServiceMock.Object,
            _llmGatewayServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SuggestScenariosAsync_Should_ReturnCachedResults_WhenCacheHit()
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
        result.Scenarios.Should().HaveCount(2);
        result.Scenarios[0].ScenarioName.Should().Be("Cached scenario 1");
        result.Scenarios[1].ScenarioName.Should().Be("Cached scenario 2");

        // n8n should NOT be called when cache hits
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
                "generate-boundary-negative",
                It.IsAny<N8nBoundaryNegativePayload>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
        result.Scenarios.Should().BeEmpty();
        result.FromCache.Should().BeFalse();
        result.LlmModel.Should().Be("gpt-4o");
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
    public async Task SuggestScenariosAsync_Should_SetFromCacheTrue_WhenCacheUsed()
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
        result.Scenarios.Should().HaveCount(2);
        result.LlmModel.Should().BeNull();
        result.TokensUsed.Should().BeNull();
        result.LatencyMs.Should().BeNull();

        // Verify no n8n call and no interaction saved
        _n8nServiceMock.Verify(
            x => x.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                It.IsAny<string>(), It.IsAny<N8nBoundaryNegativePayload>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _llmGatewayServiceMock.Verify(
            x => x.SaveInteractionAsync(It.IsAny<SaveLlmInteractionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

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
                },
                new()
                {
                    EndpointId = EndpointId2,
                    HttpMethod = "GET",
                    Path = "/api/users",
                    OperationId = "getUsers",
                    ParameterSchemaPayloads = new List<string>(),
                    ResponseSchemaPayloads = new List<string> { "{\"type\":\"array\"}" },
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
                "generate-boundary-negative",
                It.IsAny<N8nBoundaryNegativePayload>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    #endregion
}
