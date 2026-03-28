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
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class BulkReviewLlmSuggestionsCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<LlmSuggestion, Guid>> _suggestionRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<TestCaseRequest, Guid>> _requestRepoMock;
    private readonly Mock<IRepository<TestCaseExpectation, Guid>> _expectationRepoMock;
    private readonly Mock<IRepository<TestCaseVariable, Guid>> _variableRepoMock;
    private readonly Mock<IRepository<TestCaseChangeLog, Guid>> _changeLogRepoMock;
    private readonly Mock<IRepository<TestSuiteVersion, Guid>> _versionRepoMock;
    private readonly Mock<ILlmSuggestionMaterializer> _materializerMock;
    private readonly Mock<IApiTestOrderGateService> _gateServiceMock;
    private readonly Mock<ISubscriptionLimitGatewayService> _subscriptionMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly BulkReviewLlmSuggestionsCommandHandler _handler;

    public BulkReviewLlmSuggestionsCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _suggestionRepoMock = new Mock<IRepository<LlmSuggestion, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _requestRepoMock = new Mock<IRepository<TestCaseRequest, Guid>>();
        _expectationRepoMock = new Mock<IRepository<TestCaseExpectation, Guid>>();
        _variableRepoMock = new Mock<IRepository<TestCaseVariable, Guid>>();
        _changeLogRepoMock = new Mock<IRepository<TestCaseChangeLog, Guid>>();
        _versionRepoMock = new Mock<IRepository<TestSuiteVersion, Guid>>();
        _materializerMock = new Mock<ILlmSuggestionMaterializer>();
        _gateServiceMock = new Mock<IApiTestOrderGateService>();
        _subscriptionMock = new Mock<ISubscriptionLimitGatewayService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _suggestionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _suggestionRepoMock.Setup(x => x.IsDbUpdateConcurrencyException(It.IsAny<Exception>())).Returns(false);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _suggestionRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<LlmSuggestion>>()))
            .ReturnsAsync((IQueryable<LlmSuggestion> query) => query.ToList());

        _materializerMock.Setup(x => x.MaterializeFromSuggestion(
                It.IsAny<LlmSuggestion>(),
                It.IsAny<ApiOrderItemModel>(),
                It.IsAny<int>()))
            .Returns<LlmSuggestion, ApiOrderItemModel, int>((suggestion, _, orderIndex) => new TestCase
            {
                Id = Guid.NewGuid(),
                TestSuiteId = suggestion.TestSuiteId,
                EndpointId = suggestion.EndpointId,
                Name = suggestion.SuggestedName,
                TestType = suggestion.TestType,
                Priority = suggestion.Priority,
                Version = 1,
                OrderIndex = orderIndex,
                Request = new TestCaseRequest { Id = Guid.NewGuid() },
                Expectation = new TestCaseExpectation { Id = Guid.NewGuid() },
                Variables = new List<TestCaseVariable>(),
            });

        var reviewService = new LlmSuggestionReviewService(
            _suiteRepoMock.Object,
            _suggestionRepoMock.Object,
            _testCaseRepoMock.Object,
            _requestRepoMock.Object,
            _expectationRepoMock.Object,
            _variableRepoMock.Object,
            _changeLogRepoMock.Object,
            _versionRepoMock.Object,
            _materializerMock.Object,
            _gateServiceMock.Object,
            _subscriptionMock.Object,
            new Mock<ILogger<LlmSuggestionReviewService>>().Object);

        _handler = new BulkReviewLlmSuggestionsCommandHandler(
            _suiteRepoMock.Object,
            _suggestionRepoMock.Object,
            reviewService,
            new Mock<ILogger<BulkReviewLlmSuggestionsCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ApproveMatchingPendingSuggestions_AndMaterializeTestCases()
    {
        var suite = CreateSuite();
        var suggestions = new List<LlmSuggestion>
        {
            CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Negative, displayOrder: 0),
            CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Boundary, displayOrder: 1),
        };

        SetupSuiteFound(suite);
        SetupSuggestions(suggestions);
        SetupSubscriptionAllowed();
        SetupGateApproved();
        SetupNoExistingTestCases();

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = suite.Id,
            CurrentUserId = DefaultUserId,
            Action = "Approve",
            FilterBySuggestionType = "BoundaryNegative",
        };

        await _handler.HandleAsync(command);

        suggestions.Should().OnlyContain(x => x.ReviewStatus == ReviewStatus.Approved);
        command.Result.MatchedCount.Should().Be(2);
        command.Result.ProcessedCount.Should().Be(2);
        command.Result.MaterializedCount.Should().Be(2);
        command.Result.AppliedTestCaseIds.Should().HaveCount(2);
        _testCaseRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _versionRepoMock.Verify(x => x.AddAsync(It.IsAny<TestSuiteVersion>(), It.IsAny<CancellationToken>()), Times.Once);
        _subscriptionMock.Verify(x => x.IncrementUsageAsync(
            It.Is<IncrementUsageRequest>(r =>
                r.UserId == DefaultUserId &&
                r.LimitType == LimitType.MaxTestCasesPerSuite &&
                r.IncrementValue == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_RejectMatchingPendingSuggestions_WithoutMaterializing()
    {
        var suite = CreateSuite();
        var suggestions = new List<LlmSuggestion>
        {
            CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Negative, displayOrder: 0),
            CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Negative, displayOrder: 1),
        };

        SetupSuiteFound(suite);
        SetupSuggestions(suggestions);

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = suite.Id,
            CurrentUserId = DefaultUserId,
            Action = "Reject",
            ReviewNotes = "Not useful for this suite",
            FilterByTestType = "Negative",
        };

        await _handler.HandleAsync(command);

        suggestions.Should().OnlyContain(x => x.ReviewStatus == ReviewStatus.Rejected);
        command.Result.MatchedCount.Should().Be(2);
        command.Result.MaterializedCount.Should().Be(0);
        _testCaseRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()), Times.Never);
        _versionRepoMock.Verify(x => x.AddAsync(It.IsAny<TestSuiteVersion>(), It.IsAny<CancellationToken>()), Times.Never);
        _subscriptionMock.Verify(x => x.IncrementUsageAsync(It.IsAny<IncrementUsageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_OnlyProcessSuggestionsMatchingFilters()
    {
        var endpointA = Guid.NewGuid();
        var endpointB = Guid.NewGuid();
        var suite = CreateSuite();
        var negativeSuggestion = CreatePendingSuggestion(endpointA, TestType.Negative, displayOrder: 0);
        var boundarySuggestion = CreatePendingSuggestion(endpointB, TestType.Boundary, displayOrder: 1);

        SetupSuiteFound(suite);
        SetupSuggestions(new List<LlmSuggestion> { negativeSuggestion, boundarySuggestion });
        SetupSubscriptionAllowed();
        SetupGateApproved();
        SetupNoExistingTestCases();

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = suite.Id,
            CurrentUserId = DefaultUserId,
            Action = "Approve",
            FilterByTestType = "Boundary",
            FilterByEndpointId = endpointB,
        };

        await _handler.HandleAsync(command);

        negativeSuggestion.ReviewStatus.Should().Be(ReviewStatus.Pending);
        boundarySuggestion.ReviewStatus.Should().Be(ReviewStatus.Approved);
        command.Result.MatchedCount.Should().Be(1);
        command.Result.SuggestionIds.Should().ContainSingle().Which.Should().Be(boundarySuggestion.Id);
    }

    [Fact]
    public async Task HandleAsync_Should_IgnoreSuggestionsThatAreNotPending()
    {
        var suite = CreateSuite();
        var pendingSuggestion = CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Negative, displayOrder: 1);
        var approvedSuggestion = CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Negative, displayOrder: 0);
        approvedSuggestion.ReviewStatus = ReviewStatus.Approved;

        SetupSuiteFound(suite);
        SetupSuggestions(new List<LlmSuggestion> { approvedSuggestion, pendingSuggestion });
        SetupSubscriptionAllowed();
        SetupGateApproved();
        SetupNoExistingTestCases();

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = suite.Id,
            CurrentUserId = DefaultUserId,
            Action = "Approve",
        };

        await _handler.HandleAsync(command);

        approvedSuggestion.ReviewStatus.Should().Be(ReviewStatus.Approved);
        pendingSuggestion.ReviewStatus.Should().Be(ReviewStatus.Approved);
        command.Result.MatchedCount.Should().Be(1);
        command.Result.ProcessedCount.Should().Be(1);
        command.Result.SuggestionIds.Should().Equal(pendingSuggestion.Id);
        _testCaseRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_AppendAfterHighestExistingOrderIndex_WhenApprovingBatch()
    {
        var suite = CreateSuite();
        var suggestions = new List<LlmSuggestion>
        {
            CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Negative, displayOrder: 0),
            CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Boundary, displayOrder: 1),
        };
        var addedOrderIndexes = new List<int>();

        SetupSuiteFound(suite);
        SetupSuggestions(suggestions);
        SetupSubscriptionAllowed();
        SetupGateApproved();
        SetupExistingTestCases(
            new TestCase { Id = Guid.NewGuid(), TestSuiteId = suite.Id, OrderIndex = 2 },
            new TestCase { Id = Guid.NewGuid(), TestSuiteId = suite.Id, OrderIndex = 7 });

        _testCaseRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()))
            .Callback<TestCase, CancellationToken>((testCase, _) => addedOrderIndexes.Add(testCase.OrderIndex));

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = suite.Id,
            CurrentUserId = DefaultUserId,
            Action = "Approve",
        };

        await _handler.HandleAsync(command);

        addedOrderIndexes.Should().Equal(8, 9);
    }

    [Fact]
    public async Task HandleAsync_Should_CreateSuiteSnapshotContainingExistingAndNewTestCases()
    {
        var suite = CreateSuite();
        var existingFirst = new TestCase
        {
            Id = Guid.NewGuid(),
            TestSuiteId = suite.Id,
            Name = "Existing A",
            EndpointId = Guid.NewGuid(),
            OrderIndex = 1,
        };
        var existingSecond = new TestCase
        {
            Id = Guid.NewGuid(),
            TestSuiteId = suite.Id,
            Name = "Existing B",
            EndpointId = Guid.NewGuid(),
            OrderIndex = 4,
        };
        var suggestions = new List<LlmSuggestion>
        {
            CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Negative, displayOrder: 0),
            CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Boundary, displayOrder: 1),
        };
        TestSuiteVersion? capturedVersion = null;

        SetupSuiteFound(suite);
        SetupSuggestions(suggestions);
        SetupSubscriptionAllowed();
        SetupGateApproved();
        SetupExistingTestCases(existingFirst, existingSecond);

        _versionRepoMock.Setup(x => x.AddAsync(It.IsAny<TestSuiteVersion>(), It.IsAny<CancellationToken>()))
            .Callback<TestSuiteVersion, CancellationToken>((version, _) => capturedVersion = version);

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = suite.Id,
            CurrentUserId = DefaultUserId,
            Action = "Approve",
        };

        await _handler.HandleAsync(command);

        capturedVersion.Should().NotBeNull();
        capturedVersion!.TestCaseOrderSnapshot.Should().Contain("Existing A");
        capturedVersion.TestCaseOrderSnapshot.Should().Contain("Existing B");
        capturedVersion.TestCaseOrderSnapshot.Should().Contain("Suggestion 0");
        capturedVersion.TestCaseOrderSnapshot.Should().Contain("Suggestion 1");
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnZeroCounts_WhenNoSuggestionsMatch()
    {
        var suite = CreateSuite();
        var suggestions = new List<LlmSuggestion>
        {
            CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Negative, displayOrder: 0),
        };

        SetupSuiteFound(suite);
        SetupSuggestions(suggestions);

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = suite.Id,
            CurrentUserId = DefaultUserId,
            Action = "Approve",
            FilterByTestType = "Boundary",
        };

        await _handler.HandleAsync(command);

        command.Result.MatchedCount.Should().Be(0);
        command.Result.ProcessedCount.Should().Be(0);
        command.Result.MaterializedCount.Should().Be(0);
        command.Result.SuggestionIds.Should().BeEmpty();
        command.Result.AppliedTestCaseIds.Should().BeEmpty();
        _testCaseRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSuggestionTypeFilterInvalid()
    {
        SetupSuiteFound(CreateSuite());

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            Action = "Approve",
            FilterBySuggestionType = "InvalidType",
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*FilterBySuggestionType*");
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnProcessedSuggestionIdsAndAppliedTestCaseIds()
    {
        var suite = CreateSuite();
        var firstSuggestion = CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Negative, displayOrder: 0);
        var secondSuggestion = CreatePendingSuggestion(endpointId: Guid.NewGuid(), testType: TestType.Boundary, displayOrder: 1);
        var addedTestCaseIds = new List<Guid>();

        SetupSuiteFound(suite);
        SetupSuggestions(new List<LlmSuggestion> { firstSuggestion, secondSuggestion });
        SetupSubscriptionAllowed();
        SetupGateApproved();
        SetupNoExistingTestCases();

        _testCaseRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()))
            .Callback<TestCase, CancellationToken>((testCase, _) => addedTestCaseIds.Add(testCase.Id));

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = suite.Id,
            CurrentUserId = DefaultUserId,
            Action = "Approve",
        };

        await _handler.HandleAsync(command);

        command.Result.SuggestionIds.Should().Equal(firstSuggestion.Id, secondSuggestion.Id);
        command.Result.AppliedTestCaseIds.Should().Equal(addedTestCaseIds);
        command.Result.MaterializedCount.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenActionInvalid()
    {
        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            Action = "Modify",
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Action*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenTestTypeFilterInvalid()
    {
        SetupSuiteFound(CreateSuite());

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            Action = "Approve",
            FilterByTestType = "InvalidTestType",
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*FilterByTestType*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenRejectWithoutReviewNotes()
    {
        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            Action = "Reject",
            ReviewNotes = "   ",
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*ReviewNotes*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenSuiteMissing()
    {
        SetupSuiteNotFound();

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            Action = "Approve",
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenCurrentUserIsNotSuiteOwner()
    {
        SetupSuiteFound(CreateSuite());

        var command = new BulkReviewLlmSuggestionsCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = Guid.NewGuid(),
            Action = "Approve",
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*chủ sở hữu*");
    }

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultSuiteId = Guid.NewGuid();

    private static TestSuite CreateSuite() => new()
    {
        Id = DefaultSuiteId,
        CreatedById = DefaultUserId,
        Status = TestSuiteStatus.Ready,
        ApprovalStatus = ApprovalStatus.Approved,
        Version = 1,
        RowVersion = Guid.NewGuid().ToByteArray(),
    };

    private static LlmSuggestion CreatePendingSuggestion(Guid endpointId, TestType testType, int displayOrder) => new()
    {
        Id = Guid.NewGuid(),
        TestSuiteId = DefaultSuiteId,
        EndpointId = endpointId,
        SuggestedName = $"Suggestion {displayOrder}",
        SuggestedDescription = "Test description",
        TestType = testType,
        Priority = TestPriority.High,
        SuggestionType = LlmSuggestionType.BoundaryNegative,
        ReviewStatus = ReviewStatus.Pending,
        DisplayOrder = displayOrder,
        SuggestedRequest = "{}",
        SuggestedExpectation = "{}",
        SuggestedTags = "[\"llm-suggested\"]",
        RowVersion = Guid.NewGuid().ToByteArray(),
        CreatedDateTime = DateTimeOffset.UtcNow,
    };

    private void SetupSuiteFound(TestSuite suite)
    {
        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite> { suite }.AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>())).ReturnsAsync(suite);
    }

    private void SetupSuiteNotFound()
    {
        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite>().AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>())).ReturnsAsync((TestSuite)null);
    }

    private void SetupSuggestions(List<LlmSuggestion> suggestions)
    {
        _suggestionRepoMock.Setup(x => x.GetQueryableSet()).Returns(suggestions.AsQueryable());
    }

    private void SetupSubscriptionAllowed()
    {
        _subscriptionMock.Setup(x => x.CheckLimitAsync(
                It.IsAny<Guid>(),
                LimitType.MaxTestCasesPerSuite,
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });
    }

    private void SetupGateApproved()
    {
        _gateServiceMock.Setup(x => x.RequireApprovedOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiOrderItemModel>());
    }

    private void SetupNoExistingTestCases()
    {
        SetupExistingTestCases();
    }

    private void SetupExistingTestCases(params TestCase[] testCases)
    {
        var existingCases = testCases.ToList();
        _testCaseRepoMock.Setup(x => x.GetQueryableSet()).Returns(existingCases.AsQueryable());
        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>())).ReturnsAsync(existingCases);
    }
}
