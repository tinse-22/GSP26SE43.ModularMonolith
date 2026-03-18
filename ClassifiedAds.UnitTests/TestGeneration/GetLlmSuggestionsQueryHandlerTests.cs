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
    private readonly GetLlmSuggestionsQueryHandler _handler;

    public GetLlmSuggestionsQueryHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _suggestionRepoMock = new Mock<IRepository<LlmSuggestion, Guid>>();

        _handler = new GetLlmSuggestionsQueryHandler(
            _suiteRepoMock.Object,
            _suggestionRepoMock.Object);
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
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupSuggestionsFound(new List<LlmSuggestion>());

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = Guid.NewGuid(), // different user
        };

        var act = () => _handler.HandleAsync(query);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnEmptyList_WhenNoSuggestions()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupSuggestionsFound(new List<LlmSuggestion>());

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
        };

        var result = await _handler.HandleAsync(query);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnSuggestions_OrderedByDisplayOrder()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var suggestions = new List<LlmSuggestion>
        {
            CreateSuggestion(displayOrder: 1, name: "Second"),
            CreateSuggestion(displayOrder: 0, name: "First"),
            CreateSuggestion(displayOrder: 2, name: "Third"),
        };
        SetupSuggestionsFound(suggestions);

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
        };

        var result = await _handler.HandleAsync(query);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task HandleAsync_Should_FilterByReviewStatus()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var suggestions = new List<LlmSuggestion>
        {
            CreateSuggestion(reviewStatus: ReviewStatus.Pending),
            CreateSuggestion(reviewStatus: ReviewStatus.Approved),
            CreateSuggestion(reviewStatus: ReviewStatus.Pending),
        };
        SetupSuggestionsFound(suggestions);

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
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var suggestions = new List<LlmSuggestion>
        {
            CreateSuggestion(testType: TestType.Negative),
            CreateSuggestion(testType: TestType.Boundary),
            CreateSuggestion(testType: TestType.Negative),
        };
        SetupSuggestionsFound(suggestions);

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
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var targetEndpointId = Guid.NewGuid();
        var suggestions = new List<LlmSuggestion>
        {
            CreateSuggestion(endpointId: targetEndpointId),
            CreateSuggestion(endpointId: Guid.NewGuid()),
            CreateSuggestion(endpointId: targetEndpointId),
        };
        SetupSuggestionsFound(suggestions);

        var query = new GetLlmSuggestionsQuery
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            FilterByEndpointId = targetEndpointId,
        };

        var result = await _handler.HandleAsync(query);

        result.Should().HaveCount(2);
    }

    #region Helpers

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
        int displayOrder = 0,
        string name = "Test suggestion",
        ReviewStatus reviewStatus = ReviewStatus.Pending,
        TestType testType = TestType.Negative,
        Guid? endpointId = null) => new()
    {
        Id = Guid.NewGuid(),
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

    private void SetupSuggestionsFound(List<LlmSuggestion> suggestions)
    {
        _suggestionRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(suggestions.AsQueryable());
        _suggestionRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<LlmSuggestion>>()))
            .ReturnsAsync((IQueryable<LlmSuggestion> q) => q.ToList());
    }

    #endregion
}
