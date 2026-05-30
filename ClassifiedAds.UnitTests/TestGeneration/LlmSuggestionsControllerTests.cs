using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Controllers;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class LlmSuggestionsControllerTests
{
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<LlmSuggestionsController>> _loggerMock;
    private readonly Mock<ICommandHandler<GenerateLlmSuggestionPreviewCommand>> _generateHandlerMock;
    private readonly Mock<ICommandHandler<ReviewLlmSuggestionCommand>> _reviewHandlerMock;
    private readonly Mock<ICommandHandler<UpsertLlmSuggestionFeedbackCommand>> _feedbackHandlerMock;
    private readonly Mock<ICommandHandler<BulkReviewLlmSuggestionsCommand>> _bulkReviewHandlerMock;
    private readonly Mock<ICommandHandler<BulkRestoreLlmSuggestionsCommand>> _bulkRestoreHandlerMock;
    private readonly Mock<IQueryHandler<GetLlmSuggestionsQuery, List<LlmSuggestionModel>>> _getAllHandlerMock;
    private readonly Mock<IQueryHandler<GetLlmSuggestionDetailQuery, LlmSuggestionModel>> _getByIdHandlerMock;
    private readonly LlmSuggestionsController _controller;

    public LlmSuggestionsControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<LlmSuggestionsController>>();
        _generateHandlerMock = new Mock<ICommandHandler<GenerateLlmSuggestionPreviewCommand>>();
        _reviewHandlerMock = new Mock<ICommandHandler<ReviewLlmSuggestionCommand>>();
        _feedbackHandlerMock = new Mock<ICommandHandler<UpsertLlmSuggestionFeedbackCommand>>();
        _bulkReviewHandlerMock = new Mock<ICommandHandler<BulkReviewLlmSuggestionsCommand>>();
        _bulkRestoreHandlerMock = new Mock<ICommandHandler<BulkRestoreLlmSuggestionsCommand>>();
        _getAllHandlerMock = new Mock<IQueryHandler<GetLlmSuggestionsQuery, List<LlmSuggestionModel>>>();
        _getByIdHandlerMock = new Mock<IQueryHandler<GetLlmSuggestionDetailQuery, LlmSuggestionModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<GenerateLlmSuggestionPreviewCommand>))).Returns(_generateHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<ReviewLlmSuggestionCommand>))).Returns(_reviewHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<UpsertLlmSuggestionFeedbackCommand>))).Returns(_feedbackHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<BulkReviewLlmSuggestionsCommand>))).Returns(_bulkReviewHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<BulkRestoreLlmSuggestionsCommand>))).Returns(_bulkRestoreHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetLlmSuggestionsQuery, List<LlmSuggestionModel>>))).Returns(_getAllHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetLlmSuggestionDetailQuery, LlmSuggestionModel>))).Returns(_getByIdHandlerMock.Object);

        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new LlmSuggestionsController(dispatcher, _currentUserMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GeneratePreview_Should_ReturnAcceptedWithJobPayload()
    {
        var suiteId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        _generateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateLlmSuggestionPreviewCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateLlmSuggestionPreviewCommand, CancellationToken>((command, _) => command.JobId = jobId)
            .Returns(Task.CompletedTask);

        var result = await _controller.GeneratePreview(suiteId, CreateGenerateRequest());

        var accepted = result.Result.Should().BeOfType<AcceptedObjectResult>().Subject;
        var payload = accepted.Value.Should().BeOfType<GenerateTestsAcceptedResponse>().Subject;
        payload.JobId.Should().Be(jobId);
        payload.TestSuiteId.Should().Be(suiteId);
        payload.Mode.Should().Be("callback");
    }

    [Fact]
    public async Task GeneratePreview_Should_MapSpecificationForceRefreshAndAlgorithmProfile()
    {
        var suiteId = Guid.NewGuid();
        var request = CreateGenerateRequest();
        GenerateLlmSuggestionPreviewCommand captured = null!;

        _generateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateLlmSuggestionPreviewCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateLlmSuggestionPreviewCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.JobId = Guid.NewGuid();
            })
            .Returns(Task.CompletedTask);

        await _controller.GeneratePreview(suiteId, request);

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.SpecificationId.Should().Be(request.SpecificationId);
        captured.ForceRefresh.Should().BeTrue();
        captured.AlgorithmProfile.Should().NotBeNull();
        captured.AlgorithmProfile.UseFeedbackLoopContext.Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_Should_ReturnOkWithSuggestions()
    {
        var suiteId = Guid.NewGuid();
        var suggestions = new List<LlmSuggestionModel>
        {
            CreateSuggestion(suiteId, suggestionType: "BoundaryNegative"),
            CreateSuggestion(suiteId, suggestionType: "Security"),
        };

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetLlmSuggestionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suggestions);

        var result = await _controller.GetAll(suiteId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<List<LlmSuggestionModel>>().Subject.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_Should_MapFiltersAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        GetLlmSuggestionsQuery captured = null!;

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetLlmSuggestionsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetLlmSuggestionsQuery, CancellationToken>((query, _) => captured = query)
            .ReturnsAsync(new List<LlmSuggestionModel>());

        await _controller.GetAll(suiteId, reviewStatus: "Pending", testType: "Negative", endpointId: endpointId, includeDeleted: true);

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.FilterByReviewStatus.Should().Be("Pending");
        captured.FilterByTestType.Should().Be("Negative");
        captured.FilterByEndpointId.Should().Be(endpointId);
        captured.IncludeDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_Should_ReturnDeletedAndFeedbackMetadata()
    {
        var suiteId = Guid.NewGuid();
        var suggestion = CreateSuggestion(suiteId, suggestionType: "Performance");
        suggestion.IsDeleted = true;
        suggestion.CurrentUserFeedback = new LlmSuggestionFeedbackModel
        {
            Id = Guid.NewGuid(),
            SuggestionId = suggestion.Id,
            TestSuiteId = suiteId,
            UserId = _currentUserId,
            Signal = "Helpful",
            Notes = "Useful suggestion",
        };

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetLlmSuggestionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmSuggestionModel> { suggestion });

        var result = await _controller.GetAll(suiteId, includeDeleted: true);

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<List<LlmSuggestionModel>>().Subject;
        payload[0].IsDeleted.Should().BeTrue();
        payload[0].CurrentUserFeedback!.Signal.Should().Be("Helpful");
    }

    [Fact]
    public async Task GetById_Should_ReturnOkWithSuggestionDetail()
    {
        var suiteId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetLlmSuggestionDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuggestion(suiteId, suggestionId: suggestionId, suggestionType: "BoundaryNegative"));

        var result = await _controller.GetById(suiteId, suggestionId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<LlmSuggestionModel>().Subject.Id.Should().Be(suggestionId);
    }

    [Fact]
    public async Task GetById_Should_MapRouteIdentifiersAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        GetLlmSuggestionDetailQuery captured = null!;

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetLlmSuggestionDetailQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetLlmSuggestionDetailQuery, CancellationToken>((query, _) => captured = query)
            .ReturnsAsync(CreateSuggestion(suiteId, suggestionId: suggestionId, suggestionType: "Security"));

        await _controller.GetById(suiteId, suggestionId);

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.SuggestionId.Should().Be(suggestionId);
        captured.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetById_Should_ReturnResolvedEndpointAndRequirementInfo()
    {
        var suiteId = Guid.NewGuid();
        var suggestion = CreateSuggestion(suiteId, suggestionType: "Security");
        suggestion.EndpointMethod = "POST";
        suggestion.EndpointPath = "/auth/login";
        suggestion.CoveredRequirements = new List<CoveredRequirementBriefModel>
        {
            new() { Id = Guid.NewGuid(), Code = "REQ-01", Title = "Validate login" },
        };

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetLlmSuggestionDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suggestion);

        var result = await _controller.GetById(suiteId, suggestion.Id);

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<LlmSuggestionModel>().Subject;
        payload.EndpointMethod.Should().Be("POST");
        payload.EndpointPath.Should().Be("/auth/login");
        payload.CoveredRequirements.Should().ContainSingle();
    }

    [Fact]
    public async Task GetById_Should_ThrowNotFoundException_WhenSuggestionMissing()
    {
        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetLlmSuggestionDetailQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Suggestion not found"));

        var act = () => _controller.GetById(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Suggestion not found*");
    }

    [Fact]
    public async Task Review_Should_ReturnOkWithReviewedSuggestion()
    {
        var suiteId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        var expected = CreateSuggestion(suiteId, suggestionId: suggestionId, suggestionType: "BoundaryNegative");
        expected.ReviewStatus = "Approved";

        _reviewHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ReviewLlmSuggestionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ReviewLlmSuggestionCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.Review(suiteId, suggestionId, CreateReviewRequest());

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<LlmSuggestionModel>().Subject.ReviewStatus.Should().Be("Approved");
    }

    [Fact]
    public async Task Review_Should_MapActionRowVersionNotesAndModifiedContent()
    {
        var suiteId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        var request = CreateReviewRequest();
        ReviewLlmSuggestionCommand captured = null!;

        _reviewHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ReviewLlmSuggestionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ReviewLlmSuggestionCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.Result = CreateSuggestion(suiteId, suggestionId: suggestionId, suggestionType: "BoundaryNegative");
            })
            .Returns(Task.CompletedTask);

        await _controller.Review(suiteId, suggestionId, request);

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.SuggestionId.Should().Be(suggestionId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.ReviewAction.Should().Be("Modify");
        captured.RowVersion.Should().Be(request.RowVersion);
        captured.ReviewNotes.Should().Be(request.ReviewNotes);
        captured.ModifiedContent.Should().NotBeNull();
        captured.ModifiedContent.Name.Should().Be("Adjusted scenario name");
    }

    [Fact]
    public async Task Review_Should_ThrowConcurrencyException_WhenVersionConflicts()
    {
        _reviewHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ReviewLlmSuggestionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("Suggestion review rowVersion conflict"));

        var act = () => _controller.Review(Guid.NewGuid(), Guid.NewGuid(), CreateReviewRequest());

        await act.Should().ThrowAsync<ConcurrencyException>()
            .WithMessage("*rowVersion conflict*");
    }

    [Fact]
    public async Task UpsertFeedback_Should_ReturnOkWithFeedbackModel()
    {
        var suiteId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        var expected = new LlmSuggestionFeedbackModel
        {
            Id = Guid.NewGuid(),
            SuggestionId = suggestionId,
            TestSuiteId = suiteId,
            UserId = _currentUserId,
            Signal = "Helpful",
            Notes = "Useful",
        };

        _feedbackHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpsertLlmSuggestionFeedbackCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpsertLlmSuggestionFeedbackCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.UpsertFeedback(suiteId, suggestionId, new UpsertLlmSuggestionFeedbackRequest
        {
            Signal = "Helpful",
            Notes = "Useful",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<LlmSuggestionFeedbackModel>().Subject.Signal.Should().Be("Helpful");
    }

    [Fact]
    public async Task UpsertFeedback_Should_MapSignalNotesAndIdentifiers()
    {
        var suiteId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        UpsertLlmSuggestionFeedbackCommand captured = null!;

        _feedbackHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpsertLlmSuggestionFeedbackCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpsertLlmSuggestionFeedbackCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.Result = new LlmSuggestionFeedbackModel
                {
                    Id = Guid.NewGuid(),
                    SuggestionId = suggestionId,
                    TestSuiteId = suiteId,
                    UserId = _currentUserId,
                    Signal = "NotHelpful",
                };
            })
            .Returns(Task.CompletedTask);

        await _controller.UpsertFeedback(suiteId, suggestionId, new UpsertLlmSuggestionFeedbackRequest
        {
            Signal = "NotHelpful",
            Notes = "Missing edge cases",
        });

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.SuggestionId.Should().Be(suggestionId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.Signal.Should().Be("NotHelpful");
        captured.Notes.Should().Be("Missing edge cases");
    }

    [Fact]
    public async Task UpsertFeedback_Should_ThrowValidationException_WhenSignalInvalid()
    {
        _feedbackHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpsertLlmSuggestionFeedbackCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Signal must be Helpful or NotHelpful"));

        var act = () => _controller.UpsertFeedback(Guid.NewGuid(), Guid.NewGuid(), new UpsertLlmSuggestionFeedbackRequest
        {
            Signal = "Neutral",
        });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Helpful or NotHelpful*");
    }

    [Fact]
    public async Task BulkReview_Should_ReturnOkWithBulkResult()
    {
        var suiteId = Guid.NewGuid();
        var expected = new BulkReviewLlmSuggestionsResultModel
        {
            TestSuiteId = suiteId,
            Action = "Approve",
            MatchedCount = 4,
            ProcessedCount = 3,
            MaterializedCount = 2,
            SuggestionIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() },
        };

        _bulkReviewHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<BulkReviewLlmSuggestionsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<BulkReviewLlmSuggestionsCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.BulkReview(suiteId, new BulkReviewLlmSuggestionsRequest
        {
            Action = "Approve",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<BulkReviewLlmSuggestionsResultModel>().Subject.ProcessedCount.Should().Be(3);
    }

    [Fact]
    public async Task BulkReview_Should_MapActionFiltersAndReviewNotes()
    {
        var suiteId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var request = new BulkReviewLlmSuggestionsRequest
        {
            Action = "Reject",
            ReviewNotes = "Bulk rejected after review",
            FilterBySuggestionType = "Security",
            FilterByTestType = "Negative",
            FilterByEndpointId = endpointId,
        };
        BulkReviewLlmSuggestionsCommand captured = null!;

        _bulkReviewHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<BulkReviewLlmSuggestionsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<BulkReviewLlmSuggestionsCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.Result = new BulkReviewLlmSuggestionsResultModel
                {
                    TestSuiteId = suiteId,
                    Action = "Reject",
                    ProcessedCount = 1,
                };
            })
            .Returns(Task.CompletedTask);

        await _controller.BulkReview(suiteId, request);

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.Action.Should().Be("Reject");
        captured.ReviewNotes.Should().Be(request.ReviewNotes);
        captured.FilterBySuggestionType.Should().Be("Security");
        captured.FilterByTestType.Should().Be("Negative");
        captured.FilterByEndpointId.Should().Be(endpointId);
    }

    [Fact]
    public async Task BulkReview_Should_ThrowValidationException_WhenActionInvalid()
    {
        _bulkReviewHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<BulkReviewLlmSuggestionsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Action must be Approve or Reject"));

        var act = () => _controller.BulkReview(Guid.NewGuid(), new BulkReviewLlmSuggestionsRequest
        {
            Action = "Modify",
        });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Approve or Reject*");
    }

    [Fact]
    public async Task BulkRestore_Should_ReturnOkWithRestoreResult()
    {
        var suiteId = Guid.NewGuid();
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var expected = new BulkOperationResultModel
        {
            TestSuiteId = suiteId,
            Operation = "Restore",
            EntityType = "LlmSuggestion",
            RequestedCount = 2,
            ProcessedCount = 2,
            ProcessedIds = ids,
        };

        _bulkRestoreHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<BulkRestoreLlmSuggestionsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<BulkRestoreLlmSuggestionsCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.BulkRestore(suiteId, new BulkRestoreLlmSuggestionsRequest
        {
            SuggestionIds = ids,
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<BulkOperationResultModel>().Subject.ProcessedCount.Should().Be(2);
    }

    [Fact]
    public async Task BulkRestore_Should_MapSuggestionIdsAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        BulkRestoreLlmSuggestionsCommand captured = null!;

        _bulkRestoreHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<BulkRestoreLlmSuggestionsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<BulkRestoreLlmSuggestionsCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.Result = new BulkOperationResultModel
                {
                    TestSuiteId = suiteId,
                    Operation = "Restore",
                    EntityType = "LlmSuggestion",
                    RequestedCount = ids.Count,
                };
            })
            .Returns(Task.CompletedTask);

        await _controller.BulkRestore(suiteId, new BulkRestoreLlmSuggestionsRequest
        {
            SuggestionIds = ids,
        });

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.SuggestionIds.Should().BeEquivalentTo(ids, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task BulkRestore_Should_ThrowValidationException_WhenIdsMissing()
    {
        _bulkRestoreHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<BulkRestoreLlmSuggestionsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("SuggestionIds are required"));

        var act = () => _controller.BulkRestore(Guid.NewGuid(), new BulkRestoreLlmSuggestionsRequest
        {
            SuggestionIds = new List<Guid>(),
        });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*SuggestionIds*");
    }

    private static GenerateLlmSuggestionPreviewRequest CreateGenerateRequest()
    {
        return new GenerateLlmSuggestionPreviewRequest
        {
            SpecificationId = Guid.NewGuid(),
            ForceRefresh = true,
            AlgorithmProfile = new GenerationAlgorithmProfile
            {
                UseObservationConfirmationPrompting = true,
                UseDependencyAwareOrdering = true,
                UseSchemaRelationshipAnalysis = false,
                UseSemanticTokenMatching = true,
                UseFeedbackLoopContext = false,
            },
        };
    }

    private static ReviewLlmSuggestionRequest CreateReviewRequest()
    {
        return new ReviewLlmSuggestionRequest
        {
            Action = "Modify",
            RowVersion = "cm93VmVyc2lvbg==",
            ReviewNotes = "Adjusted before approval",
            ModifiedContent = new EditableLlmSuggestionInput
            {
                Name = "Adjusted scenario name",
                Description = "Updated reasoning",
                TestType = "Negative",
                Priority = "High",
                Tags = new List<string> { "llm", "reviewed" },
                Request = new EditableSuggestionRequestInput
                {
                    HttpMethod = "POST",
                    Url = "/auth/login",
                    Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                    PathParams = new Dictionary<string, string>(),
                    QueryParams = new Dictionary<string, string>(),
                    Body = "{\"email\":\"user@test.com\"}",
                },
                Expectation = new EditableSuggestionExpectationInput
                {
                    ExpectedStatus = new List<int> { 400 },
                    BodyContains = new List<string> { "$.error" },
                    BodyNotContains = new List<string> { "$.token" },
                    ResponseSchema = "{\"error\":\"string\"}",
                    HeaderChecks = new Dictionary<string, string> { ["trace-id"] = "exists" },
                    JsonPathChecks = new Dictionary<string, string> { ["$.error"] = "exists" },
                    MaxResponseTime = 2000,
                    ExpectedProvenance = "SRS",
                },
                Variables = new List<EditableSuggestionVariableInput>
                {
                    new()
                    {
                        VariableName = "errorCode",
                        ExtractFrom = "ResponseBody",
                        JsonPath = "$.error.code",
                    },
                },
            },
        };
    }

    private static LlmSuggestionModel CreateSuggestion(Guid suiteId, Guid? suggestionId = null, string suggestionType = "BoundaryNegative")
    {
        return new LlmSuggestionModel
        {
            Id = suggestionId ?? Guid.NewGuid(),
            TestSuiteId = suiteId,
            EndpointId = Guid.NewGuid(),
            EndpointMethod = "POST",
            EndpointPath = "/auth/login",
            SuggestionType = suggestionType,
            TestType = "Negative",
            SuggestedName = "Generated suggestion",
            SuggestedDescription = "LLM suggested scenario",
            SuggestedRequest = "{\"httpMethod\":\"POST\",\"url\":\"/auth/login\"}",
            SuggestedExpectation = "{\"expectedStatus\":[400]}",
            SuggestedTags = new List<string> { "llm" },
            Priority = "High",
            ReviewStatus = "Pending",
            CacheKey = "cache-1",
            DisplayOrder = 1,
            CreatedDateTime = DateTimeOffset.UtcNow,
            RowVersion = "cm93VmVyc2lvbg==",
        };
    }
}
