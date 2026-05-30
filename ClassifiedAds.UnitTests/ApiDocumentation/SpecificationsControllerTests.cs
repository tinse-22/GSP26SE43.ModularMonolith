using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Storage.DTOs;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Controllers;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using ClassifiedAds.Modules.ApiDocumentation.Queries;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using ClassifiedAds.UnitTests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class SpecificationsControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<SpecificationsController>> _loggerMock;
    private readonly Mock<ICommandHandler<UploadApiSpecificationCommand>> _uploadHandlerMock;
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
        _activateHandlerMock = new Mock<ICommandHandler<ActivateSpecificationCommand>>();
        _deleteHandlerMock = new Mock<ICommandHandler<DeleteSpecificationCommand>>();
        _restoreHandlerMock = new Mock<ICommandHandler<RestoreSpecificationCommand>>();
        _getSpecificationsHandlerMock = new Mock<IQueryHandler<GetSpecificationsQuery, List<SpecificationModel>>>();
        _getSpecificationHandlerMock = new Mock<IQueryHandler<GetSpecificationQuery, SpecificationDetailModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<UploadApiSpecificationCommand>))).Returns(_uploadHandlerMock.Object);
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
}

public class SpecificationsCommandValidationTests
{
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Mock<IRepository<Project, Guid>> _projectRepositoryMock = new();
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepositoryMock = new();
    private readonly Mock<IRepository<ApiEndpoint, Guid>> _endpointRepositoryMock = new();
    private readonly Mock<IRepository<EndpointParameter, Guid>> _parameterRepositoryMock = new();
    private readonly Mock<IRepository<EndpointResponse, Guid>> _responseRepositoryMock = new();
    private readonly Mock<ICrudService<ApiSpecification>> _specServiceMock = new();
    private readonly Mock<IStorageFileGatewayService> _storageGatewayMock = new();
    private readonly Mock<ISubscriptionLimitGatewayService> _subscriptionLimitMock = new();
    private readonly Mock<ILogger<UploadApiSpecificationCommandHandler>> _uploadLoggerMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    public SpecificationsCommandValidationTests()
    {
        _projectRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _specRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _endpointRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _parameterRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _responseRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>((operation, _, token) => operation(token));

        _subscriptionLimitMock
            .Setup(x => x.TryConsumeLimitAsync(
                It.IsAny<Guid>(),
                It.IsAny<LimitType>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });

        SetupProjects(Array.Empty<Project>());
        SetupSpecifications(Array.Empty<ApiSpecification>());
    }

    [Fact]
    public async Task UploadValidation_Should_ThrowValidationException_WhenFileIsNull()
    {
        var handler = CreateUploadHandler();
        var command = new UploadApiSpecificationCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = _currentUserId,
            Name = "Spec",
            SourceType = SourceType.OpenAPI,
            File = null!,
        };

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UploadValidation_Should_ThrowValidationException_WhenFileIsEmpty()
    {
        var handler = CreateUploadHandler();
        var command = new UploadApiSpecificationCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = _currentUserId,
            Name = "Spec",
            SourceType = SourceType.OpenAPI,
            File = CreateFormFile("spec.json", ""),
        };

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UploadValidation_Should_ThrowValidationException_WhenNameIsWhitespaceOnly()
    {
        var handler = CreateUploadHandler();
        var command = new UploadApiSpecificationCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = _currentUserId,
            Name = "   ",
            SourceType = SourceType.OpenAPI,
            File = CreateFormFile("spec.json"),
        };

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UploadValidation_Should_ThrowValidationException_WhenNameLengthExceeds200()
    {
        var handler = CreateUploadHandler();
        var command = new UploadApiSpecificationCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = _currentUserId,
            Name = new string('a', 201),
            SourceType = SourceType.OpenAPI,
            File = CreateFormFile("spec.json"),
        };

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UploadValidation_Should_ThrowValidationException_WhenSourceTypeIsInvalid()
    {
        var handler = CreateUploadHandler();
        var command = new UploadApiSpecificationCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = _currentUserId,
            Name = "Spec",
            SourceType = SourceType.Manual,
            File = CreateFormFile("spec.json"),
        };

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ActivateValidation_Should_ThrowValidationException_WhenCurrentUserDoesNotOwnProject()
    {
        SetupProjects(new[]
        {
            new Project { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Name = "Project", Status = ProjectStatus.Active },
        });

        var handler = CreateActivateHandler();
        var command = new ActivateSpecificationCommand
        {
            ProjectId = _projectRepositoryMock.Object.GetQueryableSet().First().Id,
            SpecId = Guid.NewGuid(),
            CurrentUserId = _currentUserId,
            Activate = true,
        };

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task DeactivateValidation_Should_ThrowValidationException_WhenSpecificationIsNotActive()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        SetupProjects(new[]
        {
            new Project { Id = projectId, OwnerId = _currentUserId, Name = "Project", Status = ProjectStatus.Active, ActiveSpecId = Guid.NewGuid() },
        });
        SetupSpecifications(new[]
        {
            new ApiSpecification { Id = specId, ProjectId = projectId, Name = "Spec", IsActive = false },
        });

        var handler = CreateActivateHandler();
        var command = new ActivateSpecificationCommand
        {
            ProjectId = projectId,
            SpecId = specId,
            CurrentUserId = _currentUserId,
            Activate = false,
        };

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task DeleteValidation_Should_ThrowValidationException_WhenCurrentUserDoesNotOwnProject()
    {
        var projectId = Guid.NewGuid();
        SetupProjects(new[]
        {
            new Project { Id = projectId, OwnerId = Guid.NewGuid(), Name = "Project", Status = ProjectStatus.Active },
        });

        var handler = CreateDeleteHandler();
        var command = new DeleteSpecificationCommand
        {
            ProjectId = projectId,
            SpecId = Guid.NewGuid(),
            CurrentUserId = _currentUserId,
        };

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RestoreValidation_Should_ThrowValidationException_WhenCurrentUserDoesNotOwnProject()
    {
        var projectId = Guid.NewGuid();
        SetupProjects(new[]
        {
            new Project { Id = projectId, OwnerId = Guid.NewGuid(), Name = "Project", Status = ProjectStatus.Active },
        });

        var handler = CreateRestoreHandler();
        var command = new RestoreSpecificationCommand
        {
            ProjectId = projectId,
            SpecId = Guid.NewGuid(),
            CurrentUserId = _currentUserId,
        };

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    private UploadApiSpecificationCommandHandler CreateUploadHandler()
    {
        return new UploadApiSpecificationCommandHandler(
            _projectRepositoryMock.Object,
            _specRepositoryMock.Object,
            _endpointRepositoryMock.Object,
            _parameterRepositoryMock.Object,
            _responseRepositoryMock.Object,
            _specServiceMock.Object,
            _storageGatewayMock.Object,
            _subscriptionLimitMock.Object,
            _uploadLoggerMock.Object);
    }

    private ActivateSpecificationCommandHandler CreateActivateHandler()
    {
        return new ActivateSpecificationCommandHandler(
            new Dispatcher(new Mock<IServiceProvider>().Object),
            _projectRepositoryMock.Object,
            _specRepositoryMock.Object);
    }

    private DeleteSpecificationCommandHandler CreateDeleteHandler()
    {
        return new DeleteSpecificationCommandHandler(
            new Dispatcher(new Mock<IServiceProvider>().Object),
            _projectRepositoryMock.Object,
            _specRepositoryMock.Object);
    }

    private RestoreSpecificationCommandHandler CreateRestoreHandler()
    {
        return new RestoreSpecificationCommandHandler(
            _projectRepositoryMock.Object,
            _specRepositoryMock.Object);
    }

    private void SetupProjects(IEnumerable<Project> projects)
    {
        var queryable = new TestAsyncEnumerable<Project>(projects);
        _projectRepositoryMock.Setup(x => x.GetQueryableSet()).Returns(queryable);
        _projectRepositoryMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync((IQueryable<Project> query) => query.FirstOrDefault());
    }

    private void SetupSpecifications(IEnumerable<ApiSpecification> specs)
    {
        var queryable = new TestAsyncEnumerable<ApiSpecification>(specs);
        _specRepositoryMock.Setup(x => x.GetQueryableSet()).Returns(queryable);
        _specRepositoryMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync((IQueryable<ApiSpecification> query) => query.FirstOrDefault());
    }

    private static IFormFile CreateFormFile(string fileName, string content = "{\"openapi\":\"3.0.0\"}")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "File", fileName);
    }
}
