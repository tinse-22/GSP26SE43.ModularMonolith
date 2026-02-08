using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.DTOs;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Contracts.Notification.DTOs;
using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class AddUpdatePlanCommandHandlerTests
{
    private readonly Mock<ICrudService<SubscriptionPlan>> _planServiceMock;
    private readonly Mock<IRepository<SubscriptionPlan, Guid>> _planRepoMock;
    private readonly Mock<IRepository<PlanLimit, Guid>> _limitRepoMock;
    private readonly Mock<IRepository<UserSubscription, Guid>> _subscriptionRepoMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IEmailMessageService> _emailMessageServiceMock;
    private readonly Mock<ILogger<AddUpdatePlanCommandHandler>> _loggerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddUpdatePlanCommandHandler _handler;

    public AddUpdatePlanCommandHandlerTests()
    {
        _planServiceMock = new Mock<ICrudService<SubscriptionPlan>>();
        _planRepoMock = new Mock<IRepository<SubscriptionPlan, Guid>>();
        _limitRepoMock = new Mock<IRepository<PlanLimit, Guid>>();
        _subscriptionRepoMock = new Mock<IRepository<UserSubscription, Guid>>();
        _userServiceMock = new Mock<IUserService>();
        _emailMessageServiceMock = new Mock<IEmailMessageService>();
        _loggerMock = new Mock<ILogger<AddUpdatePlanCommandHandler>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _planRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _limitRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _subscriptionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _planRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<SubscriptionPlan>().AsQueryable());
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync((SubscriptionPlan)null);

        _limitRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<PlanLimit>().AsQueryable());
        _limitRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<PlanLimit>>()))
            .ReturnsAsync(new List<PlanLimit>());

        _subscriptionRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<UserSubscription>().AsQueryable());
        _subscriptionRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(new List<UserSubscription>());

        _userServiceMock.Setup(x => x.GetUsersAsync(It.IsAny<UserQueryOptions>()))
            .ReturnsAsync(new List<UserDTO>());
        _emailMessageServiceMock.Setup(x => x.CreateEmailMessageAsync(It.IsAny<EmailMessageDTO>()))
            .Returns(Task.CompletedTask);

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
            _limitRepoMock.Object,
            _subscriptionRepoMock.Object,
            _userServiceMock.Object,
            _emailMessageServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_CreatePlan_Should_CallAddOrUpdate()
    {
        var command = new AddUpdatePlanCommand
        {
            Model = CreateValidModel(),
        };

        await _handler.HandleAsync(command);

        _planServiceMock.Verify(
            x => x.AddOrUpdateAsync(It.Is<SubscriptionPlan>(p => p.Name == "Pro"), It.IsAny<CancellationToken>()),
            Times.Once);
        command.SavedPlanId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task HandleAsync_CreatePlan_WithLimits_Should_AddLimits()
    {
        var command = new AddUpdatePlanCommand
        {
            Model = CreateValidModel(withLimits: true),
        };

        await _handler.HandleAsync(command);

        _limitRepoMock.Verify(
            x => x.AddAsync(It.IsAny<PlanLimit>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_UpdatePlan_Should_ApplyModelChanges()
    {
        var existingPlan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "OldName",
            DisplayName = "Old Display",
            PriceMonthly = 10m,
            PriceYearly = 100m,
            Currency = "USD",
        };

        _planRepoMock.SetupSequence(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(existingPlan)
            .ReturnsAsync((SubscriptionPlan)null);

        var command = new AddUpdatePlanCommand
        {
            PlanId = existingPlan.Id,
            Model = CreateValidModel(),
        };

        await _handler.HandleAsync(command);

        _planServiceMock.Verify(
            x => x.AddOrUpdateAsync(It.Is<SubscriptionPlan>(p =>
                p.Name == "Pro" && p.DisplayName == "Pro Plan"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UpdatePlan_NotFound_Should_ThrowNotFoundException()
    {
        var planId = Guid.NewGuid();

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

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenModelIsNull()
    {
        var command = new AddUpdatePlanCommand
        {
            Model = null,
        };

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_ForDuplicateLimitTypes()
    {
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

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*MaxProjects*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_ForZeroLimitValue_WhenNotUnlimited()
    {
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

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_ForNullLimitValue_WhenNotUnlimited()
    {
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

        var act = () => _handler.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_NullifyLimitValue_WhenUnlimited()
    {
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

        await _handler.HandleAsync(command);

        _limitRepoMock.Verify(
            x => x.AddAsync(It.Is<PlanLimit>(l => l.IsUnlimited && l.LimitValue == null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UpdatePlan_WithPriceChangeAndActiveSubscribers_Should_SendNotifications()
    {
        var userId = Guid.NewGuid();
        var existingPlan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Pro",
            DisplayName = "Pro Plan",
            PriceMonthly = 10m,
            PriceYearly = 100m,
            Currency = "USD",
            IsActive = true,
        };

        _planRepoMock.SetupSequence(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(existingPlan)
            .ReturnsAsync((SubscriptionPlan)null);

        _subscriptionRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(new List<UserSubscription>
            {
                new UserSubscription { Id = Guid.NewGuid(), UserId = userId, PlanId = existingPlan.Id, Status = SubscriptionStatus.Active },
            });

        _userServiceMock.Setup(x => x.GetUsersAsync(It.IsAny<UserQueryOptions>()))
            .ReturnsAsync(new List<UserDTO>
            {
                new UserDTO { Id = userId, Email = "user1@example.com" },
            });

        var command = new AddUpdatePlanCommand
        {
            PlanId = existingPlan.Id,
            Model = new CreateUpdatePlanModel
            {
                Name = "Pro",
                DisplayName = "Pro Plan",
                Description = "Updated",
                PriceMonthly = 20m,
                PriceYearly = 100m,
                Currency = "USD",
                IsActive = true,
                SortOrder = 1,
            },
        };

        await _handler.HandleAsync(command);

        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.Is<EmailMessageDTO>(m =>
                m.Tos == "user1@example.com" && m.Subject.Contains("Price update notice"))),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UpdatePlan_WithoutPriceChange_Should_NotSendNotifications()
    {
        var userId = Guid.NewGuid();
        var existingPlan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Pro",
            DisplayName = "Pro Plan",
            PriceMonthly = 10m,
            PriceYearly = 100m,
            Currency = "USD",
            IsActive = true,
        };

        _planRepoMock.SetupSequence(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(existingPlan)
            .ReturnsAsync((SubscriptionPlan)null);

        _subscriptionRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(new List<UserSubscription>
            {
                new UserSubscription { Id = Guid.NewGuid(), UserId = userId, PlanId = existingPlan.Id, Status = SubscriptionStatus.Active },
            });

        _userServiceMock.Setup(x => x.GetUsersAsync(It.IsAny<UserQueryOptions>()))
            .ReturnsAsync(new List<UserDTO>
            {
                new UserDTO { Id = userId, Email = "user1@example.com" },
            });

        var command = new AddUpdatePlanCommand
        {
            PlanId = existingPlan.Id,
            Model = new CreateUpdatePlanModel
            {
                Name = "Pro",
                DisplayName = "Pro Plan",
                Description = "Updated description only",
                PriceMonthly = 10m,
                PriceYearly = 100m,
                Currency = "USD",
                IsActive = true,
                SortOrder = 1,
            },
        };

        await _handler.HandleAsync(command);

        _emailMessageServiceMock.Verify(
            x => x.CreateEmailMessageAsync(It.IsAny<EmailMessageDTO>()),
            Times.Never);
    }

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
}
