using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.MessageBusMessages;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for GenerateLlmSuggestionPreviewCommandHandler.
/// Verifies input validation, ownership, gate check, subscription limits,
/// pending-suggestion guard, LLM call, supersede logic, entity persistence,
/// usage tracking, and result model construction.
/// </summary>
public class GenerateLlmSuggestionPreviewCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<LlmSuggestion, Guid>> _suggestionRepoMock;
    private readonly Mock<IRepository<TestGenerationJob, Guid>> _jobRepoMock;
    private readonly Mock<IRepository<SrsDocument, Guid>> _srsDocRepoMock;
    private readonly Mock<IRepository<SrsRequirement, Guid>> _srsReqRepoMock;
    private readonly Mock<IApiTestOrderGateService> _gateServiceMock;
    private readonly Mock<IApiEndpointMetadataService> _endpointMetadataServiceMock;
    private readonly Mock<IApiEndpointParameterDetailService> _endpointParameterDetailServiceMock;
    private readonly Mock<ILlmScenarioSuggester> _llmSuggesterMock;
    private readonly ILlmSuggestionPreviewPersistenceService _persistenceService;
    private readonly Mock<ISubscriptionLimitGatewayService> _subscriptionMock;
    private readonly Mock<IMessageBus> _messageBusMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly GenerateLlmSuggestionPreviewCommandHandler _handler;

    public GenerateLlmSuggestionPreviewCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _suggestionRepoMock = new Mock<IRepository<LlmSuggestion, Guid>>();
        _jobRepoMock = new Mock<IRepository<TestGenerationJob, Guid>>();
        _srsDocRepoMock = new Mock<IRepository<SrsDocument, Guid>>();
        _srsReqRepoMock = new Mock<IRepository<SrsRequirement, Guid>>();
        _gateServiceMock = new Mock<IApiTestOrderGateService>();
        _endpointMetadataServiceMock = new Mock<IApiEndpointMetadataService>();
        _endpointParameterDetailServiceMock = new Mock<IApiEndpointParameterDetailService>();
        _llmSuggesterMock = new Mock<ILlmScenarioSuggester>();
        _persistenceService = new LlmSuggestionPreviewPersistenceService(_suggestionRepoMock.Object);
        _subscriptionMock = new Mock<ISubscriptionLimitGatewayService>();
        _messageBusMock = new Mock<IMessageBus>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _suggestionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _jobRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _jobRepoMock.Setup(x => x.AddAsync(It.IsAny<TestGenerationJob>(), It.IsAny<CancellationToken>()))
            .Callback<TestGenerationJob, CancellationToken>((job, _) =>
            {
                if (job.Id == Guid.Empty)
                {
                    job.Id = Guid.NewGuid();
                }
            })
            .Returns(Task.CompletedTask);

        _llmSuggesterMock
            .Setup(x => x.BuildAsyncRefinementPayloadAsync(
                It.IsAny<LlmScenarioSuggestionContext>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new N8nBoundaryNegativePayload());

        _messageBusMock
            .Setup(x => x.SendAsync(
                It.IsAny<TriggerLlmSuggestionRefinementMessage>(),
                It.IsAny<MetaData>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default: no SRS document found
        _srsDocRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.IQueryable<SrsDocument>>()))
            .ReturnsAsync((SrsDocument)null);

        _handler = new GenerateLlmSuggestionPreviewCommandHandler(
            _suiteRepoMock.Object,
            _suggestionRepoMock.Object,
            _jobRepoMock.Object,
            _srsDocRepoMock.Object,
            _srsReqRepoMock.Object,
            _gateServiceMock.Object,
            _endpointMetadataServiceMock.Object,
            _endpointParameterDetailServiceMock.Object,
            _llmSuggesterMock.Object,
            _persistenceService,
            _subscriptionMock.Object,
            _messageBusMock.Object,
            Options.Create(new N8nIntegrationOptions
            {
                BeBaseUrl = "http://localhost:5099",
                CallbackApiKey = "secret",
            }),
            new Mock<ILogger<GenerateLlmSuggestionPreviewCommandHandler>>().Object);
    }

    // ───────────────────────────── Validation tests ─────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenTestSuiteIdEmpty()
    {
        var command = new GenerateLlmSuggestionPreviewCommand
        {
            TestSuiteId = Guid.Empty,
            SpecificationId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSpecificationIdEmpty()
    {
        var command = new GenerateLlmSuggestionPreviewCommand
        {
            TestSuiteId = Guid.NewGuid(),
            SpecificationId = Guid.Empty,
            CurrentUserId = Guid.NewGuid(),
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenSuiteDoesNotExist()
    {
        SetupSuiteNotFound();

        var command = CreateValidCommand();

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var command = CreateValidCommand();
        command.CurrentUserId = Guid.NewGuid(); // Different user

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSuiteArchived()
    {
        var suite = CreateSuite();
        suite.Status = TestSuiteStatus.Archived;
        SetupSuiteFound(suite);

        var command = CreateValidCommand();

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*archived*");
    }

    // ─────────────────────────── Business logic tests ───────────────────────────

    [Fact]
    public async Task HandleAsync_Should_CallGateService()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoPendingSuggestions();
        SetupLlmSuggesterReturnsEmpty();

        var command = CreateValidCommand();
        await _handler.HandleAsync(command);

        _gateServiceMock.Verify(
            x => x.RequireApprovedOrderAsync(command.TestSuiteId, It.IsAny<CancellationToken>()),
            Times.Once);

        _endpointMetadataServiceMock.Verify(
            x => x.GetEndpointMetadataAsync(command.SpecificationId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _endpointParameterDetailServiceMock.Verify(
            x => x.GetParameterDetailsAsync(command.SpecificationId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_PassMetadataAndParameterDetails_IntoLlmContext()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoPendingSuggestions();

        LlmScenarioSuggestionContext capturedContext = null;
        _llmSuggesterMock
            .Setup(x => x.SuggestLocalDraftAsync(It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()))
            .Callback<LlmScenarioSuggestionContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .ReturnsAsync(new LlmScenarioSuggestionResult
            {
                Scenarios = Array.Empty<LlmSuggestedScenario>(),
                LlmModel = "gpt-4",
                TokensUsed = 0,
            });

        var command = CreateValidCommand();
        await _handler.HandleAsync(command);

        capturedContext.Should().NotBeNull();
        capturedContext.EndpointMetadata.Should().NotBeNull();
        capturedContext.EndpointMetadata.Should().HaveCountGreaterThan(0);
        capturedContext.EndpointParameterDetails.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_KeepCacheLookup_WhenForceRefreshEnabled()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoPendingSuggestions();

        LlmScenarioSuggestionContext capturedContext = null;
        _llmSuggesterMock
            .Setup(x => x.SuggestLocalDraftAsync(It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()))
            .Callback<LlmScenarioSuggestionContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .ReturnsAsync(new LlmScenarioSuggestionResult
            {
                Scenarios = Array.Empty<LlmSuggestedScenario>(),
                LlmModel = "gpt-4",
                TokensUsed = 0,
            });

        var command = CreateValidCommand();
        command.ForceRefresh = true;

        await _handler.HandleAsync(command);

        capturedContext.Should().NotBeNull();
        capturedContext.BypassCache.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSubscriptionLimitExceeded()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionDenied();

        var command = CreateValidCommand();

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*LLM*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenPendingSuggestionsExist_AndNotForceRefresh()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupExistingPendingSuggestions(3);

        var command = CreateValidCommand();
        command.ForceRefresh = false;

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*ForceRefresh*");
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnEmptyResult_WhenNoScenariosGenerated()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoPendingSuggestions();
        SetupLlmSuggesterReturnsEmpty();

        var command = CreateValidCommand();
        await _handler.HandleAsync(command);

        command.Result.Should().NotBeNull();
        command.Result.TotalSuggestions.Should().Be(0);
        command.Result.Suggestions.Should().BeEmpty();
        command.Result.TestSuiteId.Should().Be(command.TestSuiteId);
    }

    // ────────────────────────────── Success path tests ──────────────────────────

    [Fact]
    public async Task HandleAsync_Should_SupersedeExistingSuggestions()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();

        // Set up existing pending suggestions that should be superseded
        var existingPending = Enumerable.Range(0, 2)
            .Select(_ => new LlmSuggestion
            {
                Id = Guid.NewGuid(),
                TestSuiteId = DefaultSuiteId,
                ReviewStatus = ReviewStatus.Pending,
                RowVersion = Guid.NewGuid().ToByteArray(),
            })
            .ToList();

        _suggestionRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(existingPending.AsQueryable());
        _suggestionRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<LlmSuggestion>>()))
            .ReturnsAsync(existingPending);

        var scenarios = CreateScenarios(1);
        SetupLlmSuggesterReturns(scenarios);

        var command = CreateValidCommand();
        command.ForceRefresh = true;
        await _handler.HandleAsync(command);

        // Verify existing pending suggestions were updated to Superseded
        _suggestionRepoMock.Verify(
            x => x.UpdateAsync(
                It.Is<LlmSuggestion>(s => s.ReviewStatus == ReviewStatus.Superseded),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_Should_PersistNewSuggestionEntities()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoPendingSuggestions();

        var scenarios = CreateScenarios(3);
        SetupLlmSuggesterReturns(scenarios);

        var command = CreateValidCommand();
        await _handler.HandleAsync(command);

        _suggestionRepoMock.Verify(
            x => x.AddAsync(It.IsAny<LlmSuggestion>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task HandleAsync_Should_PersistFullExpectedStatusList_FromScenario()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoPendingSuggestions();

        var scenario = new LlmSuggestedScenario
        {
            EndpointId = Guid.NewGuid(),
            ScenarioName = "Validation should reject request",
            Description = "Scenario with multiple acceptable error statuses",
            SuggestedTestType = TestType.Negative,
            ExpectedStatusCode = 400,
            ExpectedStatusCodes = new List<int> { 400, 422 },
            ExpectedBehavior = "validation",
        };

        LlmSuggestion persistedSuggestion = null;
        _suggestionRepoMock.Setup(x => x.AddAsync(It.IsAny<LlmSuggestion>(), It.IsAny<CancellationToken>()))
            .Callback<LlmSuggestion, CancellationToken>((suggestion, _) => persistedSuggestion = suggestion)
            .Returns(Task.CompletedTask);

        SetupLlmSuggesterReturns(new List<LlmSuggestedScenario> { scenario });

        var command = CreateValidCommand();
        await _handler.HandleAsync(command);

        persistedSuggestion.Should().NotBeNull();

        var expectation = JsonSerializer.Deserialize<N8nTestCaseExpectation>(
            persistedSuggestion.SuggestedExpectation,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        expectation.Should().NotBeNull();
        expectation.ExpectedStatus.Should().Equal(400, 422);
    }

    [Fact]
    public async Task HandleAsync_Should_NotIncrementLlmUsage_WhenReturningLocalDraft()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoPendingSuggestions();

        var scenarios = CreateScenarios(2);
        SetupLlmSuggesterReturns(scenarios, fromCache: false);

        var command = CreateValidCommand();
        await _handler.HandleAsync(command);

        _subscriptionMock.Verify(
            x => x.IncrementUsageAsync(
                It.Is<IncrementUsageRequest>(r =>
                    r.LimitType == LimitType.MaxLlmCallsPerMonth && r.IncrementValue == 1),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_NotIncrementLlmUsage_WhenFromCache()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoPendingSuggestions();

        var scenarios = CreateScenarios(2);
        SetupLlmSuggesterReturns(scenarios, fromCache: true);

        var command = CreateValidCommand();
        await _handler.HandleAsync(command);

        _subscriptionMock.Verify(
            x => x.IncrementUsageAsync(It.IsAny<IncrementUsageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_NotIncrementLlmUsage_WhenUsingLocalFallback()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoPendingSuggestions();

        var scenarios = CreateScenarios(2);
        SetupLlmSuggesterReturns(scenarios, fromCache: false, usedLocalFallback: true);

        var command = CreateValidCommand();
        await _handler.HandleAsync(command);

        _subscriptionMock.Verify(
            x => x.IncrementUsageAsync(It.IsAny<IncrementUsageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnCorrectResultModel()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoPendingSuggestions();

        var endpointId1 = Guid.NewGuid();
        var endpointId2 = Guid.NewGuid();
        var scenarios = new List<LlmSuggestedScenario>
        {
            new()
            {
                EndpointId = endpointId1,
                ScenarioName = "Scenario A",
                Description = "Test boundary A",
                SuggestedTestType = TestType.Boundary,
                ExpectedStatusCode = 400,
            },
            new()
            {
                EndpointId = endpointId2,
                ScenarioName = "Scenario B",
                Description = "Test negative B",
                SuggestedTestType = TestType.Negative,
                ExpectedStatusCode = 422,
            },
        };
        SetupLlmSuggesterReturns(scenarios, fromCache: false);

        var command = CreateValidCommand();
        await _handler.HandleAsync(command);

        command.Result.Should().NotBeNull();
        command.Result.TotalSuggestions.Should().Be(2);
        command.Result.TestSuiteId.Should().Be(DefaultSuiteId);
        command.Result.EndpointsCovered.Should().Be(2);
        command.Result.LlmModel.Should().Be("gpt-4");
        command.Result.LlmTokensUsed.Should().Be(1500);
        command.Result.FromCache.Should().BeFalse();
        command.Result.Suggestions.Should().HaveCount(2);
    }

    #region Helpers

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultSuiteId = Guid.NewGuid();
    private static readonly Guid DefaultSpecId = Guid.NewGuid();

    private static TestSuite CreateSuite()
    {
        return new TestSuite
        {
            Id = DefaultSuiteId,
            CreatedById = DefaultUserId,
            Status = TestSuiteStatus.Ready,
            ApprovalStatus = ApprovalStatus.Approved,
            Version = 1,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
    }

    private static GenerateLlmSuggestionPreviewCommand CreateValidCommand()
    {
        return new GenerateLlmSuggestionPreviewCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            SpecificationId = DefaultSpecId,
        };
    }

    private void SetupSuiteFound(TestSuite suite)
    {
        _suiteRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestSuite> { suite }.AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
    }

    private void SetupSuiteNotFound()
    {
        _suiteRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestSuite>().AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync((TestSuite)null);
    }

    private void SetupGateApproved()
    {
        var endpointId = Guid.NewGuid();
        _gateServiceMock.Setup(x => x.RequireApprovedOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiOrderItemModel>
            {
                new() { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 },
            });

        _endpointMetadataServiceMock
            .Setup(x => x.GetEndpointMetadataAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = endpointId,
                    HttpMethod = "POST",
                    Path = "/api/test",
                },
            });

        _endpointParameterDetailServiceMock
            .Setup(x => x.GetParameterDetailsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EndpointParameterDetailDto>
            {
                new()
                {
                    EndpointId = endpointId,
                    Parameters = new List<ParameterDetailDto>(),
                },
            });
    }

    private void SetupSubscriptionAllowed()
    {
        _subscriptionMock.Setup(x => x.CheckLimitAsync(
                It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });
    }

    private void SetupSubscriptionDenied()
    {
        _subscriptionMock.Setup(x => x.CheckLimitAsync(
                It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = false, DenialReason = "LLM monthly limit exceeded" });
    }

    private void SetupNoPendingSuggestions()
    {
        _suggestionRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<LlmSuggestion>().AsQueryable());
        _suggestionRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<LlmSuggestion>>()))
            .ReturnsAsync(new List<LlmSuggestion>());
    }

    private void SetupExistingPendingSuggestions(int count)
    {
        var existingList = Enumerable.Range(0, count)
            .Select(_ => new LlmSuggestion
            {
                Id = Guid.NewGuid(),
                TestSuiteId = DefaultSuiteId,
                ReviewStatus = ReviewStatus.Pending,
                RowVersion = Guid.NewGuid().ToByteArray(),
            })
            .ToList();

        _suggestionRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(existingList.AsQueryable());
        _suggestionRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<LlmSuggestion>>()))
            .ReturnsAsync(existingList);
    }

    private void SetupLlmSuggesterReturns(
        IReadOnlyList<LlmSuggestedScenario> scenarios,
        bool fromCache = false,
        bool usedLocalFallback = false)
    {
        _llmSuggesterMock.Setup(x => x.SuggestLocalDraftAsync(It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmScenarioSuggestionResult
            {
                Scenarios = scenarios,
                LlmModel = "gpt-4",
                TokensUsed = 1500,
                FromCache = fromCache,
                UsedLocalFallback = usedLocalFallback,
            });
    }

    private void SetupLlmSuggesterReturnsEmpty()
    {
        _llmSuggesterMock.Setup(x => x.SuggestLocalDraftAsync(It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmScenarioSuggestionResult
            {
                Scenarios = Array.Empty<LlmSuggestedScenario>(),
                LlmModel = "gpt-4",
                TokensUsed = 0,
                FromCache = false,
                UsedLocalFallback = false,
            });
    }

    private static List<LlmSuggestedScenario> CreateScenarios(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new LlmSuggestedScenario
            {
                EndpointId = Guid.NewGuid(),
                ScenarioName = $"Scenario {i}",
                Description = $"Description for scenario {i}",
                SuggestedTestType = i % 2 == 0 ? TestType.Boundary : TestType.Negative,
                ExpectedStatusCode = 400,
                ExpectedBehavior = "Bad request",
                Priority = "High",
                Tags = new List<string> { "llm-suggested" },
                Variables = new List<N8nTestCaseVariable>(),
            })
            .ToList();
    }

    #endregion
}
