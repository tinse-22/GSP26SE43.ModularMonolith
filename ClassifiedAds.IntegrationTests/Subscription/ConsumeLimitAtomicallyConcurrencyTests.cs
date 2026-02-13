using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.IntegrationTests.Infrastructure;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ClassifiedAds.IntegrationTests.Subscription;

/// <summary>
/// Concurrency integration test for atomic limit consumption.
/// Uses a real PostgreSQL database via Testcontainers to verify
/// serializable isolation prevents race conditions.
/// </summary>
[Collection("IntegrationTests")]
public class ConsumeLimitAtomicallyConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _dbFixture;
    private CustomWebApplicationFactory _factory = null!;

    public ConsumeLimitAtomicallyConcurrencyTests(PostgreSqlContainerFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory(_dbFixture.ConnectionString);
        // Force host creation to ensure DB is created
        _ = _factory.Services;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task FiftyParallelCalls_WithLimitTen_ShouldAllowAtMostTen()
    {
        // Arrange
        const int parallelism = 50;
        const int limit = 10;

        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        // Seed test data: plan, plan-limit, subscription
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SubscriptionDbContext>();

            // Ensure the subscription schema/tables exist
            await db.Database.EnsureCreatedAsync();

            var plan = new SubscriptionPlan
            {
                Id = planId,
                Name = "TestPlan",
                DisplayName = "Test Plan",
                IsActive = true,
                SortOrder = 1,
                CreatedDateTime = DateTimeOffset.UtcNow,
            };
            db.SubscriptionPlans.Add(plan);

            var planLimit = new PlanLimit
            {
                Id = Guid.NewGuid(),
                PlanId = planId,
                LimitType = LimitType.MaxEndpointsPerProject,
                LimitValue = limit,
                IsUnlimited = false,
                CreatedDateTime = DateTimeOffset.UtcNow,
            };
            db.PlanLimits.Add(planLimit);

            var subscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PlanId = planId,
                Status = SubscriptionStatus.Active,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
                AutoRenew = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
            };
            db.UserSubscriptions.Add(subscription);

            await db.SaveChangesAsync();
        }

        // Act: fire 50 parallel consume attempts, each trying to increment by 1
        var tasks = Enumerable.Range(0, parallelism).Select(_ => Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var sp = scope.ServiceProvider;

            // Resolve repositories from DI for each scope
            var subscriptionRepo = sp.GetRequiredService<IRepository<UserSubscription, Guid>>();
            var planRepo = sp.GetRequiredService<IRepository<SubscriptionPlan, Guid>>();
            var planLimitRepo = sp.GetRequiredService<IRepository<PlanLimit, Guid>>();
            var usageTrackingRepo = sp.GetRequiredService<IRepository<UsageTracking, Guid>>();
            var logger = sp.GetRequiredService<ILogger<ConsumeLimitAtomicallyCommandHandler>>();

            var handler = new ConsumeLimitAtomicallyCommandHandler(
                subscriptionRepo,
                planRepo,
                planLimitRepo,
                usageTrackingRepo,
                logger);

            var command = new ConsumeLimitAtomicallyCommand
            {
                UserId = userId,
                LimitType = LimitType.MaxEndpointsPerProject,
                IncrementValue = 1,
            };

            await handler.HandleAsync(command);
            return command.Result;
        }));

        var results = await Task.WhenAll(tasks);

        // Assert
        int successCount = results.Count(r => r.IsAllowed);
        int deniedCount = results.Count(r => !r.IsAllowed);

        successCount.Should().BeLessThanOrEqualTo(limit,
            "at most {0} calls should succeed when the limit is {0}", limit);
        successCount.Should().BeGreaterThan(0,
            "at least one call should succeed");
        (successCount + deniedCount).Should().Be(parallelism,
            "all {0} calls should have completed", parallelism);

        // Verify final usage in the DB matches the number of successful calls
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SubscriptionDbContext>();

            var tracking = await db.UsageTrackings
                .Where(u => u.UserId == userId)
                .ToListAsync();

            var totalEndpoints = tracking.Sum(t => t.EndpointCount);
            totalEndpoints.Should().Be(successCount,
                "persisted usage ({0}) must equal the number of successful consume calls ({1})",
                totalEndpoints, successCount);
        }
    }
}
