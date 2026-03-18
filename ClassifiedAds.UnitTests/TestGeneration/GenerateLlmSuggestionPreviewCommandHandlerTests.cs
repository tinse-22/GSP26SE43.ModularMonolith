using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Mock<IApiTestOrderGateService> _gateServiceMock;
    private readonly Mock<ILlmScenarioSuggester> _llmSuggesterMock;
    private readonly Mock<ISubscriptionLimitGatewayService> _subscriptionMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly GenerateLlmSuggestionPreviewCommandHandler _handler;

    public GenerateLlmSuggestionPreviewCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _suggestionRepoMock = new Mock<IRepository<LlmSuggestion, Guid>>();
        _gateServiceMock = new Mock<IApiTestOrderGateService>();
        _llmSuggesterMock = new Mock<ILlmScenarioSuggester>();
        _subscriptionMock = new Mock<ISubscriptionLimitGatewayService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _suggestionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _handler = new GenerateLlmSuggestionPreviewCommandHandler(
            _suiteRepoMock.Object,
            _suggestionRepoMock.Object,
            _gateServiceMock.Object,
            _llmSuggesterMock.Object,
            _subscriptionMock.Object,
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
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_IncrementLlmUsage_WhenNotFromCache()
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
            Times.Once);
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
        _gateServiceMock.Setup(x => x.RequireApprovedOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiOrderItemModel>
            {
                new() { EndpointId = Guid.NewGuid(), HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 },
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

    private void SetupLlmSuggesterReturns(IReadOnlyList<LlmSuggestedScenario> scenarios, bool fromCache = false)
    {
        _llmSuggesterMock.Setup(x => x.SuggestScenariosAsync(It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmScenarioSuggestionResult
            {
                Scenarios = scenarios,
                LlmModel = "gpt-4",
                TokensUsed = 1500,
                FromCache = fromCache,
            });
    }

    private void SetupLlmSuggesterReturnsEmpty()
    {
        _llmSuggesterMock.Setup(x => x.SuggestScenariosAsync(It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmScenarioSuggestionResult
            {
                Scenarios = Array.Empty<LlmSuggestedScenario>(),
                LlmModel = "gpt-4",
                TokensUsed = 0,
                FromCache = false,
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
