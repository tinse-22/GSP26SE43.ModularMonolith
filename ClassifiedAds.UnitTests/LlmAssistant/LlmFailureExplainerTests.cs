using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.Services;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.LlmAssistant.Models;
using ClassifiedAds.Modules.LlmAssistant.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.LlmAssistant;

public class LlmFailureExplainerTests
{
    private readonly Mock<IFailureExplanationSanitizer> _sanitizerMock;
    private readonly Mock<IFailureExplanationFingerprintBuilder> _fingerprintBuilderMock;
    private readonly Mock<IFailureExplanationPromptBuilder> _promptBuilderMock;
    private readonly Mock<ILlmFailureExplanationClient> _clientMock;
    private readonly Mock<ILlmAssistantGatewayService> _gatewayServiceMock;
    private readonly Mock<ILogger<LlmFailureExplainer>> _loggerMock;
    private readonly LlmFailureExplainer _explainer;

    private readonly TestFailureExplanationContextDto _rawContext;
    private readonly TestFailureExplanationContextDto _sanitizedContext;
    private readonly ApiEndpointMetadataDto _endpointMetadata;
    private readonly FailureExplanationPrompt _prompt;

    public LlmFailureExplainerTests()
    {
        _sanitizerMock = new Mock<IFailureExplanationSanitizer>();
        _fingerprintBuilderMock = new Mock<IFailureExplanationFingerprintBuilder>();
        _promptBuilderMock = new Mock<IFailureExplanationPromptBuilder>();
        _clientMock = new Mock<ILlmFailureExplanationClient>();
        _gatewayServiceMock = new Mock<ILlmAssistantGatewayService>();
        _loggerMock = new Mock<ILogger<LlmFailureExplainer>>();

        _rawContext = FailureExplanationTestData.CreateContext();
        _sanitizedContext = new FailureExplanationSanitizer().Sanitize(_rawContext);
        _endpointMetadata = FailureExplanationTestData.CreateEndpointMetadata(_sanitizedContext.Definition.EndpointId);
        _prompt = FailureExplanationTestData.CreatePrompt(_sanitizedContext, _endpointMetadata);

        _sanitizerMock
            .Setup(x => x.Sanitize(It.IsAny<TestFailureExplanationContextDto>()))
            .Returns(_sanitizedContext);
        _fingerprintBuilderMock
            .Setup(x => x.Build(It.IsAny<TestFailureExplanationContextDto>()))
            .Returns("fingerprint-123");
        _promptBuilderMock
            .Setup(x => x.Build(_sanitizedContext, _endpointMetadata))
            .Returns(_prompt);

        _explainer = new LlmFailureExplainer(
            _sanitizerMock.Object,
            _fingerprintBuilderMock.Object,
            _promptBuilderMock.Object,
            _clientMock.Object,
            _gatewayServiceMock.Object,
            FailureExplanationTestData.CreateOptions(),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExplainAsync_CacheHit_ShouldSkipProvider()
    {
        // Arrange
        var cachedPayload = JsonSerializer.Serialize(new
        {
            summaryVi = "Cached summary",
            possibleCauses = new[] { "Cached cause" },
            suggestedNextActions = new[] { "Cached action" },
            confidence = "Medium",
            provider = "N8n",
            model = "gpt-4.1-mini",
            tokensUsed = 12,
            generatedAt = "2026-03-15T12:05:00+00:00",
            failureCodes = new[] { "STATUS_CODE_MISMATCH" },
        });

        _gatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                _sanitizedContext.Definition.EndpointId!.Value,
                4,
                "fingerprint-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedSuggestionsDto
            {
                HasCache = true,
                SuggestionsJson = cachedPayload,
            });

        // Act
        var result = await _explainer.ExplainAsync(_rawContext, _endpointMetadata);

        // Assert
        result.Source.Should().Be("cache");
        result.SummaryVi.Should().Be("Cached summary");
        _promptBuilderMock.Verify(
            x => x.Build(It.IsAny<TestFailureExplanationContextDto>(), It.IsAny<ApiEndpointMetadataDto>()),
            Times.Never);
        _clientMock.Verify(x => x.ExplainAsync(It.IsAny<FailureExplanationPrompt>(), It.IsAny<CancellationToken>()), Times.Never);
        _gatewayServiceMock.Verify(x => x.SaveInteractionAsync(It.IsAny<SaveLlmInteractionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _gatewayServiceMock.Verify(x => x.CacheSuggestionsAsync(
            It.IsAny<Guid>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExplainAsync_CacheMiss_ShouldCallProvider()
    {
        // Arrange
        _gatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                _sanitizedContext.Definition.EndpointId!.Value,
                4,
                "fingerprint-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedSuggestionsDto
            {
                HasCache = false,
            });
        _clientMock
            .Setup(x => x.ExplainAsync(_prompt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FailureExplanationTestData.CreateProviderResponse());

        // Act
        var result = await _explainer.ExplainAsync(_rawContext, _endpointMetadata);

        // Assert
        result.Source.Should().Be("live");
        result.SummaryVi.Should().Be("Loi do backend tra ve 500.");
        _promptBuilderMock.Verify(x => x.Build(_sanitizedContext, _endpointMetadata), Times.Once);
        _clientMock.Verify(x => x.ExplainAsync(_prompt, It.IsAny<CancellationToken>()), Times.Once);
        _gatewayServiceMock.Verify(x => x.SaveInteractionAsync(
            It.Is<SaveLlmInteractionRequest>(request =>
                request.UserId == _rawContext.CreatedById
                && request.InteractionType == 1
                && request.ModelUsed == "gpt-4.1-mini"),
            It.IsAny<CancellationToken>()), Times.Once);
        _gatewayServiceMock.Verify(x => x.CacheSuggestionsAsync(
            _sanitizedContext.Definition.EndpointId!.Value,
            4,
            "fingerprint-123",
            It.IsAny<string>(),
            It.Is<TimeSpan>(ttl => ttl == TimeSpan.FromHours(24)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExplainAsync_AuditFailure_ShouldReturnExplanationGracefully()
    {
        // Arrange
        _gatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                _sanitizedContext.Definition.EndpointId!.Value,
                4,
                "fingerprint-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedSuggestionsDto
            {
                HasCache = false,
            });
        _clientMock
            .Setup(x => x.ExplainAsync(_prompt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FailureExplanationTestData.CreateProviderResponse());
        _gatewayServiceMock
            .Setup(x => x.SaveInteractionAsync(It.IsAny<SaveLlmInteractionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Audit write failed."));

        // Act
        var result = await _explainer.ExplainAsync(_rawContext, _endpointMetadata);

        // Assert
        result.Source.Should().Be("live");
        _gatewayServiceMock.Verify(x => x.CacheSuggestionsAsync(
            _sanitizedContext.Definition.EndpointId!.Value,
            4,
            "fingerprint-123",
            It.IsAny<string>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExplainAsync_CacheSaveFailure_ShouldReturnExplanationGracefully()
    {
        // Arrange
        _gatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                _sanitizedContext.Definition.EndpointId!.Value,
                4,
                "fingerprint-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedSuggestionsDto
            {
                HasCache = false,
            });
        _clientMock
            .Setup(x => x.ExplainAsync(_prompt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FailureExplanationTestData.CreateProviderResponse());
        _gatewayServiceMock
            .Setup(x => x.CacheSuggestionsAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache write failed."));

        // Act
        var result = await _explainer.ExplainAsync(_rawContext, _endpointMetadata);

        // Assert
        result.Source.Should().Be("live");
        _gatewayServiceMock.Verify(x => x.SaveInteractionAsync(It.IsAny<SaveLlmInteractionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExplainAsync_InvalidProviderPayload_ShouldThrowControlledError()
    {
        // Arrange
        _gatewayServiceMock
            .Setup(x => x.GetCachedSuggestionsAsync(
                _sanitizedContext.Definition.EndpointId!.Value,
                4,
                "fingerprint-123",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CachedSuggestionsDto
            {
                HasCache = false,
            });
        _clientMock
            .Setup(x => x.ExplainAsync(_prompt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FailureExplanationProviderResponse
            {
                SummaryVi = null,
                PossibleCauses = Array.Empty<string>(),
                SuggestedNextActions = Array.Empty<string>(),
                Confidence = "Low",
                Model = "gpt-4.1-mini",
                TokensUsed = 5,
            });

        // Act
        var act = () => _explainer.ExplainAsync(_rawContext, _endpointMetadata);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.Should().StartWith("FAILURE_EXPLANATION_PROVIDER_INVALID_JSON:");
        _gatewayServiceMock.Verify(x => x.SaveInteractionAsync(It.IsAny<SaveLlmInteractionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _gatewayServiceMock.Verify(x => x.CacheSuggestionsAsync(
            It.IsAny<Guid>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
