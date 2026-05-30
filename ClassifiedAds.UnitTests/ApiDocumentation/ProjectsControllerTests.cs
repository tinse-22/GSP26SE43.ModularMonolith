using ClassifiedAds.Application;
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
using ClassifiedAds.UnitTests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class ProjectsControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<ProjectsController>> _loggerMock;
    private readonly Mock<ICommandHandler<AddUpdateProjectCommand>> _addUpdateHandlerMock;
    private readonly Mock<ICommandHandler<ArchiveProjectCommand>> _archiveHandlerMock;
    private readonly Mock<ICommandHandler<DeleteProjectCommand>> _deleteHandlerMock;
    private readonly Mock<IQueryHandler<GetProjectsQuery, PaginatedResult<ProjectModel>>> _getProjectsHandlerMock;
    private readonly Mock<IQueryHandler<GetProjectQuery, ProjectDetailModel>> _getProjectHandlerMock;
    private readonly Dispatcher _dispatcher;
    private readonly ProjectsController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public ProjectsControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<ProjectsController>>();
        _addUpdateHandlerMock = new Mock<ICommandHandler<AddUpdateProjectCommand>>();
        _archiveHandlerMock = new Mock<ICommandHandler<ArchiveProjectCommand>>();
        _deleteHandlerMock = new Mock<ICommandHandler<DeleteProjectCommand>>();
        _getProjectsHandlerMock = new Mock<IQueryHandler<GetProjectsQuery, PaginatedResult<ProjectModel>>>();
        _getProjectHandlerMock = new Mock<IQueryHandler<GetProjectQuery, ProjectDetailModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<AddUpdateProjectCommand>)))
            .Returns(_addUpdateHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<ArchiveProjectCommand>)))
            .Returns(_archiveHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<DeleteProjectCommand>)))
            .Returns(_deleteHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetProjectsQuery, PaginatedResult<ProjectModel>>)))
            .Returns(_getProjectsHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetProjectQuery, ProjectDetailModel>)))
            .Returns(_getProjectHandlerMock.Object);

        _dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new ProjectsController(_dispatcher, _currentUserMock.Object, _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "projects-controller-tests";
        httpContext.Request.Headers.Origin = "http://localhost:3000";
        httpContext.Request.Headers.Referer = "http://localhost:3000/projects";
        httpContext.Request.Headers.UserAgent = "unit-test";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };
    }

    [Fact]
    public async Task Get_Should_ReturnOkWithPaginatedProjects()
    {
        var expected = new PaginatedResult<ProjectModel>
        {
            Items =
            {
                new ProjectModel { Id = Guid.NewGuid(), Name = "Project A", Status = ProjectStatus.Active.ToString() },
                new ProjectModel { Id = Guid.NewGuid(), Name = "Project B", Status = ProjectStatus.Active.ToString() },
            },
            TotalCount = 2,
            Page = 1,
            PageSize = 20,
        };

        _getProjectsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.Get(ProjectStatus.Active, "llm", 1, 20);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<PaginatedResult<ProjectModel>>().Subject;
        payload.TotalCount.Should().Be(2);
        payload.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Get_Should_PassFilterParametersAndCurrentUser()
    {
        GetProjectsQuery capturedQuery = null!;

        _getProjectsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetProjectsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new PaginatedResult<ProjectModel>());

        await _controller.Get(ProjectStatus.Archived, "search-value", 3, 15);

        capturedQuery.Should().NotBeNull();
        capturedQuery.OwnerId.Should().Be(_currentUserId);
        capturedQuery.Status.Should().Be(ProjectStatus.Archived);
        capturedQuery.Search.Should().Be("search-value");
        capturedQuery.Page.Should().Be(3);
        capturedQuery.PageSize.Should().Be(15);
    }

    [Fact]
    public async Task Get_Should_UseDefaultPagingValues_WhenParametersOmitted()
    {
        GetProjectsQuery capturedQuery = null!;

        _getProjectsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetProjectsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new PaginatedResult<ProjectModel>());

        await _controller.Get(null, null);

        capturedQuery.Should().NotBeNull();
        capturedQuery.Page.Should().Be(1);
        capturedQuery.PageSize.Should().Be(20);
        capturedQuery.Search.Should().BeNull();
        capturedQuery.Status.Should().BeNull();
    }

    [Fact]
    public async Task Get_Should_ReturnOkWithEmptyProjectList()
    {
        _getProjectsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResult<ProjectModel>
            {
                Items = new List<ProjectModel>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 20,
            });

        var result = await _controller.Get(null, "missing");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<PaginatedResult<ProjectModel>>().Subject;
        payload.Items.Should().BeEmpty();
        payload.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetById_Should_ReturnOkWithProjectDetails()
    {
        var projectId = Guid.NewGuid();
        var expected = new ProjectDetailModel
        {
            Id = projectId,
            Name = "Project Detail",
            Status = ProjectStatus.Active.ToString(),
            TotalSpecifications = 2,
        };

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.Get(projectId, includeArchived: true, includeSpecifications: true);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<ProjectDetailModel>().Subject;
        payload.Id.Should().Be(projectId);
        payload.TotalSpecifications.Should().Be(2);
    }

    [Fact]
    public async Task GetById_Should_PassRouteAndQueryFlags()
    {
        var projectId = Guid.NewGuid();
        GetProjectQuery capturedQuery = null!;

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetProjectQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new ProjectDetailModel { Id = projectId, Name = "Project" });

        await _controller.Get(projectId, includeArchived: true, includeSpecifications: false);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
        capturedQuery.IncludeArchived.Should().BeTrue();
        capturedQuery.IncludeSpecifications.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_Should_DefaultIncludeFlagsToFalse()
    {
        var projectId = Guid.NewGuid();
        GetProjectQuery capturedQuery = null!;

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetProjectQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new ProjectDetailModel { Id = projectId, Name = "Project" });

        await _controller.Get(projectId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.IncludeArchived.Should().BeFalse();
        capturedQuery.IncludeSpecifications.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_Should_ThrowNotFoundException_WhenHandlerFails()
    {
        var projectId = Guid.NewGuid();

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Project not found"));

        var act = () => _controller.Get(projectId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Project not found*");
    }

    [Fact]
    public async Task Post_Should_ReturnCreatedWithCreatedProject()
    {
        var savedProjectId = Guid.NewGuid();
        var model = new CreateUpdateProjectModel
        {
            Name = "New Project",
            Description = "API automation project",
            BaseUrl = "https://example.com",
        };

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateProjectCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateProjectCommand, CancellationToken>((command, _) => command.SavedProjectId = savedProjectId)
            .Returns(Task.CompletedTask);

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectDetailModel
            {
                Id = savedProjectId,
                Name = model.Name,
                Description = model.Description,
                BaseUrl = model.BaseUrl,
                Status = ProjectStatus.Active.ToString(),
            });

        var result = await _controller.Post(model);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/projects/{savedProjectId}");
        var payload = createdResult.Value.Should().BeOfType<ProjectDetailModel>().Subject;
        payload.Id.Should().Be(savedProjectId);
        payload.Name.Should().Be(model.Name);
    }

    [Fact]
    public async Task Post_Should_DispatchCreateCommandWithModelAndCurrentUser()
    {
        AddUpdateProjectCommand capturedCommand = null!;
        var savedProjectId = Guid.NewGuid();
        var model = new CreateUpdateProjectModel
        {
            Name = "Create Command Project",
            Description = "Description",
            BaseUrl = "https://create.example.com",
        };

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateProjectCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateProjectCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedProjectId = savedProjectId;
            })
            .Returns(Task.CompletedTask);

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectDetailModel { Id = savedProjectId, Name = model.Name });

        await _controller.Post(model);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().BeNull();
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Model.Should().BeSameAs(model);
    }

    [Fact]
    public async Task Post_Should_RequestCreatedProjectBySavedProjectId()
    {
        var savedProjectId = Guid.NewGuid();
        GetProjectQuery capturedQuery = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateProjectCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateProjectCommand, CancellationToken>((command, _) => command.SavedProjectId = savedProjectId)
            .Returns(Task.CompletedTask);

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetProjectQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new ProjectDetailModel { Id = savedProjectId, Name = "Created Project" });

        await _controller.Post(new CreateUpdateProjectModel { Name = "Created Project" });

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(savedProjectId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Post_Should_ThrowValidationException_WhenCreateCommandFails()
    {
        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateProjectCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Duplicate project name"));

        var act = () => _controller.Post(new CreateUpdateProjectModel { Name = "Duplicate" });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Duplicate project name*");
    }

    [Fact]
    public async Task Put_Should_ReturnOkWithUpdatedProject()
    {
        var projectId = Guid.NewGuid();
        var model = new CreateUpdateProjectModel
        {
            Name = "Updated Project",
            Description = "Updated description",
            BaseUrl = "https://updated.example.com",
        };

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateProjectCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateProjectCommand, CancellationToken>((command, _) => command.SavedProjectId = projectId)
            .Returns(Task.CompletedTask);

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectDetailModel
            {
                Id = projectId,
                Name = model.Name,
                Description = model.Description,
                BaseUrl = model.BaseUrl,
                Status = ProjectStatus.Active.ToString(),
            });

        var result = await _controller.Put(projectId, model);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<ProjectDetailModel>().Subject;
        payload.Id.Should().Be(projectId);
        payload.Name.Should().Be(model.Name);
    }

    [Fact]
    public async Task Put_Should_DispatchUpdateCommandWithProjectId()
    {
        var projectId = Guid.NewGuid();
        AddUpdateProjectCommand capturedCommand = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateProjectCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateProjectCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedProjectId = projectId;
            })
            .Returns(Task.CompletedTask);

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectDetailModel { Id = projectId, Name = "Updated" });

        await _controller.Put(projectId, new CreateUpdateProjectModel { Name = "Updated" });

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Put_Should_RequestUpdatedProjectBySavedProjectId()
    {
        var projectId = Guid.NewGuid();
        GetProjectQuery capturedQuery = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateProjectCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateProjectCommand, CancellationToken>((command, _) => command.SavedProjectId = projectId)
            .Returns(Task.CompletedTask);

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetProjectQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new ProjectDetailModel { Id = projectId, Name = "Updated Project" });

        await _controller.Put(projectId, new CreateUpdateProjectModel { Name = "Updated Project" });

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Put_Should_ThrowNotFoundException_WhenUpdateCommandFails()
    {
        var projectId = Guid.NewGuid();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateProjectCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Project not found"));

        var act = () => _controller.Put(projectId, new CreateUpdateProjectModel { Name = "Missing Project" });

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Project not found*");
    }

    [Fact]
    public async Task Archive_Should_DispatchArchiveCommandAndReturnArchivedProject()
    {
        var projectId = Guid.NewGuid();
        ArchiveProjectCommand capturedCommand = null!;

        _archiveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ArchiveProjectCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ArchiveProjectCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectDetailModel
            {
                Id = projectId,
                Name = "Archived Project",
                Status = ProjectStatus.Archived.ToString(),
            });

        var result = await _controller.Archive(projectId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Archive.Should().BeTrue();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<ProjectDetailModel>().Subject;
        payload.Status.Should().Be(ProjectStatus.Archived.ToString());
    }

    [Fact]
    public async Task Archive_Should_ThrowNotFoundException_WhenProjectDoesNotExist()
    {
        var projectId = Guid.NewGuid();

        _archiveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ArchiveProjectCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Project not found"));

        var act = () => _controller.Archive(projectId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Project not found*");
    }

    [Fact]
    public async Task Archive_Should_ThrowValidationException_WhenCurrentUserDoesNotOwnProject()
    {
        var projectId = Guid.NewGuid();

        _archiveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ArchiveProjectCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Ban khong co quyen thao tac project nay."));

        var act = () => _controller.Archive(projectId);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*khong co quyen thao tac project nay*");
    }

    [Fact]
    public async Task Unarchive_Should_DispatchArchiveCommandWithArchiveFalse()
    {
        var projectId = Guid.NewGuid();
        ArchiveProjectCommand capturedCommand = null!;

        _archiveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ArchiveProjectCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ArchiveProjectCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        _getProjectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProjectQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectDetailModel
            {
                Id = projectId,
                Name = "Restored Project",
                Status = ProjectStatus.Active.ToString(),
            });

        var result = await _controller.Unarchive(projectId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Archive.Should().BeFalse();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<ProjectDetailModel>().Subject;
        payload.Status.Should().Be(ProjectStatus.Active.ToString());
    }

    [Fact]
    public async Task Unarchive_Should_ThrowNotFoundException_WhenProjectDoesNotExist()
    {
        var projectId = Guid.NewGuid();

        _archiveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ArchiveProjectCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Project not found"));

        var act = () => _controller.Unarchive(projectId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Project not found*");
    }

    [Fact]
    public async Task Unarchive_Should_ThrowValidationException_WhenCurrentUserDoesNotOwnProject()
    {
        var projectId = Guid.NewGuid();

        _archiveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ArchiveProjectCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Ban khong co quyen thao tac project nay."));

        var act = () => _controller.Unarchive(projectId);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*khong co quyen thao tac project nay*");
    }

    [Fact]
    public async Task Delete_Should_ReturnOk()
    {
        var projectId = Guid.NewGuid();

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteProjectCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(projectId);

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task Delete_Should_DispatchDeleteCommandWithCurrentUser()
    {
        var projectId = Guid.NewGuid();
        DeleteProjectCommand capturedCommand = null!;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteProjectCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteProjectCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Delete(projectId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Delete_Should_ThrowNotFoundException_WhenHandlerFails()
    {
        var projectId = Guid.NewGuid();

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteProjectCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Project not found"));

        var act = () => _controller.Delete(projectId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Project not found*");
    }

    [Fact]
    public async Task Delete_Should_DispatchExactProjectId()
    {
        var projectId = Guid.NewGuid();
        DeleteProjectCommand capturedCommand = null!;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteProjectCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteProjectCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Delete(projectId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
    }
}

public class ProjectsCommandValidationTests
{
    private readonly Mock<ICrudService<Project>> _projectServiceMock;
    private readonly Mock<IRepository<Project, Guid>> _projectRepositoryMock;
    private readonly Mock<ISubscriptionLimitGatewayService> _subscriptionLimitMock;
    private readonly AddUpdateProjectCommandHandler _handler;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public ProjectsCommandValidationTests()
    {
        _projectServiceMock = new Mock<ICrudService<Project>>();
        _projectRepositoryMock = new Mock<IRepository<Project, Guid>>();
        _subscriptionLimitMock = new Mock<ISubscriptionLimitGatewayService>();

        SetupProjects(Array.Empty<Project>());
        _subscriptionLimitMock
            .Setup(x => x.TryConsumeLimitAsync(
                It.IsAny<Guid>(),
                It.IsAny<LimitType>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });

        _handler = new AddUpdateProjectCommandHandler(
            _projectServiceMock.Object,
            _projectRepositoryMock.Object,
            _subscriptionLimitMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenModelIsNull()
    {
        var command = new AddUpdateProjectCommand
        {
            CurrentUserId = _currentUserId,
            Model = null!,
        };

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenNameIsNull()
    {
        var command = CreateCreateCommand(new CreateUpdateProjectModel
        {
            Name = null!,
            Description = "Description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenNameIsWhitespaceOnly()
    {
        var command = CreateCreateCommand(new CreateUpdateProjectModel
        {
            Name = "   ",
            Description = "Description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenNameLengthExceeds200()
    {
        var command = CreateCreateCommand(new CreateUpdateProjectModel
        {
            Name = new string('a', 201),
            Description = "Description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenDescriptionLengthExceeds2000()
    {
        var command = CreateCreateCommand(new CreateUpdateProjectModel
        {
            Name = "Project",
            Description = new string('d', 2001),
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenBaseUrlIsNotAbsolute()
    {
        var command = CreateCreateCommand(new CreateUpdateProjectModel
        {
            Name = "Project",
            Description = "Description",
            BaseUrl = "not-a-url",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenDuplicateProjectNameExistsForSameOwner()
    {
        SetupProjects(new[]
        {
            new Project
            {
                Id = Guid.NewGuid(),
                OwnerId = _currentUserId,
                Name = "Existing Project",
                Status = ProjectStatus.Active,
            },
        });

        var command = CreateCreateCommand(new CreateUpdateProjectModel
        {
            Name = " Existing Project ",
            Description = "Description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenProjectLimitIsDenied()
    {
        _subscriptionLimitMock
            .Setup(x => x.TryConsumeLimitAsync(
                _currentUserId,
                LimitType.MaxProjects,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO
            {
                IsAllowed = false,
                DenialReason = "Project limit exceeded",
            });

        var command = CreateCreateCommand(new CreateUpdateProjectModel
        {
            Name = "Project",
            Description = "Description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Project limit exceeded*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFoundException_WhenUpdatingMissingProject()
    {
        SetupProjects(Array.Empty<Project>());

        var command = CreateUpdateCommand(Guid.NewGuid(), new CreateUpdateProjectModel
        {
            Name = "Updated Project",
            Description = "Description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenUpdatingWithNullName()
    {
        var projectId = Guid.NewGuid();
        SetupProjects(new[]
        {
            new Project
            {
                Id = projectId,
                OwnerId = _currentUserId,
                Name = "Existing Project",
                Status = ProjectStatus.Active,
            },
        });

        var command = CreateUpdateCommand(projectId, new CreateUpdateProjectModel
        {
            Name = null!,
            Description = "Updated description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenUpdatingWithWhitespaceName()
    {
        var projectId = Guid.NewGuid();
        SetupProjects(new[]
        {
            new Project
            {
                Id = projectId,
                OwnerId = _currentUserId,
                Name = "Existing Project",
                Status = ProjectStatus.Active,
            },
        });

        var command = CreateUpdateCommand(projectId, new CreateUpdateProjectModel
        {
            Name = "   ",
            Description = "Updated description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenUpdatingWithNameLengthExceeds200()
    {
        var projectId = Guid.NewGuid();
        SetupProjects(new[]
        {
            new Project
            {
                Id = projectId,
                OwnerId = _currentUserId,
                Name = "Existing Project",
                Status = ProjectStatus.Active,
            },
        });

        var command = CreateUpdateCommand(projectId, new CreateUpdateProjectModel
        {
            Name = new string('a', 201),
            Description = "Updated description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenUpdatingWithDescriptionLengthExceeds2000()
    {
        var projectId = Guid.NewGuid();
        SetupProjects(new[]
        {
            new Project
            {
                Id = projectId,
                OwnerId = _currentUserId,
                Name = "Existing Project",
                Status = ProjectStatus.Active,
            },
        });

        var command = CreateUpdateCommand(projectId, new CreateUpdateProjectModel
        {
            Name = "Updated Project",
            Description = new string('d', 2001),
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenUpdatingWithDuplicateNameOfAnotherProject()
    {
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        SetupProjects(new[]
        {
            new Project
            {
                Id = projectId,
                OwnerId = _currentUserId,
                Name = "Current Project",
                Status = ProjectStatus.Active,
            },
            new Project
            {
                Id = otherProjectId,
                OwnerId = _currentUserId,
                Name = "Duplicate Name",
                Status = ProjectStatus.Active,
            },
        });

        var command = CreateUpdateCommand(projectId, new CreateUpdateProjectModel
        {
            Name = " Duplicate Name ",
            Description = "Updated description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_TrimFields_WhenUpdatingProject()
    {
        var projectId = Guid.NewGuid();
        var existingProject = new Project
        {
            Id = projectId,
            OwnerId = _currentUserId,
            Name = "Existing Project",
            Description = "Old description",
            BaseUrl = "https://old.example.com",
            Status = ProjectStatus.Active,
        };
        SetupProjects(new[] { existingProject });

        _projectServiceMock
            .Setup(x => x.UpdateAsync(existingProject, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = CreateUpdateCommand(projectId, new CreateUpdateProjectModel
        {
            Name = "  Updated Project  ",
            Description = "  Updated description  ",
            BaseUrl = "  https://updated.example.com  ",
        });

        await _handler.HandleAsync(command);

        existingProject.Name.Should().Be("Updated Project");
        existingProject.Description.Should().Be("Updated description");
        existingProject.BaseUrl.Should().Be("https://updated.example.com");
        command.SavedProjectId.Should().Be(projectId);
        _projectServiceMock.Verify(x => x.UpdateAsync(existingProject, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenUpdatingProjectOwnedByAnotherUser()
    {
        var projectId = Guid.NewGuid();
        SetupProjects(new[]
        {
            new Project
            {
                Id = projectId,
                OwnerId = Guid.NewGuid(),
                Name = "Another Owner Project",
                Status = ProjectStatus.Active,
            },
        });

        var command = CreateUpdateCommand(projectId, new CreateUpdateProjectModel
        {
            Name = "Updated Project",
            Description = "Description",
            BaseUrl = "https://example.com",
        });

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_TrimFields_WhenCreatingProject()
    {
        Project savedProject = null!;
        _projectServiceMock
            .Setup(x => x.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Callback<Project, CancellationToken>((project, _) => savedProject = project)
            .Returns(Task.CompletedTask);

        var command = CreateCreateCommand(new CreateUpdateProjectModel
        {
            Name = "  New Project  ",
            Description = "  Description  ",
            BaseUrl = "https://example.com",
        });

        await _handler.HandleAsync(command);

        savedProject.Should().NotBeNull();
        savedProject.Name.Should().Be("New Project");
        savedProject.Description.Should().Be("Description");
        savedProject.BaseUrl.Should().Be("https://example.com");
        savedProject.OwnerId.Should().Be(_currentUserId);
        savedProject.Status.Should().Be(ProjectStatus.Active);
    }

    private AddUpdateProjectCommand CreateCreateCommand(CreateUpdateProjectModel model)
    {
        return new AddUpdateProjectCommand
        {
            CurrentUserId = _currentUserId,
            Model = model,
        };
    }

    private AddUpdateProjectCommand CreateUpdateCommand(Guid projectId, CreateUpdateProjectModel model)
    {
        return new AddUpdateProjectCommand
        {
            CurrentUserId = _currentUserId,
            ProjectId = projectId,
            Model = model,
        };
    }

    private void SetupProjects(IEnumerable<Project> projects)
    {
        var queryable = new TestAsyncEnumerable<Project>(projects);
        _projectRepositoryMock.Setup(x => x.GetQueryableSet()).Returns(queryable);
        _projectRepositoryMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync((IQueryable<Project> query) => query.FirstOrDefault());
    }
}
