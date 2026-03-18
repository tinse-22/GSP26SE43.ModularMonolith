using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Diagnostics.Metrics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class LlmSuggestionFeedbackContextServiceTests
{
    [Fact]
    public async Task BuildAsync_Should_AggregateCounts_AndIgnoreSupersededOrEndpointlessSuggestions()
    {
        var endpointId = Guid.NewGuid();
        var includedSuggestion = CreateSuggestion(Guid.NewGuid(), endpointId, ReviewStatus.Pending);
        var supersededSuggestion = CreateSuggestion(Guid.NewGuid(), endpointId, ReviewStatus.Superseded);
        var endpointlessSuggestion = CreateSuggestion(Guid.NewGuid(), null, ReviewStatus.Pending);

        var feedbacks = new List<LlmSuggestionFeedback>
        {
            CreateFeedback(includedSuggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.Helpful, "Good edge case"),
            CreateFeedback(includedSuggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.NotHelpful, "Need stricter validation"),
            CreateFeedback(supersededSuggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.Helpful, "Ignored superseded"),
            CreateFeedback(endpointlessSuggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.Helpful, "Ignored endpointless"),
        };

        var sut = CreateService(
            feedbacks,
            new List<LlmSuggestion> { includedSuggestion, supersededSuggestion, endpointlessSuggestion });

        var result = await sut.BuildAsync(DefaultSuiteId, new[] { endpointId });

        result.EndpointFeedbackContexts.Should().ContainKey(endpointId);
        result.EndpointFeedbackContexts[endpointId].Should().Contain("Helpful: 1");
        result.EndpointFeedbackContexts[endpointId].Should().Contain("NotHelpful: 1");
        result.EndpointFeedbackContexts[endpointId].Should().Contain("Good edge case");
        result.EndpointFeedbackContexts[endpointId].Should().NotContain("Ignored superseded");
        result.EndpointFeedbackContexts[endpointId].Should().NotContain("Ignored endpointless");
    }

    [Fact]
    public async Task BuildAsync_Should_SanitizeAndTruncateNotes()
    {
        var endpointId = Guid.NewGuid();
        var suggestion = CreateSuggestion(Guid.NewGuid(), endpointId, ReviewStatus.Pending);
        var longNote = "   first line\n\nsecond line   " + new string('x', 300);

        var sut = CreateService(
            new List<LlmSuggestionFeedback>
            {
                CreateFeedback(suggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.Helpful, longNote),
            },
            new List<LlmSuggestion> { suggestion });

        var result = await sut.BuildAsync(DefaultSuiteId, new[] { endpointId });

        var context = result.EndpointFeedbackContexts[endpointId];
        context.Should().Contain("first line second line");
        context.Should().Contain("...");
        context.Should().NotContain("\n\n");
    }

    [Fact]
    public async Task BuildAsync_Should_KeepFingerprintStable_ForEquivalentNormalizedFeedback()
    {
        var endpointId = Guid.NewGuid();
        var suggestion = CreateSuggestion(Guid.NewGuid(), endpointId, ReviewStatus.Pending);
        var createdAt = DateTimeOffset.UtcNow;

        var feedbackSet1 = new List<LlmSuggestionFeedback>
        {
            CreateFeedback(suggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.Helpful, "  Great   coverage  ", createdAt),
            CreateFeedback(suggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.NotHelpful, "Needs better validation", createdAt),
        };

        var feedbackSet2 = new List<LlmSuggestionFeedback>
        {
            CreateFeedback(suggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.NotHelpful, "Needs better validation", createdAt),
            CreateFeedback(suggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.Helpful, "Great coverage", createdAt),
        };

        var sut1 = CreateService(feedbackSet1, new List<LlmSuggestion> { suggestion });
        var sut2 = CreateService(feedbackSet2, new List<LlmSuggestion> { suggestion });

        var result1 = await sut1.BuildAsync(DefaultSuiteId, new[] { endpointId });
        var result2 = await sut2.BuildAsync(DefaultSuiteId, new[] { endpointId });

        result1.FeedbackFingerprint.Should().Be(result2.FeedbackFingerprint);
    }

    [Fact]
    public async Task BuildAsync_Should_ChangeFingerprint_WhenAggregatedFeedbackChanges()
    {
        var endpointId = Guid.NewGuid();
        var suggestion = CreateSuggestion(Guid.NewGuid(), endpointId, ReviewStatus.Pending);

        var sut1 = CreateService(
            new List<LlmSuggestionFeedback>
            {
                CreateFeedback(suggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.Helpful, "Useful"),
            },
            new List<LlmSuggestion> { suggestion });

        var sut2 = CreateService(
            new List<LlmSuggestionFeedback>
            {
                CreateFeedback(suggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.NotHelpful, "Useful"),
            },
            new List<LlmSuggestion> { suggestion });

        var result1 = await sut1.BuildAsync(DefaultSuiteId, new[] { endpointId });
        var result2 = await sut2.BuildAsync(DefaultSuiteId, new[] { endpointId });

        result1.FeedbackFingerprint.Should().NotBe(result2.FeedbackFingerprint);
    }

    [Fact]
    public async Task BuildAsync_Should_ReturnEmptyResult_WhenNoUsableFeedbackExists()
    {
        var endpointId = Guid.NewGuid();
        var suggestion = CreateSuggestion(Guid.NewGuid(), endpointId, ReviewStatus.Pending);

        var sut = CreateService(new List<LlmSuggestionFeedback>(), new List<LlmSuggestion> { suggestion });

        var result = await sut.BuildAsync(DefaultSuiteId, new[] { endpointId });

        result.EndpointFeedbackContexts.Should().BeEmpty();
        result.FeedbackFingerprint.Should().Be(LlmSuggestionFeedbackContextResult.EmptyFingerprint);
    }

    [Fact]
    public async Task BuildAsync_Should_IgnoreFeedback_WhenOwningSuggestionBelongsToDifferentSuite()
    {
        var endpointId = Guid.NewGuid();
        var suggestion = CreateSuggestion(Guid.NewGuid(), endpointId, ReviewStatus.Pending);
        suggestion.TestSuiteId = Guid.NewGuid();

        var feedback = CreateFeedback(suggestion, Guid.NewGuid(), LlmSuggestionFeedbackSignal.Helpful, "Should be ignored");
        feedback.TestSuiteId = DefaultSuiteId;

        var sut = CreateService(
            new List<LlmSuggestionFeedback> { feedback },
            new List<LlmSuggestion> { suggestion });

        var result = await sut.BuildAsync(DefaultSuiteId, new[] { endpointId });

        result.EndpointFeedbackContexts.Should().BeEmpty();
        result.FeedbackFingerprint.Should().Be(LlmSuggestionFeedbackContextResult.EmptyFingerprint);
    }

    private static readonly Guid DefaultSuiteId = Guid.NewGuid();

    private static LlmSuggestionFeedbackContextService CreateService(
        List<LlmSuggestionFeedback> feedbacks,
        List<LlmSuggestion> suggestions)
    {
        var feedbackRepoMock = new Mock<IRepository<LlmSuggestionFeedback, Guid>>();
        var suggestionRepoMock = new Mock<IRepository<LlmSuggestion, Guid>>();

        feedbackRepoMock.Setup(x => x.GetQueryableSet()).Returns(feedbacks.AsQueryable());
        feedbackRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<LlmSuggestionFeedback>>()))
            .ReturnsAsync((IQueryable<LlmSuggestionFeedback> query) => query.ToList());

        suggestionRepoMock.Setup(x => x.GetQueryableSet()).Returns(suggestions.AsQueryable());

        var serviceProvider = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider();

        return new LlmSuggestionFeedbackContextService(
            feedbackRepoMock.Object,
            suggestionRepoMock.Object,
            new LlmSuggestionFeedbackMetrics(serviceProvider.GetRequiredService<IMeterFactory>()));
    }

    private static LlmSuggestion CreateSuggestion(Guid suggestionId, Guid? endpointId, ReviewStatus reviewStatus) => new()
    {
        Id = suggestionId,
        TestSuiteId = DefaultSuiteId,
        EndpointId = endpointId,
        SuggestedName = "Feedback candidate",
        SuggestedDescription = "Description",
        SuggestedRequest = "{}",
        SuggestedExpectation = "{}",
        SuggestedTags = "[\"llm-suggested\"]",
        SuggestionType = LlmSuggestionType.BoundaryNegative,
        TestType = TestType.Negative,
        Priority = TestPriority.High,
        ReviewStatus = reviewStatus,
        RowVersion = Guid.NewGuid().ToByteArray(),
        CreatedDateTime = DateTimeOffset.UtcNow,
    };

    private static LlmSuggestionFeedback CreateFeedback(
        LlmSuggestion suggestion,
        Guid userId,
        LlmSuggestionFeedbackSignal signal,
        string notes,
        DateTimeOffset? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        SuggestionId = suggestion.Id,
        TestSuiteId = suggestion.TestSuiteId,
        EndpointId = suggestion.EndpointId,
        UserId = userId,
        FeedbackSignal = signal,
        Notes = notes,
        CreatedDateTime = createdAt ?? DateTimeOffset.UtcNow,
        RowVersion = Guid.NewGuid().ToByteArray(),
    };
}
