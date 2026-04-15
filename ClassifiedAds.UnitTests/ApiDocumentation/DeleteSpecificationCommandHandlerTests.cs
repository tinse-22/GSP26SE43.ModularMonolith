using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Events;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class DeleteSpecificationCommandHandlerTests
{
    private readonly Mock<Dispatcher> _dispatcherMock;
    private readonly Mock<IRepository<Project, Guid>> _projectRepoMock;
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly DeleteSpecificationCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _specId = Guid.NewGuid();

    public DeleteSpecificationCommandHandlerTests()
    {
        _dispatcherMock = new Mock<Dispatcher>(new Mock<IServiceProvider>().Object);
        _projectRepoMock = new Mock<IRepository<Project, Guid>>();
        _specRepoMock = new Mock<IRepository<ApiSpecification, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _specRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _handler = new DeleteSpecificationCommandHandler(
            _dispatcherMock.Object,
            _projectRepoMock.Object,
            _specRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_Should_SoftDeleteSpecification()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId };
        var spec = new ApiSpecification
        {
            Id = _specId,
            ProjectId = _projectId,
            IsActive = true,
            IsDeleted = false,
            DeletedAt = null,
        };

        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync(spec);

        var command = new DeleteSpecificationCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert – soft-delete flags set
        spec.IsDeleted.Should().BeTrue();
        spec.DeletedAt.Should().NotBeNull();
        spec.IsActive.Should().BeFalse();

        _specRepoMock.Verify(x => x.UpdateAsync(
            It.Is<ApiSpecification>(s => s.Id == _specId && s.IsDeleted && !s.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ActiveSpec_Should_ClearProjectActiveSpecId()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId, ActiveSpecId = _specId };
        var spec = new ApiSpecification
        {
            Id = _specId,
            ProjectId = _projectId,
            IsActive = true,
            IsDeleted = false,
        };

        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync(spec);

        var command = new DeleteSpecificationCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert – project's active spec cleared
        project.ActiveSpecId.Should().BeNull();
        _projectRepoMock.Verify(x => x.UpdateAsync(
            It.Is<Project>(p => p.ActiveSpecId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NonActiveSpec_Should_NotUpdateProject()
    {
        // Arrange
        var otherSpecId = Guid.NewGuid();
        var project = new Project { Id = _projectId, OwnerId = _userId, ActiveSpecId = otherSpecId };
        var spec = new ApiSpecification
        {
            Id = _specId,
            ProjectId = _projectId,
            IsActive = false,
            IsDeleted = false,
        };

        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync(spec);

        var command = new DeleteSpecificationCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert – project not updated because deleted spec was not the active one
        project.ActiveSpecId.Should().Be(otherSpecId);
        _projectRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ProjectNotFound_Should_ThrowNotFoundException()
    {
        // Arrange
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync((Project)null);

        var command = new DeleteSpecificationCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
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

        var command = new DeleteSpecificationCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
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

        var command = new DeleteSpecificationCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_RaiseDomainEventAfterSoftDelete()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId };
        var spec = new ApiSpecification
        {
            Id = _specId,
            ProjectId = _projectId,
            IsDeleted = false,
        };

        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync(spec);

        var command = new DeleteSpecificationCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert – domain event dispatched
        _dispatcherMock.Verify(x => x.DispatchAsync(
            It.IsAny<EntityDeletedEvent<ApiSpecification>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
