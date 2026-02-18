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

public class ImportCurlCommandHandlerTests
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
    private readonly ImportCurlCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public ImportCurlCommandHandlerTests()
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

        _pathParamServiceMock.Setup(x => x.ExtractPathParameters(It.IsAny<string>()))
            .Returns(new List<PathParameterInfo>());

        _specRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _specServiceMock.Setup(x => x.AddAsync(It.IsAny<ApiSpecification>(), It.IsAny<CancellationToken>()))
            .Callback<ApiSpecification, CancellationToken>((s, _) => { if (s.Id == Guid.Empty) s.Id = Guid.NewGuid(); });

        _handler = new ImportCurlCommandHandler(
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
    public async Task HandleAsync_SimpleGetCurl_Should_CreateSpecAndEndpoint()
    {
        // Arrange
        SetupValidProject();

        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "User API",
                CurlCommand = "curl https://api.example.com/users",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedSpecId.Should().NotBe(Guid.Empty);
        _specServiceMock.Verify(x => x.AddAsync(
            It.Is<ApiSpecification>(s =>
                s.Name == "User API" &&
                s.SourceType == SourceType.cURL &&
                s.ParseStatus == ParseStatus.Success),
            It.IsAny<CancellationToken>()), Times.Once);
        _endpointRepoMock.Verify(
            x => x.AddAsync(It.Is<ApiEndpoint>(e =>
                e.HttpMethod == ClassifiedAds.Modules.ApiDocumentation.Entities.HttpMethod.GET &&
                e.Path == "/users"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PostCurlWithBody_Should_CreateBodyParameter()
    {
        // Arrange
        SetupValidProject();

        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "Create User",
                CurlCommand = "curl -X POST -H 'Content-Type: application/json' -d '{\"name\":\"test\"}' https://api.example.com/users",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _parameterRepoMock.Verify(
            x => x.AddAsync(It.Is<EndpointParameter>(p =>
                p.Name == "body" &&
                p.Location == ParameterLocation.Body),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CurlWithQueryParams_Should_CreateQueryParameters()
    {
        // Arrange
        SetupValidProject();

        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "Search API",
                CurlCommand = "curl 'https://api.example.com/search?q=test&page=1'",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _parameterRepoMock.Verify(
            x => x.AddAsync(It.Is<EndpointParameter>(p =>
                p.Location == ParameterLocation.Query),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_CurlWithCustomHeaders_Should_CreateHeaderParameters()
    {
        // Arrange
        SetupValidProject();

        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "Auth API",
                CurlCommand = "curl -H 'Authorization: Bearer token123' -H 'X-Custom: value' https://api.example.com/data",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _parameterRepoMock.Verify(
            x => x.AddAsync(It.Is<EndpointParameter>(p =>
                p.Location == ParameterLocation.Header),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WithAutoActivate_Should_ActivateSpec()
    {
        // Arrange
        var project = new Project { Id = _projectId, OwnerId = _userId, ActiveSpecId = null };
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync(project);
        _subscriptionLimitMock.Setup(x => x.TryConsumeLimitAsync(
                It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });

        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "Active API",
                CurlCommand = "curl https://api.example.com/test",
                AutoActivate = true,
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
        var command = new ImportCurlCommand
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
        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "",
                CurlCommand = "curl https://example.com",
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_EmptyCurlCommand_Should_ThrowValidationException()
    {
        // Arrange
        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "Test",
                CurlCommand = "",
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
        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = new string('x', 201),
                CurlCommand = "curl https://example.com",
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

        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "Test",
                CurlCommand = "curl https://api.example.com/test",
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

        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "Test",
                CurlCommand = "curl https://api.example.com/test",
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
                DenialReason = "Limit exceeded",
            });

        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "Test",
                CurlCommand = "curl https://api.example.com/test",
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ConsumeLimitAtomically()
    {
        // Arrange
        SetupValidProject();

        var command = new ImportCurlCommand
        {
            ProjectId = _projectId,
            CurrentUserId = _userId,
            Model = new ImportCurlModel
            {
                Name = "Test",
                CurlCommand = "curl https://api.example.com/test",
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
