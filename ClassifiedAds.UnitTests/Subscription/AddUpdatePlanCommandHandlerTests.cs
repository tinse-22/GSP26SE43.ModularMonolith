using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class AddUpdatePlanCommandHandlerTests
{
    private readonly Mock<ICrudService<SubscriptionPlan>> _planServiceMock;
    private readonly Mock<IRepository<SubscriptionPlan, Guid>> _planRepoMock;
    private readonly Mock<IRepository<PlanLimit, Guid>> _limitRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddUpdatePlanCommandHandler _handler;

    public AddUpdatePlanCommandHandlerTests()
    {
        _planServiceMock = new Mock<ICrudService<SubscriptionPlan>>();
        _planRepoMock = new Mock<IRepository<SubscriptionPlan, Guid>>();
        _limitRepoMock = new Mock<IRepository<PlanLimit, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _planRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _limitRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        // Setup ExecuteInTransactionAsync to simply invoke the operation
        _limitRepoMock.Setup(x => x.UnitOfWork.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        // Setup empty queryable for plans (name uniqueness check)
        _planRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SubscriptionPlan>().AsQueryable());
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync((SubscriptionPlan)null);

        // Setup empty queryable for limits
        _limitRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<PlanLimit>().AsQueryable());
        _limitRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<PlanLimit>>()))
            .ReturnsAsync(new List<PlanLimit>());

        // When AddOrUpdateAsync is called on a new plan (Id == Guid.Empty), assign a new Id
        _planServiceMock
            .Setup(x => x.AddOrUpdateAsync(It.IsAny<SubscriptionPlan>(), It.IsAny<CancellationToken>()))
            .Callback<SubscriptionPlan, CancellationToken>((plan, _) =>
            {
                if (plan.Id == Guid.Empty)
                {
                    plan.Id = Guid.NewGuid();
                }
            })
            .Returns(Task.CompletedTask);

        _handler = new AddUpdatePlanCommandHandler(
            _planServiceMock.Object,
            _planRepoMock.Object,
            _limitRepoMock.Object);
    }

    #region Create Plan Tests

    [Fact]
    public async Task HandleAsync_CreatePlan_Should_CallAddOrUpdate()
    {
        // Arrange
        var command = new AddUpdatePlanCommand
        {
            Model = CreateValidModel(),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _planServiceMock.Verify(
            x => x.AddOrUpdateAsync(It.Is<SubscriptionPlan>(p => p.Name == "Pro"), It.IsAny<CancellationToken>()),
            Times.Once);
        command.SavedPlanId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task HandleAsync_CreatePlan_Should_SetSavedPlanId()
    {
        // Arrange
        var command = new AddUpdatePlanCommand
        {
            Model = CreateValidModel(),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedPlanId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task HandleAsync_CreatePlan_WithLimits_Should_AddLimits()
    {
        // Arrange
        var command = new AddUpdatePlanCommand
        {
            Model = CreateValidModel(withLimits: true),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _limitRepoMock.Verify(
            x => x.AddAsync(It.IsAny<PlanLimit>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #endregion

    #region Update Plan Tests

    [Fact]
    public async Task HandleAsync_UpdatePlan_Should_ApplyModelChanges()
    {
        // Arrange
        var existingPlan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "OldName",
            DisplayName = "Old Display",
        };

        // First call returns existing plan (GetExistingPlanAsync),
        // second call returns null (EnsureNameUniquenessAsync)
        _planRepoMock.SetupSequence(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(existingPlan)
            .ReturnsAsync((SubscriptionPlan)null);

        var command = new AddUpdatePlanCommand
        {
            PlanId = existingPlan.Id,
            Model = CreateValidModel(),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _planServiceMock.Verify(
            x => x.AddOrUpdateAsync(It.Is<SubscriptionPlan>(p =>
                p.Name == "Pro" && p.DisplayName == "Pro Plan"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UpdatePlan_NotFound_Should_ThrowNotFoundException()
    {
        // Arrange
        var planId = Guid.NewGuid();

        // Ensure FirstOrDefaultAsync returns null for the plan lookup (override default)
        _planRepoMock.Reset();
        _planRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _planRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<SubscriptionPlan>().AsQueryable());
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync((SubscriptionPlan)null);

        var command = new AddUpdatePlanCommand
        {
            PlanId = planId,
            Model = CreateValidModel(),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenModelIsNull()
    {
        // Arrange
        var command = new AddUpdatePlanCommand
        {
            Model = null,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_ForDuplicateLimitTypes()
    {
        // Arrange
        var command = new AddUpdatePlanCommand
        {
            Model = new CreateUpdatePlanModel
            {
                Name = "Pro",
                DisplayName = "Pro Plan",
                Limits = new List<PlanLimitModel>
                {
                    new PlanLimitModel { LimitType = "MaxProjects", LimitValue = 5 },
                    new PlanLimitModel { LimitType = "MaxProjects", LimitValue = 10 },
                },
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*MaxProjects*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_ForZeroLimitValue_WhenNotUnlimited()
    {
        // Arrange
        var command = new AddUpdatePlanCommand
        {
            Model = new CreateUpdatePlanModel
            {
                Name = "Pro",
                DisplayName = "Pro Plan",
                Limits = new List<PlanLimitModel>
                {
                    new PlanLimitModel { LimitType = "MaxProjects", LimitValue = 0, IsUnlimited = false },
                },
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_ForNullLimitValue_WhenNotUnlimited()
    {
        // Arrange
        var command = new AddUpdatePlanCommand
        {
            Model = new CreateUpdatePlanModel
            {
                Name = "Pro",
                DisplayName = "Pro Plan",
                Limits = new List<PlanLimitModel>
                {
                    new PlanLimitModel { LimitType = "MaxProjects", LimitValue = null, IsUnlimited = false },
                },
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_NullifyLimitValue_WhenUnlimited()
    {
        // Arrange
        var command = new AddUpdatePlanCommand
        {
            Model = new CreateUpdatePlanModel
            {
                Name = "Enterprise",
                DisplayName = "Enterprise Plan",
                Limits = new List<PlanLimitModel>
                {
                    new PlanLimitModel { LimitType = "MaxProjects", LimitValue = 999, IsUnlimited = true },
                },
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _limitRepoMock.Verify(
            x => x.AddAsync(It.Is<PlanLimit>(l => l.IsUnlimited && l.LimitValue == null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helpers

    private static CreateUpdatePlanModel CreateValidModel(bool withLimits = false)
    {
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = "Pro Plan",
            Description = "Professional plan",
            PriceMonthly = 29.99m,
            PriceYearly = 299.99m,
            Currency = "USD",
            IsActive = true,
            SortOrder = 1,
        };

        if (withLimits)
        {
            model.Limits = new List<PlanLimitModel>
            {
                new PlanLimitModel { LimitType = "MaxProjects", LimitValue = 10, IsUnlimited = false },
                new PlanLimitModel { LimitType = "MaxTestRunsPerMonth", IsUnlimited = true },
            };
        }

        return model;
    }

    #endregion
}
