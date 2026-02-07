using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Domain.Events;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.EventHandlers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class PlanEventHandlerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IRepository<AuditLogEntry, Guid>> _auditLogRepoMock;
    private readonly Mock<IRepository<OutboxMessage, Guid>> _outboxRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public PlanEventHandlerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _auditLogRepoMock = new Mock<IRepository<AuditLogEntry, Guid>>();
        _outboxRepoMock = new Mock<IRepository<OutboxMessage, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _auditLogRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _outboxRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
    }

    #region PlanCreatedEventHandler Tests

    [Fact]
    public async Task PlanCreatedEventHandler_Should_CreateAuditLog()
    {
        // Arrange
        var handler = new PlanCreatedEventHandler(
            _currentUserMock.Object,
            _auditLogRepoMock.Object,
            _outboxRepoMock.Object);

        var plan = CreateSamplePlan();
        var domainEvent = new EntityCreatedEvent<SubscriptionPlan>(plan, DateTime.UtcNow);

        // Act
        await handler.HandleAsync(domainEvent);

        // Assert
        _auditLogRepoMock.Verify(
            x => x.AddOrUpdateAsync(
                It.Is<AuditLogEntry>(a =>
                    a.Action == "CREATED_PLAN" &&
                    a.ObjectId == plan.Id.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PlanCreatedEventHandler_Should_CreateOutboxMessages()
    {
        // Arrange
        var handler = new PlanCreatedEventHandler(
            _currentUserMock.Object,
            _auditLogRepoMock.Object,
            _outboxRepoMock.Object);

        var plan = CreateSamplePlan();
        var domainEvent = new EntityCreatedEvent<SubscriptionPlan>(plan, DateTime.UtcNow);

        // Act
        await handler.HandleAsync(domainEvent);

        // Assert — should create 2 outbox messages: AuditLogEntryCreated and PlanCreated
        _outboxRepoMock.Verify(
            x => x.AddOrUpdateAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task PlanCreatedEventHandler_Should_SaveChanges()
    {
        // Arrange
        var handler = new PlanCreatedEventHandler(
            _currentUserMock.Object,
            _auditLogRepoMock.Object,
            _outboxRepoMock.Object);

        var plan = CreateSamplePlan();
        var domainEvent = new EntityCreatedEvent<SubscriptionPlan>(plan, DateTime.UtcNow);

        // Act
        await handler.HandleAsync(domainEvent);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlanCreatedEventHandler_Should_UseEmptyGuid_WhenNotAuthenticated()
    {
        // Arrange
        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(false);

        var handler = new PlanCreatedEventHandler(
            _currentUserMock.Object,
            _auditLogRepoMock.Object,
            _outboxRepoMock.Object);

        var plan = CreateSamplePlan();
        var domainEvent = new EntityCreatedEvent<SubscriptionPlan>(plan, DateTime.UtcNow);

        // Act
        await handler.HandleAsync(domainEvent);

        // Assert
        _auditLogRepoMock.Verify(
            x => x.AddOrUpdateAsync(
                It.Is<AuditLogEntry>(a => a.UserId == Guid.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region PlanUpdatedEventHandler Tests

    [Fact]
    public async Task PlanUpdatedEventHandler_Should_CreateAuditLog_WithUpdateAction()
    {
        // Arrange
        var handler = new PlanUpdatedEventHandler(
            _currentUserMock.Object,
            _auditLogRepoMock.Object,
            _outboxRepoMock.Object);

        var plan = CreateSamplePlan();
        var domainEvent = new EntityUpdatedEvent<SubscriptionPlan>(plan, DateTime.UtcNow);

        // Act
        await handler.HandleAsync(domainEvent);

        // Assert
        _auditLogRepoMock.Verify(
            x => x.AddOrUpdateAsync(
                It.Is<AuditLogEntry>(a => a.Action == "UPDATED_PLAN"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PlanUpdatedEventHandler_Should_CreateOutboxMessages()
    {
        // Arrange
        var handler = new PlanUpdatedEventHandler(
            _currentUserMock.Object,
            _auditLogRepoMock.Object,
            _outboxRepoMock.Object);

        var plan = CreateSamplePlan();
        var domainEvent = new EntityUpdatedEvent<SubscriptionPlan>(plan, DateTime.UtcNow);

        // Act
        await handler.HandleAsync(domainEvent);

        // Assert
        _outboxRepoMock.Verify(
            x => x.AddOrUpdateAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #endregion

    #region PlanDeletedEventHandler Tests

    [Fact]
    public async Task PlanDeletedEventHandler_Should_CreateAuditLog_WithDeleteAction()
    {
        // Arrange
        var handler = new PlanDeletedEventHandler(
            _currentUserMock.Object,
            _auditLogRepoMock.Object,
            _outboxRepoMock.Object);

        var plan = CreateSamplePlan();
        var domainEvent = new EntityDeletedEvent<SubscriptionPlan>(plan, DateTime.UtcNow);

        // Act
        await handler.HandleAsync(domainEvent);

        // Assert
        _auditLogRepoMock.Verify(
            x => x.AddOrUpdateAsync(
                It.Is<AuditLogEntry>(a => a.Action == "DELETED_PLAN"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PlanDeletedEventHandler_Should_CreateOutboxMessages()
    {
        // Arrange
        var handler = new PlanDeletedEventHandler(
            _currentUserMock.Object,
            _auditLogRepoMock.Object,
            _outboxRepoMock.Object);

        var plan = CreateSamplePlan();
        var domainEvent = new EntityDeletedEvent<SubscriptionPlan>(plan, DateTime.UtcNow);

        // Act
        await handler.HandleAsync(domainEvent);

        // Assert
        _outboxRepoMock.Verify(
            x => x.AddOrUpdateAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task PlanDeletedEventHandler_Should_SaveChanges()
    {
        // Arrange
        var handler = new PlanDeletedEventHandler(
            _currentUserMock.Object,
            _auditLogRepoMock.Object,
            _outboxRepoMock.Object);

        var plan = CreateSamplePlan();
        var domainEvent = new EntityDeletedEvent<SubscriptionPlan>(plan, DateTime.UtcNow);

        // Act
        await handler.HandleAsync(domainEvent);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helpers

    private static SubscriptionPlan CreateSamplePlan()
    {
        return new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Pro",
            DisplayName = "Pro Plan",
            Description = "Professional plan",
            PriceMonthly = 29.99m,
            Currency = "USD",
            IsActive = true,
        };
    }

    #endregion
}
