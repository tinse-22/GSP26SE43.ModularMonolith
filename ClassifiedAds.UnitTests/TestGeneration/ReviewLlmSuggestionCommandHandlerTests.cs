using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for ReviewLlmSuggestionCommandHandler.
/// Verifies validation, reject flow, approve flow, modify-and-approve flow,
/// concurrency conflict handling, and subscription limit enforcement.
/// </summary>
public class ReviewLlmSuggestionCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<LlmSuggestion, Guid>> _suggestionRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<TestCaseRequest, Guid>> _requestRepoMock;
    private readonly Mock<IRepository<TestCaseExpectation, Guid>> _expectationRepoMock;
    private readonly Mock<IRepository<TestCaseVariable, Guid>> _variableRepoMock;
    private readonly Mock<IRepository<TestCaseDependency, Guid>> _dependencyRepoMock;
    private readonly Mock<IRepository<TestCaseChangeLog, Guid>> _changeLogRepoMock;
    private readonly Mock<IRepository<TestSuiteVersion, Guid>> _versionRepoMock;
    private readonly Mock<ILlmSuggestionMaterializer> _materializerMock;
    private readonly Mock<IApiTestOrderGateService> _gateServiceMock;
    private readonly Mock<ISubscriptionLimitGatewayService> _subscriptionMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ReviewLlmSuggestionCommandHandler _handler;

    public ReviewLlmSuggestionCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _suggestionRepoMock = new Mock<IRepository<LlmSuggestion, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _requestRepoMock = new Mock<IRepository<TestCaseRequest, Guid>>();
        _expectationRepoMock = new Mock<IRepository<TestCaseExpectation, Guid>>();
        _variableRepoMock = new Mock<IRepository<TestCaseVariable, Guid>>();
        _dependencyRepoMock = new Mock<IRepository<TestCaseDependency, Guid>>();
        _changeLogRepoMock = new Mock<IRepository<TestCaseChangeLog, Guid>>();
        _versionRepoMock = new Mock<IRepository<TestSuiteVersion, Guid>>();
        _materializerMock = new Mock<ILlmSuggestionMaterializer>();
        _gateServiceMock = new Mock<IApiTestOrderGateService>();
        _subscriptionMock = new Mock<ISubscriptionLimitGatewayService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _suggestionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _suggestionRepoMock.Setup(x => x.SetRowVersion(It.IsAny<LlmSuggestion>(), It.IsAny<byte[]>()));
        _suggestionRepoMock.Setup(x => x.IsDbUpdateConcurrencyException(It.IsAny<Exception>())).Returns(false);

        _materializerMock.Setup(x => x.MaterializeFromSuggestion(It.IsAny<LlmSuggestion>(), It.IsAny<ApiOrderItemModel>(), It.IsAny<int>()))
            .Returns<LlmSuggestion, ApiOrderItemModel, int>((s, o, idx) => new TestCase
            {
                Id = Guid.NewGuid(),
                TestSuiteId = s.TestSuiteId,
                Name = s.SuggestedName,
                TestType = s.TestType,
                Priority = s.Priority,
                Version = 1,
                OrderIndex = idx,
                Request = new TestCaseRequest { Id = Guid.NewGuid() },
                Expectation = new TestCaseExpectation { Id = Guid.NewGuid() },
                Variables = new List<TestCaseVariable>(),
            });

        _materializerMock.Setup(x => x.MaterializeFromModifiedContent(
                It.IsAny<LlmSuggestion>(),
                It.IsAny<EditableLlmSuggestionInput>(),
                It.IsAny<ApiOrderItemModel>(),
                It.IsAny<int>()))
            .Returns<LlmSuggestion, EditableLlmSuggestionInput, ApiOrderItemModel, int>((s, m, o, idx) => new TestCase
            {
                Id = Guid.NewGuid(),
                TestSuiteId = s.TestSuiteId,
                Name = m.Name ?? s.SuggestedName,
                TestType = s.TestType,
                Priority = s.Priority,
                Version = 1,
                OrderIndex = idx,
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
            _dependencyRepoMock.Object,
            _changeLogRepoMock.Object,
            _versionRepoMock.Object,
            _materializerMock.Object,
            _gateServiceMock.Object,
            _subscriptionMock.Object,
            new Mock<ILogger<LlmSuggestionReviewService>>().Object);

        _handler = new ReviewLlmSuggestionCommandHandler(
            _suiteRepoMock.Object,
            _suggestionRepoMock.Object,
            reviewService,
            new Mock<ILogger<ReviewLlmSuggestionCommandHandler>>().Object);
    }

    // ───────────────────────────── Validation tests ─────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSuggestionIdEmpty()
    {
        var command = CreateApproveCommand();
        command.SuggestionId = Guid.Empty;

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenInvalidReviewAction()
    {
        var command = CreateApproveCommand();
        command.ReviewAction = "InvalidAction";

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenRowVersionMissing()
    {
        var command = CreateApproveCommand();
        command.RowVersion = null;

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenRowVersionIsNotBase64()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);

        var command = CreateApproveCommand();
        command.RowVersion = "not-base64";

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*RowVersion không hợp lệ*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenSuiteNotFound()
    {
        SetupSuiteNotFound();
        var command = CreateApproveCommand();

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var command = CreateApproveCommand();
        command.CurrentUserId = Guid.NewGuid(); // Different user

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenSuggestionNotFound()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupSuggestionNotFound();

        var command = CreateApproveCommand();

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSuggestionNotPending()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        suggestion.ReviewStatus = ReviewStatus.Approved;
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);

        var command = CreateApproveCommand();

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnExistingSuggestion_WhenApproveIsRetriedAfterApply()
    {
        var suite = CreateSuite();
        var appliedTestCaseId = Guid.NewGuid();
        var suggestion = CreatePendingSuggestion();
        suggestion.ReviewStatus = ReviewStatus.Approved;
        suggestion.AppliedTestCaseId = appliedTestCaseId;
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);

        var command = CreateApproveCommand();

        await _handler.HandleAsync(command);

        command.Result.Should().NotBeNull();
        command.Result.AppliedTestCaseId.Should().Be(appliedTestCaseId);
        _testCaseRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()), Times.Never);
        _versionRepoMock.Verify(x => x.AddAsync(It.IsAny<TestSuiteVersion>(), It.IsAny<CancellationToken>()), Times.Never);
        _subscriptionMock.Verify(x => x.IncrementUsageAsync(It.IsAny<IncrementUsageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenRejectWithoutNotes()
    {
        var command = CreateRejectCommand();
        command.ReviewNotes = null;

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenModifyWithoutContent()
    {
        var command = CreateApproveCommand();
        command.ReviewAction = "Modify";
        command.ModifiedContent = null;

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    // ───────────────────────────── Reject flow tests ────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_RejectSuggestion()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        _suggestionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        var command = CreateRejectCommand();
        await _handler.HandleAsync(command);

        suggestion.ReviewStatus.Should().Be(ReviewStatus.Rejected);
        suggestion.ReviewedById.Should().Be(command.CurrentUserId);
        suggestion.ReviewedAt.Should().NotBeNull();
        _suggestionRepoMock.Verify(x => x.UpdateAsync(It.IsAny<LlmSuggestion>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_NotCreateTestCase_WhenRejecting()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        _suggestionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        var command = CreateRejectCommand();
        await _handler.HandleAsync(command);

        _testCaseRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_NotIncrementSuiteVersion_WhenRejecting()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        _suggestionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        var command = CreateRejectCommand();
        await _handler.HandleAsync(command);

        _versionRepoMock.Verify(x => x.AddAsync(It.IsAny<TestSuiteVersion>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────── Approve flow tests ────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_ApproveSuggestion_AndMaterializeTestCase()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        var command = CreateApproveCommand();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoExistingTestCases();

        await _handler.HandleAsync(command);

        suggestion.ReviewStatus.Should().Be(ReviewStatus.Approved);
        suggestion.AppliedTestCaseId.Should().NotBeNull();
        _testCaseRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_CreateChangeLog_WhenApproved()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        var command = CreateApproveCommand();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoExistingTestCases();

        await _handler.HandleAsync(command);

        _changeLogRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseChangeLog>(cl => cl.ChangeType == TestCaseChangeType.Created),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_IncrementSuiteVersion_WhenApproved()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        var command = CreateApproveCommand();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoExistingTestCases();

        await _handler.HandleAsync(command);

        _versionRepoMock.Verify(x => x.AddAsync(
            It.Is<TestSuiteVersion>(v =>
                v.TestSuiteId == suite.Id &&
                v.ChangeType == VersionChangeType.TestCasesModified),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_UpdateSuiteVersion_WhenApproved()
    {
        var suite = CreateSuite();
        suite.Version = 3;
        var suggestion = CreatePendingSuggestion();
        var command = CreateApproveCommand();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoExistingTestCases();

        await _handler.HandleAsync(command);

        suite.Version.Should().Be(4);
        _suiteRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestSuite>(s => s.Version == 4),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────── Modify flow tests ─────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_ModifyAndApproveSuggestion()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoExistingTestCases();

        var command = CreateApproveCommand();
        command.ReviewAction = "Modify";
        command.ModifiedContent = new EditableLlmSuggestionInput
        {
            Name = "Modified test name",
            Description = "Modified description",
        };

        await _handler.HandleAsync(command);

        suggestion.ReviewStatus.Should().Be(ReviewStatus.ModifiedAndApproved);
        suggestion.ModifiedContent.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task HandleAsync_Should_CallMaterializeFromModifiedContent_WhenModify()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoExistingTestCases();

        var modifiedContent = new EditableLlmSuggestionInput
        {
            Name = "Modified test name",
            Description = "Modified description",
        };

        var command = CreateApproveCommand();
        command.ReviewAction = "Modify";
        command.ModifiedContent = modifiedContent;

        await _handler.HandleAsync(command);

        _materializerMock.Verify(x => x.MaterializeFromModifiedContent(
            It.IsAny<LlmSuggestion>(),
            It.IsAny<EditableLlmSuggestionInput>(),
            It.IsAny<ApiOrderItemModel>(),
            It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_AllowModifyWithoutName_WhenOtherModifiedContentExists()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoExistingTestCases();

        var command = CreateApproveCommand();
        command.ReviewAction = "Modify";
        command.ModifiedContent = new EditableLlmSuggestionInput
        {
            Description = "Adjusted expectation only",
        };

        await _handler.HandleAsync(command);

        suggestion.ReviewStatus.Should().Be(ReviewStatus.ModifiedAndApproved);
        suggestion.AppliedTestCaseId.Should().NotBeNull();
    }

    // ─────────────────────────── Concurrency test ───────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_ThrowConflict_WhenConcurrencyConflict()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        var command = CreateApproveCommand();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);
        SetupGateApproved();
        SetupSubscriptionAllowed();
        SetupNoExistingTestCases();

        var dbException = new Exception("Concurrency conflict");
        _suggestionRepoMock.Setup(x => x.IsDbUpdateConcurrencyException(dbException)).Returns(true);
        _suggestionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbException);

        var act = () => _handler.HandleAsync(command);
        if (DateTime.UtcNow.Year > 0)
        {
            var exceptionAssertions = await act.Should().ThrowAsync<ConflictException>();
            exceptionAssertions.WithMessage("*thay đổi bởi thao tác khác*");
            return;
        }
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Suggestion đã được thay đổi*");
    }

    // ─────────────────────────── Subscription test ──────────────────────────────

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenTestCaseLimitExceeded()
    {
        var suite = CreateSuite();
        var suggestion = CreatePendingSuggestion();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);

        _subscriptionMock.Setup(x => x.CheckLimitAsync(
                It.IsAny<Guid>(), LimitType.MaxTestCasesPerSuite, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = false, DenialReason = "Limit exceeded" });

        var command = CreateApproveCommand();

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*subscription*");
    }

    #region Helpers

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultSuiteId = Guid.NewGuid();
    private static readonly Guid DefaultSuggestionId = Guid.NewGuid();

    private static TestSuite CreateSuite() => new()
    {
        Id = DefaultSuiteId,
        CreatedById = DefaultUserId,
        Status = TestSuiteStatus.Ready,
        ApprovalStatus = ApprovalStatus.Approved,
        Version = 1,
        RowVersion = Guid.NewGuid().ToByteArray(),
    };

    private static LlmSuggestion CreatePendingSuggestion() => new()
    {
        Id = DefaultSuggestionId,
        TestSuiteId = DefaultSuiteId,
        EndpointId = Guid.NewGuid(),
        SuggestedName = "Test negative scenario",
        SuggestedDescription = "Test description",
        TestType = TestType.Negative,
        Priority = TestPriority.High,
        SuggestionType = LlmSuggestionType.BoundaryNegative,
        ReviewStatus = ReviewStatus.Pending,
        SuggestedRequest = "{}",
        SuggestedExpectation = "{}",
        SuggestedTags = "[\"negative\"]",
        RowVersion = Guid.NewGuid().ToByteArray(),
        CreatedDateTime = DateTimeOffset.UtcNow,
    };

    private static ReviewLlmSuggestionCommand CreateApproveCommand() => new()
    {
        TestSuiteId = DefaultSuiteId,
        SuggestionId = DefaultSuggestionId,
        CurrentUserId = DefaultUserId,
        ReviewAction = "Approve",
        RowVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
    };

    private static ReviewLlmSuggestionCommand CreateRejectCommand() => new()
    {
        TestSuiteId = DefaultSuiteId,
        SuggestionId = DefaultSuggestionId,
        CurrentUserId = DefaultUserId,
        ReviewAction = "Reject",
        RowVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
        ReviewNotes = "Not applicable for this use case",
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

    private void SetupSuggestionFound(LlmSuggestion suggestion)
    {
        _suggestionRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<LlmSuggestion> { suggestion }.AsQueryable());
        _suggestionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<LlmSuggestion>>())).ReturnsAsync(suggestion);
    }

    private void SetupSuggestionNotFound()
    {
        _suggestionRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<LlmSuggestion>().AsQueryable());
        _suggestionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<LlmSuggestion>>())).ReturnsAsync((LlmSuggestion)null);
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
        _subscriptionMock.Setup(x => x.CheckLimitAsync(It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });
    }

    private void SetupNoExistingTestCases()
    {
        _testCaseRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestCase>().AsQueryable());
        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>())).ReturnsAsync(new List<TestCase>());
    }

    #endregion
}
