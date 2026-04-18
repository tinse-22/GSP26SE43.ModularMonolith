using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Queries;
using ClassifiedAds.UnitTests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class GetProjectsQueryHandlerTests
{
    private readonly Mock<IRepository<Project, Guid>> _projectRepoMock = new();
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock = new();

    [Fact]
    public async Task HandleAsync_Should_ReturnOnlyActiveProjects_WhenStatusNotProvided()
    {
        var ownerId = Guid.NewGuid();
        var activeProject = new Project { Id = Guid.NewGuid(), OwnerId = ownerId, Name = "Active", Status = ProjectStatus.Active, CreatedDateTime = DateTimeOffset.UtcNow };
        var archivedProject = new Project { Id = Guid.NewGuid(), OwnerId = ownerId, Name = "Archived", Status = ProjectStatus.Archived, CreatedDateTime = DateTimeOffset.UtcNow.AddMinutes(-1) };

        SetupProjectQueryable(new[] { activeProject, archivedProject });
        SetupSpecificationQueryable(Array.Empty<ApiSpecification>());

        var handler = new GetProjectsQueryHandler(_projectRepoMock.Object, _specRepoMock.Object);

        var result = await handler.HandleAsync(new GetProjectsQuery
        {
            OwnerId = ownerId,
        });

        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(activeProject.Id);
        result.Items[0].Status.Should().Be(ProjectStatus.Active.ToString());
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnArchivedProjects_WhenStatusArchivedRequested()
    {
        var ownerId = Guid.NewGuid();
        var activeProject = new Project { Id = Guid.NewGuid(), OwnerId = ownerId, Name = "Active", Status = ProjectStatus.Active, CreatedDateTime = DateTimeOffset.UtcNow };
        var archivedProject = new Project { Id = Guid.NewGuid(), OwnerId = ownerId, Name = "Archived", Status = ProjectStatus.Archived, CreatedDateTime = DateTimeOffset.UtcNow.AddMinutes(-1) };

        SetupProjectQueryable(new[] { activeProject, archivedProject });
        SetupSpecificationQueryable(Array.Empty<ApiSpecification>());

        var handler = new GetProjectsQueryHandler(_projectRepoMock.Object, _specRepoMock.Object);

        var result = await handler.HandleAsync(new GetProjectsQuery
        {
            OwnerId = ownerId,
            Status = ProjectStatus.Archived,
        });

        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(archivedProject.Id);
        result.Items[0].Status.Should().Be(ProjectStatus.Archived.ToString());
    }

    private void SetupProjectQueryable(IEnumerable<Project> projects)
    {
        var queryable = new TestAsyncEnumerable<Project>(projects);
        _projectRepoMock.Setup(x => x.GetQueryableSet()).Returns(queryable);
    }

    private void SetupSpecificationQueryable(IEnumerable<ApiSpecification> specifications)
    {
        var queryable = new TestAsyncEnumerable<ApiSpecification>(specifications);
        _specRepoMock.Setup(x => x.GetQueryableSet()).Returns(queryable);
    }
}

public class GetProjectQueryHandlerTests
{
    private readonly Mock<IRepository<Project, Guid>> _projectRepoMock = new();
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock = new();
    private readonly Mock<IRepository<ApiEndpoint, Guid>> _endpointRepoMock = new();

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_ForArchivedProject_WhenIncludeArchivedIsFalse()
    {
        var ownerId = Guid.NewGuid();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = "Archived",
            Status = ProjectStatus.Archived,
        };

        SetupProjectRepository(new[] { project });
        SetupSpecificationRepository(Array.Empty<ApiSpecification>());
        SetupEndpointRepository(Array.Empty<ApiEndpoint>());

        var handler = new GetProjectQueryHandler(_projectRepoMock.Object, _specRepoMock.Object, _endpointRepoMock.Object);

        var act = () => handler.HandleAsync(new GetProjectQuery
        {
            ProjectId = project.Id,
            OwnerId = ownerId,
        });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnSpecifications_ForArchivedProject_WhenExplicitlyIncluded()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var activeSpecId = Guid.NewGuid();
        var archivedProject = new Project
        {
            Id = projectId,
            OwnerId = ownerId,
            Name = "Archived",
            Status = ProjectStatus.Archived,
            ActiveSpecId = activeSpecId,
        };
        var activeSpec = new ApiSpecification
        {
            Id = activeSpecId,
            ProjectId = projectId,
            Name = "Spec A",
            Version = "v1",
            SourceType = SourceType.Manual,
            ParseStatus = ParseStatus.Success,
            IsActive = false,
            CreatedDateTime = DateTimeOffset.UtcNow,
        };
        var anotherSpec = new ApiSpecification
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Spec B",
            Version = "v2",
            SourceType = SourceType.OpenApi,
            ParseStatus = ParseStatus.Success,
            IsActive = false,
            CreatedDateTime = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        var deletedSpec = new ApiSpecification
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Deleted Spec",
            Version = "v0",
            SourceType = SourceType.Manual,
            ParseStatus = ParseStatus.Success,
            IsDeleted = true,
            CreatedDateTime = DateTimeOffset.UtcNow.AddMinutes(-2),
        };
        var endpoints = new[]
        {
            new ApiEndpoint { Id = Guid.NewGuid(), ApiSpecId = activeSpecId, Path = "/api/a", HttpMethod = HttpMethod.GET },
            new ApiEndpoint { Id = Guid.NewGuid(), ApiSpecId = activeSpecId, Path = "/api/b", HttpMethod = HttpMethod.POST },
        };

        SetupProjectRepository(new[] { archivedProject });
        SetupSpecificationRepository(new[] { activeSpec, anotherSpec, deletedSpec });
        SetupEndpointRepository(endpoints);

        var handler = new GetProjectQueryHandler(_projectRepoMock.Object, _specRepoMock.Object, _endpointRepoMock.Object);

        var result = await handler.HandleAsync(new GetProjectQuery
        {
            ProjectId = projectId,
            OwnerId = ownerId,
            IncludeArchived = true,
            IncludeSpecifications = true,
        });

        result.Id.Should().Be(projectId);
        result.Status.Should().Be(ProjectStatus.Archived.ToString());
        result.TotalSpecifications.Should().Be(3);
        result.ActiveSpecSummary.Should().NotBeNull();
        result.ActiveSpecSummary.EndpointCount.Should().Be(2);
        result.Specifications.Should().HaveCount(2);
        result.Specifications.Select(x => x.Id).Should().BeEquivalentTo(new[] { activeSpec.Id, anotherSpec.Id });
    }

    private void SetupProjectRepository(IEnumerable<Project> projects)
    {
        var queryable = new TestAsyncEnumerable<Project>(projects);
        _projectRepoMock.Setup(x => x.GetQueryableSet()).Returns(queryable);
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync((IQueryable<Project> query) => query.FirstOrDefault());
    }

    private void SetupSpecificationRepository(IEnumerable<ApiSpecification> specifications)
    {
        var queryable = new TestAsyncEnumerable<ApiSpecification>(specifications);
        _specRepoMock.Setup(x => x.GetQueryableSet()).Returns(queryable);
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync((IQueryable<ApiSpecification> query) => query.FirstOrDefault());
    }

    private void SetupEndpointRepository(IEnumerable<ApiEndpoint> endpoints)
    {
        var queryable = new TestAsyncEnumerable<ApiEndpoint>(endpoints);
        _endpointRepoMock.Setup(x => x.GetQueryableSet()).Returns(queryable);
        _endpointRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiEndpoint>>()))
            .ReturnsAsync((IQueryable<ApiEndpoint> query) => query.FirstOrDefault());
    }
}
