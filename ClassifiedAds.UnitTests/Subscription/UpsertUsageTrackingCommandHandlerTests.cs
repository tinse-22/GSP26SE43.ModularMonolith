using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class UpsertUsageTrackingCommandHandlerTests
{
    private readonly Mock<IRepository<UsageTracking, Guid>> _usageRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UpsertUsageTrackingCommandHandler _handler;

    public UpsertUsageTrackingCommandHandlerTests()
    {
        _usageRepoMock = new Mock<IRepository<UsageTracking, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _usageRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _usageRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<UsageTracking>().AsQueryable());

        _handler = new UpsertUsageTrackingCommandHandler(_usageRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_CreateNewUsageTracking_Should_AddEntity()
    {
        // Arrange
        _usageRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UsageTracking>>()))
            .ReturnsAsync((UsageTracking)null);

        var command = new UpsertUsageTrackingCommand
        {
            UserId = Guid.NewGuid(),
            Model = new UpsertUsageTrackingModel
            {
                PeriodStart = new DateOnly(2026, 2, 1),
                PeriodEnd = new DateOnly(2026, 2, 28),
                ProjectCount = 2,
                EndpointCount = 5,
                StorageUsedMB = 25,
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedUsageTrackingId.Should().NotBe(Guid.Empty);
        _usageRepoMock.Verify(x => x.AddAsync(It.IsAny<UsageTracking>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UpdateExistingUsageTracking_WithIncrement_Should_AddValues()
    {
        // Arrange
        var usage = new UsageTracking
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PeriodStart = new DateOnly(2026, 2, 1),
            PeriodEnd = new DateOnly(2026, 2, 28),
            ProjectCount = 1,
            EndpointCount = 2,
            StorageUsedMB = 10,
        };

        _usageRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UsageTracking>>()))
            .ReturnsAsync(usage);

        var command = new UpsertUsageTrackingCommand
        {
            UserId = usage.UserId,
            Model = new UpsertUsageTrackingModel
            {
                PeriodStart = usage.PeriodStart,
                PeriodEnd = usage.PeriodEnd,
                ReplaceValues = false,
                ProjectCount = 2,
                EndpointCount = 3,
                StorageUsedMB = 5,
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        usage.ProjectCount.Should().Be(3);
        usage.EndpointCount.Should().Be(5);
        usage.StorageUsedMB.Should().Be(15);
        _usageRepoMock.Verify(x => x.UpdateAsync(usage, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_InvalidPeriod_Should_ThrowValidationException()
    {
        // Arrange
        var command = new UpsertUsageTrackingCommand
        {
            UserId = Guid.NewGuid(),
            Model = new UpsertUsageTrackingModel
            {
                PeriodStart = new DateOnly(2026, 3, 1),
                PeriodEnd = new DateOnly(2026, 2, 1),
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
