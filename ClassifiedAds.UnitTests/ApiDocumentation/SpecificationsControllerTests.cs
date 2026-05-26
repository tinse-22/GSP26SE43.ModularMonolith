using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Controllers;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using ClassifiedAds.Modules.ApiDocumentation.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class SpecificationsControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<SpecificationsController>> _loggerMock;
    private readonly Mock<ICommandHandler<UploadApiSpecificationCommand>> _uploadHandlerMock;
    private readonly Mock<ICommandHandler<CreateManualSpecificationCommand>> _createManualHandlerMock;
    private readonly Mock<ICommandHandler<ImportCurlCommand>> _importCurlHandlerMock;
    private readonly Mock<ICommandHandler<ActivateSpecificationCommand>> _activateHandlerMock;
    private readonly Mock<ICommandHandler<DeleteSpecificationCommand>> _deleteHandlerMock;
    private readonly Mock<ICommandHandler<RestoreSpecificationCommand>> _restoreHandlerMock;
    private readonly Mock<IQueryHandler<GetSpecificationsQuery, List<SpecificationModel>>> _getSpecificationsHandlerMock;
    private readonly Mock<IQueryHandler<GetSpecificationQuery, SpecificationDetailModel>> _getSpecificationHandlerMock;
    private readonly SpecificationsController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public SpecificationsControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<SpecificationsController>>();
        _uploadHandlerMock = new Mock<ICommandHandler<UploadApiSpecificationCommand>>();
        _createManualHandlerMock = new Mock<ICommandHandler<CreateManualSpecificationCommand>>();
        _importCurlHandlerMock = new Mock<ICommandHandler<ImportCurlCommand>>();
        _activateHandlerMock = new Mock<ICommandHandler<ActivateSpecificationCommand>>();
        _deleteHandlerMock = new Mock<ICommandHandler<DeleteSpecificationCommand>>();
        _restoreHandlerMock = new Mock<ICommandHandler<RestoreSpecificationCommand>>();
        _getSpecificationsHandlerMock = new Mock<IQueryHandler<GetSpecificationsQuery, List<SpecificationModel>>>();
        _getSpecificationHandlerMock = new Mock<IQueryHandler<GetSpecificationQuery, SpecificationDetailModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<UploadApiSpecificationCommand>))).Returns(_uploadHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<CreateManualSpecificationCommand>))).Returns(_createManualHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<ImportCurlCommand>))).Returns(_importCurlHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<ActivateSpecificationCommand>))).Returns(_activateHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<DeleteSpecificationCommand>))).Returns(_deleteHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<RestoreSpecificationCommand>))).Returns(_restoreHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetSpecificationsQuery, List<SpecificationModel>>))).Returns(_getSpecificationsHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetSpecificationQuery, SpecificationDetailModel>))).Returns(_getSpecificationHandlerMock.Object);

        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new SpecificationsController(dispatcher, _currentUserMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task Get_Should_ReturnOkWithSpecifications()
    {
        var projectId = Guid.NewGuid();
        var specs = new List<SpecificationModel>
        {
            new() { Id = Guid.NewGuid(), ProjectId = projectId, Name = "OpenAPI Spec", ParseStatus = ParseStatus.Success.ToString() },
            new() { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Postman Spec", ParseStatus = ParseStatus.Pending.ToString() },
        };

        _getSpecificationsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(specs);

        var result = await _controller.Get(projectId, ParseStatus.Success, SourceType.OpenAPI, false);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeAssignableTo<List<SpecificationModel>>().Subject;
        payload.Should().HaveCount(2);
    }

    [Fact]
    public async Task Get_Should_PassFiltersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        GetSpecificationsQuery capturedQuery = null!;

        _getSpecificationsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSpecificationsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<SpecificationModel>());

        await _controller.Get(projectId, ParseStatus.Failed, SourceType.Postman, true);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
        capturedQuery.ParseStatus.Should().Be(ParseStatus.Failed);
        capturedQuery.SourceType.Should().Be(SourceType.Postman);
        capturedQuery.IncludeDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Get_Should_DefaultIncludeDeletedToFalse()
    {
        var projectId = Guid.NewGuid();
        GetSpecificationsQuery capturedQuery = null!;

        _getSpecificationsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSpecificationsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<SpecificationModel>());

        await _controller.Get(projectId, null, null);

        capturedQuery.Should().NotBeNull();
        capturedQuery.IncludeDeleted.Should().BeFalse();
        capturedQuery.ParseStatus.Should().BeNull();
        capturedQuery.SourceType.Should().BeNull();
    }

    [Fact]
    public async Task Get_Should_ReturnOkWithEmptyList()
    {
        var projectId = Guid.NewGuid();

        _getSpecificationsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SpecificationModel>());

        var result = await _controller.Get(projectId, null, null, true);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<SpecificationModel>>().Subject.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_Should_ReturnOkWithSpecificationDetail()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel
            {
                Id = specId,
                ProjectId = projectId,
                Name = "Spec Detail",
                EndpointCount = 3,
            });

        var result = await _controller.GetById(projectId, specId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SpecificationDetailModel>().Subject;
        payload.Id.Should().Be(specId);
        payload.EndpointCount.Should().Be(3);
    }

    [Fact]
    public async Task GetById_Should_PassProjectAndSpecIdentifiers()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        GetSpecificationQuery capturedQuery = null!;

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSpecificationQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Spec" });

        await _controller.GetById(projectId, specId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetById_Should_ReturnParseErrorsWhenProvided()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel
            {
                Id = specId,
                ProjectId = projectId,
                Name = "Spec",
                ParseErrors = new List<string> { "schema invalid" },
            });

        var result = await _controller.GetById(projectId, specId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SpecificationDetailModel>().Subject;
        payload.ParseErrors.Should().ContainSingle().Which.Should().Be("schema invalid");
    }

    [Fact]
    public async Task GetById_Should_ThrowNotFoundException_WhenSpecMissing()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Specification not found"));

        var act = () => _controller.GetById(projectId, specId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Specification not found*");
    }

    [Fact]
    public void GetUploadMethods_Should_ReturnOkWithStorageGatewayContract()
    {
        var projectId = Guid.NewGuid();

        var result = _controller.GetUploadMethods(projectId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeAssignableTo<List<UploadMethodOptionModel>>().Subject;
        payload.Should().ContainSingle();
        payload[0].Method.Should().Be(SpecificationUploadMethod.StorageGatewayContract.ToString());
    }

    [Fact]
    public void GetUploadMethods_Should_UseProjectSpecificUploadRoute()
    {
        var projectId = Guid.NewGuid();

        var result = _controller.GetUploadMethods(projectId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeAssignableTo<List<UploadMethodOptionModel>>().Subject;
        payload[0].UploadApi.Should().Be($"/api/projects/{projectId}/specifications/upload");
    }

    [Fact]
    public void GetUploadMethods_Should_ReturnSingleUploadMethodOption()
    {
        var result = _controller.GetUploadMethods(Guid.NewGuid());

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeAssignableTo<List<UploadMethodOptionModel>>().Subject;
        payload.Should().HaveCount(1);
    }

    [Fact]
    public void GetUploadMethods_Should_ReturnNonEmptyMethodNameAndRoute()
    {
        var result = _controller.GetUploadMethods(Guid.NewGuid());

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeAssignableTo<List<UploadMethodOptionModel>>().Subject;
        payload[0].Method.Should().NotBeNullOrWhiteSpace();
        payload[0].UploadApi.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Upload_Should_ReturnCreatedWithUploadedSpecification()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var model = new UploadSpecificationModel
        {
            Name = "OpenAPI Upload",
            File = CreateFormFile("spec.json"),
            SourceType = SourceType.OpenAPI,
            Version = "1.0.0",
            AutoActivate = true,
        };

        _uploadHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UploadApiSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UploadApiSpecificationCommand, CancellationToken>((command, _) => command.SavedSpecId = specId)
            .Returns(Task.CompletedTask);

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = model.Name });

        var result = await _controller.Upload(projectId, model);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/projects/{projectId}/specifications/{specId}");
        createdResult.Value.Should().BeOfType<SpecificationDetailModel>().Subject.Id.Should().Be(specId);
    }

    [Fact]
    public async Task Upload_Should_MapFormModelIntoCommand()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var model = new UploadSpecificationModel
        {
            Name = "Postman Upload",
            File = CreateFormFile("collection.json"),
            SourceType = SourceType.Postman,
            Version = "2.0.0",
            AutoActivate = false,
            UploadMethod = SpecificationUploadMethod.StorageGatewayContract,
        };
        UploadApiSpecificationCommand capturedCommand = null!;

        _uploadHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UploadApiSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UploadApiSpecificationCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedSpecId = specId;
            })
            .Returns(Task.CompletedTask);

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = model.Name });

        await _controller.Upload(projectId, model);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Name.Should().Be(model.Name);
        capturedCommand.SourceType.Should().Be(SourceType.Postman);
        capturedCommand.Version.Should().Be("2.0.0");
        capturedCommand.AutoActivate.Should().BeFalse();
    }

    [Fact]
    public async Task Upload_Should_RequestSavedSpecificationBySavedSpecId()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        GetSpecificationQuery capturedQuery = null!;

        _uploadHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UploadApiSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UploadApiSpecificationCommand, CancellationToken>((command, _) => command.SavedSpecId = specId)
            .Returns(Task.CompletedTask);

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSpecificationQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Uploaded" });

        await _controller.Upload(projectId, new UploadSpecificationModel { Name = "Uploaded", File = CreateFormFile("spec.yaml") });

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Upload_Should_ThrowValidationException_WhenCommandFails()
    {
        var projectId = Guid.NewGuid();

        _uploadHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UploadApiSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Invalid upload"));

        var act = () => _controller.Upload(projectId, new UploadSpecificationModel { Name = "Invalid", File = CreateFormFile("spec.txt") });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Invalid upload*");
    }

    [Fact]
    public async Task CreateManual_Should_ReturnCreatedWithSpecification()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var model = CreateManualModel();

        _createManualHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateManualSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateManualSpecificationCommand, CancellationToken>((command, _) => command.SavedSpecId = specId)
            .Returns(Task.CompletedTask);

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = model.Name });

        var result = await _controller.CreateManual(projectId, model);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/projects/{projectId}/specifications/{specId}");
    }

    [Fact]
    public async Task CreateManual_Should_MapBodyModelIntoCommand()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var model = CreateManualModel();
        CreateManualSpecificationCommand capturedCommand = null!;

        _createManualHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateManualSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateManualSpecificationCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedSpecId = specId;
            })
            .Returns(Task.CompletedTask);

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = model.Name });

        await _controller.CreateManual(projectId, model);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Model.Should().BeSameAs(model);
    }

    [Fact]
    public async Task CreateManual_Should_RequestSavedSpecificationBySavedSpecId()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        GetSpecificationQuery capturedQuery = null!;

        _createManualHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateManualSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateManualSpecificationCommand, CancellationToken>((command, _) => command.SavedSpecId = specId)
            .Returns(Task.CompletedTask);

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSpecificationQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Manual" });

        await _controller.CreateManual(projectId, CreateManualModel());

        capturedQuery.Should().NotBeNull();
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public async Task CreateManual_Should_ThrowValidationException_WhenCommandFails()
    {
        var projectId = Guid.NewGuid();

        _createManualHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateManualSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Manual specification invalid"));

        var act = () => _controller.CreateManual(projectId, CreateManualModel());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Manual specification invalid*");
    }

    [Fact]
    public async Task ImportCurl_Should_ReturnCreatedWithImportedSpecification()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var model = new ImportCurlModel { Name = "cURL Import", Version = "1.0.0", CurlCommand = "curl https://example.com", AutoActivate = true };

        _importCurlHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ImportCurlCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ImportCurlCommand, CancellationToken>((command, _) => command.SavedSpecId = specId)
            .Returns(Task.CompletedTask);

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = model.Name });

        var result = await _controller.ImportCurl(projectId, model);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/projects/{projectId}/specifications/{specId}");
    }

    [Fact]
    public async Task ImportCurl_Should_MapBodyModelIntoCommand()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var model = new ImportCurlModel { Name = "Capture", Version = "2.0", CurlCommand = "curl -X POST https://example.com", AutoActivate = false };
        ImportCurlCommand capturedCommand = null!;

        _importCurlHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ImportCurlCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ImportCurlCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedSpecId = specId;
            })
            .Returns(Task.CompletedTask);

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = model.Name });

        await _controller.ImportCurl(projectId, model);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Model.Should().BeSameAs(model);
    }

    [Fact]
    public async Task ImportCurl_Should_RequestSavedSpecificationBySavedSpecId()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        GetSpecificationQuery capturedQuery = null!;

        _importCurlHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ImportCurlCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ImportCurlCommand, CancellationToken>((command, _) => command.SavedSpecId = specId)
            .Returns(Task.CompletedTask);

        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSpecificationQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Imported" });

        await _controller.ImportCurl(projectId, new ImportCurlModel { Name = "Imported", CurlCommand = "curl https://example.com" });

        capturedQuery.Should().NotBeNull();
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public async Task ImportCurl_Should_ThrowValidationException_WhenCommandFails()
    {
        var projectId = Guid.NewGuid();

        _importCurlHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ImportCurlCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Invalid curl command"));

        var act = () => _controller.ImportCurl(projectId, new ImportCurlModel { Name = "Invalid", CurlCommand = "not-a-curl" });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Invalid curl command*");
    }

    [Fact]
    public async Task Activate_Should_ReturnOkWithActivatedSpecification()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _activateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ActivateSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Active Spec", IsActive = true });

        var result = await _controller.Activate(projectId, specId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<SpecificationDetailModel>().Subject.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Activate_Should_DispatchCommandWithActivateTrue()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        ActivateSpecificationCommand capturedCommand = null!;

        _activateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ActivateSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ActivateSpecificationCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);
        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Spec", IsActive = true });

        await _controller.Activate(projectId, specId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.Activate.Should().BeTrue();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SpecId.Should().Be(specId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Activate_Should_RequestSpecificationBySameIds()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        GetSpecificationQuery capturedQuery = null!;

        _activateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ActivateSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSpecificationQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Spec", IsActive = true });

        await _controller.Activate(projectId, specId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Activate_Should_ThrowNotFoundException_WhenCommandFails()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _activateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ActivateSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Specification not found"));

        var act = () => _controller.Activate(projectId, specId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Specification not found*");
    }

    [Fact]
    public async Task Deactivate_Should_ReturnOkWithDeactivatedSpecification()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _activateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ActivateSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Spec", IsActive = false });

        var result = await _controller.Deactivate(projectId, specId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<SpecificationDetailModel>().Subject.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_Should_DispatchCommandWithActivateFalse()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        ActivateSpecificationCommand capturedCommand = null!;

        _activateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ActivateSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ActivateSpecificationCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);
        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Spec", IsActive = false });

        await _controller.Deactivate(projectId, specId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.Activate.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_Should_RequestSpecificationBySameIds()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        GetSpecificationQuery capturedQuery = null!;

        _activateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ActivateSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSpecificationQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Spec", IsActive = false });

        await _controller.Deactivate(projectId, specId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public async Task Deactivate_Should_ThrowValidationException_WhenCommandFails()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _activateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ActivateSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Specification is not active"));

        var act = () => _controller.Deactivate(projectId, specId);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Specification is not active*");
    }

    [Fact]
    public async Task Delete_Should_ReturnNoContent()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(projectId, specId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_Should_DispatchDeleteCommandWithIdentifiers()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        DeleteSpecificationCommand capturedCommand = null!;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteSpecificationCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Delete(projectId, specId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SpecId.Should().Be(specId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Delete_Should_ForwardExactProjectIdAndSpecId()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        DeleteSpecificationCommand capturedCommand = null!;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteSpecificationCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Delete(projectId, specId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SpecId.Should().Be(specId);
    }

    [Fact]
    public async Task Delete_Should_ThrowNotFoundException_WhenCommandFails()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Specification not found"));

        var act = () => _controller.Delete(projectId, specId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Specification not found*");
    }

    [Fact]
    public async Task Restore_Should_ReturnOkWithRestoredSpecification()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _restoreHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<RestoreSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Restored", IsDeleted = false });

        var result = await _controller.Restore(projectId, specId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<SpecificationDetailModel>().Subject.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Restore_Should_DispatchRestoreCommandWithIdentifiers()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        RestoreSpecificationCommand capturedCommand = null!;

        _restoreHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<RestoreSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<RestoreSpecificationCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);
        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Restored" });

        await _controller.Restore(projectId, specId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SpecId.Should().Be(specId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Restore_Should_RequestSpecificationBySameIds()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        GetSpecificationQuery capturedQuery = null!;

        _restoreHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<RestoreSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _getSpecificationHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSpecificationQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSpecificationQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new SpecificationDetailModel { Id = specId, ProjectId = projectId, Name = "Restored" });

        await _controller.Restore(projectId, specId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Restore_Should_ThrowNotFoundException_WhenCommandFails()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _restoreHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<RestoreSpecificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Deleted specification not found"));

        var act = () => _controller.Restore(projectId, specId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Deleted specification not found*");
    }

    private static IFormFile CreateFormFile(string fileName)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"openapi\":\"3.0.0\"}");
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "File", fileName);
    }

    private static CreateManualSpecificationModel CreateManualModel()
    {
        return new CreateManualSpecificationModel
        {
            Name = "Manual Spec",
            Version = "1.0.0",
            AutoActivate = true,
            Endpoints = new List<ManualEndpointDefinition>
            {
                new()
                {
                    HttpMethod = "GET",
                    Path = "/api/specs",
                    Summary = "Get specs",
                },
            },
        };
    }
}
