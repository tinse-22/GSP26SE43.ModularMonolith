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

public class TestSuitesControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<TestSuitesController>> _loggerMock;
    private readonly Mock<ICommandHandler<AddUpdateTestSuiteScopeCommand>> _addUpdateHandlerMock;
    private readonly Mock<ICommandHandler<ArchiveTestSuiteScopeCommand>> _archiveHandlerMock;
    private readonly Mock<IQueryHandler<GetTestSuiteScopesQuery, List<TestSuiteScopeModel>>> _getAllHandlerMock;
    private readonly Mock<IQueryHandler<GetTestSuiteScopeQuery, TestSuiteScopeModel>> _getByIdHandlerMock;
    private readonly TestSuitesController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public TestSuitesControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<TestSuitesController>>();
        _addUpdateHandlerMock = new Mock<ICommandHandler<AddUpdateTestSuiteScopeCommand>>();
        _archiveHandlerMock = new Mock<ICommandHandler<ArchiveTestSuiteScopeCommand>>();
        _getAllHandlerMock = new Mock<IQueryHandler<GetTestSuiteScopesQuery, List<TestSuiteScopeModel>>>();
        _getByIdHandlerMock = new Mock<IQueryHandler<GetTestSuiteScopeQuery, TestSuiteScopeModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<AddUpdateTestSuiteScopeCommand>))).Returns(_addUpdateHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<ArchiveTestSuiteScopeCommand>))).Returns(_archiveHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetTestSuiteScopesQuery, List<TestSuiteScopeModel>>))).Returns(_getAllHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetTestSuiteScopeQuery, TestSuiteScopeModel>))).Returns(_getByIdHandlerMock.Object);

        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new TestSuitesController(dispatcher, _currentUserMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GetAll_Should_ReturnOkWithTestSuiteScopes()
    {
        var projectId = Guid.NewGuid();
        var suites = new List<TestSuiteScopeModel>
        {
            CreateScope(projectId, name: "Manual Suite", generationType: GenerationType.Manual),
            CreateScope(projectId, name: "Auto Suite", generationType: GenerationType.Auto),
        };

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestSuiteScopesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suites);

        var result = await _controller.GetAll(projectId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeAssignableTo<List<TestSuiteScopeModel>>().Subject;
        payload.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_Should_PassProjectIdAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        GetTestSuiteScopesQuery capturedQuery = null!;

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestSuiteScopesQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestSuiteScopesQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<TestSuiteScopeModel>());

        await _controller.GetAll(projectId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetAll_Should_ReturnOkWithEmptyList()
    {
        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestSuiteScopesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestSuiteScopeModel>());

        var result = await _controller.GetAll(Guid.NewGuid());

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<TestSuiteScopeModel>>().Subject.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_Should_ReturnSuiteMetadataFields()
    {
        var projectId = Guid.NewGuid();
        var suite = CreateScope(projectId, name: "LLM Suite", generationType: GenerationType.LLMAssisted);
        suite.SelectedEndpointIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        suite.SelectedEndpointCount = 2;
        suite.TestCaseCount = 7;

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestSuiteScopesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestSuiteScopeModel> { suite });

        var result = await _controller.GetAll(projectId);

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<List<TestSuiteScopeModel>>().Subject;
        payload[0].GenerationType.Should().Be(GenerationType.LLMAssisted);
        payload[0].SelectedEndpointCount.Should().Be(2);
        payload[0].TestCaseCount.Should().Be(7);
    }

    [Fact]
    public async Task GetById_Should_ReturnOkWithSuiteDetail()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestSuiteScopeQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateScope(projectId, suiteId, "Scope Detail"));

        var result = await _controller.GetById(projectId, suiteId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<TestSuiteScopeModel>().Subject.Id.Should().Be(suiteId);
    }

    [Fact]
    public async Task GetById_Should_PassProjectSuiteAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        GetTestSuiteScopeQuery capturedQuery = null!;

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestSuiteScopeQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestSuiteScopeQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateScope(projectId, suiteId, "Scope Detail"));

        await _controller.GetById(projectId, suiteId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SuiteId.Should().Be(suiteId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetById_Should_ReturnBusinessContextAndRowVersion()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var suite = CreateScope(projectId, suiteId, "Business Context Suite");
        suite.EndpointBusinessContexts = new Dictionary<Guid, string> { [endpointId] = "User must be authenticated" };
        suite.RowVersion = "cm93LXZlcnNpb24=";

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestSuiteScopeQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suite);

        var result = await _controller.GetById(projectId, suiteId);

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<TestSuiteScopeModel>().Subject;
        payload.EndpointBusinessContexts.Should().ContainValue("User must be authenticated");
        payload.RowVersion.Should().Be("cm93LXZlcnNpb24=");
    }

    [Fact]
    public async Task GetById_Should_ThrowNotFoundException_WhenSuiteMissing()
    {
        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestSuiteScopeQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Test suite not found"));

        var act = () => _controller.GetById(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Test suite not found*");
    }

    [Fact]
    public async Task Create_Should_ReturnCreatedWithScopeModel()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var request = CreateRequest();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateTestSuiteScopeCommand, CancellationToken>((command, _) =>
            {
                command.Result = CreateScope(projectId, suiteId, request.Name);
            })
            .Returns(Task.CompletedTask);

        var result = await _controller.Create(projectId, request);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/projects/{projectId}/test-suites/{suiteId}");
        createdResult.Value.Should().BeOfType<TestSuiteScopeModel>().Subject.Id.Should().Be(suiteId);
    }

    [Fact]
    public async Task Create_Should_MapRequestIntoCommand()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var request = CreateRequest();
        AddUpdateTestSuiteScopeCommand capturedCommand = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateTestSuiteScopeCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateScope(projectId, suiteId, request.Name);
            })
            .Returns(Task.CompletedTask);

        await _controller.Create(projectId, request);

        capturedCommand.Should().NotBeNull();
        capturedCommand.SuiteId.Should().BeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Name.Should().Be(request.Name);
        capturedCommand.Description.Should().Be(request.Description);
        capturedCommand.ApiSpecId.Should().Be(request.ApiSpecId);
        capturedCommand.GenerationType.Should().Be(GenerationType.Auto);
    }

    [Fact]
    public async Task Create_Should_ThrowValidationException_WhenNoEndpointSelected()
    {
        var request = CreateRequest();
        request.SelectedEndpointIds = new List<Guid>();
        request.EndpointBusinessContexts = new Dictionary<Guid, string>();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Phải chọn ít nhất một endpoint."));

        var act = () => _controller.Create(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Phải chọn ít nhất một endpoint.*");
    }

    [Fact]
    public async Task Create_Should_ThrowValidationException_WhenNameMissing()
    {
        var request = CreateRequest();
        request.Name = " ";

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Tên test suite là bắt buộc."));

        var act = () => _controller.Create(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Tên test suite là bắt buộc.*");
    }

    [Fact]
    public async Task Create_Should_ThrowValidationException_WhenApiSpecIdMissing()
    {
        var request = CreateRequest();
        request.ApiSpecId = Guid.Empty;
        request.GenerationType = GenerationType.Manual;
        request.SelectedEndpointIds = new List<Guid>();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("ApiSpecId là bắt buộc."));

        var act = () => _controller.Create(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*ApiSpecId là bắt buộc.*");
    }

    [Fact]
    public async Task Create_Should_ThrowValidationException_WhenEndpointsDoNotBelongToSpecification()
    {
        var request = CreateRequest();
        request.SelectedEndpointIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Các endpoint không thuộc specification đã chọn: endpoint-a, endpoint-b."));

        var act = () => _controller.Create(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Các endpoint không thuộc specification đã chọn:*");
    }

    [Fact]
    public async Task Create_Should_ReturnResultFromCommand()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var expected = CreateScope(projectId, suiteId, "Created Suite");
        expected.TestCaseCount = 9;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateTestSuiteScopeCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.Create(projectId, CreateRequest());

        var payload = result.Result.Should().BeOfType<CreatedResult>().Subject.Value
            .Should().BeOfType<TestSuiteScopeModel>().Subject;
        payload.TestCaseCount.Should().Be(9);
        payload.Name.Should().Be("Created Suite");
    }

    [Fact]
    public async Task Create_Should_ThrowValidationException_WhenCommandFails()
    {
        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Test suite name already exists"));

        var act = () => _controller.Create(Guid.NewGuid(), CreateRequest());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Test suite name already exists*");
    }

    [Fact]
    public async Task Update_Should_ReturnOkWithUpdatedScope()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var request = UpdateRequest();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateTestSuiteScopeCommand, CancellationToken>((command, _) =>
            {
                command.Result = CreateScope(projectId, suiteId, request.Name);
            })
            .Returns(Task.CompletedTask);

        var result = await _controller.Update(projectId, suiteId, request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<TestSuiteScopeModel>().Subject.Id.Should().Be(suiteId);
    }

    [Fact]
    public async Task Update_Should_MapRequestIntoCommandIncludingRowVersion()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var request = UpdateRequest();
        AddUpdateTestSuiteScopeCommand capturedCommand = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateTestSuiteScopeCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateScope(projectId, suiteId, request.Name);
            })
            .Returns(Task.CompletedTask);

        await _controller.Update(projectId, suiteId, request);

        capturedCommand.Should().NotBeNull();
        capturedCommand.SuiteId.Should().Be(suiteId);
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.RowVersion.Should().Be(request.RowVersion);
        capturedCommand.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task Update_Should_MapSelectionsAndGlobalRules()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var request = UpdateRequest();
        AddUpdateTestSuiteScopeCommand capturedCommand = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateTestSuiteScopeCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateScope(projectId, suiteId, request.Name);
            })
            .Returns(Task.CompletedTask);

        await _controller.Update(projectId, suiteId, request);

        capturedCommand.SelectedEndpointIds.Should().BeEquivalentTo(request.SelectedEndpointIds);
        capturedCommand.EndpointBusinessContexts.Should().BeEquivalentTo(request.EndpointBusinessContexts);
        capturedCommand.GlobalBusinessRules.Should().Be(request.GlobalBusinessRules);
    }

    [Fact]
    public async Task Update_Should_ThrowValidationException_WhenNameMissing()
    {
        var request = UpdateRequest();
        request.Name = " ";

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Tên test suite là bắt buộc."));

        var act = () => _controller.Update(Guid.NewGuid(), Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Tên test suite là bắt buộc.*");
    }

    [Fact]
    public async Task Update_Should_ThrowValidationException_WhenApiSpecIdMissing()
    {
        var request = UpdateRequest();
        request.ApiSpecId = Guid.Empty;
        request.GenerationType = GenerationType.Manual;
        request.SelectedEndpointIds = new List<Guid>();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("ApiSpecId là bắt buộc."));

        var act = () => _controller.Update(Guid.NewGuid(), Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*ApiSpecId là bắt buộc.*");
    }

    [Fact]
    public async Task Update_Should_ThrowValidationException_WhenNoEndpointSelected()
    {
        var request = UpdateRequest();
        request.SelectedEndpointIds = new List<Guid>();
        request.EndpointBusinessContexts = new Dictionary<Guid, string>();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Phải chọn ít nhất một endpoint."));

        var act = () => _controller.Update(Guid.NewGuid(), Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Phải chọn ít nhất một endpoint.*");
    }

    [Fact]
    public async Task Update_Should_ThrowValidationException_WhenEndpointsDoNotBelongToSpecification()
    {
        var request = UpdateRequest();
        request.SelectedEndpointIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Các endpoint không thuộc specification đã chọn: endpoint-a, endpoint-b."));

        var act = () => _controller.Update(Guid.NewGuid(), Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Các endpoint không thuộc specification đã chọn:*");
    }

    [Fact]
    public async Task Update_Should_ThrowValidationException_WhenRowVersionMissing()
    {
        var request = UpdateRequest();
        request.RowVersion = null;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("RowVersion là bắt buộc khi cập nhật."));

        var act = () => _controller.Update(Guid.NewGuid(), Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*RowVersion là bắt buộc khi cập nhật.*");
    }

    [Fact]
    public async Task Update_Should_ThrowValidationException_WhenRowVersionInvalid()
    {
        var request = UpdateRequest();
        request.RowVersion = "not-base64";

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("RowVersion không hợp lệ."));

        var act = () => _controller.Update(Guid.NewGuid(), Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*RowVersion không hợp lệ.*");
    }

    [Fact]
    public async Task Update_Should_ReturnCommandResult()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var expected = CreateScope(projectId, suiteId, "Updated Suite");
        expected.Status = TestSuiteStatus.Ready;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateTestSuiteScopeCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.Update(projectId, suiteId, UpdateRequest());

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<TestSuiteScopeModel>().Subject;
        payload.Status.Should().Be(TestSuiteStatus.Ready);
        payload.Name.Should().Be("Updated Suite");
    }

    [Fact]
    public async Task Update_Should_ThrowNotFoundException_WhenSuiteMissing()
    {
        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Test suite scope not found"));

        var act = () => _controller.Update(Guid.NewGuid(), Guid.NewGuid(), UpdateRequest());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Test suite scope not found*");
    }

    [Fact]
    public async Task Archive_Should_ReturnNoContent()
    {
        _archiveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ArchiveTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Archive(Guid.NewGuid(), Guid.NewGuid(), "cm93");

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Archive_Should_MapProjectSuiteUserAndRowVersion()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        ArchiveTestSuiteScopeCommand capturedCommand = null!;

        _archiveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ArchiveTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ArchiveTestSuiteScopeCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Archive(projectId, suiteId, "cm93VmVyc2lvbg==");

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SuiteId.Should().Be(suiteId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.RowVersion.Should().Be("cm93VmVyc2lvbg==");
    }

    [Fact]
    public async Task Archive_Should_ThrowNotFoundException_WhenSuiteMissing()
    {
        _archiveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ArchiveTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Test suite scope not found"));

        var act = () => _controller.Archive(Guid.NewGuid(), Guid.NewGuid(), "cm93");

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Test suite scope not found*");
    }

    [Fact]
    public async Task Archive_Should_ThrowConflictException_WhenRowVersionConflicts()
    {
        _archiveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ArchiveTestSuiteScopeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("CONCURRENCY_CONFLICT", "Archive conflict"));

        var act = () => _controller.Archive(Guid.NewGuid(), Guid.NewGuid(), "cm93");

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Archive conflict*");
    }

    private static CreateTestSuiteScopeRequest CreateRequest()
    {
        var endpointId = Guid.NewGuid();
        return new CreateTestSuiteScopeRequest
        {
            Name = "Customer Journey Suite",
            Description = "Coverage for customer onboarding flow",
            ApiSpecId = Guid.NewGuid(),
            GenerationType = GenerationType.Auto,
            SelectedEndpointIds = new List<Guid> { endpointId },
            EndpointBusinessContexts = new Dictionary<Guid, string>
            {
                [endpointId] = "Customer must verify email before checkout",
            },
            GlobalBusinessRules = "Do not allow anonymous purchase flow.",
        };
    }

    private static UpdateTestSuiteScopeRequest UpdateRequest()
    {
        var endpointId1 = Guid.NewGuid();
        var endpointId2 = Guid.NewGuid();
        return new UpdateTestSuiteScopeRequest
        {
            RowVersion = "cm93VmVyc2lvbg==",
            Name = "Updated Customer Journey Suite",
            Description = "Updated coverage after endpoint review",
            ApiSpecId = Guid.NewGuid(),
            GenerationType = GenerationType.Manual,
            SelectedEndpointIds = new List<Guid> { endpointId1, endpointId2 },
            EndpointBusinessContexts = new Dictionary<Guid, string>
            {
                [endpointId1] = "Order must have verified shipping address",
            },
            GlobalBusinessRules = "Refund flow must preserve transaction audit trail.",
        };
    }

    private static TestSuiteScopeModel CreateScope(
        Guid projectId,
        Guid? suiteId = null,
        string name = "Test Suite",
        GenerationType generationType = GenerationType.Auto)
    {
        return new TestSuiteScopeModel
        {
            Id = suiteId ?? Guid.NewGuid(),
            ProjectId = projectId,
            ApiSpecId = Guid.NewGuid(),
            Name = name,
            Description = "Suite description",
            GenerationType = generationType,
            Status = TestSuiteStatus.Draft,
            ApprovalStatus = ApprovalStatus.NotApplicable,
            SelectedEndpointIds = new List<Guid> { Guid.NewGuid() },
            EndpointBusinessContexts = new Dictionary<Guid, string>(),
            GlobalBusinessRules = "Global rule",
            SelectedEndpointCount = 1,
            TestCaseCount = 3,
            CreatedById = Guid.NewGuid(),
            CreatedDateTime = DateTimeOffset.UtcNow,
            RowVersion = "cm93VmVyc2lvbg==",
        };
    }
}
