using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using ClassifiedAds.UnitTests.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class AddUpdateEndpointCommandHandlerTests
{
    private readonly Mock<Dispatcher> _dispatcherMock;
    private readonly Mock<IRepository<Project, Guid>> _projectRepoMock;
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock;
    private readonly Mock<IRepository<ApiEndpoint, Guid>> _endpointRepoMock;
    private readonly Mock<IRepository<EndpointParameter, Guid>> _parameterRepoMock;
    private readonly Mock<IRepository<EndpointResponse, Guid>> _responseRepoMock;
    private readonly Mock<ISubscriptionLimitGatewayService> _subscriptionLimitMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddUpdateEndpointCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _specId = Guid.NewGuid();

    public AddUpdateEndpointCommandHandlerTests()
    {
        _dispatcherMock = new Mock<Dispatcher>(new Mock<IServiceProvider>().Object);
        _projectRepoMock = new Mock<IRepository<Project, Guid>>();
        _specRepoMock = new Mock<IRepository<ApiSpecification, Guid>>();
        _endpointRepoMock = new Mock<IRepository<ApiEndpoint, Guid>>();
        _parameterRepoMock = new Mock<IRepository<EndpointParameter, Guid>>();
        _responseRepoMock = new Mock<IRepository<EndpointResponse, Guid>>();
        _subscriptionLimitMock = new Mock<ISubscriptionLimitGatewayService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _endpointRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _endpointRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<ApiEndpoint>().AsQueryable());
        _endpointRepoMock.Setup(x => x.AddAsync(It.IsAny<ApiEndpoint>(), It.IsAny<CancellationToken>()))
            .Callback<ApiEndpoint, CancellationToken>((e, _) => { if (e.Id == Guid.Empty) e.Id = Guid.NewGuid(); });
        _parameterRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new TestAsyncEnumerable<EndpointParameter>(new List<EndpointParameter>()));
        _responseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new TestAsyncEnumerable<EndpointResponse>(new List<EndpointResponse>()));

        _handler = new AddUpdateEndpointCommandHandler(
            _dispatcherMock.Object,
            _projectRepoMock.Object,
            _specRepoMock.Object,
            _endpointRepoMock.Object,
            _parameterRepoMock.Object,
            _responseRepoMock.Object,
            _subscriptionLimitMock.Object);
    }

    private void SetupValidProjectAndSpec()
    {
        var project = new Project { Id = _projectId, OwnerId = _userId };
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);

        var spec = new ApiSpecification { Id = _specId, ProjectId = _projectId };
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync(spec);

        _subscriptionLimitMock.Setup(x => x.TryConsumeLimitAsync(
                It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });
    }

    [Fact]
    public async Task HandleAsync_CreateEndpoint_Should_AddEndpointSuccessfully()
    {
        // Arrange
        SetupValidProjectAndSpec();

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel
            {
                HttpMethod = "POST",
                Path = "/api/users",
                Summary = "Create a user",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedEndpointId.Should().NotBe(Guid.Empty);
        _endpointRepoMock.Verify(x => x.AddAsync(It.IsAny<ApiEndpoint>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CreateEndpoint_WithParameters_Should_AddParametersToo()
    {
        // Arrange
        SetupValidProjectAndSpec();

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel
            {
                HttpMethod = "GET",
                Path = "/api/users/{id}",
                Parameters = new List<ManualParameterDefinition>
                {
                    new() { Name = "id", Location = "Path", DataType = "string", IsRequired = true },
                    new() { Name = "include", Location = "Query", DataType = "string" },
                },
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _parameterRepoMock.Verify(
            x => x.AddAsync(It.IsAny<EndpointParameter>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_CreateEndpoint_WithResponses_Should_AddResponsesToo()
    {
        // Arrange
        SetupValidProjectAndSpec();

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel
            {
                HttpMethod = "GET",
                Path = "/api/users",
                Responses = new List<ManualResponseDefinition>
                {
                    new() { StatusCode = 200, Description = "Success" },
                    new() { StatusCode = 404, Description = "Not found" },
                },
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _responseRepoMock.Verify(
            x => x.AddAsync(It.IsAny<EndpointResponse>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_NullModel_Should_ThrowValidationException()
    {
        // Arrange
        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = null,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_InvalidHttpMethod_Should_ThrowValidationException()
    {
        // Arrange
        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel
            {
                HttpMethod = "INVALID",
                Path = "/api/test",
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_EmptyPath_Should_ThrowValidationException()
    {
        // Arrange
        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel
            {
                HttpMethod = "GET",
                Path = "",
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_PathTooLong_Should_ThrowValidationException()
    {
        // Arrange
        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel
            {
                HttpMethod = "GET",
                Path = new string('a', 501),
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_ProjectNotFound_Should_ThrowNotFoundException()
    {
        // Arrange
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync((Project)null);

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel { HttpMethod = "GET", Path = "/test" },
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
        var project = new Project { Id = _projectId, OwnerId = Guid.NewGuid() }; // Different owner
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel { HttpMethod = "GET", Path = "/test" },
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

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel { HttpMethod = "GET", Path = "/test" },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_LimitExceeded_Should_ThrowValidationException()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId };
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);

        var spec = new ApiSpecification { Id = _specId, ProjectId = _projectId };
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync(spec);

        _subscriptionLimitMock.Setup(x => x.TryConsumeLimitAsync(
                It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO
            {
                IsAllowed = false,
                DenialReason = "Limit exceeded",
            });

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel { HttpMethod = "GET", Path = "/test" },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_UpdateEndpoint_Should_UpdateAndRecreateChildren()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        SetupValidProjectAndSpec();

        var existingEndpoint = new ApiEndpoint
        {
            Id = endpointId,
            ApiSpecId = _specId,
            HttpMethod = ClassifiedAds.Modules.ApiDocumentation.Entities.HttpMethod.GET,
            Path = "/old-path",
        };

        _endpointRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiEndpoint>>()))
            .ReturnsAsync(existingEndpoint);

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            EndpointId = endpointId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel
            {
                HttpMethod = "POST",
                Path = "/new-path",
                Summary = "Updated endpoint",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedEndpointId.Should().Be(endpointId);
        _endpointRepoMock.Verify(x => x.UpdateAsync(It.IsAny<ApiEndpoint>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UpdateEndpoint_NotFound_Should_ThrowNotFoundException()
    {
        // Arrange
        SetupValidProjectAndSpec();

        _endpointRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiEndpoint>>()))
            .ReturnsAsync((ApiEndpoint)null);

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            EndpointId = Guid.NewGuid(),
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel { HttpMethod = "GET", Path = "/test" },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_CreateEndpoint_Should_ConsumeLimit()
    {
        // Arrange
        SetupValidProjectAndSpec();

        var command = new AddUpdateEndpointCommand
        {
            ProjectId = _projectId,
            SpecId = _specId,
            CurrentUserId = _userId,
            Model = new CreateUpdateEndpointModel
            {
                HttpMethod = "GET",
                Path = "/api/test",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _subscriptionLimitMock.Verify(
            x => x.TryConsumeLimitAsync(
                _userId,
                LimitType.MaxEndpointsPerProject,
                1,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
