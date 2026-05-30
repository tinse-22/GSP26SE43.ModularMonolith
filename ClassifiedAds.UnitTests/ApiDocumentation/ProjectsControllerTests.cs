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
