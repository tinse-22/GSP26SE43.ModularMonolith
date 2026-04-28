using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Queries;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class GetLlmSuggestionsQueryHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<LlmSuggestion, Guid>> _suggestionRepoMock;
    private readonly Mock<IRepository<LlmSuggestionFeedback, Guid>> _feedbackRepoMock;
    private readonly Mock<IRepository<SrsDocument, Guid>> _srsDocRepoMock;
    private readonly Mock<IRepository<SrsRequirement, Guid>> _srsReqRepoMock;
    private readonly GetLlmSuggestionsQueryHandler _handler;

    public GetLlmSuggestionsQueryHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _suggestionRepoMock = new Mock<IRepository<LlmSuggestion, Guid>>();
        _feedbackRepoMock = new Mock<IRepository<LlmSuggestionFeedback, Guid>>();
        _srsDocRepoMock = new Mock<IRepository<SrsDocument, Guid>>();
        _srsReqRepoMock = new Mock<IRepository<SrsRequirement, Guid>>();

        _handler = new GetLlmSuggestionsQueryHandler(
            _suiteRepoMock.Object,
            _suggestionRepoMock.Object,
            _feedbackRepoMock.Object,
            _srsDocRepoMock.Object,
            _srsReqRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenSuiteDoesNotExist()
    {
        SetupSuiteNotFound();

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = Guid.NewGuid(),
            CurrentUserId = DefaultUserId,
        };

        var act = () => _handler.HandleAsync(query);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner()
    {
        SetupSuiteFound(CreateSuite());
        SetupSuggestionsFound(new List<LlmSuggestion>());
        SetupFeedbacksFound(new List<LlmSuggestionFeedback>());

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = Guid.NewGuid(),
        };

        var act = () => _handler.HandleAsync(query);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnEmptyList_WhenNoSuggestions()
    {
        SetupSuiteFound(CreateSuite());
        SetupSuggestionsFound(new List<LlmSuggestion>());
        SetupFeedbacksFound(new List<LlmSuggestionFeedback>());

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
        };

        var result = await _handler.HandleAsync(query);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnSuggestions_OrderedByDisplayOrder()
    {
        SetupSuiteFound(CreateSuite());
        SetupSuggestionsFound(new List<LlmSuggestion>
        {
            CreateSuggestion(Guid.NewGuid(), displayOrder: 1, name: "Second"),
            CreateSuggestion(Guid.NewGuid(), displayOrder: 0, name: "First"),
            CreateSuggestion(Guid.NewGuid(), displayOrder: 2, name: "Third"),
        });
        SetupFeedbacksFound(new List<LlmSuggestionFeedback>());

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
        };

        var result = await _handler.HandleAsync(query);

        result.Select(x => x.SuggestedName).Should().ContainInOrder("First", "Second", "Third");
    }

    [Fact]
    public async Task HandleAsync_Should_FilterByReviewStatus()
    {
        SetupSuiteFound(CreateSuite());
        SetupSuggestionsFound(new List<LlmSuggestion>
        {
            CreateSuggestion(Guid.NewGuid(), reviewStatus: ReviewStatus.Pending),
            CreateSuggestion(Guid.NewGuid(), reviewStatus: ReviewStatus.Approved),
            CreateSuggestion(Guid.NewGuid(), reviewStatus: ReviewStatus.Pending),
        });
        SetupFeedbacksFound(new List<LlmSuggestionFeedback>());

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            FilterByReviewStatus = "Pending",
        };

        var result = await _handler.HandleAsync(query);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_Should_FilterByTestType()
    {
        SetupSuiteFound(CreateSuite());
        SetupSuggestionsFound(new List<LlmSuggestion>
        {
            CreateSuggestion(Guid.NewGuid(), testType: TestType.Negative),
            CreateSuggestion(Guid.NewGuid(), testType: TestType.Boundary),
            CreateSuggestion(Guid.NewGuid(), testType: TestType.Negative),
        });
        SetupFeedbacksFound(new List<LlmSuggestionFeedback>());

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            FilterByTestType = "Boundary",
        };

        var result = await _handler.HandleAsync(query);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_Should_FilterByEndpointId()
    {
        SetupSuiteFound(CreateSuite());
        var targetEndpointId = Guid.NewGuid();
        SetupSuggestionsFound(new List<LlmSuggestion>
        {
            CreateSuggestion(Guid.NewGuid(), endpointId: targetEndpointId),
            CreateSuggestion(Guid.NewGuid(), endpointId: Guid.NewGuid()),
            CreateSuggestion(Guid.NewGuid(), endpointId: targetEndpointId),
        });
        SetupFeedbacksFound(new List<LlmSuggestionFeedback>());

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            FilterByEndpointId = targetEndpointId,
        };

        var result = await _handler.HandleAsync(query);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_Should_AttachCurrentUserFeedbackAndSummary()
    {
        SetupSuiteFound(CreateSuite());

        var suggestionWithFeedback = CreateSuggestion(Guid.NewGuid(), displayOrder: 0, name: "With feedback");
        var suggestionWithoutFeedback = CreateSuggestion(Guid.NewGuid(), displayOrder: 1, name: "Without feedback");
        SetupSuggestionsFound(new List<LlmSuggestion> { suggestionWithFeedback, suggestionWithoutFeedback });

        var feedbacks = new List<LlmSuggestionFeedback>
        {
            CreateFeedback(suggestionWithFeedback.Id, DefaultUserId, LlmSuggestionFeedbackSignal.Helpful, "Useful"),
            CreateFeedback(suggestionWithFeedback.Id, Guid.NewGuid(), LlmSuggestionFeedbackSignal.NotHelpful, "Needs more detail"),
        };
        SetupFeedbacksFound(feedbacks);

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
        };

        var result = await _handler.HandleAsync(query);

        result.Should().HaveCount(2);

        var first = result[0];
        first.CurrentUserFeedback.Should().NotBeNull();
        first.CurrentUserFeedback.Signal.Should().Be("Helpful");
        first.FeedbackSummary.HelpfulCount.Should().Be(1);
        first.FeedbackSummary.NotHelpfulCount.Should().Be(1);

        var second = result[1];
        second.CurrentUserFeedback.Should().BeNull();
        second.FeedbackSummary.HelpfulCount.Should().Be(0);
        second.FeedbackSummary.NotHelpfulCount.Should().Be(0);
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

    private static LlmSuggestion CreateSuggestion(
        Guid suggestionId,
        int displayOrder = 0,
        string name = "Test suggestion",
        ReviewStatus reviewStatus = ReviewStatus.Pending,
        TestType testType = TestType.Negative,
        Guid? endpointId = null) => new()
        {
            Id = suggestionId,
            TestSuiteId = DefaultSuiteId,
            EndpointId = endpointId ?? Guid.NewGuid(),
            DisplayOrder = displayOrder,
            SuggestedName = name,
            SuggestedDescription = "Test description",
            SuggestionType = LlmSuggestionType.BoundaryNegative,
            TestType = testType,
            Priority = TestPriority.High,
            ReviewStatus = reviewStatus,
            SuggestedRequest = "{}",
            SuggestedExpectation = "{}",
            SuggestedTags = "[\"negative\"]",
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedDateTime = DateTimeOffset.UtcNow,
        };

    private static LlmSuggestionFeedback CreateFeedback(
        Guid suggestionId,
        Guid userId,
        LlmSuggestionFeedbackSignal signal,
        string notes) => new()
        {
            Id = Guid.NewGuid(),
            SuggestionId = suggestionId,
            TestSuiteId = DefaultSuiteId,
            EndpointId = Guid.NewGuid(),
            UserId = userId,
            FeedbackSignal = signal,
            Notes = notes,
            CreatedDateTime = DateTimeOffset.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray(),
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

    private void SetupSuggestionsFound(List<LlmSuggestion> suggestions)
    {
        _suggestionRepoMock.Setup(x => x.GetQueryableSet()).Returns(suggestions.AsQueryable());
        _suggestionRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<LlmSuggestion>>()))
            .ReturnsAsync((IQueryable<LlmSuggestion> query) => query.ToList());
    }

    private void SetupFeedbacksFound(List<LlmSuggestionFeedback> feedbacks)
    {
        _feedbackRepoMock.Setup(x => x.GetQueryableSet()).Returns(feedbacks.AsQueryable());
        _feedbackRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<LlmSuggestionFeedback>>()))
            .ReturnsAsync((IQueryable<LlmSuggestionFeedback> query) => query.ToList());
    }
}
