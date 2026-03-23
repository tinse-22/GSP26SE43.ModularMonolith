using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class UpsertLlmSuggestionFeedbackCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<LlmSuggestion, Guid>> _suggestionRepoMock;
    private readonly Mock<ILlmSuggestionFeedbackUpsertService> _feedbackUpsertServiceMock;
    private readonly UpsertLlmSuggestionFeedbackCommandHandler _handler;

    public UpsertLlmSuggestionFeedbackCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _suggestionRepoMock = new Mock<IRepository<LlmSuggestion, Guid>>();
        _feedbackUpsertServiceMock = new Mock<ILlmSuggestionFeedbackUpsertService>();

        var serviceProvider = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider();

        _handler = new UpsertLlmSuggestionFeedbackCommandHandler(
            _suiteRepoMock.Object,
            _suggestionRepoMock.Object,
            _feedbackUpsertServiceMock.Object,
            new LlmSuggestionFeedbackMetrics(serviceProvider.GetRequiredService<IMeterFactory>()),
            new Mock<ILogger<UpsertLlmSuggestionFeedbackCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var command = CreateCommand();
        command.CurrentUserId = Guid.NewGuid();

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
        _feedbackUpsertServiceMock.Verify(
            x => x.UpsertAsync(It.IsAny<LlmSuggestionFeedbackUpsertRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSuiteArchived()
    {
        var suite = CreateSuite();
        suite.Status = TestSuiteStatus.Archived;
        SetupSuiteFound(suite);

        var command = CreateCommand();

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*archived*");
        _feedbackUpsertServiceMock.Verify(
            x => x.UpsertAsync(It.IsAny<LlmSuggestionFeedbackUpsertRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenSuggestionDoesNotExist()
    {
        SetupSuiteFound(CreateSuite());
        SetupSuggestionNotFound();

        var command = CreateCommand();

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<NotFoundException>();
        _feedbackUpsertServiceMock.Verify(
            x => x.UpsertAsync(It.IsAny<LlmSuggestionFeedbackUpsertRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSuggestionSuperseded()
    {
        SetupSuiteFound(CreateSuite());

        var suggestion = CreateSuggestion();
        suggestion.ReviewStatus = ReviewStatus.Superseded;
        SetupSuggestionFound(suggestion);

        var command = CreateCommand();

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*superseded*");
        _feedbackUpsertServiceMock.Verify(
            x => x.UpsertAsync(It.IsAny<LlmSuggestionFeedbackUpsertRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_RejectNumericSignalValue()
    {
        SetupSuiteFound(CreateSuite());
        SetupSuggestionFound(CreateSuggestion());

        var command = CreateCommand();
        command.Signal = "0";

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Helpful*NotHelpful*");
        _feedbackUpsertServiceMock.Verify(
            x => x.UpsertAsync(It.IsAny<LlmSuggestionFeedbackUpsertRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_DelegateAtomicUpsert_AndMapResult()
    {
        SetupSuiteFound(CreateSuite());
        SetupSuggestionFound(CreateSuggestion());

        LlmSuggestionFeedbackUpsertRequest capturedRequest = null;
        var persistedFeedback = CreateFeedback();
        persistedFeedback.Notes = "Helpful note for endpoint";

        _feedbackUpsertServiceMock
            .Setup(x => x.UpsertAsync(It.IsAny<LlmSuggestionFeedbackUpsertRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LlmSuggestionFeedbackUpsertRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new LlmSuggestionFeedbackUpsertResult
            {
                Feedback = persistedFeedback,
                WasUpdate = false,
            });

        var command = CreateCommand();
        command.Notes = "  Helpful note for endpoint  ";

        await _handler.HandleAsync(command);

        capturedRequest.Should().NotBeNull();
        capturedRequest.TestSuiteId.Should().Be(DefaultSuiteId);
        capturedRequest.SuggestionId.Should().Be(DefaultSuggestionId);
        capturedRequest.CurrentUserId.Should().Be(DefaultUserId);
        capturedRequest.Signal.Should().Be(LlmSuggestionFeedbackSignal.Helpful);
        capturedRequest.Notes.Should().Be("Helpful note for endpoint");

        command.Result.Should().NotBeNull();
        command.Result.Signal.Should().Be("Helpful");
        command.Result.Notes.Should().Be("Helpful note for endpoint");
    }

    [Fact]
    public async Task HandleAsync_Should_PreserveUpdateResult_FromAtomicUpsertService()
    {
        SetupSuiteFound(CreateSuite());
        SetupSuggestionFound(CreateSuggestion());

        var persistedFeedback = CreateFeedback();
        persistedFeedback.FeedbackSignal = LlmSuggestionFeedbackSignal.NotHelpful;
        persistedFeedback.Notes = "Needs stronger validation";
        persistedFeedback.UpdatedDateTime = DateTimeOffset.UtcNow;

        _feedbackUpsertServiceMock
            .Setup(x => x.UpsertAsync(It.IsAny<LlmSuggestionFeedbackUpsertRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmSuggestionFeedbackUpsertResult
            {
                Feedback = persistedFeedback,
                WasUpdate = true,
            });

        var command = CreateCommand();
        command.Signal = "NotHelpful";
        command.Notes = "  Needs stronger validation  ";

        await _handler.HandleAsync(command);

        command.Result.Should().NotBeNull();
        command.Result.Signal.Should().Be("NotHelpful");
        command.Result.Notes.Should().Be("Needs stronger validation");
    }

    private static readonly Guid DefaultSuiteId = Guid.NewGuid();
    private static readonly Guid DefaultSuggestionId = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultEndpointId = Guid.NewGuid();

    private static UpsertLlmSuggestionFeedbackCommand CreateCommand() => new()
    {
        TestSuiteId = DefaultSuiteId,
        SuggestionId = DefaultSuggestionId,
        CurrentUserId = DefaultUserId,
        Signal = "Helpful",
        Notes = "Looks good",
    };

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
        EndpointId = DefaultEndpointId,
        SuggestedName = "Negative login case",
        SuggestedDescription = "Reject null email",
        SuggestedRequest = "{}",
        SuggestedExpectation = "{}",
        SuggestedTags = "[\"llm-suggested\"]",
        SuggestionType = LlmSuggestionType.BoundaryNegative,
        TestType = TestType.Negative,
        Priority = TestPriority.High,
        ReviewStatus = ReviewStatus.Pending,
        RowVersion = Guid.NewGuid().ToByteArray(),
        CreatedDateTime = DateTimeOffset.UtcNow,
    };

    private static LlmSuggestionFeedback CreateFeedback() => new()
    {
        Id = Guid.NewGuid(),
        SuggestionId = DefaultSuggestionId,
        TestSuiteId = DefaultSuiteId,
        EndpointId = DefaultEndpointId,
        UserId = DefaultUserId,
        FeedbackSignal = LlmSuggestionFeedbackSignal.Helpful,
        Notes = "Initial note",
        CreatedDateTime = DateTimeOffset.UtcNow.AddMinutes(-10),
        RowVersion = Guid.NewGuid().ToByteArray(),
    };

    private void SetupSuiteFound(TestSuite suite)
    {
        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite> { suite }.AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>())).ReturnsAsync(suite);
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
}
