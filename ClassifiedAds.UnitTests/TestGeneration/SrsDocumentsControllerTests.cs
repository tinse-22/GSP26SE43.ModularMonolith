using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Controllers;
using ClassifiedAds.Modules.TestGeneration.Entities;
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

public class SrsDocumentsControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<SrsDocumentsController>> _loggerMock;
    private readonly Mock<ICommandHandler<CreateSrsDocumentCommand>> _createDocumentHandlerMock;
    private readonly Mock<ICommandHandler<TriggerSrsAnalysisCommand>> _triggerAnalysisHandlerMock;
    private readonly Mock<ICommandHandler<AddSrsRequirementCommand>> _addRequirementHandlerMock;
    private readonly Mock<ICommandHandler<DeleteSrsRequirementCommand>> _deleteRequirementHandlerMock;
    private readonly Mock<ICommandHandler<UpdateSrsRequirementCommand>> _updateRequirementHandlerMock;
    private readonly Mock<ICommandHandler<AnswerSrsRequirementClarificationCommand>> _answerClarificationHandlerMock;
    private readonly Mock<ICommandHandler<TriggerSrsRefinementCommand>> _triggerRefinementHandlerMock;
    private readonly Mock<ICommandHandler<UpdateSrsDocumentCommand>> _updateDocumentHandlerMock;
    private readonly Mock<ICommandHandler<DeleteSrsDocumentCommand>> _deleteDocumentHandlerMock;
    private readonly Mock<IQueryHandler<GetSrsDocumentsQuery, List<SrsDocumentModel>>> _getDocumentsHandlerMock;
    private readonly Mock<IQueryHandler<GetSrsDocumentDetailQuery, SrsDocumentModel>> _getDocumentHandlerMock;
    private readonly Mock<IQueryHandler<GetSrsAnalysisJobQuery, SrsAnalysisJobModel>> _getJobHandlerMock;
    private readonly Mock<IQueryHandler<GetSrsRequirementsQuery, List<SrsRequirementModel>>> _getRequirementsHandlerMock;
    private readonly Mock<IQueryHandler<GetSrsRequirementClarificationsQuery, List<SrsRequirementClarificationModel>>> _getClarificationsHandlerMock;
    private readonly SrsDocumentsController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public SrsDocumentsControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<SrsDocumentsController>>();
        _createDocumentHandlerMock = new Mock<ICommandHandler<CreateSrsDocumentCommand>>();
        _triggerAnalysisHandlerMock = new Mock<ICommandHandler<TriggerSrsAnalysisCommand>>();
        _addRequirementHandlerMock = new Mock<ICommandHandler<AddSrsRequirementCommand>>();
        _deleteRequirementHandlerMock = new Mock<ICommandHandler<DeleteSrsRequirementCommand>>();
        _updateRequirementHandlerMock = new Mock<ICommandHandler<UpdateSrsRequirementCommand>>();
        _answerClarificationHandlerMock = new Mock<ICommandHandler<AnswerSrsRequirementClarificationCommand>>();
        _triggerRefinementHandlerMock = new Mock<ICommandHandler<TriggerSrsRefinementCommand>>();
        _updateDocumentHandlerMock = new Mock<ICommandHandler<UpdateSrsDocumentCommand>>();
        _deleteDocumentHandlerMock = new Mock<ICommandHandler<DeleteSrsDocumentCommand>>();
        _getDocumentsHandlerMock = new Mock<IQueryHandler<GetSrsDocumentsQuery, List<SrsDocumentModel>>>();
        _getDocumentHandlerMock = new Mock<IQueryHandler<GetSrsDocumentDetailQuery, SrsDocumentModel>>();
        _getJobHandlerMock = new Mock<IQueryHandler<GetSrsAnalysisJobQuery, SrsAnalysisJobModel>>();
        _getRequirementsHandlerMock = new Mock<IQueryHandler<GetSrsRequirementsQuery, List<SrsRequirementModel>>>();
        _getClarificationsHandlerMock = new Mock<IQueryHandler<GetSrsRequirementClarificationsQuery, List<SrsRequirementClarificationModel>>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var services = new Dictionary<Type, object>
        {
            [typeof(ICommandHandler<CreateSrsDocumentCommand>)] = _createDocumentHandlerMock.Object,
            [typeof(ICommandHandler<TriggerSrsAnalysisCommand>)] = _triggerAnalysisHandlerMock.Object,
            [typeof(ICommandHandler<AddSrsRequirementCommand>)] = _addRequirementHandlerMock.Object,
            [typeof(ICommandHandler<DeleteSrsRequirementCommand>)] = _deleteRequirementHandlerMock.Object,
            [typeof(ICommandHandler<UpdateSrsRequirementCommand>)] = _updateRequirementHandlerMock.Object,
            [typeof(ICommandHandler<AnswerSrsRequirementClarificationCommand>)] = _answerClarificationHandlerMock.Object,
            [typeof(ICommandHandler<TriggerSrsRefinementCommand>)] = _triggerRefinementHandlerMock.Object,
            [typeof(ICommandHandler<UpdateSrsDocumentCommand>)] = _updateDocumentHandlerMock.Object,
            [typeof(ICommandHandler<DeleteSrsDocumentCommand>)] = _deleteDocumentHandlerMock.Object,
            [typeof(IQueryHandler<GetSrsDocumentsQuery, List<SrsDocumentModel>>)] = _getDocumentsHandlerMock.Object,
            [typeof(IQueryHandler<GetSrsDocumentDetailQuery, SrsDocumentModel>)] = _getDocumentHandlerMock.Object,
            [typeof(IQueryHandler<GetSrsAnalysisJobQuery, SrsAnalysisJobModel>)] = _getJobHandlerMock.Object,
            [typeof(IQueryHandler<GetSrsRequirementsQuery, List<SrsRequirementModel>>)] = _getRequirementsHandlerMock.Object,
            [typeof(IQueryHandler<GetSrsRequirementClarificationsQuery, List<SrsRequirementClarificationModel>>)] = _getClarificationsHandlerMock.Object,
        };

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(It.IsAny<Type>()))
            .Returns((Type serviceType) => services.TryGetValue(serviceType, out var service) ? service : null);

        _controller = new SrsDocumentsController(new Dispatcher(serviceProviderMock.Object), _currentUserMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GetAll_Should_ReturnOkWithDocuments()
    {
        var projectId = Guid.NewGuid();
        var documents = new List<SrsDocumentModel>
        {
            CreateDocumentModel(projectId, "Login flow"),
            CreateDocumentModel(projectId, "Checkout flow"),
        };

        _getDocumentsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsDocumentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var result = await _controller.GetAll(projectId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<SrsDocumentModel>>().Subject.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_Should_MapProjectAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        GetSrsDocumentsQuery capturedQuery = null!;

        _getDocumentsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsDocumentsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSrsDocumentsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<SrsDocumentModel>());

        await _controller.GetAll(projectId);

        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetById_Should_ReturnOkWithDocumentAndRequirements()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var document = CreateDocumentModel(projectId, "Order document");
        document.Id = documentId;
        document.Requirements.Add(CreateRequirementModel(documentId, "REQ-01"));

        _getDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsDocumentDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await _controller.GetById(projectId, documentId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SrsDocumentModel>().Subject;
        payload.Id.Should().Be(documentId);
        payload.Requirements.Should().ContainSingle();
    }

    [Fact]
    public async Task GetById_Should_MapIdentifiers()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        GetSrsDocumentDetailQuery capturedQuery = null!;

        _getDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsDocumentDetailQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSrsDocumentDetailQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateDocumentModel(projectId, "Spec"));

        await _controller.GetById(projectId, documentId);

        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SrsDocumentId.Should().Be(documentId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Create_Should_ReturnCreatedWithDocument()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var request = new CreateSrsDocumentRequest
        {
            Title = "Payment rules",
            SourceType = SrsSourceType.TextInput,
            RawContent = "User can pay by card.",
        };

        _createDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateSrsDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateSrsDocumentCommand, CancellationToken>((command, _) =>
            {
                command.Result = CreateDocumentModel(projectId, request.Title);
                command.Result.Id = documentId;
            })
            .Returns(Task.CompletedTask);

        var result = await _controller.Create(projectId, request);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/projects/{projectId}/srs-documents/{documentId}");
        createdResult.Value.Should().BeOfType<SrsDocumentModel>().Subject.Title.Should().Be("Payment rules");
    }

    [Fact]
    public async Task Create_Should_MapBodyIntoCommand()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var storageFileId = Guid.NewGuid();
        var request = new CreateSrsDocumentRequest
        {
            Title = "Upload document",
            TestSuiteId = suiteId,
            SourceType = SrsSourceType.FileUpload,
            StorageFileId = storageFileId,
        };
        CreateSrsDocumentCommand capturedCommand = null!;

        _createDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateSrsDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateSrsDocumentCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateDocumentModel(projectId, request.Title);
            })
            .Returns(Task.CompletedTask);

        await _controller.Create(projectId, request);

        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Title.Should().Be(request.Title);
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.SourceType.Should().Be(SrsSourceType.FileUpload);
        capturedCommand.StorageFileId.Should().Be(storageFileId);
    }

    [Fact]
    public async Task Create_Should_ThrowValidationException_WhenCommandFails()
    {
        var projectId = Guid.NewGuid();

        _createDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateSrsDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("SRS title is required"));

        var act = () => _controller.Create(projectId, new CreateSrsDocumentRequest());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*SRS title is required*");
    }

    [Fact]
    public async Task Analyze_Should_ReturnAcceptedWithJobId()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        _triggerAnalysisHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<TriggerSrsAnalysisCommand>(), It.IsAny<CancellationToken>()))
            .Callback<TriggerSrsAnalysisCommand, CancellationToken>((command, _) => command.JobId = jobId)
            .Returns(Task.CompletedTask);

        var result = await _controller.Analyze(projectId, documentId);

        var acceptedResult = result.Result.Should().BeOfType<AcceptedResult>().Subject;
        var payload = acceptedResult.Value.Should().BeOfType<SrsAnalysisAcceptedResponse>().Subject;
        payload.JobId.Should().Be(jobId);
        payload.Message.Should().Contain("Analysis job queued");
    }

    [Fact]
    public async Task Analyze_Should_MapIdentifiersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        TriggerSrsAnalysisCommand capturedCommand = null!;

        _triggerAnalysisHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<TriggerSrsAnalysisCommand>(), It.IsAny<CancellationToken>()))
            .Callback<TriggerSrsAnalysisCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Analyze(projectId, documentId);

        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SrsDocumentId.Should().Be(documentId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Analyze_Should_PropagateNotFoundException()
    {
        _triggerAnalysisHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<TriggerSrsAnalysisCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("SRS document not found"));

        var act = () => _controller.Analyze(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*SRS document not found*");
    }

    [Fact]
    public async Task GetJobStatus_Should_ReturnOkWithJob()
    {
        var job = new SrsAnalysisJobModel
        {
            Id = Guid.NewGuid(),
            SrsDocumentId = Guid.NewGuid(),
            Status = SrsAnalysisJobStatus.Completed,
            JobType = SrsAnalysisJobType.Analysis,
            RequirementsExtracted = 7,
        };

        _getJobHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsAnalysisJobQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var result = await _controller.GetJobStatus(Guid.NewGuid(), job.SrsDocumentId, job.Id);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<SrsAnalysisJobModel>().Subject.RequirementsExtracted.Should().Be(7);
    }

    [Fact]
    public async Task GetJobStatus_Should_MapIdentifiersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        GetSrsAnalysisJobQuery capturedQuery = null!;

        _getJobHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsAnalysisJobQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSrsAnalysisJobQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new SrsAnalysisJobModel());

        await _controller.GetJobStatus(projectId, documentId, jobId);

        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SrsDocumentId.Should().Be(documentId);
        capturedQuery.JobId.Should().Be(jobId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetRequirements_Should_ReturnOkWithRequirements()
    {
        var documentId = Guid.NewGuid();
        var requirements = new List<SrsRequirementModel>
        {
            CreateRequirementModel(documentId, "REQ-01"),
            CreateRequirementModel(documentId, "REQ-02"),
        };

        _getRequirementsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsRequirementsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requirements);

        var result = await _controller.GetRequirements(Guid.NewGuid(), documentId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<SrsRequirementModel>>().Subject.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRequirements_Should_MapFiltersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        GetSrsRequirementsQuery capturedQuery = null!;

        _getRequirementsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsRequirementsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSrsRequirementsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<SrsRequirementModel>());

        await _controller.GetRequirements(projectId, documentId, SrsRequirementType.Functional, true, endpointId);

        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SrsDocumentId.Should().Be(documentId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
        capturedQuery.RequirementType.Should().Be(SrsRequirementType.Functional);
        capturedQuery.IsReviewed.Should().BeTrue();
        capturedQuery.EndpointId.Should().Be(endpointId);
    }

    [Fact]
    public async Task GetRequirements_Should_DefaultOptionalFiltersToNull()
    {
        GetSrsRequirementsQuery capturedQuery = null!;

        _getRequirementsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsRequirementsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSrsRequirementsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<SrsRequirementModel>());

        await _controller.GetRequirements(Guid.NewGuid(), Guid.NewGuid());

        capturedQuery.RequirementType.Should().BeNull();
        capturedQuery.IsReviewed.Should().BeNull();
        capturedQuery.EndpointId.Should().BeNull();
    }

    [Fact]
    public async Task Update_Should_ReturnOkWithUpdatedDocument()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var updated = CreateDocumentModel(projectId, "Linked to suite");
        updated.Id = documentId;

        _updateDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateSrsDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateSrsDocumentCommand, CancellationToken>((command, _) => command.Result = updated)
            .Returns(Task.CompletedTask);

        var result = await _controller.Update(projectId, documentId, new UpdateSrsDocumentRequest { TestSuiteId = Guid.NewGuid() });

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<SrsDocumentModel>().Subject.Id.Should().Be(documentId);
    }

    [Fact]
    public async Task Update_Should_MapBodyAndRouteIds()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        UpdateSrsDocumentCommand capturedCommand = null!;

        _updateDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateSrsDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateSrsDocumentCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateDocumentModel(projectId, "Updated");
            })
            .Returns(Task.CompletedTask);

        await _controller.Update(projectId, documentId, new UpdateSrsDocumentRequest { TestSuiteId = suiteId, ClearTestSuiteId = false });

        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SrsDocumentId.Should().Be(documentId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.ClearTestSuiteId.Should().BeFalse();
    }

    [Fact]
    public async Task Update_Should_AllowClearingLinkedSuite()
    {
        UpdateSrsDocumentCommand capturedCommand = null!;

        _updateDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateSrsDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateSrsDocumentCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateDocumentModel(command.ProjectId, "Unlinked");
            })
            .Returns(Task.CompletedTask);

        await _controller.Update(Guid.NewGuid(), Guid.NewGuid(), new UpdateSrsDocumentRequest { ClearTestSuiteId = true });

        capturedCommand.TestSuiteId.Should().BeNull();
        capturedCommand.ClearTestSuiteId.Should().BeTrue();
    }

    [Fact]
    public async Task AddRequirement_Should_ReturnCreatedWithRequirement()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();

        _addRequirementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddSrsRequirementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddSrsRequirementCommand, CancellationToken>((command, _) =>
            {
                command.Result = CreateRequirementModel(documentId, "REQ-101");
                command.Result.Id = requirementId;
            })
            .Returns(Task.CompletedTask);

        var result = await _controller.AddRequirement(projectId, documentId, new AddSrsRequirementRequest { Title = "Support export" });

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/projects/{projectId}/srs-documents/{documentId}/requirements/{requirementId}");
    }

    [Fact]
    public async Task AddRequirement_Should_MapBodyIntoCommand()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        AddSrsRequirementCommand capturedCommand = null!;
        var request = new AddSrsRequirementRequest
        {
            Title = "Display report status",
            Description = "The user sees a generated report status.",
            RequirementType = SrsRequirementType.NonFunctional,
            TestableConstraints = "Status updates in under 2 seconds",
            EndpointId = endpointId,
        };

        _addRequirementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddSrsRequirementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddSrsRequirementCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateRequirementModel(documentId, "REQ-102");
            })
            .Returns(Task.CompletedTask);

        await _controller.AddRequirement(projectId, documentId, request);

        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SrsDocumentId.Should().Be(documentId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Title.Should().Be(request.Title);
        capturedCommand.Description.Should().Be(request.Description);
        capturedCommand.RequirementType.Should().Be(SrsRequirementType.NonFunctional);
        capturedCommand.TestableConstraints.Should().Be(request.TestableConstraints);
        capturedCommand.EndpointId.Should().Be(endpointId);
    }

    [Fact]
    public async Task AddRequirement_Should_ThrowValidationException_WhenCommandFails()
    {
        _addRequirementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddSrsRequirementCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Requirement title is required"));

        var act = () => _controller.AddRequirement(Guid.NewGuid(), Guid.NewGuid(), new AddSrsRequirementRequest());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Requirement title is required*");
    }

    [Fact]
    public async Task DeleteRequirement_Should_ReturnNoContent()
    {
        _deleteRequirementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteSrsRequirementCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeleteRequirement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteRequirement_Should_MapIdentifiersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        DeleteSrsRequirementCommand capturedCommand = null!;

        _deleteRequirementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteSrsRequirementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteSrsRequirementCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.DeleteRequirement(projectId, documentId, requirementId);

        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SrsDocumentId.Should().Be(documentId);
        capturedCommand.RequirementId.Should().Be(requirementId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task DeleteRequirement_Should_PropagateNotFoundException()
    {
        _deleteRequirementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteSrsRequirementCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Requirement not found"));

        var act = () => _controller.DeleteRequirement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Requirement not found*");
    }

    [Fact]
    public async Task UpdateRequirement_Should_ReturnOkWithRequirement()
    {
        var documentId = Guid.NewGuid();

        _updateRequirementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateSrsRequirementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateSrsRequirementCommand, CancellationToken>((command, _) => command.Result = CreateRequirementModel(documentId, "REQ-200"))
            .Returns(Task.CompletedTask);

        var result = await _controller.UpdateRequirement(Guid.NewGuid(), documentId, Guid.NewGuid(), new UpdateSrsRequirementRequest { Title = "Updated title" });

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<SrsRequirementModel>().Subject.RequirementCode.Should().Be("REQ-200");
    }

    [Fact]
    public async Task UpdateRequirement_Should_MapBodyIntoCommand()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        UpdateSrsRequirementCommand capturedCommand = null!;
        var request = new UpdateSrsRequirementRequest
        {
            Title = "Refined title",
            TestableConstraints = "Constraint text",
            EndpointId = endpointId,
            IsReviewed = true,
        };

        _updateRequirementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateSrsRequirementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateSrsRequirementCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateRequirementModel(documentId, "REQ-201");
            })
            .Returns(Task.CompletedTask);

        await _controller.UpdateRequirement(projectId, documentId, requirementId, request);

        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SrsDocumentId.Should().Be(documentId);
        capturedCommand.RequirementId.Should().Be(requirementId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Title.Should().Be(request.Title);
        capturedCommand.TestableConstraints.Should().Be(request.TestableConstraints);
        capturedCommand.EndpointId.Should().Be(endpointId);
        capturedCommand.IsReviewed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRequirement_Should_AllowClearEndpointFlag()
    {
        UpdateSrsRequirementCommand capturedCommand = null!;

        _updateRequirementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateSrsRequirementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateSrsRequirementCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateRequirementModel(command.SrsDocumentId, "REQ-202");
            })
            .Returns(Task.CompletedTask);

        await _controller.UpdateRequirement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new UpdateSrsRequirementRequest { ClearEndpointId = true });

        capturedCommand.ClearEndpointId.Should().BeTrue();
        capturedCommand.EndpointId.Should().BeNull();
    }

    [Fact]
    public async Task GetClarifications_Should_ReturnOkWithClarifications()
    {
        var requirementId = Guid.NewGuid();
        var clarifications = new List<SrsRequirementClarificationModel>
        {
            new() { Id = Guid.NewGuid(), SrsRequirementId = requirementId, Question = "What is the timeout?", IsCritical = true },
            new() { Id = Guid.NewGuid(), SrsRequirementId = requirementId, Question = "Should retries be capped?", IsAnswered = true },
        };

        _getClarificationsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsRequirementClarificationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clarifications);

        var result = await _controller.GetClarifications(Guid.NewGuid(), Guid.NewGuid(), requirementId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<SrsRequirementClarificationModel>>().Subject.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetClarifications_Should_MapIdentifiersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        GetSrsRequirementClarificationsQuery capturedQuery = null!;

        _getClarificationsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsRequirementClarificationsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSrsRequirementClarificationsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<SrsRequirementClarificationModel>());

        await _controller.GetClarifications(projectId, documentId, requirementId);

        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SrsDocumentId.Should().Be(documentId);
        capturedQuery.RequirementId.Should().Be(requirementId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task AnswerClarification_Should_ReturnOkWithClarification()
    {
        var clarification = new SrsRequirementClarificationModel
        {
            Id = Guid.NewGuid(),
            SrsRequirementId = Guid.NewGuid(),
            UserAnswer = "30 seconds",
            IsAnswered = true,
        };

        _answerClarificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AnswerSrsRequirementClarificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AnswerSrsRequirementClarificationCommand, CancellationToken>((command, _) => command.Result = clarification)
            .Returns(Task.CompletedTask);

        var result = await _controller.AnswerClarification(Guid.NewGuid(), Guid.NewGuid(), clarification.SrsRequirementId, clarification.Id, new AnswerClarificationRequest { UserAnswer = "30 seconds" });

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<SrsRequirementClarificationModel>().Subject.IsAnswered.Should().BeTrue();
    }

    [Fact]
    public async Task AnswerClarification_Should_MapAnswerAndIdentifiers()
    {
        var requirementId = Guid.NewGuid();
        var clarificationId = Guid.NewGuid();
        AnswerSrsRequirementClarificationCommand capturedCommand = null!;

        _answerClarificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AnswerSrsRequirementClarificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AnswerSrsRequirementClarificationCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = new SrsRequirementClarificationModel();
            })
            .Returns(Task.CompletedTask);

        await _controller.AnswerClarification(Guid.NewGuid(), Guid.NewGuid(), requirementId, clarificationId, new AnswerClarificationRequest { UserAnswer = "Use VAT inclusive amount" });

        capturedCommand.SrsRequirementId.Should().Be(requirementId);
        capturedCommand.ClarificationId.Should().Be(clarificationId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.UserAnswer.Should().Be("Use VAT inclusive amount");
    }

    [Fact]
    public async Task AnswerClarification_Should_PropagateValidationException()
    {
        _answerClarificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AnswerSrsRequirementClarificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("UserAnswer is required"));

        var act = () => _controller.AnswerClarification(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new AnswerClarificationRequest());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*UserAnswer is required*");
    }

    [Fact]
    public async Task RefineRequirement_Should_ReturnAcceptedWithJobId()
    {
        var jobId = Guid.NewGuid();

        _triggerRefinementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<TriggerSrsRefinementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<TriggerSrsRefinementCommand, CancellationToken>((command, _) => command.JobId = jobId)
            .Returns(Task.CompletedTask);

        var result = await _controller.RefineRequirement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var acceptedResult = result.Should().BeOfType<AcceptedObjectResult>().Subject;
        acceptedResult.Value.Should().NotBeNull();
        acceptedResult.Value.Should().BeEquivalentTo(new { JobId = jobId, Message = "Refinement job queued. Poll /analysis-jobs/{jobId} for status." });
    }

    [Fact]
    public async Task RefineRequirement_Should_MapIdentifiersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        TriggerSrsRefinementCommand capturedCommand = null!;

        _triggerRefinementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<TriggerSrsRefinementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<TriggerSrsRefinementCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.RefineRequirement(projectId, documentId, requirementId);

        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SrsDocumentId.Should().Be(documentId);
        capturedCommand.RequirementId.Should().Be(requirementId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task RefineRequirement_Should_PropagateNotFoundException()
    {
        _triggerRefinementHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<TriggerSrsRefinementCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Requirement missing"));

        var act = () => _controller.RefineRequirement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Requirement missing*");
    }

    [Fact]
    public async Task Delete_Should_ReturnNoContent()
    {
        _deleteDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteSrsDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_Should_MapIdentifiersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        DeleteSrsDocumentCommand capturedCommand = null!;

        _deleteDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteSrsDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteSrsDocumentCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Delete(projectId, documentId);

        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SrsDocumentId.Should().Be(documentId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Delete_Should_PropagateNotFoundException()
    {
        _deleteDocumentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteSrsDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("SRS document not found"));

        var act = () => _controller.Delete(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*SRS document not found*");
    }

    private static SrsDocumentModel CreateDocumentModel(Guid projectId, string title)
    {
        return new SrsDocumentModel
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title,
            SourceType = SrsSourceType.TextInput,
            AnalysisStatus = SrsAnalysisStatus.Pending,
        };
    }

    private static SrsRequirementModel CreateRequirementModel(Guid documentId, string code)
    {
        return new SrsRequirementModel
        {
            Id = Guid.NewGuid(),
            SrsDocumentId = documentId,
            RequirementCode = code,
            Title = "Requirement",
            RequirementType = SrsRequirementType.Functional,
            IsReviewed = false,
        };
    }
}
