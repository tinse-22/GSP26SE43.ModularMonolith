using ClassifiedAds.Application;
using ClassifiedAds.IntegrationTests.Infrastructure;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Persistence;
using ClassifiedAds.Modules.Subscription.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Threading.Tasks;

namespace ClassifiedAds.IntegrationTests.Subscription;

[Collection("IntegrationTests")]
public class SyncPaymentFromPayOsIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _dbFixture;
    private readonly Mock<IPayOsService> _payOsServiceMock = new(MockBehavior.Strict);
    private CustomWebApplicationFactory _factory = null!;

    public SyncPaymentFromPayOsIntegrationTests(PostgreSqlContainerFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory(
            _dbFixture.ConnectionString,
            services =>
            {
                services.RemoveAll<IPayOsService>();
                services.AddSingleton(_payOsServiceMock.Object);
            });

        _ = _factory.Services;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task SyncPaidStatus_ShouldPromoteIntentAndCreateSubscriptionAndTransaction()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var intentId = Guid.NewGuid();
        const long orderCode = 260213120001;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SubscriptionDbContext>();
            await db.Database.EnsureCreatedAsync();

            db.SubscriptionPlans.Add(new SubscriptionPlan
            {
                Id = planId,
                Name = "Pro",
                DisplayName = "Pro Plan",
                IsActive = true,
                PriceMonthly = 129000,
                Currency = "VND",
                SortOrder = 1,
                CreatedDateTime = DateTimeOffset.UtcNow,
            });

            db.PaymentIntents.Add(new PaymentIntent
            {
                Id = intentId,
                UserId = userId,
                Amount = 129000,
                Currency = "VND",
                Purpose = PaymentPurpose.SubscriptionPurchase,
                PlanId = planId,
                BillingCycle = BillingCycle.Monthly,
                Status = PaymentIntentStatus.Processing,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                OrderCode = orderCode,
                CreatedDateTime = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
        }

        _payOsServiceMock
            .Setup(x => x.GetPaymentInfoAsync(orderCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOsGetPaymentData
            {
                Id = "pay-link-001",
                OrderCode = orderCode,
                Amount = 129000,
                Status = "PAID",
                Reference = "TXN-001",
                TransactionDateTime = DateTimeOffset.UtcNow.ToString("O"),
                CheckoutUrl = "https://payos.vn/checkout/1",
            });

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var command = new SyncPaymentFromPayOsCommand
            {
                UserId = userId,
                IntentId = intentId,
            };

            // Act
            await dispatcher.DispatchAsync(command);

            // Assert command result
            command.Status.Should().Be("synced");
            command.PayOsStatus.Should().Be("PAID");
        }

        await using (var verifyScope = _factory.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<SubscriptionDbContext>();

            var paymentIntent = await db.PaymentIntents.SingleAsync(x => x.Id == intentId);
            paymentIntent.Status.Should().Be(PaymentIntentStatus.Succeeded);
            paymentIntent.SubscriptionId.Should().NotBeNull();

            var subscription = await db.UserSubscriptions.SingleAsync(x => x.Id == paymentIntent.SubscriptionId!.Value);
            subscription.UserId.Should().Be(userId);
            subscription.PlanId.Should().Be(planId);
            subscription.Status.Should().Be(SubscriptionStatus.Active);
            subscription.BillingCycle.Should().Be(BillingCycle.Monthly);

            var transaction = await db.PaymentTransactions.SingleAsync(x => x.PaymentIntentId == intentId);
            transaction.Status.Should().Be(PaymentStatus.Succeeded);
            transaction.Provider.Should().Be("PAYOS");
            transaction.ProviderRef.Should().Be("pay-link-001");
        }

        _payOsServiceMock.Verify(x => x.GetPaymentInfoAsync(orderCode, It.IsAny<CancellationToken>()), Times.Once);
    }
}
