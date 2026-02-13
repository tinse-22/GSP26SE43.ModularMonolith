using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class DeleteEndpointCommandHandlerTests
{
    private readonly Mock<Dispatcher> _dispatcherMock;
    private readonly Mock<IRepository<Project, Guid>> _projectRepoMock;
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock;
    private readonly Mock<IRepository<ApiEndpoint, Guid>> _endpointRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly DeleteEndpointCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _specId = Guid.NewGuid();

    public DeleteEndpointCommandHandlerTests()
    {
        _dispatcherMock = new Mock<Dispatcher>(new Mock<IServiceProvider>().Object);
        _projectRepoMock = new Mock<IRepository<Project, Guid>>();
        _specRepoMock = new Mock<IRepository<ApiSpecification, Guid>>();
        _endpointRepoMock = new Mock<IRepository<ApiEndpoint, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _endpointRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _handler = new DeleteEndpointCommandHandler(
            _dispatcherMock.Object,
            _projectRepoMock.Object,
            _specRepoMock.Object,
            _endpointRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_Should_DeleteEndpoint()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        var project = new Project { Id = _projectId, OwnerId = _userId };
        var spec = new ApiSpecification { Id = _specId, ProjectId = _projectId };
        var endpoint = new ApiEndpoint { Id = endpointId, ApiSpecId = _specId };

        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync(spec);
        _endpointRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiEndpoint>>()))
            .ReturnsAsync(endpoint);

        var command = new DeleteEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            EndpointId = endpointId,
            CurrentUserId = _userId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _endpointRepoMock.Verify(x => x.Delete(It.Is<ApiEndpoint>(e => e.Id == endpointId)), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ProjectNotFound_Should_ThrowNotFoundException()
    {
        // Arrange
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync((Project)null);

        var command = new DeleteEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            EndpointId = Guid.NewGuid(),
            CurrentUserId = _userId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_NotOwner_Should_ThrowValidationException()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = Guid.NewGuid() };
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);

        var command = new DeleteEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            EndpointId = Guid.NewGuid(),
            CurrentUserId = _userId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_SpecNotFound_Should_ThrowNotFoundException()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId };
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync((ApiSpecification)null);

        var command = new DeleteEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            EndpointId = Guid.NewGuid(),
            CurrentUserId = _userId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_EndpointNotFound_Should_ThrowNotFoundException()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId };
        var spec = new ApiSpecification { Id = _specId, ProjectId = _projectId };

        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync(spec);
        _endpointRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiEndpoint>>()))
            .ReturnsAsync((ApiEndpoint)null);

        var command = new DeleteEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            EndpointId = Guid.NewGuid(),
            CurrentUserId = _userId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
