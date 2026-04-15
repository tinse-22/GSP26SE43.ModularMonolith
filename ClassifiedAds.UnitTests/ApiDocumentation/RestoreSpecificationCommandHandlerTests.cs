using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class RestoreSpecificationCommandHandlerTests
{
    private readonly Mock<IRepository<Project, Guid>> _projectRepoMock;
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly RestoreSpecificationCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _specId = Guid.NewGuid();

    public RestoreSpecificationCommandHandlerTests()
    {
        _projectRepoMock = new Mock<IRepository<Project, Guid>>();
        _specRepoMock = new Mock<IRepository<ApiSpecification, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _specRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _handler = new RestoreSpecificationCommandHandler(
            _projectRepoMock.Object,
            _specRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_Should_RestoreSoftDeletedSpecification()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId };
        var spec = new ApiSpecification
        {
            Id = _specId,
            ProjectId = _projectId,
            IsDeleted = true,
            DeletedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync(spec);

        var command = new RestoreSpecificationCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert – soft-delete flags cleared
        spec.IsDeleted.Should().BeFalse();
        spec.DeletedAt.Should().BeNull();

        _specRepoMock.Verify(x => x.UpdateAsync(
            It.Is<ApiSpecification>(s => s.Id == _specId && !s.IsDeleted && s.DeletedAt == null),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ProjectNotFound_Should_ThrowNotFoundException()
    {
        // Arrange
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync((Project)null);

        var command = new RestoreSpecificationCommand
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

        var command = new RestoreSpecificationCommand
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
    public async Task HandleAsync_SpecNotFoundOrNotDeleted_Should_ThrowNotFoundException()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId };
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync((ApiSpecification)null);

        var command = new RestoreSpecificationCommand
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
}
