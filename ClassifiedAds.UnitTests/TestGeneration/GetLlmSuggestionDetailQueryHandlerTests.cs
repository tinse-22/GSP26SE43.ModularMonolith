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
    private readonly GetLlmSuggestionDetailQueryHandler _handler;

    public GetLlmSuggestionDetailQueryHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _suggestionRepoMock = new Mock<IRepository<LlmSuggestion, Guid>>();

        _handler = new GetLlmSuggestionDetailQueryHandler(
            _suiteRepoMock.Object,
            _suggestionRepoMock.Object);
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
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var query = new GetLlmSuggestionDetailQuery
        {
            TestSuiteId = DefaultSuiteId,
            SuggestionId = DefaultSuggestionId,
            CurrentUserId = Guid.NewGuid(), // different user
        };

        var act = () => _handler.HandleAsync(query);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenSuggestionDoesNotExist()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
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
        var suite = CreateSuite();
        var suggestion = CreateSuggestion();
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);

        var query = new GetLlmSuggestionDetailQuery
        {
            TestSuiteId = DefaultSuiteId,
            SuggestionId = DefaultSuggestionId,
            CurrentUserId = DefaultUserId,
        };

        var result = await _handler.HandleAsync(query);

        result.Should().NotBeNull();
        result.Id.Should().Be(suggestion.Id);
        result.TestSuiteId.Should().Be(suggestion.TestSuiteId);
        result.SuggestedName.Should().Be(suggestion.SuggestedName);
        result.ReviewStatus.Should().Be("Pending");
        result.RowVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_DeserializeTagsCorrectly()
    {
        var suite = CreateSuite();
        var suggestion = CreateSuggestion();
        suggestion.SuggestedTags = "[\"negative\",\"auto-generated\",\"llm-suggested\"]";
        SetupSuiteFound(suite);
        SetupSuggestionFound(suggestion);

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

    private void SetupSuggestionFound(LlmSuggestion suggestion)
    {
        _suggestionRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<LlmSuggestion> { suggestion }.AsQueryable());
        _suggestionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<LlmSuggestion>>()))
            .ReturnsAsync(suggestion);
    }

    private void SetupSuggestionNotFound()
    {
        _suggestionRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<LlmSuggestion>().AsQueryable());
        _suggestionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<LlmSuggestion>>()))
            .ReturnsAsync((LlmSuggestion)null);
    }

    #endregion
}
