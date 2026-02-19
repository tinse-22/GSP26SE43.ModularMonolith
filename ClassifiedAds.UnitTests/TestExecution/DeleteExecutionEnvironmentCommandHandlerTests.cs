using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Commands;
using ClassifiedAds.Modules.TestExecution.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class DeleteExecutionEnvironmentCommandHandlerTests
{
    private readonly Mock<IRepository<ExecutionEnvironment, Guid>> _envRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly DeleteExecutionEnvironmentCommandHandler _handler;

    public DeleteExecutionEnvironmentCommandHandlerTests()
    {
        _envRepoMock = new Mock<IRepository<ExecutionEnvironment, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _envRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _envRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<ExecutionEnvironment>().AsQueryable());

        _handler = new DeleteExecutionEnvironmentCommandHandler(
            _envRepoMock.Object,
            new Mock<ILogger<DeleteExecutionEnvironmentCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_DeleteEnvironment_WhenValidInput()
    {
        // Arrange
        var env = new ExecutionEnvironment
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Name = "Staging",
            BaseUrl = "https://staging.example.com",
            IsDefault = false,
        };

        _envRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ExecutionEnvironment>>()))
            .ReturnsAsync(env);

        var command = new DeleteExecutionEnvironmentCommand
        {
            EnvironmentId = env.Id,
            ProjectId = env.ProjectId,
            CurrentUserId = Guid.NewGuid(),
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _envRepoMock.Verify(x => x.Delete(It.Is<ExecutionEnvironment>(e => e.Id == env.Id)), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenProjectIdEmpty()
    {
        // Arrange
        var command = new DeleteExecutionEnvironmentCommand
        {
            EnvironmentId = Guid.NewGuid(),
            ProjectId = Guid.Empty,
            CurrentUserId = Guid.NewGuid(),
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenCurrentUserIdEmpty()
    {
        // Arrange
        var command = new DeleteExecutionEnvironmentCommand
        {
            EnvironmentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.Empty,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFoundException_WhenEnvironmentNotFound()
    {
        // Arrange
        _envRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ExecutionEnvironment>>()))
            .ReturnsAsync((ExecutionEnvironment)null);

        var command = new DeleteExecutionEnvironmentCommand
        {
            EnvironmentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
