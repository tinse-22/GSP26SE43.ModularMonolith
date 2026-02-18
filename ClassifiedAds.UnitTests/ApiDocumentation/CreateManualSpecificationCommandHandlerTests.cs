using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class CreateManualSpecificationCommandHandlerTests
{
    private readonly Mock<IRepository<Project, Guid>> _projectRepoMock;
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock;
    private readonly Mock<IRepository<ApiEndpoint, Guid>> _endpointRepoMock;
    private readonly Mock<IRepository<EndpointParameter, Guid>> _parameterRepoMock;
    private readonly Mock<IRepository<EndpointResponse, Guid>> _responseRepoMock;
    private readonly Mock<ICrudService<ApiSpecification>> _specServiceMock;
    private readonly Mock<ISubscriptionLimitGatewayService> _subscriptionLimitMock;
    private readonly Mock<IPathParameterTemplateService> _pathParamServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateManualSpecificationCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public CreateManualSpecificationCommandHandlerTests()
    {
        _projectRepoMock = new Mock<IRepository<Project, Guid>>();
        _specRepoMock = new Mock<IRepository<ApiSpecification, Guid>>();
        _endpointRepoMock = new Mock<IRepository<ApiEndpoint, Guid>>();
        _parameterRepoMock = new Mock<IRepository<EndpointParameter, Guid>>();
        _responseRepoMock = new Mock<IRepository<EndpointResponse, Guid>>();
        _specServiceMock = new Mock<ICrudService<ApiSpecification>>();
        _subscriptionLimitMock = new Mock<ISubscriptionLimitGatewayService>();
        _pathParamServiceMock = new Mock<IPathParameterTemplateService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _pathParamServiceMock.Setup(x => x.EnsurePathParameterConsistency(
                It.IsAny<string>(), It.IsAny<List<ManualParameterDefinition>>()))
            .Returns<string, List<ManualParameterDefinition>>((_, p) => p ?? new List<ManualParameterDefinition>());

        _specRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _specServiceMock.Setup(x => x.AddAsync(It.IsAny<ApiSpecification>(), It.IsAny<CancellationToken>()))
            .Callback<ApiSpecification, CancellationToken>((s, _) => { if (s.Id == Guid.Empty) s.Id = Guid.NewGuid(); });

        _handler = new CreateManualSpecificationCommandHandler(
            _projectRepoMock.Object,
            _specRepoMock.Object,
            _endpointRepoMock.Object,
            _parameterRepoMock.Object,
            _responseRepoMock.Object,
            _specServiceMock.Object,
            _subscriptionLimitMock.Object,
            _pathParamServiceMock.Object);
    }

    private void SetupValidProject()
    {
        var project = new Project { Id = _projectId, OwnerId = _userId };
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);

        _subscriptionLimitMock.Setup(x => x.TryConsumeLimitAsync(
                It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_Should_CreateSpecWithEndpoints()
    {
        // Arrange
        SetupValidProject();

        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "My API Spec",
                Version = "1.0",
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new()
                    {
                        HttpMethod = "GET",
                        Path = "/api/users",
                        Summary = "List users",
                    },
                    new()
                    {
                        HttpMethod = "POST",
                        Path = "/api/users",
                        Summary = "Create user",
                    },
                },
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedSpecId.Should().NotBe(Guid.Empty);
        _specServiceMock.Verify(x => x.AddAsync(
            It.Is<ApiSpecification>(s =>
                s.Name == "My API Spec" &&
                s.SourceType == SourceType.Manual &&
                s.ParseStatus == ParseStatus.Success),
            It.IsAny<CancellationToken>()), Times.Once);
        _endpointRepoMock.Verify(
            x => x.AddAsync(It.IsAny<ApiEndpoint>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WithParameters_Should_CreateParametersForEndpoints()
    {
        // Arrange
        SetupValidProject();

        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "API Spec",
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new()
                    {
                        HttpMethod = "GET",
                        Path = "/api/users/{id}",
                        Parameters = new List<ManualParameterDefinition>
                        {
                            new() { Name = "id", Location = "Path", DataType = EndpointParameterDataType.Uuid, IsRequired = true },
                            new() { Name = "fields", Location = "Query", DataType = EndpointParameterDataType.String },
                        },
                    },
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
    public async Task HandleAsync_WithAutoActivate_Should_ActivateSpecAndUpdateProject()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId, ActiveSpecId = null };
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);

        _subscriptionLimitMock.Setup(x => x.TryConsumeLimitAsync(
                It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });

        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "Active Spec",
                AutoActivate = true,
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new() { HttpMethod = "GET", Path = "/test" },
                },
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        project.ActiveSpecId.Should().NotBeNull();
        _projectRepoMock.Verify(
            x => x.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NullModel_Should_ThrowValidationException()
    {
        // Arrange
        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = null,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_EmptyName_Should_ThrowValidationException()
    {
        // Arrange
        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "",
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new() { HttpMethod = "GET", Path = "/test" },
                },
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_NameTooLong_Should_ThrowValidationException()
    {
        // Arrange
        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = new string('x', 201),
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new() { HttpMethod = "GET", Path = "/test" },
                },
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_NoEndpoints_Should_ThrowValidationException()
    {
        // Arrange
        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "My Spec",
                Endpoints = new List<ManualEndpointDefinition>(),
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_EndpointWithInvalidHttpMethod_Should_ThrowValidationException()
    {
        // Arrange
        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "My Spec",
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new() { HttpMethod = "INVALID", Path = "/test" },
                },
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_EndpointWithEmptyPath_Should_ThrowValidationException()
    {
        // Arrange
        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "My Spec",
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new() { HttpMethod = "GET", Path = "" },
                },
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

        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "My Spec",
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new() { HttpMethod = "GET", Path = "/test" },
                },
            },
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

        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "My Spec",
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new() { HttpMethod = "GET", Path = "/test" },
                },
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_LimitExceeded_Should_ThrowValidationException()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId };
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);

        _subscriptionLimitMock.Setup(x => x.TryConsumeLimitAsync(
                It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO
            {
                IsAllowed = false,
                DenialReason = "Endpoint limit exceeded",
            });

        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "My Spec",
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new() { HttpMethod = "GET", Path = "/test" },
                },
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ConsumeLimitByEndpointCount()
    {
        // Arrange
        SetupValidProject();

        var command = new CreateManualSpecificationCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new CreateManualSpecificationModel
            {
                Name = "Spec",
                Endpoints = new List<ManualEndpointDefinition>
                {
                    new() { HttpMethod = "GET", Path = "/a" },
                    new() { HttpMethod = "POST", Path = "/b" },
                    new() { HttpMethod = "PUT", Path = "/c" },
                },
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _subscriptionLimitMock.Verify(
            x => x.TryConsumeLimitAsync(
                _userId,
                LimitType.MaxEndpointsPerProject,
                3,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
