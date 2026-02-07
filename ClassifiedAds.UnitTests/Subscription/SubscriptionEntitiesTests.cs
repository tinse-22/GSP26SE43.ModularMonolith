using ClassifiedAds.Modules.Subscription.Entities;
using System;

namespace ClassifiedAds.UnitTests.Subscription;

public class SubscriptionEntitiesTests
{
    #region SubscriptionPlan Tests

    [Fact]
    public void SubscriptionPlan_Should_HaveDefaultValues()
    {
        // Arrange & Act
        var plan = new SubscriptionPlan();

        // Assert
        plan.Id.Should().Be(Guid.Empty);
        plan.Name.Should().BeNull();
        plan.DisplayName.Should().BeNull();
        plan.Description.Should().BeNull();
        plan.PriceMonthly.Should().BeNull();
        plan.PriceYearly.Should().BeNull();
        plan.Currency.Should().BeNull();
        plan.IsActive.Should().BeFalse();
        plan.SortOrder.Should().Be(0);
    }

    [Fact]
    public void SubscriptionPlan_Should_SetProperties()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var plan = new SubscriptionPlan
        {
            Id = id,
            Name = "Enterprise",
            DisplayName = "Enterprise Plan",
            Description = "Full-featured plan",
            PriceMonthly = 99.99m,
            PriceYearly = 999.99m,
            Currency = "USD",
            IsActive = true,
            SortOrder = 3,
        };

        // Assert
        plan.Id.Should().Be(id);
        plan.Name.Should().Be("Enterprise");
        plan.DisplayName.Should().Be("Enterprise Plan");
        plan.Description.Should().Be("Full-featured plan");
        plan.PriceMonthly.Should().Be(99.99m);
        plan.PriceYearly.Should().Be(999.99m);
        plan.Currency.Should().Be("USD");
        plan.IsActive.Should().BeTrue();
        plan.SortOrder.Should().Be(3);
    }

    #endregion

    #region PlanLimit Tests

    [Fact]
    public void PlanLimit_Should_HaveDefaultValues()
    {
        // Arrange & Act
        var limit = new PlanLimit();

        // Assert
        limit.PlanId.Should().Be(Guid.Empty);
        limit.LimitType.Should().Be(LimitType.MaxProjects);
        limit.LimitValue.Should().BeNull();
        limit.IsUnlimited.Should().BeFalse();
    }

    [Fact]
    public void PlanLimit_Should_SetNavigationProperty()
    {
        // Arrange
        var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Pro" };

        // Act
        var limit = new PlanLimit
        {
            PlanId = plan.Id,
            Plan = plan,
            LimitType = LimitType.MaxProjects,
            LimitValue = 10,
        };

        // Assert
        limit.Plan.Should().Be(plan);
        limit.PlanId.Should().Be(plan.Id);
    }

    #endregion

    #region UserSubscription Tests

    [Fact]
    public void UserSubscription_Should_HaveDefaultValues()
    {
        // Arrange & Act
        var subscription = new UserSubscription();

        // Assert
        subscription.UserId.Should().Be(Guid.Empty);
        subscription.PlanId.Should().Be(Guid.Empty);
        subscription.Status.Should().Be(SubscriptionStatus.Trial);
        subscription.BillingCycle.Should().BeNull();
        subscription.EndDate.Should().BeNull();
        subscription.NextBillingDate.Should().BeNull();
        subscription.TrialEndsAt.Should().BeNull();
        subscription.CancelledAt.Should().BeNull();
        subscription.AutoRenew.Should().BeFalse();
        subscription.ExternalSubId.Should().BeNull();
        subscription.ExternalCustId.Should().BeNull();
    }

    [Fact]
    public void UserSubscription_Should_SetAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        var subscription = new UserSubscription
        {
            UserId = userId,
            PlanId = planId,
            Status = SubscriptionStatus.Active,
            BillingCycle = BillingCycle.Monthly,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            NextBillingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            TrialEndsAt = now.AddDays(14),
            CancelledAt = null,
            AutoRenew = true,
            ExternalSubId = "sub_123",
            ExternalCustId = "cus_456",
        };

        // Assert
        subscription.UserId.Should().Be(userId);
        subscription.PlanId.Should().Be(planId);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.BillingCycle.Should().Be(BillingCycle.Monthly);
        subscription.AutoRenew.Should().BeTrue();
        subscription.ExternalSubId.Should().Be("sub_123");
        subscription.ExternalCustId.Should().Be("cus_456");
    }

    #endregion

    #region PaymentTransaction Tests

    [Fact]
    public void PaymentTransaction_Should_SetProperties()
    {
        // Arrange & Act
        var transaction = new PaymentTransaction
        {
            UserId = Guid.NewGuid(),
            SubscriptionId = Guid.NewGuid(),
            Amount = 29.99m,
            Currency = "USD",
            Status = PaymentStatus.Succeeded,
            PaymentMethod = "card",
            ExternalTxnId = "pi_123",
            InvoiceUrl = "https://invoice.example.com/123",
        };

        // Assert
        transaction.Amount.Should().Be(29.99m);
        transaction.Currency.Should().Be("USD");
        transaction.Status.Should().Be(PaymentStatus.Succeeded);
        transaction.PaymentMethod.Should().Be("card");
        transaction.ExternalTxnId.Should().Be("pi_123");
        transaction.InvoiceUrl.Should().Be("https://invoice.example.com/123");
        transaction.FailureReason.Should().BeNull();
    }

    [Fact]
    public void PaymentTransaction_Should_HaveFailureReason_WhenFailed()
    {
        // Arrange & Act
        var transaction = new PaymentTransaction
        {
            Status = PaymentStatus.Failed,
            FailureReason = "Insufficient funds",
        };

        // Assert
        transaction.Status.Should().Be(PaymentStatus.Failed);
        transaction.FailureReason.Should().Be("Insufficient funds");
    }

    #endregion

    #region UsageTracking Tests

    [Fact]
    public void UsageTracking_Should_HaveZeroDefaults()
    {
        // Arrange & Act
        var usage = new UsageTracking();

        // Assert
        usage.ProjectCount.Should().Be(0);
        usage.EndpointCount.Should().Be(0);
        usage.TestSuiteCount.Should().Be(0);
        usage.TestCaseCount.Should().Be(0);
        usage.TestRunCount.Should().Be(0);
        usage.LlmCallCount.Should().Be(0);
        usage.StorageUsedMB.Should().Be(0);
    }

    [Fact]
    public void UsageTracking_Should_TrackUsage()
    {
        // Arrange & Act
        var usage = new UsageTracking
        {
            UserId = Guid.NewGuid(),
            PeriodStart = new DateOnly(2026, 1, 1),
            PeriodEnd = new DateOnly(2026, 1, 31),
            ProjectCount = 5,
            EndpointCount = 50,
            TestSuiteCount = 10,
            TestCaseCount = 200,
            TestRunCount = 30,
            LlmCallCount = 100,
            StorageUsedMB = 512.5m,
        };

        // Assert
        usage.ProjectCount.Should().Be(5);
        usage.EndpointCount.Should().Be(50);
        usage.TestRunCount.Should().Be(30);
        usage.LlmCallCount.Should().Be(100);
        usage.StorageUsedMB.Should().Be(512.5m);
    }

    #endregion

    #region SubscriptionHistory Tests

    [Fact]
    public void SubscriptionHistory_Should_SetProperties()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var oldPlanId = Guid.NewGuid();
        var newPlanId = Guid.NewGuid();

        // Act
        var history = new SubscriptionHistory
        {
            SubscriptionId = subscriptionId,
            OldPlanId = oldPlanId,
            NewPlanId = newPlanId,
            ChangeType = ChangeType.Upgraded,
            ChangeReason = "User requested upgrade",
            EffectiveDate = new DateOnly(2026, 2, 1),
        };

        // Assert
        history.SubscriptionId.Should().Be(subscriptionId);
        history.OldPlanId.Should().Be(oldPlanId);
        history.NewPlanId.Should().Be(newPlanId);
        history.ChangeType.Should().Be(ChangeType.Upgraded);
        history.ChangeReason.Should().Be("User requested upgrade");
    }

    [Fact]
    public void SubscriptionHistory_OldPlanId_Should_BeNullable()
    {
        // Arrange & Act
        var history = new SubscriptionHistory
        {
            OldPlanId = null,
            NewPlanId = Guid.NewGuid(),
            ChangeType = ChangeType.Created,
        };

        // Assert
        history.OldPlanId.Should().BeNull();
    }

    #endregion

    #region Enum Tests

    [Theory]
    [InlineData(LimitType.MaxProjects, 0)]
    [InlineData(LimitType.MaxEndpointsPerProject, 1)]
    [InlineData(LimitType.MaxTestCasesPerSuite, 2)]
    [InlineData(LimitType.MaxTestRunsPerMonth, 3)]
    [InlineData(LimitType.MaxConcurrentRuns, 4)]
    [InlineData(LimitType.RetentionDays, 5)]
    [InlineData(LimitType.MaxLlmCallsPerMonth, 6)]
    [InlineData(LimitType.MaxStorageMB, 7)]
    public void LimitType_Should_HaveExpectedValues(LimitType limitType, int expectedValue)
    {
        ((int)limitType).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Trial, 0)]
    [InlineData(SubscriptionStatus.Active, 1)]
    [InlineData(SubscriptionStatus.PastDue, 2)]
    [InlineData(SubscriptionStatus.Cancelled, 3)]
    [InlineData(SubscriptionStatus.Expired, 4)]
    public void SubscriptionStatus_Should_HaveExpectedValues(SubscriptionStatus status, int expectedValue)
    {
        ((int)status).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(BillingCycle.Monthly, 0)]
    [InlineData(BillingCycle.Yearly, 1)]
    public void BillingCycle_Should_HaveExpectedValues(BillingCycle cycle, int expectedValue)
    {
        ((int)cycle).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, 0)]
    [InlineData(PaymentStatus.Succeeded, 1)]
    [InlineData(PaymentStatus.Failed, 2)]
    [InlineData(PaymentStatus.Refunded, 3)]
    public void PaymentStatus_Should_HaveExpectedValues(PaymentStatus status, int expectedValue)
    {
        ((int)status).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(ChangeType.Created, 0)]
    [InlineData(ChangeType.Upgraded, 1)]
    [InlineData(ChangeType.Downgraded, 2)]
    [InlineData(ChangeType.Cancelled, 3)]
    [InlineData(ChangeType.Reactivated, 4)]
    public void ChangeType_Should_HaveExpectedValues(ChangeType changeType, int expectedValue)
    {
        ((int)changeType).Should().Be(expectedValue);
    }

    #endregion

    #region AuditLogEntry Tests

    [Fact]
    public void AuditLogEntry_Should_SetProperties()
    {
        // Arrange & Act
        var entry = new AuditLogEntry
        {
            UserId = Guid.NewGuid(),
            Action = "CREATED_PLAN",
            ObjectId = Guid.NewGuid().ToString(),
            Log = "{ \"Name\": \"Pro\" }",
        };

        // Assert
        entry.Action.Should().Be("CREATED_PLAN");
        entry.Log.Should().Contain("Pro");
    }

    #endregion

    #region OutboxMessage Tests

    [Fact]
    public void OutboxMessage_Should_SetProperties()
    {
        // Arrange & Act
        var message = new OutboxMessage
        {
            EventType = "PLAN_CREATED",
            TriggeredById = Guid.NewGuid(),
            ObjectId = Guid.NewGuid().ToString(),
            Payload = "{ \"data\": true }",
            Published = false,
            ActivityId = "activity-123",
        };

        // Assert
        message.EventType.Should().Be("PLAN_CREATED");
        message.Published.Should().BeFalse();
        message.ActivityId.Should().Be("activity-123");
    }

    [Fact]
    public void OutboxMessage_Published_Should_DefaultToFalse()
    {
        // Arrange & Act
        var message = new OutboxMessage();

        // Assert
        message.Published.Should().BeFalse();
    }

    #endregion
}
