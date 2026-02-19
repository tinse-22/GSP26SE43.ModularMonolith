using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Commands;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class AddUpdateExecutionEnvironmentCommandHandlerTests
{
    private readonly Mock<IRepository<ExecutionEnvironment, Guid>> _envRepoMock;
    private readonly Mock<IExecutionAuthConfigService> _authConfigServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddUpdateExecutionEnvironmentCommandHandler _handler;

    public AddUpdateExecutionEnvironmentCommandHandlerTests()
    {
        _envRepoMock = new Mock<IRepository<ExecutionEnvironment, Guid>>();
        _authConfigServiceMock = new Mock<IExecutionAuthConfigService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _envRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _envRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<ExecutionEnvironment>().AsQueryable());

        _authConfigServiceMock.Setup(x => x.SerializeAuthConfig(It.IsAny<ExecutionAuthConfigModel>()))
            .Returns((string)null);
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>()))
            .Returns((ExecutionAuthConfigModel)null);
        _authConfigServiceMock.Setup(x => x.MaskAuthConfig(It.IsAny<ExecutionAuthConfigModel>()))
            .Returns((ExecutionAuthConfigModel)null);

        _handler = new AddUpdateExecutionEnvironmentCommandHandler(
            _envRepoMock.Object,
            _authConfigServiceMock.Object,
            new Mock<ILogger<AddUpdateExecutionEnvironmentCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Create_Should_AddNewEnvironment()
    {
        // Arrange
        var command = new AddUpdateExecutionEnvironmentCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            Name = "Development",
            BaseUrl = "https://dev.example.com",
            IsDefault = false,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _envRepoMock.Verify(x => x.AddAsync(
            It.Is<ExecutionEnvironment>(e =>
                e.Name == "Development" &&
                e.BaseUrl == "https://dev.example.com" &&
                e.IsDefault == false),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        command.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_Create_WithDefault_Should_UseTransaction()
    {
        // Arrange
        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<System.Data.IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, System.Data.IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _envRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<ExecutionEnvironment>>()))
            .ReturnsAsync(new List<ExecutionEnvironment>());

        var command = new AddUpdateExecutionEnvironmentCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            Name = "Production",
            BaseUrl = "https://api.example.com",
            IsDefault = true,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _unitOfWorkMock.Verify(x => x.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(),
            System.Data.IsolationLevel.Serializable,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Create_WithDefault_Should_ThrowConflictException_WhenMultipleDefaultsDetected()
    {
        // Arrange
        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<System.Data.IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, System.Data.IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _envRepoMock.SetupSequence(x => x.ToListAsync(It.IsAny<IQueryable<ExecutionEnvironment>>()))
            .ReturnsAsync(new List<ExecutionEnvironment>())
            .ReturnsAsync(new List<ExecutionEnvironment>
            {
                new() { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Name = "A", BaseUrl = "https://a.example.com", IsDefault = true },
                new() { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Name = "B", BaseUrl = "https://b.example.com", IsDefault = true },
            });

        var command = new AddUpdateExecutionEnvironmentCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            Name = "Production",
            BaseUrl = "https://api.example.com",
            IsDefault = true,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("DEFAULT_ENVIRONMENT_CONFLICT");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenInvalidBaseUrl()
    {
        // Arrange
        var command = new AddUpdateExecutionEnvironmentCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            Name = "Test",
            BaseUrl = "not-a-url",
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenNameIsEmpty()
    {
        // Arrange
        var command = new AddUpdateExecutionEnvironmentCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            Name = "",
            BaseUrl = "https://api.example.com",
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
        var command = new AddUpdateExecutionEnvironmentCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.Empty,
            Name = "Test",
            BaseUrl = "https://api.example.com",
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Update_Should_ThrowNotFoundException_WhenNotFound()
    {
        // Arrange
        _envRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ExecutionEnvironment>>()))
            .ReturnsAsync((ExecutionEnvironment)null);

        var command = new AddUpdateExecutionEnvironmentCommand
        {
            EnvironmentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            Name = "Test",
            BaseUrl = "https://api.example.com",
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
