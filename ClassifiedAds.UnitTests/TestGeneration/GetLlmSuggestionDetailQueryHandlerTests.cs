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

public class GetLlmSuggestionDetailQueryHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<LlmSuggestion, Guid>> _suggestionRepoMock;
    private readonly Mock<IRepository<LlmSuggestionFeedback, Guid>> _feedbackRepoMock;
    private readonly GetLlmSuggestionDetailQueryHandler _handler;

    public GetLlmSuggestionDetailQueryHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _suggestionRepoMock = new Mock<IRepository<LlmSuggestion, Guid>>();
        _feedbackRepoMock = new Mock<IRepository<LlmSuggestionFeedback, Guid>>();

        _handler = new GetLlmSuggestionDetailQueryHandler(
            _suiteRepoMock.Object,
            _suggestionRepoMock.Object,
            _feedbackRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenSuiteDoesNotExist()
    {
        SetupSuiteNotFound();

        var query = new GetLlmSuggestionDetailQuery
        {
            TestSuiteId = Guid.NewGuid(),
            SuggestionId = Guid.NewGuid(),
            CurrentUserId = DefaultUserId,
        };

        var act = () => _handler.HandleAsync(query);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner()
    {
        SetupSuiteFound(CreateSuite());

        var query = new GetLlmSuggestionDetailQuery
        {
            TestSuiteId = DefaultSuiteId,
            SuggestionId = DefaultSuggestionId,
            CurrentUserId = Guid.NewGuid(),
        };

        var act = () => _handler.HandleAsync(query);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenSuggestionDoesNotExist()
    {
        SetupSuiteFound(CreateSuite());
        SetupSuggestionNotFound();

        var query = new GetLlmSuggestionDetailQuery
        {
            TestSuiteId = DefaultSuiteId,
            SuggestionId = Guid.NewGuid(),
            CurrentUserId = DefaultUserId,
        };

        var act = () => _handler.HandleAsync(query);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnSuggestionModel()
    {
        var suggestion = CreateSuggestion();
        SetupSuiteFound(CreateSuite());
        SetupSuggestionFound(suggestion);
        SetupFeedbacksFound(new List<LlmSuggestionFeedback>());

        var query = new GetLlmSuggestionDetailQuery
        {
            TestSuiteId = DefaultSuiteId,
            SuggestionId = DefaultSuggestionId,
            CurrentUserId = DefaultUserId,
        };

        var result = await _handler.HandleAsync(query);

        result.Id.Should().Be(suggestion.Id);
        result.TestSuiteId.Should().Be(suggestion.TestSuiteId);
        result.SuggestedName.Should().Be(suggestion.SuggestedName);
        result.ReviewStatus.Should().Be("Pending");
        result.RowVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnFeedbackSummaryAndCurrentUserFeedback()
    {
        var suggestion = CreateSuggestion();
        SetupSuiteFound(CreateSuite());
        SetupSuggestionFound(suggestion);
        SetupFeedbacksFound(new List<LlmSuggestionFeedback>
        {
            CreateFeedback(DefaultUserId, LlmSuggestionFeedbackSignal.Helpful, "Useful"),
            CreateFeedback(Guid.NewGuid(), LlmSuggestionFeedbackSignal.NotHelpful, "Too generic"),
        });

        var query = new GetLlmSuggestionDetailQuery
        {
            TestSuiteId = DefaultSuiteId,
            SuggestionId = DefaultSuggestionId,
            CurrentUserId = DefaultUserId,
        };

        var result = await _handler.HandleAsync(query);

        result.CurrentUserFeedback.Should().NotBeNull();
        result.CurrentUserFeedback.Signal.Should().Be("Helpful");
        result.CurrentUserFeedback.Notes.Should().Be("Useful");
        result.FeedbackSummary.HelpfulCount.Should().Be(1);
        result.FeedbackSummary.NotHelpfulCount.Should().Be(1);
        result.FeedbackSummary.LastFeedbackAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_DeserializeTagsCorrectly()
    {
        var suggestion = CreateSuggestion();
        suggestion.SuggestedTags = "[\"negative\",\"auto-generated\",\"llm-suggested\"]";
        SetupSuiteFound(CreateSuite());
        SetupSuggestionFound(suggestion);
        SetupFeedbacksFound(new List<LlmSuggestionFeedback>());

        var query = new GetLlmSuggestionDetailQuery
        {
            TestSuiteId = DefaultSuiteId,
            SuggestionId = DefaultSuggestionId,
            CurrentUserId = DefaultUserId,
        };

        var result = await _handler.HandleAsync(query);

        result.SuggestedTags.Should().HaveCount(3);
        result.SuggestedTags.Should().Contain("negative");
        result.SuggestedTags.Should().Contain("llm-suggested");
    }

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

    private static LlmSuggestion CreateSuggestion() => new()
    {
        Id = DefaultSuggestionId,
        TestSuiteId = DefaultSuiteId,
        EndpointId = Guid.NewGuid(),
        DisplayOrder = 0,
        SuggestedName = "Missing required field",
        SuggestedDescription = "Send request without required field",
        SuggestionType = LlmSuggestionType.BoundaryNegative,
        TestType = TestType.Negative,
        Priority = TestPriority.High,
        ReviewStatus = ReviewStatus.Pending,
        SuggestedRequest = "{\"httpMethod\":\"POST\",\"url\":\"/api/test\"}",
        SuggestedExpectation = "{\"expectedStatus\":[400]}",
        SuggestedTags = "[\"negative\",\"auto-generated\"]",
        RowVersion = Guid.NewGuid().ToByteArray(),
        CreatedDateTime = DateTimeOffset.UtcNow,
    };

    private static LlmSuggestionFeedback CreateFeedback(
        Guid userId,
        LlmSuggestionFeedbackSignal signal,
        string notes) => new()
    {
        Id = Guid.NewGuid(),
        SuggestionId = DefaultSuggestionId,
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

    private void SetupFeedbacksFound(List<LlmSuggestionFeedback> feedbacks)
    {
        _feedbackRepoMock.Setup(x => x.GetQueryableSet()).Returns(feedbacks.AsQueryable());
        _feedbackRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<LlmSuggestionFeedback>>()))
            .ReturnsAsync((IQueryable<LlmSuggestionFeedback> query) => query.ToList());
    }
}
