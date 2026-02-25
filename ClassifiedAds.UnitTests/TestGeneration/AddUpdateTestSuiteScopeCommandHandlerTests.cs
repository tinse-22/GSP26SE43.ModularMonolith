using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class AddUpdateTestSuiteScopeCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IApiEndpointMetadataService> _endpointMetadataServiceMock;
    private readonly Mock<ITestSuiteScopeService> _scopeServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddUpdateTestSuiteScopeCommandHandler _handler;

    public AddUpdateTestSuiteScopeCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _endpointMetadataServiceMock = new Mock<IApiEndpointMetadataService>();
        _scopeServiceMock = new Mock<ITestSuiteScopeService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _suiteRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite>().AsQueryable());

        _handler = new AddUpdateTestSuiteScopeCommandHandler(
            _suiteRepoMock.Object,
            _endpointMetadataServiceMock.Object,
            _scopeServiceMock.Object,
            new Mock<ILogger<AddUpdateTestSuiteScopeCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Create_Should_AddNewSuiteWithNormalizedEndpoints()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId1 = Guid.NewGuid();
        var endpointId2 = Guid.NewGuid();
        var normalizedIds = new List<Guid> { endpointId1, endpointId2 };

        _scopeServiceMock.Setup(x => x.NormalizeEndpointIds(It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(normalizedIds);

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                specId, normalizedIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedIds.Select(id => new ApiEndpointMetadataDto { EndpointId = id }).ToList());

        var command = new AddUpdateTestSuiteScopeCommand
        {
            ProjectId = projectId,
            CurrentUserId = userId,
            Name = "My Suite",
            ApiSpecId = specId,
            GenerationType = GenerationType.Auto,
            SelectedEndpointIds = new List<Guid> { endpointId1, endpointId2 },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _suiteRepoMock.Verify(x => x.AddAsync(
            It.Is<TestSuite>(s =>
                s.ProjectId == projectId &&
                s.Name == "My Suite" &&
                s.SelectedEndpointIds.Count == 2 &&
                s.Status == TestSuiteStatus.Draft &&
                s.CreatedById == userId),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        command.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_Create_Should_PersistGlobalBusinessRules()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var normalizedIds = new List<Guid> { endpointId };

        _scopeServiceMock.Setup(x => x.NormalizeEndpointIds(It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(normalizedIds);

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                specId, normalizedIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedIds.Select(id => new ApiEndpointMetadataDto { EndpointId = id }).ToList());

        var globalRules = "  Users must verify email before placing orders. All monetary amounts use VND.  ";

        var command = new AddUpdateTestSuiteScopeCommand
        {
            ProjectId = projectId,
            CurrentUserId = userId,
            Name = "Suite with Global BR",
            ApiSpecId = specId,
            GenerationType = GenerationType.Auto,
            SelectedEndpointIds = new List<Guid> { endpointId },
            GlobalBusinessRules = globalRules,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _suiteRepoMock.Verify(x => x.AddAsync(
            It.Is<TestSuite>(s =>
                s.GlobalBusinessRules == globalRules.Trim() &&
                s.Name == "Suite with Global BR"),
            It.IsAny<CancellationToken>()), Times.Once);

        command.Result.Should().NotBeNull();
        command.Result.GlobalBusinessRules.Should().Be(globalRules.Trim());
    }

    [Fact]
    public async Task HandleAsync_Create_Should_AllowNullGlobalBusinessRules()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var normalizedIds = new List<Guid> { endpointId };

        _scopeServiceMock.Setup(x => x.NormalizeEndpointIds(It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(normalizedIds);

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                specId, normalizedIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedIds.Select(id => new ApiEndpointMetadataDto { EndpointId = id }).ToList());

        var command = new AddUpdateTestSuiteScopeCommand
        {
            ProjectId = projectId,
            CurrentUserId = userId,
            Name = "Suite without BR",
            ApiSpecId = specId,
            GenerationType = GenerationType.Auto,
            SelectedEndpointIds = new List<Guid> { endpointId },
            GlobalBusinessRules = null,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _suiteRepoMock.Verify(x => x.AddAsync(
            It.Is<TestSuite>(s => s.GlobalBusinessRules == null),
            It.IsAny<CancellationToken>()), Times.Once);

        command.Result.Should().NotBeNull();
        command.Result.GlobalBusinessRules.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenEndpointsNotInSpec()
    {
        // Arrange
        var specId = Guid.NewGuid();
        var validId = Guid.NewGuid();
        var invalidId = Guid.NewGuid();
        var normalizedIds = new List<Guid> { validId, invalidId };

        _scopeServiceMock.Setup(x => x.NormalizeEndpointIds(It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(normalizedIds);

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                specId, normalizedIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiEndpointMetadataDto>
            {
                new() { EndpointId = validId },
            });

        var command = new AddUpdateTestSuiteScopeCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            Name = "Suite",
            ApiSpecId = specId,
            SelectedEndpointIds = normalizedIds,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenEmptyEndpoints()
    {
        // Arrange
        _scopeServiceMock.Setup(x => x.NormalizeEndpointIds(It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(new List<Guid>());

        var command = new AddUpdateTestSuiteScopeCommand
        {
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            Name = "Suite",
            ApiSpecId = Guid.NewGuid(),
            SelectedEndpointIds = new List<Guid>(),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Update_Should_ThrowNotFoundException_WhenSuiteNotFound()
    {
        // Arrange
        var normalizedIds = new List<Guid> { Guid.NewGuid() };

        _scopeServiceMock.Setup(x => x.NormalizeEndpointIds(It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(normalizedIds);

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(), normalizedIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedIds.Select(id => new ApiEndpointMetadataDto { EndpointId = id }).ToList());

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync((TestSuite)null);

        var command = new AddUpdateTestSuiteScopeCommand
        {
            SuiteId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            Name = "Suite",
            ApiSpecId = Guid.NewGuid(),
            SelectedEndpointIds = normalizedIds,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Update_Should_ThrowValidationException_WhenNotOwner()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var normalizedIds = new List<Guid> { Guid.NewGuid() };
        var suite = new TestSuite
        {
            Id = suiteId,
            ProjectId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
            Name = "Suite",
        };

        _scopeServiceMock.Setup(x => x.NormalizeEndpointIds(It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(normalizedIds);

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(), normalizedIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedIds.Select(id => new ApiEndpointMetadataDto { EndpointId = id }).ToList());

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);

        var command = new AddUpdateTestSuiteScopeCommand
        {
            SuiteId = suiteId,
            ProjectId = suite.ProjectId,
            CurrentUserId = Guid.NewGuid(), // different user
            Name = "Suite",
            ApiSpecId = specId,
            SelectedEndpointIds = normalizedIds,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Update_Should_ThrowValidationException_WhenSuiteArchived()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var normalizedIds = new List<Guid> { Guid.NewGuid() };
        var suite = new TestSuite
        {
            Id = suiteId,
            ProjectId = Guid.NewGuid(),
            CreatedById = userId,
            Name = "Suite",
            Status = TestSuiteStatus.Archived,
        };

        _scopeServiceMock.Setup(x => x.NormalizeEndpointIds(It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(normalizedIds);

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(), normalizedIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedIds.Select(id => new ApiEndpointMetadataDto { EndpointId = id }).ToList());

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);

        var command = new AddUpdateTestSuiteScopeCommand
        {
            SuiteId = suiteId,
            ProjectId = suite.ProjectId,
            CurrentUserId = userId,
            Name = "Suite",
            ApiSpecId = specId,
            SelectedEndpointIds = normalizedIds,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*archive*");
    }
}
